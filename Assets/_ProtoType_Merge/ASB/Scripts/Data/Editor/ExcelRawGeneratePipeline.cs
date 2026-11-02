using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    internal enum UpdateBlockReason
    {
        None,
        NoSchema,
        ValidationFailed,
        NeedsGenerate
    }

    internal enum UpdateApplyResult
    {
        Applied,
        Created,
        AppliedLinkFailed,
        Failed,
        Uncertain
    }

    internal sealed class PreparedUpdate
    {
        public readonly RawExcelSheetSO Raw;
        public readonly ExcelSheetSchemaSO Schema;
        public readonly ExcelSheetParseResult ParseResult;
        public readonly string TargetAssetPath;
        public readonly bool CreateTable;

        public PreparedUpdate(
            RawExcelSheetSO raw,
            ExcelSheetSchemaSO schema,
            ExcelSheetParseResult parseResult,
            string targetAssetPath,
            bool createTable = false)
        {
            Raw = raw;
            Schema = schema;
            ParseResult = parseResult;
            TargetAssetPath = targetAssetPath;
            CreateTable = createTable;
        }
    }

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
            out UpdateApplyResult applyResult,
            out string error)
        {
            usedSchema = schema;
            applyResult = UpdateApplyResult.Failed;
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
                if (!TryPrepareUpdate(raw, schema, out PreparedUpdate prepared, out usedSchema, out _, out error))
                {
                    return false;
                }

                applyResult = ApplyUpdate(prepared, out _, out error);
                return applyResult == UpdateApplyResult.Applied;
            }
            catch (Exception ex)
            {
                error = ex.Message + "\n" + ex.StackTrace;
                raw.importStatus = RawImportStatus.Failed;
                raw.lastError = error;
                EditorUtility.SetDirty(raw);
                AssetDatabase.SaveAssetIfDirty(raw);
                Debug.LogError("[ExcelRawGenerate] " + error);
                return false;
            }
        }

        // Update Data 앞부분(검사)만 수행한다. 테이블은 건드리지 않는다.
        // preferredSchema가 null이면 이 Raw에 맞는 기존 스키마를 찾는다.
        // updateSchema가 false면 검증이 스키마 값을 바꾸지 않는다("검사만").
        public static bool TryPrepareUpdate(
            RawExcelSheetSO raw,
            ExcelSheetSchemaSO preferredSchema,
            out PreparedUpdate prepared,
            out ExcelSheetSchemaSO usedSchema,
            out UpdateBlockReason blockReason,
            out string error,
            bool updateSchema = true,
            bool allowCreateTable = false)
        {
            prepared = null;
            usedSchema = preferredSchema;
            blockReason = UpdateBlockReason.None;
            error = string.Empty;

            usedSchema = ExcelSchemaAdapter.ResolveExistingSchemaForRaw(raw, preferredSchema);
            if (usedSchema == null)
            {
                blockReason = UpdateBlockReason.NoSchema;
                error = "No schema exists for this Raw sheet. Create a schema first (auto-create/clone is disabled).";
                return false;
            }

            if (!ExcelSchemaAdapter.Validate(raw, usedSchema, out List<string> errors, out _, updateSchema))
            {
                blockReason = UpdateBlockReason.ValidationFailed;
                error = "Validation failed:\n" + string.Join("\n", errors);
                return false;
            }

            ExcelSheetParseResult parseResult = ExcelSchemaAdapter.ToParseResult(raw, usedSchema);
            string targetAssetPath = ExcelSchemaAdapter.GetTargetAssetPath(usedSchema);
            ScriptableObject existingAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(targetAssetPath);
            if (existingAsset == null && !allowCreateTable)
            {
                blockReason = UpdateBlockReason.NeedsGenerate;
                error = $"Update Data requires an existing DataTable asset. Use Generate first.\n{targetAssetPath}";
                return false;
            }

            if (existingAsset == null && (AssetDatabase.LoadMainAssetAtPath(targetAssetPath) != null ||
                File.Exists(ExcelRawImportUtility.ToAbsoluteAssetPath(targetAssetPath))))
            {
                blockReason = UpdateBlockReason.ValidationFailed;
                error = $"테이블 출력 경로에 다른 파일이 있어요: {targetAssetPath}";
                return false;
            }

            if (!AreGeneratedTypesReady(parseResult))
            {
                blockReason = UpdateBlockReason.NeedsGenerate;
                error = $"Generated type was not found for '{parseResult.ClassName}'. Use Generate first.";
                return false;
            }

            string tableTypeName = CodeGenerator.ToTypeBaseName(parseResult.ClassName) + "DataTable";
            Type tableType = ScriptableExporter.FindTypeByName(tableTypeName);
            if (tableType == null || !typeof(ScriptableObject).IsAssignableFrom(tableType))
            {
                blockReason = UpdateBlockReason.NeedsGenerate;
                error = $"생성된 DataTable 클래스를 찾을 수 없어요: {tableTypeName}";
                return false;
            }

            if (existingAsset != null && existingAsset.GetType() != tableType)
            {
                blockReason = UpdateBlockReason.NeedsGenerate;
                error = $"Existing asset type does not match generated DataTable type. Use Generate instead of Update Data.\nAsset: {existingAsset.GetType().Name}\nExpected: {tableTypeName}";
                return false;
            }

            if (!IsStructureCompatible(parseResult, usedSchema, out error) ||
                !IsLoadedTypeCompatible(parseResult, tableType, out error))
            {
                blockReason = UpdateBlockReason.NeedsGenerate;
                return false;
            }

            prepared = new PreparedUpdate(raw, usedSchema, parseResult, targetAssetPath, existingAsset == null);
            return true;
        }

        // TryPrepareUpdate를 통과한 시트의 테이블 데이터를 다시 채운다.
        // 출력이 실패하면 테이블은 이전 데이터 그대로다. 데이터 저장 뒤 연결 정보(스키마 GUID) 단계만
        // 실패하면 AppliedLinkFailed로 따로 알린다(데이터는 이미 반영됨, 되돌리지 않음).
        public static UpdateApplyResult ApplyUpdate(PreparedUpdate prepared, out bool dataChanged, out string error)
        {
            dataChanged = false;
            RawExcelSheetSO raw = prepared.Raw;
            string targetAssetPath = prepared.TargetAssetPath;

            if (prepared.CreateTable)
            {
                return CreateTable(prepared, out dataChanged, out error);
            }

            ScriptableObject tableAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(targetAssetPath);
            if (tableAsset == null)
            {
                error = $"DataTable asset was not found: {targetAssetPath}";
                return UpdateApplyResult.Failed;
            }

            if (!ScriptableExporter.TryReplaceTableData(prepared.ParseResult, tableAsset, out dataChanged, out error))
            {
                return UpdateApplyResult.Failed;
            }

            if (dataChanged)
            {
                try
                {
                    AssetDatabase.SaveAssetIfDirty(tableAsset);
                }
                catch (Exception ex)
                {
                    // 메모리는 이미 바뀌었고 디스크에 저장됐는지는 알 수 없어 되돌리지 않고 따로 알린다.
                    error = $"데이터를 바꾼 뒤 테이블 저장 중 오류가 났어요. 디스크에 저장됐는지 확인이 필요해요: {ex.Message}";
                    Debug.LogError($"[ExcelRawGenerate] {targetAssetPath}: {error}\n{ex.StackTrace}");
                    return UpdateApplyResult.Uncertain;
                }
            }

            bool linked = TryUpdateTableLink(prepared.Schema, targetAssetPath, out string linkError);
            try
            {
                raw.importStatus = RawImportStatus.Generated;
                raw.lastError = linked ? string.Empty : linkError;
                EditorUtility.SetDirty(raw);
                AssetDatabase.SaveAssetIfDirty(raw);
            }
            catch (Exception ex)
            {
                // Raw는 임시본이라 상태 저장 실패가 테이블 반영 결과를 바꾸지 않는다.
                Debug.LogWarning($"[ExcelRawGenerate] Raw status save failed for {raw.sheetName}: {ex.Message}");
            }

            if (!linked)
            {
                error = "데이터는 반영됐지만 연결 정보(스키마 GUID) 저장에 실패했어요.\n" + linkError;
                Debug.LogWarning($"[ExcelRawGenerate] {targetAssetPath}: {error}");
                return UpdateApplyResult.AppliedLinkFailed;
            }

            Debug.Log(dataChanged
                ? $"[ExcelRawGenerate] Updated DataTable data: {targetAssetPath}"
                : $"[ExcelRawGenerate] DataTable unchanged: {targetAssetPath}");
            return UpdateApplyResult.Applied;
        }

        private static UpdateApplyResult CreateTable(PreparedUpdate prepared, out bool dataChanged, out string error)
        {
            dataChanged = false;
            error = string.Empty;
            string path = prepared.TargetAssetPath;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(ExcelRawImportUtility.ToAbsoluteAssetPath(path)))
            {
                error = $"테이블 출력 경로가 이미 사용 중이에요: {path}";
                return UpdateApplyResult.Failed;
            }

            string typeName = CodeGenerator.ToTypeBaseName(prepared.ParseResult.ClassName) + "DataTable";
            Type type = ScriptableExporter.FindTypeByName(typeName);
            if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type) ||
                !IsLoadedTypeCompatible(prepared.ParseResult, type, out error))
            {
                if (string.IsNullOrEmpty(error)) error = $"테이블 클래스를 찾을 수 없어요: {typeName}";
                return UpdateApplyResult.Failed;
            }

            ScriptableObject table = ScriptableObject.CreateInstance(type);
            if (table == null)
            {
                error = $"테이블 인스턴스를 만들 수 없어요: {typeName}";
                return UpdateApplyResult.Failed;
            }

            try
            {
                // 먼저 메모리에서 모든 행과 키 표를 완성한 뒤 에셋으로 쓴다.
                if (!ScriptableExporter.TryReplaceTableData(prepared.ParseResult, table, out dataChanged, out error))
                {
                    return UpdateApplyResult.Failed;
                }

                ExcelRawImportUtility.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
                AssetDatabase.CreateAsset(table, path);
                if (!string.Equals(AssetDatabase.GetAssetPath(table), path, StringComparison.Ordinal) ||
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) == null)
                {
                    bool fileExists = File.Exists(ExcelRawImportUtility.ToAbsoluteAssetPath(path));
                    error = fileExists
                        ? $"테이블 생성 직후 에셋을 확인하지 못했어요. 파일 상태를 확인하세요: {path}"
                        : $"테이블 에셋이 생성되지 않았어요: {path}";
                    return fileExists ? UpdateApplyResult.Uncertain : UpdateApplyResult.Failed;
                }
            }
            catch (Exception ex)
            {
                bool assetMayExist = AssetDatabase.LoadMainAssetAtPath(path) != null ||
                    File.Exists(ExcelRawImportUtility.ToAbsoluteAssetPath(path));
                error = assetMayExist
                    ? $"테이블 생성 중 오류가 났어요. 파일이 생겼을 수 있어 상태를 확인하세요: {ex.Message}"
                    : $"테이블 생성 중 오류가 났어요: {ex.Message}";
                return assetMayExist ? UpdateApplyResult.Uncertain : UpdateApplyResult.Failed;
            }
            finally
            {
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(table)))
                {
                    UnityEngine.Object.DestroyImmediate(table);
                }
            }

            try
            {
                AssetDatabase.SaveAssetIfDirty(table);
            }
            catch (Exception ex)
            {
                error = $"테이블 생성 뒤 저장 중 오류가 났어요. 디스크 상태를 확인하세요: {ex.Message}";
                return UpdateApplyResult.Uncertain;
            }

            bool linked = TryUpdateTableLink(prepared.Schema, path, out string linkError);
            try
            {
                prepared.Raw.importStatus = RawImportStatus.Generated;
                prepared.Raw.lastError = linked ? string.Empty : linkError;
                EditorUtility.SetDirty(prepared.Raw);
                AssetDatabase.SaveAssetIfDirty(prepared.Raw);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ExcelRawGenerate] Raw status save failed for {prepared.Raw.sheetName}: {ex.Message}");
            }

            if (!linked)
            {
                error = "테이블은 생성됐지만 연결 정보 저장에 실패했어요.\n" + linkError;
                return UpdateApplyResult.AppliedLinkFailed;
            }

            return UpdateApplyResult.Created;
        }

        // 테이블 .meta의 스키마 GUID(Excel Data Viewer가 사용)와 스키마의 테이블 경로를 맞춘다.
        // SaveAndReimport는 에셋을 다시 가져오므로 값이 이미 같으면 부르지 않는다.
        private static bool TryUpdateTableLink(ExcelSheetSchemaSO schema, string targetAssetPath, out string error)
        {
            error = string.Empty;
            try
            {
                AssetImporter importer = AssetImporter.GetAtPath(targetAssetPath);
                if (importer == null)
                {
                    error = $"AssetImporter was not found: {targetAssetPath}";
                    return false;
                }

                string schemaGuid = ExcelRawImportUtility.GetSchemaGuid(schema);
                if (importer.userData != schemaGuid)
                {
                    importer.userData = schemaGuid;
                    importer.SaveAndReimport();
                }

                string targetAssetGuid = AssetDatabase.AssetPathToGUID(targetAssetPath);
                if (schema.targetTableAssetPath != targetAssetPath || schema.targetTableAssetGuid != targetAssetGuid)
                {
                    schema.targetTableAssetPath = targetAssetPath;
                    schema.targetTableAssetGuid = targetAssetGuid;
                    EditorUtility.SetDirty(schema);
                }

                AssetDatabase.SaveAssetIfDirty(schema);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // 생성 스크립트의 서명이 같아도 컴파일 전이면 실제 클래스가 다를 수 있다.
        // 지금 로드된 클래스에 DataList(List<행 타입>)와 모든 열의 필드·타입이 있는지 직접 확인한다.
        private static bool IsLoadedTypeCompatible(ExcelSheetParseResult parseResult, Type tableType, out string error)
        {
            error = string.Empty;
            string rowTypeName = CodeGenerator.ToTypeBaseName(parseResult.ClassName) + "Data";
            Type rowType = ScriptableExporter.FindTypeByName(rowTypeName);
            if (rowType == null)
            {
                error = $"Generated row type was not found: {rowTypeName}. Use Generate first.";
                return false;
            }

            FieldInfo dataListField = tableType.GetField("DataList", BindingFlags.Instance | BindingFlags.Public);
            if (dataListField == null || dataListField.FieldType != typeof(List<>).MakeGenericType(rowType))
            {
                error = $"{tableType.Name}.DataList is not List<{rowTypeName}>. Use Generate instead of Update Data.";
                return false;
            }

            for (int i = 0; i < parseResult.Names.Count; i++)
            {
                string fieldName = CodeGenerator.SanitizeFieldName(parseResult.Names[i]);
                Type expectedType = ToExpectedFieldType(parseResult.Types[i]);
                FieldInfo field = rowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
                if (field == null)
                {
                    error = $"Field '{fieldName}' is missing on {rowTypeName}. Use Generate instead of Update Data.";
                    return false;
                }

                if (field.FieldType != expectedType)
                {
                    error = $"Field '{fieldName}' is {field.FieldType.Name} but schema expects {expectedType.Name}. Use Generate instead of Update Data.";
                    return false;
                }
            }

            return true;
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
