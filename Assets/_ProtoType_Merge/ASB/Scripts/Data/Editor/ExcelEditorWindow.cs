using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    public class ExcelEditorWindow : EditorWindow
    {
        // EditorPrefs key: "SheetA=true|SheetB=false" 형태로 직렬화
        private const string PendingUseDictMapKey = "ExcelParser_PendingUseDictMap";
        private const string PendingSheetNamesKey = "ExcelParser_PendingSheetNames";

        private string _selectedExcelPath = string.Empty;
        private List<ExcelSheetParseResult> _previewSheets = new List<ExcelSheetParseResult>();

        // 시트 이름 → Dictionary 사용 여부
        private readonly Dictionary<string, bool> _sheetUseDictionary = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _sheetSelectedForUpdate = new Dictionary<string, bool>();

        private Vector2 _sheetScroll;
        private Vector2 _logScroll;
        private readonly List<string> _logs = new List<string>();

        [MenuItem("Tools/Excel Importer/Open Window")]
        public static void ShowWindow()
        {
            ExcelEditorWindow window = GetWindow<ExcelEditorWindow>("Excel Importer");
            window.minSize = new Vector2(480f, 360f);
            window.Show();
        }

        [MenuItem("Tools/Excel Importer/Remove Stale Conflict Scripts")]
        private static void RemoveStaleConflictScripts()
        {
            string[] staleFiles =
            {
                $"{ExcelImportPaths.GeneratedScriptFolder}/ExcelParsedSheet.cs",
                $"{ExcelImportPaths.GeneratedScriptFolder}/ParsedSheet.cs"
            };

            int removed = 0;
            for (int i = 0; i < staleFiles.Length; i++)
            {
                if (AssetDatabase.DeleteAsset(staleFiles[i]))
                {
                    removed++;
                    Debug.Log($"[Excel Importer] Removed stale conflict script: {staleFiles[i]}");
                }
            }

            if (removed == 0)
            {
                Debug.Log("[Excel Importer] No stale conflict scripts found in Generated folder.");
            }
            else
            {
                AssetDatabase.Refresh();
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            string pendingPath = EditorPrefs.GetString(ExcelImportPaths.PendingFilePathKey, string.Empty);
            if (string.IsNullOrEmpty(pendingPath))
            {
                return;
            }

            EditorPrefs.DeleteKey(ExcelImportPaths.PendingFilePathKey);
            Dictionary<string, bool> useDictMap = DeserializeDictMap(EditorPrefs.GetString(PendingUseDictMapKey, string.Empty));
            EditorPrefs.DeleteKey(PendingUseDictMapKey);
            List<string> pendingSheetNames = DeserializeSheetNames(EditorPrefs.GetString(PendingSheetNamesKey, string.Empty));
            EditorPrefs.DeleteKey(PendingSheetNamesKey);

            if (!File.Exists(pendingPath))
            {
                Debug.LogError($"[Excel Importer] Pending excel not found: {pendingPath}");
                return;
            }

            try
            {
                // #region agent log
                ExcelImportDebugLog.Write("H2", "ExcelEditorWindow.OnScriptsReloaded", "step2_start", "{\"path\":\"" + pendingPath + "\"}");
                // #endregion
                string excelName = Path.GetFileNameWithoutExtension(pendingPath);
                List<ExcelSheetParseResult> sheets = ExcelParser.Parse(pendingPath);
                List<ExcelSheetParseResult> exportSheets = FilterSheets(sheets, pendingSheetNames);
                ScriptableExporter.ExportAll(exportSheets, useDictMap, ExcelImportPaths.GetTableAssetFolder(excelName));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[Excel Importer] Step 2 complete: exported {exportSheets.Count} sheet(s) from {pendingPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Excel Importer] Step 2 failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Excel Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Excel File", GUILayout.Width(140f)))
            {
                SelectExcelFile();
            }

            if (GUILayout.Button("Open Default Folder", GUILayout.Width(140f)))
            {
                string defaultFolder = ScriptableExporterFindAbsoluteFolder(ExcelImportPaths.DefaultExcelFolder);
                EditorUtility.RevealInFinder(defaultFolder);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Path", _selectedExcelPath);
            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Detected Sheets", EditorStyles.boldLabel);
            _sheetScroll = EditorGUILayout.BeginScrollView(_sheetScroll, GUILayout.Height(260f));
            if (_previewSheets.Count == 0)
            {
                EditorGUILayout.HelpBox("Select an .xlsx file to preview sheets.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < _previewSheets.Count; i++)
                {
                    ExcelSheetParseResult sheet = _previewSheets[i];
                    EnsureSheetState(sheet);

                    bool useDict = _sheetUseDictionary[sheet.SheetName];
                    bool selected = _sheetSelectedForUpdate[sheet.SheetName];
                    EditorGUILayout.BeginHorizontal();
                    _sheetSelectedForUpdate[sheet.SheetName] = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));

                    EditorGUILayout.LabelField(
                        $"{sheet.SheetName}  (columns: {sheet.Names.Count}, rows: {sheet.Rows.Count})");
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16f);
                    GUI.backgroundColor = !useDict ? Color.cyan : Color.white;
                    if (GUILayout.Button("List", GUILayout.Height(20f), GUILayout.Width(80f)))
                    {
                        _sheetUseDictionary[sheet.SheetName] = false;
                    }
                    GUI.backgroundColor = useDict ? Color.cyan : Color.white;
                    if (GUILayout.Button("Dictionary", GUILayout.Height(20f), GUILayout.Width(80f)))
                    {
                        _sheetUseDictionary[sheet.SheetName] = true;
                    }
                    GUI.backgroundColor = Color.white;

                    if (useDict)
                    {
                        string keyField = sheet.Names.Count > 0 ? sheet.Names[0] : "?";
                        string keyType  = sheet.Types.Count > 0 ? sheet.Types[0] : "?";
                        GUILayout.Label($"  Key: {keyField} ({keyType})", EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(4f);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_selectedExcelPath)))
            {
                if (GUILayout.Button("Bake Data", GUILayout.Height(32f)))
                {
                    BakeData();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Update Selected Sheet Data", GUILayout.Height(28f)))
                {
                    UpdateSelectedSheetData();
                }

                if (GUILayout.Button("Rebake Selected Sheets", GUILayout.Height(28f)))
                {
                    RebakeSelectedSheets();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < _logs.Count; i++)
            {
                EditorGUILayout.LabelField(_logs[i], EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void SelectExcelFile()
        {
            string defaultFolder = ScriptableExporterFindAbsoluteFolder(ExcelImportPaths.DefaultExcelFolder);
            if (!Directory.Exists(defaultFolder))
            {
                Directory.CreateDirectory(defaultFolder);
            }

            string path = EditorUtility.OpenFilePanel("Select Excel File", defaultFolder, "xlsx");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            _selectedExcelPath = path;
            _logs.Clear();
            _sheetSelectedForUpdate.Clear();
            AddLog($"Selected: {path}");

            try
            {
                _previewSheets = ExcelParser.Parse(path);
                InitializeSheetSelection(_previewSheets);
                AddLog($"Preview: {_previewSheets.Count} sheet(s) parsed.");
            }
            catch (Exception ex)
            {
                _previewSheets.Clear();
                AddLog($"Preview failed: {ex.Message}");
                Debug.LogError($"[Excel Importer] Preview failed: {ex}\n{ex.StackTrace}");
            }
        }

        private void BakeData()
        {
            _logs.Clear();

            if (string.IsNullOrEmpty(_selectedExcelPath) || !File.Exists(_selectedExcelPath))
            {
                AddLog("Bake failed: invalid excel path.");
                return;
            }

            try
            {
                // #region agent log
                ExcelImportDebugLog.Write("H1", "ExcelEditorWindow.BakeData", "step1_start", "{\"path\":\"" + _selectedExcelPath + "\"}");
                // #endregion
                AddLog("[Step 1] Parsing excel...");
                List<ExcelSheetParseResult> sheets = ExcelParser.Parse(_selectedExcelPath);
                _previewSheets = sheets;
                PreserveSheetSelection(sheets);

                List<string> selectedNames = GetSelectedSheetNames();
                List<ExcelSheetParseResult> selectedSheets = FilterSheets(sheets, selectedNames);
                if (selectedSheets.Count == 0)
                {
                    AddLog("Bake failed: no sheet selected.");
                    return;
                }

                AddLog("[Step 1] Generating C# scripts...");
                CodeGenerator.GenerateAll(selectedSheets, _sheetUseDictionary);

                EditorPrefs.SetString(ExcelImportPaths.PendingFilePathKey, _selectedExcelPath);
                EditorPrefs.SetString(PendingUseDictMapKey, SerializeDictMap(_sheetUseDictionary));
                EditorPrefs.SetString(PendingSheetNamesKey, SerializeSheetNames(selectedNames));
                AddLog("[Step 1] Pending asset export registered. Refreshing assets...");

                AssetDatabase.Refresh();
                // #region agent log
                ExcelImportDebugLog.Write("H1", "ExcelEditorWindow.BakeData", "step1_done", "{\"sheetCount\":" + selectedSheets.Count + "}");
                // #endregion
                AddLog("[Step 1] Done. Script compile 후 Step 2(asset)가 자동 실행됩니다.");
            }
            catch (Exception ex)
            {
                AddLog($"Bake failed: {ex.Message}");
                Debug.LogError($"[Excel Importer] Bake failed: {ex}\n{ex.StackTrace}");
            }
        }

        private void UpdateSelectedSheetData()
        {
            _logs.Clear();

            if (string.IsNullOrEmpty(_selectedExcelPath) || !File.Exists(_selectedExcelPath))
            {
                AddLog("Update failed: invalid excel path.");
                return;
            }

            try
            {
                AddLog("Parsing excel...");
                List<ExcelSheetParseResult> sheets = ExcelParser.Parse(_selectedExcelPath);
                _previewSheets = sheets;
                PreserveSheetSelection(sheets);

                List<string> selectedNames = GetSelectedSheetNames();
                List<ExcelSheetParseResult> selectedSheets = FilterSheets(sheets, selectedNames);
                if (selectedSheets.Count == 0)
                {
                    AddLog("Update failed: no sheet selected.");
                    return;
                }

                // 스키마 검증 — 컬럼 구조가 바뀐 시트가 있으면 차단
                for (int i = 0; i < selectedSheets.Count; i++)
                {
                    if (!IsSchemaCompatible(selectedSheets[i], out string mismatch))
                    {
                        string msg = $"[{selectedSheets[i].SheetName}] 시트 구조가 변경되었습니다.\n{mismatch}\nBakeData 또는 Rebake Selected를 실행하세요.";
                        EditorUtility.DisplayDialog("스키마 변경 감지", msg, "확인");
                        AddLog($"Update blocked: {selectedSheets[i].SheetName} — {mismatch}");
                        return;
                    }
                }

                string excelName = Path.GetFileNameWithoutExtension(_selectedExcelPath);
                ScriptableExporter.ExportAll(selectedSheets, _sheetUseDictionary, ExcelImportPaths.GetTableAssetFolder(excelName));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                AddLog($"Updated data asset(s): {selectedSheets.Count} sheet(s).");
            }
            catch (Exception ex)
            {
                AddLog($"Update failed: {ex.Message}");
                Debug.LogError($"[Excel Importer] Update failed: {ex}\n{ex.StackTrace}");
            }
        }

        private static bool IsSchemaCompatible(ExcelSheetParseResult sheet, out string mismatch)
        {
            mismatch = string.Empty;
            string rowTypeName = CodeGenerator.ToTypeBaseName(sheet.SheetName) + "Data";
            System.Type rowType = ScriptableExporter.FindTypeByName(rowTypeName);

            if (rowType == null)
            {
                mismatch = $"'{rowTypeName}' 클래스를 찾을 수 없습니다. BakeData를 먼저 실행하세요.";
                return false;
            }

            var existingFields = new Dictionary<string, string>();
            foreach (System.Reflection.FieldInfo f in rowType.GetFields(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                existingFields[f.Name] = f.FieldType.Name;
            }

            var added   = new List<string>();
            var removed = new List<string>();

            var excelFieldNames = new HashSet<string>();
            for (int i = 0; i < sheet.Names.Count; i++)
            {
                string fieldName = CodeGenerator.SanitizeFieldName(sheet.Names[i]);
                excelFieldNames.Add(fieldName);
                if (!existingFields.ContainsKey(fieldName))
                    added.Add(fieldName);
            }

            foreach (string existing in existingFields.Keys)
            {
                if (!excelFieldNames.Contains(existing))
                    removed.Add(existing);
            }

            if (added.Count == 0 && removed.Count == 0) return true;

            var sb = new System.Text.StringBuilder();
            if (added.Count   > 0) sb.AppendLine($"추가된 컬럼: {string.Join(", ", added)}");
            if (removed.Count > 0) sb.AppendLine($"제거된 컬럼: {string.Join(", ", removed)}");
            mismatch = sb.ToString().Trim();
            return false;
        }

        private void RebakeSelectedSheets()
        {
            _logs.Clear();

            if (string.IsNullOrEmpty(_selectedExcelPath) || !File.Exists(_selectedExcelPath))
            {
                AddLog("Rebake failed: invalid excel path.");
                return;
            }

            try
            {
                AddLog("[Step 1] Parsing excel...");
                List<ExcelSheetParseResult> sheets = ExcelParser.Parse(_selectedExcelPath);
                _previewSheets = sheets;
                PreserveSheetSelection(sheets);

                List<string> selectedNames = GetSelectedSheetNames();
                List<ExcelSheetParseResult> selectedSheets = FilterSheets(sheets, selectedNames);
                if (selectedSheets.Count == 0)
                {
                    AddLog("Rebake failed: no sheet selected.");
                    return;
                }

                AddLog("[Step 1] Generating selected C# scripts...");
                CodeGenerator.GenerateAll(selectedSheets, _sheetUseDictionary);

                EditorPrefs.SetString(ExcelImportPaths.PendingFilePathKey, _selectedExcelPath);
                EditorPrefs.SetString(PendingUseDictMapKey, SerializeDictMap(_sheetUseDictionary));
                EditorPrefs.SetString(PendingSheetNamesKey, SerializeSheetNames(selectedNames));
                AddLog("[Step 1] Pending selected asset export registered. Refreshing assets...");

                AssetDatabase.Refresh();
                AddLog($"[Step 1] Done. Script compile will run Step 2 for {selectedSheets.Count} selected sheet(s).");
            }
            catch (Exception ex)
            {
                AddLog($"Rebake failed: {ex.Message}");
                Debug.LogError($"[Excel Importer] Rebake failed: {ex}\n{ex.StackTrace}");
            }
        }

        private void AddLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logs.Add(line);
            Debug.Log("[Excel Importer] " + message);
        }

        private void EnsureSheetState(ExcelSheetParseResult sheet)
        {
            if (sheet == null) return;

            if (!_sheetUseDictionary.ContainsKey(sheet.SheetName))
            {
                _sheetUseDictionary[sheet.SheetName] = false;
            }

            if (!_sheetSelectedForUpdate.ContainsKey(sheet.SheetName))
            {
                _sheetSelectedForUpdate[sheet.SheetName] = true;
            }
        }

        private void InitializeSheetSelection(IReadOnlyList<ExcelSheetParseResult> sheets)
        {
            _sheetSelectedForUpdate.Clear();
            PreserveSheetSelection(sheets);
        }

        private void PreserveSheetSelection(IReadOnlyList<ExcelSheetParseResult> sheets)
        {
            if (sheets == null) return;

            for (int i = 0; i < sheets.Count; i++)
            {
                EnsureSheetState(sheets[i]);
            }
        }

        private List<string> GetSelectedSheetNames()
        {
            var selected = new List<string>();
            for (int i = 0; i < _previewSheets.Count; i++)
            {
                ExcelSheetParseResult sheet = _previewSheets[i];
                if (sheet == null) continue;

                if (_sheetSelectedForUpdate.TryGetValue(sheet.SheetName, out bool isSelected) && isSelected)
                {
                    selected.Add(sheet.SheetName);
                }
            }

            return selected;
        }

        // "SheetA=true|SheetB=false" 형태로 직렬화
        private static string SerializeDictMap(Dictionary<string, bool> map)
        {
            var parts = new List<string>();
            foreach (var kv in map)
            {
                parts.Add($"{kv.Key}={kv.Value}");
            }
            return string.Join("|", parts);
        }

        private static Dictionary<string, bool> DeserializeDictMap(string raw)
        {
            var result = new Dictionary<string, bool>();
            if (string.IsNullOrEmpty(raw)) return result;

            string[] entries = raw.Split('|');
            foreach (string entry in entries)
            {
                int idx = entry.IndexOf('=');
                if (idx < 0) continue;
                string key = entry.Substring(0, idx);
                bool val = entry.Substring(idx + 1).Trim().ToLower() == "true";
                result[key] = val;
            }
            return result;
        }

        private static string SerializeSheetNames(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0) return string.Empty;
            return string.Join("|", names);
        }

        private static List<string> DeserializeSheetNames(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(raw)) return result;

            string[] entries = raw.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string name = entries[i]?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    result.Add(name);
                }
            }

            return result;
        }

        private static List<ExcelSheetParseResult> FilterSheets(
            IReadOnlyList<ExcelSheetParseResult> sheets,
            IReadOnlyList<string> sheetNames)
        {
            var result = new List<ExcelSheetParseResult>();
            if (sheets == null) return result;

            if (sheetNames == null || sheetNames.Count == 0)
            {
                for (int i = 0; i < sheets.Count; i++)
                {
                    if (sheets[i] != null)
                    {
                        result.Add(sheets[i]);
                    }
                }

                return result;
            }

            var selected = new HashSet<string>(sheetNames, StringComparer.Ordinal);
            for (int i = 0; i < sheets.Count; i++)
            {
                ExcelSheetParseResult sheet = sheets[i];
                if (sheet != null && selected.Contains(sheet.SheetName))
                {
                    result.Add(sheet);
                }
            }

            return result;
        }

        private static string ScriptableExporterFindAbsoluteFolder(string unityAssetPath)
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
