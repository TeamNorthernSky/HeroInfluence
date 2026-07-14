using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    internal static class ExcelRawGeneratePipeline
    {
        private const char Separator = '|';

        public static void StartGenerate(RawExcelSheetSO raw, ExcelSheetSchemaSO schema)
        {
            if (raw == null || schema == null)
            {
                Debug.LogError("[ExcelRawGenerate] Raw or schema is missing.");
                return;
            }

            bool clonedSchema = ExcelSchemaAdapter.RequiresSchemaClone(raw, schema);
            schema = ExcelSchemaAdapter.EnsureSchemaForRaw(raw, schema);
            if (schema == null)
            {
                Debug.LogError("[ExcelRawGenerate] Schema could not be prepared for this Raw sheet.");
                return;
            }

            if (clonedSchema)
            {
                raw.importStatus = RawImportStatus.SchemaApplied;
                EditorUtility.SetDirty(raw);
                EditorUtility.SetDirty(schema);
                AssetDatabase.SaveAssets();
            }

            if (!ExcelSchemaAdapter.Validate(raw, schema, out List<string> errors, out _))
            {
                Debug.LogError("[ExcelRawGenerate] Validation failed:\n" + string.Join("\n", errors));
                return;
            }

            raw.importStatus = RawImportStatus.Generating;
            raw.lastError = string.Empty;
            EditorUtility.SetDirty(raw);
            EditorUtility.SetDirty(schema);
            AssetDatabase.SaveAssets();

            ExcelSheetParseResult parseResult = ExcelSchemaAdapter.ToParseResult(raw, schema);
            var sheets = new List<ExcelSheetParseResult> { parseResult };
            var useDictMap = new Dictionary<string, bool> { { parseResult.SheetName, schema.useDictionary } };
            try
            {
                CodeGenerator.GenerateAll(sheets, useDictMap);
            }
            catch (Exception ex)
            {
                Fail(raw, ex.Message + "\n" + ex.StackTrace);
                return;
            }

            string rawPath = AssetDatabase.GetAssetPath(raw);
            string schemaPath = AssetDatabase.GetAssetPath(schema);
            EditorPrefs.SetString(ExcelImportPaths.PendingRawGenerateKey, SerializePending(rawPath, schemaPath));

            if (AreGeneratedTypesReady(parseResult))
            {
                CompletePendingGenerate(rawPath, schemaPath);
            }
            else
            {
                AssetDatabase.Refresh();
                Debug.Log($"[ExcelRawGenerate] Script compile pending for '{parseResult.ClassName}'.");
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            string pending = EditorPrefs.GetString(ExcelImportPaths.PendingRawGenerateKey, string.Empty);
            if (string.IsNullOrEmpty(pending))
            {
                return;
            }

            EditorPrefs.DeleteKey(ExcelImportPaths.PendingRawGenerateKey);
            if (!TryDeserializePending(pending, out string rawPath, out string schemaPath))
            {
                Debug.LogError("[ExcelRawGenerate] Pending payload is invalid.");
                return;
            }

            EditorApplication.delayCall += () => CompletePendingGenerate(rawPath, schemaPath);
        }

        private static void CompletePendingGenerate(string rawPath, string schemaPath)
        {
            EditorPrefs.DeleteKey(ExcelImportPaths.PendingRawGenerateKey);
            RawExcelSheetSO raw = AssetDatabase.LoadAssetAtPath<RawExcelSheetSO>(rawPath);
            ExcelSheetSchemaSO schema = AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(schemaPath);
            if (raw == null || schema == null)
            {
                Debug.LogError($"[ExcelRawGenerate] Pending assets missing. raw='{rawPath}', schema='{schemaPath}'");
                return;
            }

            try
            {
                if (!ExcelSchemaAdapter.CanReuseSchemaForRaw(raw, schema))
                {
                    Fail(raw, "Pending schema does not belong to this raw sheet. Re-run Generate so the schema can be cloned before script generation.");
                    return;
                }

                if (!ExcelSchemaAdapter.Validate(raw, schema, out List<string> errors, out _))
                {
                    Fail(raw, "Validation failed after script reload:\n" + string.Join("\n", errors));
                    return;
                }

                ExcelSheetParseResult parseResult = ExcelSchemaAdapter.ToParseResult(raw, schema);
                if (!AreGeneratedTypesReady(parseResult))
                {
                    Fail(raw, $"Generated types are not ready for {parseResult.ClassName}.");
                    return;
                }

                string excelName = !string.IsNullOrWhiteSpace(schema.workbookFileName)
                    ? schema.workbookFileName
                    : string.IsNullOrWhiteSpace(schema.sourceWorkbookPath)
                        ? raw.workbookFileName
                        : System.IO.Path.GetFileNameWithoutExtension(schema.sourceWorkbookPath);
                string targetFolder = ExcelImportPaths.GetTableAssetFolder(excelName);
                var sheets = new List<ExcelSheetParseResult> { parseResult };
                var useDictMap = new Dictionary<string, bool> { { parseResult.SheetName, schema.useDictionary } };
                ScriptableExporter.ExportAll(sheets, useDictMap, targetFolder);
                AssetDatabase.SaveAssets();

                string targetAssetPath = ExcelSchemaAdapter.GetTargetAssetPath(schema);
                ScriptableObject generated = AssetDatabase.LoadAssetAtPath<ScriptableObject>(targetAssetPath);
                if (generated == null)
                {
                    Fail(raw, $"Generated DataTable asset was not found: {targetAssetPath}");
                    return;
                }

                string schemaGuid = ExcelRawImportUtility.GetSchemaGuid(schema);
                AssetImporter importer = AssetImporter.GetAtPath(targetAssetPath);
                if (importer == null)
                {
                    Fail(raw, $"AssetImporter was not found: {targetAssetPath}");
                    return;
                }

                importer.userData = schemaGuid;
                importer.SaveAndReimport();

                schema.targetTableAssetPath = targetAssetPath;
                schema.targetTableAssetGuid = AssetDatabase.AssetPathToGUID(targetAssetPath);
                EditorUtility.SetDirty(schema);

                raw.importStatus = RawImportStatus.Generated;
                raw.lastError = string.Empty;
                EditorUtility.SetDirty(raw);
                AssetDatabase.SaveAssets();

                AssetDatabase.DeleteAsset(rawPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[ExcelRawGenerate] Generated DataTable and removed Raw SO: {targetAssetPath}");
            }
            catch (Exception ex)
            {
                Fail(raw, ex.Message + "\n" + ex.StackTrace);
            }
        }

        private static bool AreGeneratedTypesReady(ExcelSheetParseResult sheet)
        {
            string baseName = CodeGenerator.ToTypeBaseName(sheet.ClassName);
            return ScriptableExporter.FindTypeByName(baseName + "Data") != null &&
                   ScriptableExporter.FindTypeByName(baseName + "DataTable") != null;
        }

        private static void Fail(RawExcelSheetSO raw, string message)
        {
            raw.importStatus = RawImportStatus.Failed;
            raw.lastError = message;
            EditorUtility.SetDirty(raw);
            AssetDatabase.SaveAssets();
            Debug.LogError("[ExcelRawGenerate] " + message);
        }

        private static string SerializePending(string rawPath, string schemaPath)
        {
            return $"{rawPath}{Separator}{schemaPath}";
        }

        private static bool TryDeserializePending(string payload, out string rawPath, out string schemaPath)
        {
            rawPath = string.Empty;
            schemaPath = string.Empty;
            string[] parts = payload.Split(Separator);
            if (parts.Length != 2)
            {
                return false;
            }

            rawPath = parts[0];
            schemaPath = parts[1];
            return !string.IsNullOrEmpty(rawPath) && !string.IsNullOrEmpty(schemaPath);
        }
    }
}
