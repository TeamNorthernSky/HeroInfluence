using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

            schema = ExcelSchemaAdapter.ResolveExistingSchemaForRaw(raw, schema);
            if (schema == null)
            {
                Debug.LogError("[ExcelRawGenerate] No schema exists for this Raw sheet. Create a schema first (auto-create/clone is disabled).");
                return;
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

        public static bool UpdateData(
            RawExcelSheetSO raw,
            ExcelSheetSchemaSO schema,
            out ExcelSheetSchemaSO usedSchema,
            out string error)
        {
            usedSchema = schema;
            error = string.Empty;

            if (raw == null)
            {
                error = "Raw sheet is missing.";
                return false;
            }

            if (schema == null)
            {
                error = "Schema is missing.";
                return false;
            }

            try
            {
                usedSchema = ExcelSchemaAdapter.ResolveExistingSchemaForRaw(raw, schema);
                if (usedSchema == null)
                {
                    error = "No schema exists for this Raw sheet. Create a schema first (auto-create/clone is disabled).";
                    return false;
                }

                if (!ExcelSchemaAdapter.Validate(raw, usedSchema, out List<string> errors, out _))
                {
                    error = "Validation failed:\n" + string.Join("\n", errors);
                    return false;
                }

                ExcelSheetParseResult parseResult = ExcelSchemaAdapter.ToParseResult(raw, usedSchema);
                string targetAssetPath = ExcelSchemaAdapter.GetTargetAssetPath(usedSchema);
                ScriptableObject existingAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(targetAssetPath);
                if (existingAsset == null)
                {
                    error = $"Update Data requires an existing DataTable asset. Use Generate first.\n{targetAssetPath}";
                    return false;
                }

                if (!AreGeneratedTypesReady(parseResult))
                {
                    error = $"Generated type was not found for '{parseResult.ClassName}'. Use Generate first.";
                    return false;
                }

                string tableTypeName = CodeGenerator.ToTypeBaseName(parseResult.ClassName) + "DataTable";
                Type tableType = ScriptableExporter.FindTypeByName(tableTypeName);
                if (existingAsset.GetType() != tableType)
                {
                    error = $"Existing asset type does not match generated DataTable type. Use Generate instead of Update Data.\nAsset: {existingAsset.GetType().Name}\nExpected: {tableTypeName}";
                    return false;
                }

                if (!IsStructureCompatible(parseResult, usedSchema, out error))
                {
                    return false;
                }

                string targetFolder = Path.GetDirectoryName(targetAssetPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(targetFolder))
                {
                    error = $"Target folder could not be resolved: {targetAssetPath}";
                    return false;
                }

                var sheets = new List<ExcelSheetParseResult> { parseResult };
                var useDictMap = new Dictionary<string, bool> { { parseResult.SheetName, usedSchema.useDictionary } };
                ScriptableExporter.ExportAll(sheets, useDictMap, targetFolder);
                AssetDatabase.SaveAssets();

                ScriptableObject updatedAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(targetAssetPath);
                if (updatedAsset == null)
                {
                    error = $"Updated DataTable asset was not found: {targetAssetPath}";
                    return false;
                }

                string schemaGuid = ExcelRawImportUtility.GetSchemaGuid(usedSchema);
                AssetImporter importer = AssetImporter.GetAtPath(targetAssetPath);
                if (importer == null)
                {
                    error = $"AssetImporter was not found: {targetAssetPath}";
                    return false;
                }

                importer.userData = schemaGuid;
                importer.SaveAndReimport();

                usedSchema.targetTableAssetPath = targetAssetPath;
                usedSchema.targetTableAssetGuid = AssetDatabase.AssetPathToGUID(targetAssetPath);
                EditorUtility.SetDirty(usedSchema);

                raw.importStatus = RawImportStatus.Generated;
                raw.lastError = string.Empty;
                EditorUtility.SetDirty(raw);
                AssetDatabase.SaveAssets();

                Debug.Log($"[ExcelRawGenerate] Updated DataTable data: {targetAssetPath}");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message + "\n" + ex.StackTrace;
                raw.importStatus = RawImportStatus.Failed;
                raw.lastError = error;
                EditorUtility.SetDirty(raw);
                AssetDatabase.SaveAssets();
                Debug.LogError("[ExcelRawGenerate] " + error);
                return false;
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

        private static bool IsStructureCompatible(
            ExcelSheetParseResult parseResult,
            ExcelSheetSchemaSO schema,
            out string error)
        {
            error = string.Empty;

            string baseName = CodeGenerator.ToTypeBaseName(parseResult.ClassName);
            string scriptAssetPath = $"{ExcelImportPaths.GeneratedScriptFolder}/{baseName}DataTable.cs";
            string absoluteScriptPath = ToAbsoluteAssetPath(scriptAssetPath);
            string schemaSignature = ExcelSchemaAdapter.BuildTemplateSignature(schema);

            if (File.Exists(absoluteScriptPath))
            {
                string existingSignature = CodeGenerator.ExtractTemplateSignature(File.ReadAllText(absoluteScriptPath));
                if (!string.IsNullOrEmpty(existingSignature))
                {
                    if (!string.Equals(existingSignature, schemaSignature, StringComparison.Ordinal))
                    {
                        error = "Schema structure differs from generated script. Use Generate instead of Update Data.";
                        return false;
                    }

                    return true;
                }
            }

            return IsReflectionStructureCompatible(parseResult, out error);
        }

        private static bool IsReflectionStructureCompatible(ExcelSheetParseResult parseResult, out string error)
        {
            error = string.Empty;
            string baseName = CodeGenerator.ToTypeBaseName(parseResult.ClassName);
            Type rowType = ScriptableExporter.FindTypeByName(baseName + "Data");
            if (rowType == null)
            {
                error = $"Generated row type was not found: {baseName}Data";
                return false;
            }

            FieldInfo[] fields = rowType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            if (fields.Length != parseResult.Names.Count)
            {
                error = "Schema structure differs from generated type. Use Generate instead of Update Data.";
                return false;
            }

            for (int i = 0; i < parseResult.Names.Count; i++)
            {
                string expectedName = CodeGenerator.SanitizeFieldName(parseResult.Names[i]);
                Type expectedType = ToExpectedFieldType(parseResult.Types[i]);
                if (fields[i].Name != expectedName || fields[i].FieldType != expectedType)
                {
                    error = "Schema structure differs from generated type. Use Generate instead of Update Data.";
                    return false;
                }
            }

            return true;
        }

        private static Type ToExpectedFieldType(string generatorType)
        {
            switch (generatorType?.Trim().ToLowerInvariant())
            {
                case "int":
                    return typeof(int);
                case "float":
                case "percent":
                    return typeof(float);
                case "bool":
                    return typeof(bool);
                case "list<int>":
                    return typeof(List<int>);
                case "list<float>":
                    return typeof(List<float>);
                case "list<bool>":
                    return typeof(List<bool>);
                case "list<string>":
                    return typeof(List<string>);
                default:
                    return typeof(string);
            }
        }

        private static string ToAbsoluteAssetPath(string unityAssetPath)
        {
            string relative = unityAssetPath.Replace('\\', '/');
            if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Substring("Assets/".Length);
            }

            return Path.Combine(Application.dataPath, relative);
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
