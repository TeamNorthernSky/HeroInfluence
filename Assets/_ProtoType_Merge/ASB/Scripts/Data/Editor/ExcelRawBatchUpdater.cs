using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    internal enum SheetBatchState
    {
        Updated,
        Passed,
        Failed,
        NeedsGenerate,
        Skipped,
        UpdatedLinkFailed,
        UpdateUncertain
    }

    [Serializable]
    internal sealed class SheetBatchResult
    {
        public string sheetName;
        public string physicalSheetId;
        public string schemaPath;
        public SheetBatchState state;
        public string message;
    }

    [Serializable]
    internal sealed class WorkbookBatchReport
    {
        public string workbookPath;
        public string workbookName;
        public bool checkOnly;
        public string fatalError;
        public List<SheetBatchResult> Sheets = new List<SheetBatchResult>();

        public int Count(SheetBatchState state)
        {
            return Sheets.Count(s => s.state == state);
        }

        public SheetBatchResult Find(string physicalSheetId)
        {
            if (string.IsNullOrEmpty(physicalSheetId))
            {
                return null;
            }

            return Sheets.FirstOrDefault(s => s.physicalSheetId == physicalSheetId);
        }

        // 시트 단위로 다시 돌린 결과를 기존 결과에 덮어쓴다.
        public void Merge(WorkbookBatchReport other)
        {
            for (int i = 0; i < other.Sheets.Count; i++)
            {
                SheetBatchResult incoming = other.Sheets[i];
                int index = Sheets.FindIndex(s => s.physicalSheetId == incoming.physicalSheetId);
                if (index >= 0)
                {
                    Sheets[index] = incoming;
                }
                else
                {
                    Sheets.Add(incoming);
                }
            }

            fatalError = other.fatalError;
        }
    }

    // 마커 없는 엑셀 한 개의 시트를 한 번에 Update Data 한다.
    // 엑셀을 다시 읽고 → 모든 시트를 먼저 검사한 뒤 → 통과한 시트만 테이블에 반영한다.
    // 실패한 시트는 테이블을 건드리지 않고 결과에 남겨 Raw Sheet Mapper에서 시트 단위로 고치게 한다.
    // Generate(코드 생성·컴파일)는 하지 않는다. 구조가 바뀌었거나 테이블이 없는 시트는 NeedsGenerate로 넘긴다.
    internal static class ExcelRawBatchUpdater
    {
        private const string ProgressTitle = "엑셀 전체 업데이트";
        public const string UnchangedMessage = "데이터 변경 없음";

        // 스키마가 하나라도 있고 파일이 남아 있는 엑셀만 대상으로 한다.
        public static List<string> FindKnownWorkbookPaths()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(ExcelImportPaths.SchemaFolder))
            {
                return new List<string>();
            }

            string[] guids = AssetDatabase.FindAssets("t:ExcelSheetSchemaSO", new[] { ExcelImportPaths.SchemaFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                ExcelSheetSchemaSO schema = AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                string path = ResolveWorkbookPath(schema);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            List<string> result = paths.ToList();
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public static WorkbookBatchReport Run(string workbookAssetPath, bool checkOnly, ICollection<string> sheetNames = null)
        {
            var report = new WorkbookBatchReport
            {
                workbookPath = workbookAssetPath,
                workbookName = Path.GetFileNameWithoutExtension(workbookAssetPath),
                checkOnly = checkOnly
            };

            try
            {
                EditorUtility.DisplayProgressBar(ProgressTitle, $"{report.workbookName} 읽는 중...", 0f);
                string workbookGuid = AssetDatabase.AssetPathToGUID(workbookAssetPath);
                var partialMarkerSheets = new List<string>();
                List<RawExcelSheetSO> raws = ExcelRawImportUtility.ImportMarkerlessSheets(
                    ExcelRawImportUtility.ToAbsoluteAssetPath(workbookAssetPath),
                    sheetNames,
                    partialMarkerSheets);

                for (int i = 0; i < partialMarkerSheets.Count; i++)
                {
                    report.Sheets.Add(new SheetBatchResult
                    {
                        sheetName = partialMarkerSheets[i],
                        physicalSheetId = ExcelRawImportUtility.MakePhysicalSheetId(workbookGuid, partialMarkerSheets[i]),
                        state = SheetBatchState.Failed,
                        message = "#Type/#Name/#Data 마커가 일부만 있어요. 마커를 모두 지우거나 모두 채우세요."
                    });
                }

                var passed = new List<KeyValuePair<SheetBatchResult, PreparedUpdate>>();
                for (int i = 0; i < raws.Count; i++)
                {
                    RawExcelSheetSO raw = raws[i];
                    EditorUtility.DisplayProgressBar(ProgressTitle, $"검사 중: {raw.sheetName}", 0.1f + 0.4f * i / raws.Count);
                    SheetBatchResult result = PrepareSheet(raw, checkOnly, out PreparedUpdate prepared);
                    report.Sheets.Add(result);
                    if (prepared != null)
                    {
                        passed.Add(new KeyValuePair<SheetBatchResult, PreparedUpdate>(result, prepared));
                    }
                }

                AddMissingSheets(report, raws, workbookAssetPath, workbookGuid, sheetNames);

                if (!checkOnly)
                {
                    for (int i = 0; i < passed.Count; i++)
                    {
                        SheetBatchResult result = passed[i].Key;
                        EditorUtility.DisplayProgressBar(ProgressTitle, $"반영 중: {result.sheetName}", 0.5f + 0.5f * i / passed.Count);
                        ApplySheet(passed[i].Value, result);
                    }
                }
            }
            catch (Exception ex)
            {
                report.fatalError = ex.Message;
                Debug.LogError($"[ExcelRawBatch] {report.workbookName}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            LogSummary(report);
            return report;
        }

        // 검사만이면 스키마 값을 바꾸지 않는다. 저장은 이 시트의 Raw(와 반영 모드의 스키마)만 한다.
        private static SheetBatchResult PrepareSheet(RawExcelSheetSO raw, bool checkOnly, out PreparedUpdate prepared)
        {
            prepared = null;
            var result = new SheetBatchResult
            {
                sheetName = raw.sheetName,
                physicalSheetId = raw.physicalSheetId
            };

            try
            {
                bool ok = ExcelRawGeneratePipeline.TryPrepareUpdate(
                    raw,
                    null,
                    out prepared,
                    out ExcelSheetSchemaSO schema,
                    out UpdateBlockReason reason,
                    out string error,
                    !checkOnly);
                AssetDatabase.SaveAssetIfDirty(raw);
                if (!checkOnly && schema != null)
                {
                    AssetDatabase.SaveAssetIfDirty(schema);
                }

                result.schemaPath = schema != null ? AssetDatabase.GetAssetPath(schema) : string.Empty;
                if (ok)
                {
                    result.state = SheetBatchState.Passed;
                    return result;
                }

                switch (reason)
                {
                    case UpdateBlockReason.NoSchema:
                        result.state = SheetBatchState.Skipped;
                        result.message = "스키마가 없어 건너뛰었어요. 데이터 시트라면 New Schema로 만드세요.";
                        break;
                    case UpdateBlockReason.NeedsGenerate:
                        result.state = SheetBatchState.NeedsGenerate;
                        result.message = "구조가 바뀌었거나 테이블이 없어 Generate가 필요해요.\n" + error;
                        break;
                    default:
                        result.state = SheetBatchState.Failed;
                        result.message = error;
                        break;
                }
            }
            catch (Exception ex)
            {
                prepared = null;
                result.state = SheetBatchState.Failed;
                result.message = ex.Message;
                Debug.LogError($"[ExcelRawBatch] {raw.sheetName}: {ex.Message}\n{ex.StackTrace}");
            }

            return result;
        }

        private static void ApplySheet(PreparedUpdate prepared, SheetBatchResult result)
        {
            try
            {
                UpdateApplyResult applyResult = ExcelRawGeneratePipeline.ApplyUpdate(prepared, out bool dataChanged, out string error);
                switch (applyResult)
                {
                    case UpdateApplyResult.Applied:
                        result.state = SheetBatchState.Updated;
                        result.message = dataChanged ? string.Empty : UnchangedMessage;
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
                prepared.Raw.importStatus = RawImportStatus.Failed;
                prepared.Raw.lastError = ex.Message + "\n" + ex.StackTrace;
                EditorUtility.SetDirty(prepared.Raw);
                AssetDatabase.SaveAssetIfDirty(prepared.Raw);
                Debug.LogError($"[ExcelRawBatch] {result.sheetName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // 스키마는 있는데 엑셀에서 시트를 못 찾으면 그 테이블은 갱신되지 않으므로 실패로 알린다.
        // 엑셀 경로는 같은데 GUID가 다른 스키마(파일을 지웠다 다시 넣은 경우)도 연결이 끊긴 것으로 알린다.
        private static void AddMissingSheets(
            WorkbookBatchReport report,
            List<RawExcelSheetSO> raws,
            string workbookAssetPath,
            string workbookGuid,
            ICollection<string> sheetNames)
        {
            if (string.IsNullOrEmpty(workbookGuid) || !AssetDatabase.IsValidFolder(ExcelImportPaths.SchemaFolder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:ExcelSheetSchemaSO", new[] { ExcelImportPaths.SchemaFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string schemaPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                ExcelSheetSchemaSO schema = AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(schemaPath);
                if (schema == null)
                {
                    continue;
                }

                bool sameGuid = string.Equals(schema.sourceWorkbookGuid, workbookGuid, StringComparison.Ordinal);
                bool samePathOnly = !sameGuid && string.Equals(schema.sourceWorkbookPath, workbookAssetPath, StringComparison.Ordinal);
                if (!sameGuid && !samePathOnly)
                {
                    continue;
                }

                if (sheetNames != null && !sheetNames.Contains(schema.sourceSheetName))
                {
                    continue;
                }

                if (raws.Any(raw => ExcelSchemaAdapter.SchemaMatchesRaw(raw, schema)))
                {
                    continue;
                }

                string physicalSheetId = string.IsNullOrEmpty(schema.physicalSheetId)
                    ? ExcelRawImportUtility.MakePhysicalSheetId(workbookGuid, schema.sourceSheetName)
                    : schema.physicalSheetId;
                if (report.Find(physicalSheetId) != null)
                {
                    continue;
                }

                report.Sheets.Add(new SheetBatchResult
                {
                    sheetName = schema.sourceSheetName,
                    physicalSheetId = physicalSheetId,
                    schemaPath = schemaPath,
                    state = SheetBatchState.Failed,
                    message = samePathOnly
                        ? "스키마에 저장된 엑셀 GUID가 지금 파일과 달라 연결이 끊겼어요(파일을 지웠다 다시 넣었을 수 있어요). 이 스키마의 테이블은 갱신되지 않았어요."
                        : "엑셀에서 마커 없는 이 시트를 찾지 못했어요(삭제·이름 변경·마커 추가). 이 스키마의 테이블은 갱신되지 않았어요."
                });
            }
        }

        // 시트 단위로 다시 처리해야 하는 상태. 반영·연결 실패는 다시 돌리면 데이터를 다시 쓰고 연결 정보를 재시도한다.
        public static bool IsProblem(SheetBatchState state)
        {
            return state == SheetBatchState.Failed ||
                   state == SheetBatchState.NeedsGenerate ||
                   state == SheetBatchState.UpdatedLinkFailed ||
                   state == SheetBatchState.UpdateUncertain;
        }

        private static string ResolveWorkbookPath(ExcelSheetSchemaSO schema)
        {
            if (schema == null)
            {
                return string.Empty;
            }

            string path = string.IsNullOrEmpty(schema.sourceWorkbookGuid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(schema.sourceWorkbookGuid);
            if (string.IsNullOrEmpty(path))
            {
                path = schema.sourceWorkbookPath;
            }

            return !string.IsNullOrEmpty(path) && File.Exists(ExcelRawImportUtility.ToAbsoluteAssetPath(path))
                ? path
                : string.Empty;
        }

        private static void LogSummary(WorkbookBatchReport report)
        {
            string summary =
                $"[ExcelRawBatch] {report.workbookName}{(report.checkOnly ? " (검사만)" : string.Empty)}: " +
                $"반영 {report.Count(SheetBatchState.Updated)}, 통과 {report.Count(SheetBatchState.Passed)}, " +
                $"반영·연결 실패 {report.Count(SheetBatchState.UpdatedLinkFailed)}, " +
                $"반영 불확실 {report.Count(SheetBatchState.UpdateUncertain)}, " +
                $"실패 {report.Count(SheetBatchState.Failed)}, Generate 필요 {report.Count(SheetBatchState.NeedsGenerate)}, " +
                $"건너뜀 {report.Count(SheetBatchState.Skipped)}";
            List<string> problems = report.Sheets
                .Where(s => IsProblem(s.state))
                .Select(s => s.sheetName)
                .ToList();
            if (problems.Count == 0 && string.IsNullOrEmpty(report.fatalError))
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogWarning(summary + (problems.Count > 0 ? "\n문제 시트: " + string.Join(", ", problems) : string.Empty));
            }
        }
    }
}
