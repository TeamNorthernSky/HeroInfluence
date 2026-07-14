using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    internal enum ExcelSheetMarkerState
    {
        None,
        Complete,
        Partial
    }

    internal static class ExcelRawImportUtility
    {
        private static readonly DataFormatter CellFormatter = new DataFormatter(CultureInfo.InvariantCulture);

        public static List<RawExcelSheetSO> ImportMarkerlessSheets(string absoluteExcelPath)
        {
            if (string.IsNullOrWhiteSpace(absoluteExcelPath) || !File.Exists(absoluteExcelPath))
            {
                throw new FileNotFoundException("Excel file not found.", absoluteExcelPath);
            }

            string assetPath = AbsoluteToAssetPath(absoluteExcelPath);
            string workbookGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(workbookGuid))
            {
                throw new InvalidOperationException("Raw import only supports Excel files inside the Unity Assets folder.");
            }

            EnsureFolder(ExcelImportPaths.RawImportFolder);
            var imported = new List<RawExcelSheetSO>();

            using (FileStream stream = new FileStream(absoluteExcelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                IWorkbook workbook = new XSSFWorkbook(stream);
                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    ISheet sheet = workbook.GetSheetAt(i);
                    if (sheet == null)
                    {
                        continue;
                    }

                    ExcelSheetMarkerState markerState = GetMarkerState(sheet);
                    if (markerState == ExcelSheetMarkerState.Complete)
                    {
                        continue;
                    }

                    if (markerState == ExcelSheetMarkerState.Partial)
                    {
                        Debug.LogError($"[ExcelRawImport] Sheet '{sheet.SheetName}' has partial markers. Fix #Type/#Name/#Data before importing.");
                        continue;
                    }

                    imported.Add(CreateOrUpdateRawSheet(absoluteExcelPath, assetPath, workbookGuid, sheet));
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return imported;
        }

        public static ExcelSheetMarkerState GetMarkerState(ISheet sheet)
        {
            bool hasType = false;
            bool hasName = false;
            bool hasData = false;
            bool hasAnyMarker = false;

            for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                IRow row = sheet.GetRow(r);
                string marker = NormalizeMarker(GetCellString(row, 0));
                if (string.IsNullOrEmpty(marker))
                {
                    continue;
                }

                if (marker == "#type" || marker == "#name" || marker == "#data" || marker == "#end" || marker == "#dataname")
                {
                    hasAnyMarker = true;
                }

                if (marker == "#type") hasType = true;
                if (marker == "#name") hasName = true;
                if (marker == "#data") hasData = true;
            }

            if (hasType && hasName && hasData)
            {
                return ExcelSheetMarkerState.Complete;
            }

            return hasAnyMarker ? ExcelSheetMarkerState.Partial : ExcelSheetMarkerState.None;
        }

        public static string MakePhysicalSheetId(string workbookGuid, string sheetName)
        {
            return Sha1($"{workbookGuid}|{sheetName}");
        }

        public static string MakeLogicalSheetKey(string classOrSheetName)
        {
            return CodeGenerator.ToTypeBaseName(classOrSheetName).ToLowerInvariant();
        }

        public static string GetSchemaGuid(ExcelSheetSchemaSO schema)
        {
            if (schema == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(schema);
            string guid = AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(schema.schemaGuid) ? guid : schema.schemaGuid;
        }

        public static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        public static string AbsoluteToAssetPath(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return "Assets" + normalized.Substring(dataPath.Length);
        }

        public static string ToAbsoluteAssetPath(string unityAssetPath)
        {
            string relative = unityAssetPath.Replace('\\', '/');
            if (relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Substring("Assets/".Length);
            }

            return Path.Combine(Application.dataPath, relative);
        }

        public static string SanitizeAssetFileName(string raw)
        {
            string baseName = CodeGenerator.ToTypeBaseName(raw);
            return string.IsNullOrWhiteSpace(baseName) ? "Sheet" : baseName;
        }

        private static RawExcelSheetSO CreateOrUpdateRawSheet(
            string absoluteExcelPath,
            string assetPath,
            string workbookGuid,
            ISheet sheet)
        {
            string physicalId = MakePhysicalSheetId(workbookGuid, sheet.SheetName);
            string rawPath = FindRawAssetPath(physicalId);
            if (string.IsNullOrEmpty(rawPath))
            {
                string workbookFileName = Path.GetFileNameWithoutExtension(absoluteExcelPath);
                string rawFolder = ExcelImportPaths.GetRawImportFolder(workbookFileName);
                EnsureFolder(rawFolder);
                rawPath = $"{rawFolder}/{SanitizeAssetFileName(sheet.SheetName)}_Raw.asset";
            }

            RawExcelSheetSO raw = AssetDatabase.LoadAssetAtPath<RawExcelSheetSO>(rawPath);
            if (raw == null)
            {
                raw = ScriptableObject.CreateInstance<RawExcelSheetSO>();
                AssetDatabase.CreateAsset(raw, AssetDatabase.GenerateUniqueAssetPath(rawPath));
                rawPath = AssetDatabase.GetAssetPath(raw);
            }

            raw.sourceWorkbookGuid = workbookGuid;
            raw.sourceWorkbookPath = assetPath;
            raw.workbookFileName = Path.GetFileNameWithoutExtension(absoluteExcelPath);
            raw.sheetName = sheet.SheetName;
            raw.physicalSheetId = physicalId;
            raw.logicalSheetKey = MakeLogicalSheetKey(sheet.SheetName);
            raw.importStatus = RawImportStatus.Imported;
            raw.lastError = string.Empty;
            raw.Rows = ReadRows(sheet);

            EditorUtility.SetDirty(raw);
            Debug.Log($"[ExcelRawImport] Imported raw sheet '{sheet.SheetName}' -> {rawPath}");
            return raw;
        }

        private static string FindRawAssetPath(string physicalSheetId)
        {
            if (!AssetDatabase.IsValidFolder(ExcelImportPaths.RawImportFolder))
            {
                return string.Empty;
            }

            string[] guids = AssetDatabase.FindAssets("t:RawExcelSheetSO", new[] { ExcelImportPaths.RawImportFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                RawExcelSheetSO raw = AssetDatabase.LoadAssetAtPath<RawExcelSheetSO>(path);
                if (raw != null && raw.physicalSheetId == physicalSheetId)
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private static List<RawExcelRow> ReadRows(ISheet sheet)
        {
            int lastColumn = GetLastUsedColumn(sheet);
            var rows = new List<RawExcelRow>();
            for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                IRow row = sheet.GetRow(r);
                var rawRow = new RawExcelRow();
                for (int c = 0; c <= lastColumn; c++)
                {
                    rawRow.Cells.Add(GetCellString(row, c));
                }

                rows.Add(rawRow);
            }

            return rows;
        }

        private static int GetLastUsedColumn(ISheet sheet)
        {
            int last = 0;
            for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                IRow row = sheet.GetRow(r);
                if (row != null && row.LastCellNum > 0)
                {
                    last = Math.Max(last, row.LastCellNum - 1);
                }
            }

            return last;
        }

        private static string GetCellString(IRow row, int columnIndex)
        {
            if (row == null)
            {
                return string.Empty;
            }

            ICell cell = row.GetCell(columnIndex);
            if (cell == null)
            {
                return string.Empty;
            }

            IFormulaEvaluator evaluator = row.Sheet?.Workbook?.GetCreationHelper()?.CreateFormulaEvaluator();
            string formatted = evaluator != null
                ? CellFormatter.FormatCellValue(cell, evaluator)
                : CellFormatter.FormatCellValue(cell);

            return formatted?.Trim() ?? string.Empty;
        }

        private static string NormalizeMarker(string marker)
        {
            return marker?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static string Sha1(string input)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? string.Empty));
                return string.Concat(bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }
}
