using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    public static class ScriptableExporter
    {
        public static void ExportAll(IReadOnlyList<ExcelSheetParseResult> sheets, Dictionary<string, bool> useDictMap = null, string assetFolder = null)
        {
            if (sheets == null || sheets.Count == 0)
            {
                Debug.LogWarning("[ScriptableExporter] No sheets to export.");
                return;
            }

            if (string.IsNullOrEmpty(assetFolder))
                assetFolder = ExcelImportPaths.TableAssetFolder;
            EnsureAssetFolderExists(assetFolder);

            for (int i = 0; i < sheets.Count; i++)
            {
                ExcelSheetParseResult sheet = sheets[i];
                if (sheet == null)
                {
                    continue;
                }

                ExportSheet(sheet, assetFolder);
            }

            AssetDatabase.SaveAssets();
        }

        private static void ExportSheet(ExcelSheetParseResult sheet, string assetFolder)
        {
            string baseName = CodeGenerator.ToTypeBaseName(sheet.ClassName);
            string rowTypeName = baseName + "Data";
            string tableTypeName = baseName + "DataTable";
            string assetName = ExcelSchemaAdapter.SanitizeOutputAssetName(sheet.OutputAssetName, tableTypeName);
            string assetPath = $"{assetFolder}/{assetName}.asset";

            Type rowType = FindTypeByName(rowTypeName);
            Type tableType = FindTypeByName(tableTypeName);
            if (rowType == null || tableType == null)
            {
                Debug.LogError($"[ScriptableExporter] Types not found: {rowTypeName}, {tableTypeName}. Wait for script compile.");
                return;
            }

            ScriptableObject tableAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);

            // 로드 실패(타입 변경 등)로 null이 됐지만 파일은 남아있는 경우 삭제 후 재생성
            if (tableAsset == null)
            {
                string absolutePath = ToAbsoluteAssetPath(assetPath);
                if (File.Exists(absolutePath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                tableAsset = ScriptableObject.CreateInstance(tableType);
                AssetDatabase.CreateAsset(tableAsset, assetPath);
            }
            // 타입이 바뀐 경우(로드는 됐지만 실제 타입이 다른 경우) 교체
            else if (tableAsset.GetType() != tableType)
            {
                AssetDatabase.DeleteAsset(assetPath);
                tableAsset = ScriptableObject.CreateInstance(tableType);
                AssetDatabase.CreateAsset(tableAsset, assetPath);
            }

            FieldInfo dataListField = tableType.GetField("DataList", BindingFlags.Instance | BindingFlags.Public);
            if (dataListField == null)
            {
                Debug.LogError($"[ScriptableExporter] DataList field not found on {tableTypeName}.");
                return;
            }

            IList dataList = dataListField.GetValue(tableAsset) as IList;
            if (dataList == null)
            {
                dataList = CreateListInstance(rowType);
                dataListField.SetValue(tableAsset, dataList);
            }
            else
            {
                dataList.Clear();
            }

            var missingFieldsWarned = new HashSet<string>(StringComparer.Ordinal);

            for (int rowIndex = 0; rowIndex < sheet.Rows.Count; rowIndex++)
            {
                List<string> rowValues = sheet.Rows[rowIndex];
                object rowInstance = Activator.CreateInstance(rowType);

                for (int colIndex = 0; colIndex < sheet.Names.Count; colIndex++)
                {
                    if (colIndex >= rowValues.Count)
                    {
                        break;
                    }

                    string fieldName = CodeGenerator.SanitizeFieldName(sheet.Names[colIndex]);
                    FieldInfo field = rowType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
                    if (field == null)
                    {
                        // 조용히 넘기면 그 컬럼 값이 전부 소실되는데 성공 로그만 남아 사용자가 알 수 없다.
                        // 시트당 한 번만 경고해 행 수만큼 반복되는 것을 막는다.
                        if (missingFieldsWarned.Add(fieldName))
                        {
                            Debug.LogWarning(
                                $"[ScriptableExporter] '{sheet.SheetName}' 시트의 컬럼 '{sheet.Names[colIndex]}'" +
                                $"(필드명 '{fieldName}')이 {rowType.Name}에 없다. 이 컬럼 값은 저장되지 않는다. " +
                                "스크립트를 재생성하고 컴파일 후 다시 Bake할 것.");
                        }

                        continue;
                    }

                    object converted = ConvertCellValue(rowValues[colIndex], field.FieldType, fieldName, sheet);
                    field.SetValue(rowInstance, converted);
                }

                dataList.Add(rowInstance);
            }

            EditorUtility.SetDirty(tableAsset);
            Debug.Log($"[ScriptableExporter] Exported {sheet.Rows.Count} rows -> {assetPath}");
        }

        private static IList ConvertToList(string value, Type elementType, ListDelimiter delimiter)
        {
            IList list = CreateListInstance(elementType);
            if (string.IsNullOrWhiteSpace(value))
            {
                return list;
            }

            List<string> tokens = ExcelListParser.Split(value, elementType == typeof(string), delimiter);
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                object element = ConvertCellValue(token, elementType);
                list.Add(element);
            }

            return list;
        }

        /// <summary>
        /// 리스트 셀 전처리. "1,2,3", "{1,2,3}", "\"{9}\"" 형태를 CSVDataLoad.ParseIntListField와 동일하게 정규화합니다.
        /// </summary>
        private static IList CreateListInstance(Type elementType)
        {
            Type listType = typeof(List<>).MakeGenericType(elementType);
            return (IList)Activator.CreateInstance(listType);
        }

        public static Type FindTypeByName(string typeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(t => t != null);
                    }
                })
                .FirstOrDefault(t => t.Name == typeName);
        }

        public static object ConvertCellValue(string raw, Type targetType)
        {
            return ConvertCellValue(raw, targetType, null, null);
        }

        public static object ConvertCellValue(string raw, Type targetType, string fieldName, ExcelSheetParseResult sheet)
        {
            if (targetType == null)
            {
                return null;
            }

            string value = raw ?? string.Empty;

            if (targetType == typeof(string))
            {
                return value;
            }

            if (targetType == typeof(int))
            {
                return ConvertToInt(value);
            }

            if (targetType == typeof(float))
            {
                return ConvertToFloat(value);
            }

            if (targetType == typeof(bool))
            {
                return ConvertToBool(value);
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value, true);
            }

            // List<int> / List<float> / List<string> / List<bool>
            // 셀 값 "1,2,3" 또는 "{1,2,3}" → new List<int> { 1, 2, 3 }
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = targetType.GetGenericArguments()[0];
                ListDelimiter delimiter = ListDelimiter.Comma;
                if (elementType == typeof(string) &&
                    sheet != null &&
                    !string.IsNullOrEmpty(fieldName) &&
                    sheet.ListDelimiters != null &&
                    sheet.ListDelimiters.TryGetValue(fieldName, out ListDelimiter configured))
                {
                    delimiter = configured;
                }

                return ConvertToList(value, elementType, delimiter);
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        private static int ConvertToInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                return result;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatResult))
            {
                return (int)Math.Round(floatResult);
            }

            return 0;
        }

        private static float ConvertToFloat(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0f;
            }

            // "25%", "25 %" → 0.25f
            string trimmed = value.Trim();
            if (trimmed.EndsWith("%"))
            {
                string numPart = trimmed.Substring(0, trimmed.Length - 1).Trim();
                if (float.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                {
                    return pct / 100f;
                }
                return 0f;
            }

            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }

            return 0f;
        }

        private static bool ConvertToBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();
            if (bool.TryParse(value, out bool boolResult))
            {
                return boolResult;
            }

            if (value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value == "0" || value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("n", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ConvertToInt(value) != 0;
        }

        /// <summary>
        /// 폴더가 없으면 디스크와 AssetDatabase 양쪽에 모두 생성한 뒤 Refresh 한다.
        /// </summary>
        private static void EnsureAssetFolderExists(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            // 경로 각 레벨을 순차적으로 AssetDatabase.CreateFolder로 생성
            // Refresh 없이 동기적으로 등록되므로 바로 CreateAsset 가능
            string[] parts = assetFolderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
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
    }
}
