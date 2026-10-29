using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    internal enum SchemaPreviewState
    {
        Match,
        NewColumns,
        HeaderMismatch,
        NoSource,
        DuplicateSource,
        AlreadyHasSchema,
        DuplicateTarget,
        BrokenLink
    }

    internal sealed class SchemaPreviewItem
    {
        public RawExcelSheetSO raw;
        public ExcelSheetSchemaSO sourceSchema;
        public ExcelSheetSchemaSO targetSchema;
        public SchemaPreviewState state;
        public string message;
        public bool selected;

        public bool CanApply => state == SchemaPreviewState.Match || state == SchemaPreviewState.NewColumns;
    }

    internal sealed class SchemaBatchPreview
    {
        public string targetPath;
        public string sourcePath;
        public readonly List<SchemaPreviewItem> items = new List<SchemaPreviewItem>();
        public readonly List<string> missingSourceSheets = new List<string>();
        public readonly List<string> partialMarkerSheets = new List<string>();
        public readonly Dictionary<string, string> existingOutputOwners = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    // 원본 엑셀의 스키마를 시트 이름으로만 짝지어 새 엑셀에 복사한다.
    internal static class ExcelSchemaBatchApplier
    {
        public static List<string> SourceCandidates(string targetPath)
        {
            return ExcelRawBatchUpdater.FindKnownWorkbookPaths()
                .Where(path => !string.Equals(path, targetPath, StringComparison.Ordinal) &&
                    FindSchemas(AssetDatabase.AssetPathToGUID(path)).Count > 0)
                .ToList();
        }

        public static string DefaultSource(string targetPath, IList<string> candidates)
        {
            string targetName = Path.GetFileNameWithoutExtension(targetPath);
            return candidates
                .OrderByDescending(path => CommonPrefixLength(targetName, Path.GetFileNameWithoutExtension(path)))
                .ThenByDescending(path => Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal)
                .ThenByDescending(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static int CommonPrefixLength(string left, string right)
        {
            int count = 0;
            while (count < left.Length && count < right.Length && left[count] == right[count]) count++;
            return count;
        }

        public static SchemaBatchPreview BuildPreview(
            string targetPath,
            string sourcePath,
            List<RawExcelSheetSO> raws,
            ICollection<string> partialMarkerSheets = null)
        {
            var preview = new SchemaBatchPreview { targetPath = targetPath, sourcePath = sourcePath };
            if (partialMarkerSheets != null) preview.partialMarkerSheets.AddRange(partialMarkerSheets);

            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            List<ExcelSheetSchemaSO> allSchemas = LoadAllSchemas();
            foreach (ExcelSheetSchemaSO schema in allSchemas)
            {
                string path = ExcelSchemaAdapter.GetTargetAssetPath(schema);
                if (!preview.existingOutputOwners.ContainsKey(path))
                    preview.existingOutputOwners.Add(path, schema.sourceSheetName);
            }
            List<ExcelSheetSchemaSO> sourceSchemas = allSchemas
                .Where(schema => schema.sourceWorkbookGuid == sourceGuid).ToList();
            var byName = sourceSchemas.GroupBy(s => s.sourceSheetName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            var targetNames = new HashSet<string>(raws.Select(raw => raw.sheetName), StringComparer.Ordinal);

            foreach (RawExcelSheetSO raw in raws)
            {
                var item = new SchemaPreviewItem { raw = raw };
                ExcelSheetSchemaSO existing = ExcelSchemaAdapter.FindExistingSchemaForRaw(raw, out List<ExcelSheetSchemaSO> duplicates);
                List<ExcelSheetSchemaSO> brokenLinks = allSchemas.Where(schema =>
                    string.Equals(schema.sourceWorkbookPath, raw.sourceWorkbookPath, StringComparison.Ordinal) &&
                    string.Equals(schema.sourceSheetName, raw.sheetName, StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(schema.sourceWorkbookGuid) &&
                    !string.Equals(schema.sourceWorkbookGuid, raw.sourceWorkbookGuid, StringComparison.Ordinal))
                    .ToList();
                if (duplicates.Count > 0 || brokenLinks.Count > 1 || (existing != null && brokenLinks.Count > 0))
                {
                    item.state = SchemaPreviewState.DuplicateTarget;
                    item.message = "이 시트에 현재 또는 이전 GUID로 연결된 스키마가 여러 개 있어요. 개별 확인이 필요해요.";
                }
                else if (existing != null)
                {
                    item.targetSchema = existing;
                    item.state = SchemaPreviewState.AlreadyHasSchema;
                    item.message = "이미 스키마가 있어 건드리지 않아요.";
                }
                else if (brokenLinks.Count == 1)
                {
                    item.targetSchema = brokenLinks[0];
                    item.state = SchemaPreviewState.BrokenLink;
                    item.message = "엑셀 경로와 시트 이름은 같지만 GUID가 달라 기존 스키마와 연결이 끊겼어요. " +
                        "새 스키마를 만들지 말고 기존 스키마를 연결하세요: " + AssetDatabase.GetAssetPath(brokenLinks[0]);
                }
                else if (!byName.TryGetValue(raw.sheetName, out List<ExcelSheetSchemaSO> matches))
                {
                    item.state = SchemaPreviewState.NoSource;
                    item.message = "원본 엑셀에 같은 이름의 스키마가 없어요.";
                }
                else if (matches.Count != 1)
                {
                    item.state = SchemaPreviewState.DuplicateSource;
                    item.message = "원본 엑셀에 같은 이름의 스키마가 여러 개 있어요.";
                }
                else
                {
                    item.sourceSchema = matches[0];
                    List<string> headerErrors = ExcelSchemaAdapter.ValidateHeaders(raw, item.sourceSchema);
                    if (headerErrors.Count > 0)
                    {
                        item.state = SchemaPreviewState.HeaderMismatch;
                        item.message = string.Join("\n", headerErrors);
                    }
                    else
                    {
                        List<string> extraHeaders = FindNewHeaders(raw, item.sourceSchema);
                        item.state = extraHeaders.Count > 0 ? SchemaPreviewState.NewColumns : SchemaPreviewState.Match;
                        item.message = extraHeaders.Count > 0
                            ? "새 열: " + string.Join(", ", extraHeaders) + ". 새 열은 테이블에 들어가지 않아요. 포함하려면 Rebuild와 Generate를 하세요."
                            : "현재 검증 기준으로 헤더가 일치해요.";
                        item.selected = true;
                    }
                }

                preview.items.Add(item);
            }

            foreach (string name in byName.Keys.OrderBy(name => name, StringComparer.Ordinal))
            {
                if (!targetNames.Contains(name)) preview.missingSourceSheets.Add(name);
            }

            return preview;
        }

        private static List<string> FindNewHeaders(RawExcelSheetSO raw, ExcelSheetSchemaSO schema)
        {
            if (raw.Rows == null || schema.headerRowIndex < 0 || schema.headerRowIndex >= raw.Rows.Count)
                return new List<string>();
            var known = new HashSet<string>(schema.Columns.Where(c => c != null && !string.IsNullOrEmpty(c.sourceHeaderName))
                .Select(c => c.sourceHeaderName), StringComparer.Ordinal);
            return (raw.Rows[schema.headerRowIndex]?.Cells ?? new List<string>())
                .Where(header => !string.IsNullOrWhiteSpace(header) && !known.Contains(header))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static List<ExcelSheetSchemaSO> FindSchemas(string workbookGuid)
        {
            if (string.IsNullOrEmpty(workbookGuid)) return new List<ExcelSheetSchemaSO>();
            return LoadAllSchemas().Where(schema => schema.sourceWorkbookGuid == workbookGuid).ToList();
        }

        private static List<ExcelSheetSchemaSO> LoadAllSchemas()
        {
            var result = new List<ExcelSheetSchemaSO>();
            if (!AssetDatabase.IsValidFolder(ExcelImportPaths.SchemaFolder)) return result;
            foreach (string guid in AssetDatabase.FindAssets("t:ExcelSheetSchemaSO", new[] { ExcelImportPaths.SchemaFolder }))
            {
                ExcelSheetSchemaSO schema = AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (schema != null) result.Add(schema);
            }
            return result;
        }

        public static bool TryReconnectBrokenLink(RawExcelSheetSO raw, ExcelSheetSchemaSO schema, out string error)
        {
            error = string.Empty;
            if (raw == null || schema == null || string.IsNullOrEmpty(raw.sourceWorkbookGuid))
            {
                error = "시트 또는 기존 스키마를 찾을 수 없어요.";
                return false;
            }

            ExcelSheetSchemaSO current = ExcelSchemaAdapter.FindExistingSchemaForRaw(raw, out _);
            List<ExcelSheetSchemaSO> brokenLinks = LoadAllSchemas().Where(candidate =>
                string.Equals(candidate.sourceWorkbookPath, raw.sourceWorkbookPath, StringComparison.Ordinal) &&
                string.Equals(candidate.sourceSheetName, raw.sheetName, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(candidate.sourceWorkbookGuid) &&
                !string.Equals(candidate.sourceWorkbookGuid, raw.sourceWorkbookGuid, StringComparison.Ordinal))
                .ToList();
            if (current != null || brokenLinks.Count != 1 || brokenLinks[0] != schema)
            {
                error = "연결 상태가 바뀌었거나 중복 스키마가 있어요. 미리보기를 새로 읽고 확인하세요.";
                return false;
            }

            try
            {
                ExcelSchemaAdapter.RelinkSchemaToRaw(raw, schema);
                raw.importStatus = RawImportStatus.SchemaApplied;
                raw.lastError = string.Empty;
                EditorUtility.SetDirty(raw);
                AssetDatabase.SaveAssetIfDirty(raw);
                return true;
            }
            catch (Exception ex)
            {
                error = "스키마 연결 저장 중 오류가 났어요. 다시 읽어 상태를 확인하세요: " + ex.Message;
                return false;
            }
        }

        // 선택한 시트끼리, 또는 기존 스키마와 출력 경로가 겹치면 스키마를 쓰기 전에 막는다.
        public static Dictionary<string, string> FindOutputConflicts(
            SchemaBatchPreview preview, ICollection<string> selectedSheetNames)
        {
            var conflicts = new Dictionary<string, string>(StringComparer.Ordinal);
            var selected = new HashSet<string>(selectedSheetNames ?? new string[0], StringComparer.Ordinal);
            List<SchemaPreviewItem> items = preview.items
                .Where(item => item.CanApply && selected.Contains(item.raw.sheetName)).ToList();
            var byPath = items.GroupBy(item => ExpectedOutputPath(item.raw), StringComparer.Ordinal);
            foreach (var group in byPath)
            {
                List<SchemaPreviewItem> samePath = group.ToList();
                if (samePath.Count < 2) continue;
                string message = "선택한 시트 " + string.Join(", ", samePath.Select(item => item.raw.sheetName)) +
                    "의 테이블 출력 경로가 같아요: " + group.Key;
                foreach (SchemaPreviewItem item in samePath) conflicts[item.raw.sheetName] = message;
            }

            foreach (SchemaPreviewItem item in items)
            {
                if (conflicts.ContainsKey(item.raw.sheetName)) continue;
                string expected = ExpectedOutputPath(item.raw);
                if (preview.existingOutputOwners.TryGetValue(expected, out string ownerName))
                {
                    conflicts[item.raw.sheetName] = "기존 스키마 '" + ownerName +
                        "'와 테이블 출력 경로가 같아요: " + expected;
                }
            }
            return conflicts;
        }

        private static string ExpectedOutputPath(RawExcelSheetSO raw)
        {
            return ExcelImportPaths.GetTableAssetFolder(raw.workbookFileName) + "/" +
                CodeGenerator.ToTypeBaseName(raw.sheetName) + "DataTable.asset";
        }

        public static WorkbookBatchReport Run(
            string targetPath,
            string sourcePath,
            bool createTables,
            ICollection<string> selectedSheetNames,
            bool retryExisting = false)
        {
            var report = new WorkbookBatchReport
            {
                workbookPath = targetPath,
                workbookName = Path.GetFileNameWithoutExtension(targetPath),
                operation = WorkbookBatchOperation.SchemaApply,
                sourceWorkbookPath = sourcePath,
                createTables = createTables
            };
            try
            {
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sourcePath)))
                    throw new InvalidOperationException("원본 엑셀을 찾을 수 없어요.");
                var partial = new List<string>();
                List<RawExcelSheetSO> raws = ExcelRawImportUtility.ImportMarkerlessSheets(
                    ExcelRawImportUtility.ToAbsoluteAssetPath(targetPath),
                    retryExisting ? selectedSheetNames : null,
                    partial);
                SchemaBatchPreview preview = BuildPreview(targetPath, sourcePath, raws, partial);
                var selected = new HashSet<string>(selectedSheetNames ?? new string[0], StringComparer.Ordinal);
                Dictionary<string, string> outputConflicts = FindOutputConflicts(preview, selected);

                foreach (SchemaPreviewItem item in preview.items)
                {
                    var result = new SheetBatchResult
                    {
                        sheetName = item.raw.sheetName,
                        physicalSheetId = item.raw.physicalSheetId,
                        schemaPath = item.targetSchema != null ? AssetDatabase.GetAssetPath(item.targetSchema) : string.Empty
                    };
                    report.Sheets.Add(result);
                    if (item.state == SchemaPreviewState.BrokenLink)
                    {
                        result.state = SheetBatchState.Failed;
                        result.message = item.message;
                        continue;
                    }
                    if (!selected.Contains(item.raw.sheetName))
                    {
                        result.state = item.state == SchemaPreviewState.HeaderMismatch ||
                            item.state == SchemaPreviewState.DuplicateSource ||
                            item.state == SchemaPreviewState.DuplicateTarget
                            ? SheetBatchState.Failed : SheetBatchState.Skipped;
                        result.message = item.CanApply ? "사용자가 선택하지 않았어요." : item.message;
                        continue;
                    }
                    if (outputConflicts.TryGetValue(item.raw.sheetName, out string conflict))
                    {
                        result.state = SheetBatchState.Failed;
                        result.message = conflict;
                        continue;
                    }

                    try
                    {
                        ExcelSheetSchemaSO schema = item.targetSchema;
                        if (schema == null)
                        {
                            if (!item.CanApply)
                            {
                                result.state = item.state == SchemaPreviewState.NoSource
                                    ? SheetBatchState.Skipped : SheetBatchState.Failed;
                                result.message = item.message;
                                continue;
                            }
                            schema = ExcelSchemaAdapter.CreateSchemaFromTemplate(item.raw, item.sourceSchema);
                            item.raw.importStatus = RawImportStatus.SchemaApplied;
                            EditorUtility.SetDirty(item.raw);
                            AssetDatabase.SaveAssetIfDirty(item.raw);
                        }
                        else if (!retryExisting)
                        {
                            result.state = SheetBatchState.Skipped;
                            result.message = "이미 스키마가 있어 건드리지 않았어요.";
                            continue;
                        }

                        result.schemaPath = AssetDatabase.GetAssetPath(schema);
                        if (!createTables)
                        {
                            result.state = SheetBatchState.SchemaApplied;
                            result.message = "스키마를 적용했어요.";
                            continue;
                        }

                        if (!retryExisting && AssetDatabase.LoadMainAssetAtPath(ExcelSchemaAdapter.GetTargetAssetPath(schema)) != null)
                        {
                            result.state = SheetBatchState.NeedsGenerate;
                            result.message = "테이블 출력 경로에 이미 에셋이 있어 자동 생성을 건너뛰었어요. 시트 단위로 확인하세요.";
                            continue;
                        }

                        bool preparedOk = ExcelRawGeneratePipeline.TryPrepareUpdate(
                            item.raw, schema, out PreparedUpdate prepared, out _,
                            out UpdateBlockReason reason, out string error,
                            true, true);
                        AssetDatabase.SaveAssetIfDirty(item.raw);
                        AssetDatabase.SaveAssetIfDirty(schema);
                        if (!preparedOk)
                        {
                            result.state = reason == UpdateBlockReason.NeedsGenerate
                                ? SheetBatchState.NeedsGenerate : SheetBatchState.Failed;
                            result.message = error;
                            continue;
                        }

                        UpdateApplyResult applied = ExcelRawGeneratePipeline.ApplyUpdate(prepared, out bool changed, out error);
                        switch (applied)
                        {
                            case UpdateApplyResult.Created:
                                result.state = SheetBatchState.Created;
                                result.message = "새 테이블을 생성했어요.";
                                break;
                            case UpdateApplyResult.Applied:
                                result.state = SheetBatchState.Updated;
                                result.message = changed ? "테이블을 갱신했어요." : ExcelRawBatchUpdater.UnchangedMessage;
                                break;
                            case UpdateApplyResult.AppliedLinkFailed:
                                result.state = SheetBatchState.UpdatedLinkFailed;
                                result.message = error;
                                break;
                            case UpdateApplyResult.Uncertain:
                                result.state = SheetBatchState.UpdateUncertain;
                                result.message = error;
                                break;
                            default:
                                result.state = SheetBatchState.Failed;
                                result.message = error;
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.state = SheetBatchState.Failed;
                        result.message = ex.Message;
                        Debug.LogError($"[ExcelSchemaBatch] {item.raw.sheetName}: {ex}");
                    }
                }

                foreach (string name in partial)
                {
                    report.Sheets.Add(new SheetBatchResult
                    {
                        sheetName = name,
                        physicalSheetId = ExcelRawImportUtility.MakePhysicalSheetId(AssetDatabase.AssetPathToGUID(targetPath), name),
                        state = SheetBatchState.Failed,
                        message = "#Type/#Name/#Data 마커가 일부만 있어요."
                    });
                }
            }
            catch (Exception ex)
            {
                report.fatalError = ex.Message;
                Debug.LogError($"[ExcelSchemaBatch] {ex}");
            }
            return report;
        }
    }
}
