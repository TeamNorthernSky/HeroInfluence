using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    public static class ExcelImportPaths
    {
        public const string GeneratedScriptFolder = "Assets/_ProtoType_Merge/ASB/Scripts/Data/Generated";
        public const string TableAssetFolder = "Assets/_ProtoType_Merge/ASB/Data/Tables";
        public const string DefaultExcelFolder = "Assets/_ProtoType_Merge/ASB/Data/Excel";
        public const string PendingFilePathKey = "ExcelParser_PendingFilePath";

        public static string GetTableAssetFolder(string excelFileName) =>
            $"{TableAssetFolder}/{excelFileName}";
    }

    /// <summary>에디터 파서 전용 DTO. Generated 데이터 클래스와 이름이 겹치지 않도록 Editor 네임스페이스에 둡니다.</summary>
    public sealed class ExcelSheetParseResult
    {
        public string SheetName;
        public string CustomClassName;
        /// <summary>#DataName이 있으면 그 값, 없으면 SheetName을 클래스명 기준으로 사용합니다.</summary>
        public string ClassName => string.IsNullOrEmpty(CustomClassName) ? SheetName : CustomClassName;
        public List<string> Types = new List<string>();
        public List<string> Names = new List<string>();
        public List<List<string>> Rows = new List<List<string>>();
    }

    public static class ExcelParser
    {
        private const int MarkerColumnIndex = 0;
        private const int FirstDataColumnIndex = 1;

        private static readonly DataFormatter CellValueFormatter = new DataFormatter(CultureInfo.InvariantCulture);

        public static List<ExcelSheetParseResult> Parse(string absoluteFilePath)
        {
            if (string.IsNullOrWhiteSpace(absoluteFilePath) || !File.Exists(absoluteFilePath))
            {
                throw new FileNotFoundException("Excel file not found.", absoluteFilePath);
            }

            var results = new List<ExcelSheetParseResult>();
            // #region agent log
            ExcelImportDebugLog.Write(
                "H3",
                "ExcelParser.Parse",
                "dto_type_assembly",
                "{\"fullName\":\"" + typeof(ExcelSheetParseResult).FullName +
                "\",\"assembly\":\"" + typeof(ExcelSheetParseResult).Assembly.GetName().Name + "\"}");
            // #endregion

            using (FileStream stream = new FileStream(absoluteFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                IWorkbook workbook = new XSSFWorkbook(stream);
                for (int sheetIndex = 0; sheetIndex < workbook.NumberOfSheets; sheetIndex++)
                {
                    ISheet sheet = workbook.GetSheetAt(sheetIndex);
                    if (sheet == null)
                    {
                        continue;
                    }

                    ExcelSheetParseResult parsed = ParseSheet(sheet);
                    if (parsed != null)
                    {
                        results.Add(parsed);
                    }
                }
            }

            return results;
        }

        private static ExcelSheetParseResult ParseSheet(ISheet sheet)
        {
            int typeRowIndex = -1;
            int nameRowIndex = -1;
            int dataStartRowIndex = -1;
            string customClassName = null;

            for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                IRow row = sheet.GetRow(rowIndex);
                if (row == null)
                {
                    continue;
                }

                string marker = NormalizeMarker(GetCellString(row, MarkerColumnIndex));
                switch (marker)
                {
                    case "#dataname":
                        customClassName = GetCellString(row, FirstDataColumnIndex).Trim();
                        break;
                    case "#type":
                        typeRowIndex = rowIndex;
                        break;
                    case "#name":
                        nameRowIndex = rowIndex;
                        break;
                    case "#data":
                        if (dataStartRowIndex < 0)
                        {
                            dataStartRowIndex = rowIndex;
                        }
                        break;
                }
            }

            if (typeRowIndex < 0 || nameRowIndex < 0 || dataStartRowIndex < 0)
            {
                Debug.LogWarning($"[ExcelParser] Skip sheet '{sheet.SheetName}': missing #Type, #Name, or #Data row.");
                return null;
            }

            IRow typeRow = sheet.GetRow(typeRowIndex);
            IRow nameRow = sheet.GetRow(nameRowIndex);
            int lastColumn = GetLastUsedColumn(sheet, typeRowIndex, nameRowIndex, dataStartRowIndex);

            var includedColumns = new List<int>();
            var types = new List<string>();
            var names = new List<string>();

            for (int col = FirstDataColumnIndex; col <= lastColumn; col++)
            {
                string typeCell = GetCellString(typeRow, col);
                if (IsSkipColumn(typeCell))
                {
                    continue;
                }

                string fieldType = typeCell.Trim().ToLowerInvariant();
                string fieldName = GetCellString(nameRow, col).Trim();
                if (string.IsNullOrEmpty(fieldName))
                {
                    continue;
                }

                includedColumns.Add(col);
                types.Add(fieldType);
                names.Add(fieldName);
            }

            if (includedColumns.Count == 0)
            {
                Debug.LogWarning($"[ExcelParser] Skip sheet '{sheet.SheetName}': no valid columns.");
                return null;
            }

            var parsed = new ExcelSheetParseResult
            {
                SheetName       = sheet.SheetName,
                CustomClassName = customClassName,
                Types           = types,
                Names           = names
            };

            for (int rowIndex = dataStartRowIndex; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                IRow row = sheet.GetRow(rowIndex);
                if (row == null || IsRowEmpty(row, lastColumn))
                {
                    continue;
                }

                string marker = NormalizeMarker(GetCellString(row, MarkerColumnIndex));
                if (marker == "#end")
                {
                    break;
                }

                if (!IsDataRowMarker(marker))
                {
                    continue;
                }

                var values = new List<string>(includedColumns.Count);
                for (int i = 0; i < includedColumns.Count; i++)
                {
                    values.Add(GetCellString(row, includedColumns[i]));
                }

                parsed.Rows.Add(values);
            }

            return parsed;
        }

        private static bool IsDataRowMarker(string marker)
        {
            if (string.IsNullOrEmpty(marker))
            {
                return true;
            }

            return string.Equals(marker, "#data", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSkipColumn(string typeCell)
        {
            return string.Equals(typeCell?.Trim(), "#skip", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMarker(string marker)
        {
            return marker?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static int GetLastUsedColumn(ISheet sheet, params int[] rowIndices)
        {
            int lastColumn = FirstDataColumnIndex;
            for (int i = 0; i < rowIndices.Length; i++)
            {
                IRow row = sheet.GetRow(rowIndices[i]);
                if (row == null)
                {
                    continue;
                }

                if (row.LastCellNum > lastColumn)
                {
                    lastColumn = row.LastCellNum - 1;
                }
            }

            return Math.Max(lastColumn, FirstDataColumnIndex);
        }

        private static bool IsRowEmpty(IRow row, int lastColumn)
        {
            for (int col = MarkerColumnIndex; col <= lastColumn; col++)
            {
                if (!string.IsNullOrWhiteSpace(GetCellString(row, col)))
                {
                    return false;
                }
            }

            return true;
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
                ? CellValueFormatter.FormatCellValue(cell, evaluator)
                : CellValueFormatter.FormatCellValue(cell);

            return formatted?.Trim() ?? string.Empty;
        }
    }
}
