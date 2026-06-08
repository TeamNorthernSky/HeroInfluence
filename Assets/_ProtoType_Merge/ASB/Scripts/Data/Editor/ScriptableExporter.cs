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
        public static void ExportAll(IReadOnlyList<ExcelSheetParseResult> sheets, Dictionary<string, bool> useDictMap = null)
        {
            if (sheets == null || sheets.Count == 0)
            {
                Debug.LogWarning("[ScriptableExporter] No sheets to export.");
                return;
            }

            string assetFolder = ExcelImportPaths.TableAssetFolder;
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
        }

        private static void ExportSheet(ExcelSheetParseResult sheet, string assetFolder)
        {
            string baseName = CodeGenerator.ToTypeBaseName(sheet.SheetName);
            string rowTypeName = baseName + "Data";
            string tableTypeName = baseName + "DataTable";
            string assetPath = $"{assetFolder}/{tableTypeName}.asset";

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
                        continue;
                    }

                    object converted = ConvertCellValue(rowValues[colIndex], field.FieldType);
                    field.SetValue(rowInstance, converted);
                }

                dataList.Add(rowInstance);
            }

            EditorUtility.SetDirty(tableAsset);
            Debug.Log($"[ScriptableExporter] Exported {sheet.Rows.Count} rows -> {assetPath}");
        }

        private static IList ConvertToList(string value, Type elementType)
        {
            IList list = CreateListInstance(elementType);
            if (string.IsNullOrWhiteSpace(value))
            {
                return list;
            }

            string[] tokens = value.Split(',');
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].Trim();
                object element = ConvertCellValue(token, elementType);
                list.Add(element);
            }

            return list;
        }

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
            // 셀 값 "1,2,3" → new List<int> { 1, 2, 3 }
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return ConvertToList(value, targetType.GetGenericArguments()[0]);
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
            // 이미 AssetDatabase가 인식하는 폴더면 아무것도 안 해도 됨
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            // 디스크에 물리 폴더 생성 후 AssetDatabase에 알림
            Directory.CreateDirectory(ToAbsoluteAssetPath(assetFolderPath));
            AssetDatabase.Refresh();

            // Refresh 후에도 인식이 안 되면 CreateFolder로 강제 등록
            if (!AssetDatabase.IsValidFolder(assetFolderPath))
            {
                string parent = System.IO.Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
                string folderName = System.IO.Path.GetFileName(assetFolderPath);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
                {
                    AssetDatabase.CreateFolder(parent, folderName);
                }
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
