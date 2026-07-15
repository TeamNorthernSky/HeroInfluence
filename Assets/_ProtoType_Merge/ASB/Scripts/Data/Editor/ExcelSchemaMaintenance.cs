using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    // legacy 마이그레이션 유틸. 절대 자동 실행하지 않는다. 메뉴에서 수동 실행하며
    // 항상 dry-run 리포트를 먼저 보여주고, 확인창 후에만 적용한다.
    internal static class ExcelSchemaMaintenance
    {
        [MenuItem("Tools/Excel Importer/Backfill Schema Identity (dry-run)")]
        private static void BackfillSchemaIdentity()
        {
            List<string> plan = BuildBackfillPlan(out _);
            if (plan.Count == 0)
            {
                EditorUtility.DisplayDialog("Backfill Schema Identity", "No schema needs backfilling.", "OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"{plan.Count} schema(s) would be updated:");
            sb.AppendLine();
            for (int i = 0; i < plan.Count; i++)
            {
                sb.AppendLine(plan[i]);
            }

            Debug.Log("[ExcelSchemaMaintenance] Backfill dry-run:\n" + sb);

            bool apply = EditorUtility.DisplayDialog(
                "Backfill Schema Identity (dry-run)",
                sb + "\nApply these changes? (Details logged to Console)",
                "Apply", "Cancel");
            if (apply)
            {
                ApplyBackfill();
            }
        }

        private static void ApplyBackfill()
        {
            BuildBackfillPlan(out List<Action> actions);
            for (int i = 0; i < actions.Count; i++)
            {
                actions[i]();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ExcelSchemaMaintenance] Backfilled {actions.Count} schema(s).");
        }

        private static List<string> BuildBackfillPlan(out List<Action> actions)
        {
            var plan = new List<string>();
            actions = new List<Action>();
            if (!AssetDatabase.IsValidFolder(ExcelImportPaths.SchemaFolder) ||
                !AssetDatabase.IsValidFolder(ExcelImportPaths.RawImportFolder))
            {
                return plan;
            }

            List<RawExcelSheetSO> raws = LoadAll<RawExcelSheetSO>(ExcelImportPaths.RawImportFolder);
            string[] schemaGuids = AssetDatabase.FindAssets("t:ExcelSheetSchemaSO", new[] { ExcelImportPaths.SchemaFolder });
            for (int i = 0; i < schemaGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(schemaGuids[i]);
                ExcelSheetSchemaSO schema = AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(path);
                if (schema == null)
                {
                    continue;
                }

                bool needGuid = string.IsNullOrEmpty(schema.sourceWorkbookGuid);
                bool needPhysical = string.IsNullOrEmpty(schema.physicalSheetId);
                if (!needGuid && !needPhysical)
                {
                    continue;
                }

                RawExcelSheetSO match = FindRawForSchema(raws, schema);
                if (match == null)
                {
                    plan.Add($"[skip] {schema.name}: no matching raw found");
                    continue;
                }

                var changes = new List<string>();
                if (needGuid)
                {
                    changes.Add($"sourceWorkbookGuid='{match.sourceWorkbookGuid}'");
                }

                if (needPhysical)
                {
                    changes.Add($"physicalSheetId='{match.physicalSheetId}'");
                }

                plan.Add($"[fill] {schema.name} <- raw '{match.workbookFileName}/{match.sheetName}': {string.Join(", ", changes)}");

                ExcelSheetSchemaSO capturedSchema = schema;
                RawExcelSheetSO capturedRaw = match;
                actions.Add(() =>
                {
                    if (string.IsNullOrEmpty(capturedSchema.sourceWorkbookGuid))
                    {
                        capturedSchema.sourceWorkbookGuid = capturedRaw.sourceWorkbookGuid;
                    }

                    if (string.IsNullOrEmpty(capturedSchema.sourceWorkbookPath))
                    {
                        capturedSchema.sourceWorkbookPath = capturedRaw.sourceWorkbookPath;
                    }

                    if (string.IsNullOrEmpty(capturedSchema.physicalSheetId))
                    {
                        capturedSchema.physicalSheetId = capturedRaw.physicalSheetId;
                    }

                    if (string.IsNullOrEmpty(capturedSchema.logicalSheetKey))
                    {
                        capturedSchema.logicalSheetKey = capturedRaw.logicalSheetKey;
                    }

                    EditorUtility.SetDirty(capturedSchema);
                });
            }

            return plan;
        }

        private static RawExcelSheetSO FindRawForSchema(List<RawExcelSheetSO> raws, ExcelSheetSchemaSO schema)
        {
            for (int i = 0; i < raws.Count; i++)
            {
                RawExcelSheetSO raw = raws[i];
                if (raw == null || !string.Equals(raw.sheetName, schema.sourceSheetName, StringComparison.Ordinal))
                {
                    continue;
                }

                bool sameWorkbook =
                    (!string.IsNullOrEmpty(schema.workbookFileName) &&
                     string.Equals(raw.workbookFileName, schema.workbookFileName, StringComparison.Ordinal)) ||
                    (!string.IsNullOrEmpty(schema.sourceWorkbookPath) &&
                     string.Equals(raw.sourceWorkbookPath, schema.sourceWorkbookPath, StringComparison.Ordinal));
                if (sameWorkbook)
                {
                    return raw;
                }
            }

            return null;
        }

        private static List<T> LoadAll<T>(string folder) where T : UnityEngine.Object
        {
            var list = new List<T>();
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    list.Add(asset);
                }
            }

            return list;
        }
    }
}
