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

        private string _selectedExcelPath = string.Empty;
        private List<ExcelSheetParseResult> _previewSheets = new List<ExcelSheetParseResult>();

        // 시트 이름 → Dictionary 사용 여부
        private readonly Dictionary<string, bool> _sheetUseDictionary = new Dictionary<string, bool>();

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
                List<ExcelSheetParseResult> sheets = ExcelParser.Parse(pendingPath);
                ScriptableExporter.ExportAll(sheets, useDictMap);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[Excel Importer] Step 2 complete: exported {sheets.Count} sheet(s) from {pendingPath}");
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
            _sheetScroll = EditorGUILayout.BeginScrollView(_sheetScroll, GUILayout.Height(160f));
            if (_previewSheets.Count == 0)
            {
                EditorGUILayout.HelpBox("Select an .xlsx file to preview sheets.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < _previewSheets.Count; i++)
                {
                    ExcelSheetParseResult sheet = _previewSheets[i];
                    if (!_sheetUseDictionary.ContainsKey(sheet.SheetName))
                    {
                        _sheetUseDictionary[sheet.SheetName] = false;
                    }

                    bool useDict = _sheetUseDictionary[sheet.SheetName];

                    EditorGUILayout.LabelField(
                        $"• {sheet.SheetName}  (columns: {sheet.Names.Count}, rows: {sheet.Rows.Count})");

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
            AddLog($"Selected: {path}");

            try
            {
                _previewSheets = ExcelParser.Parse(path);
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

                AddLog("[Step 1] Generating C# scripts...");
                CodeGenerator.GenerateAll(sheets, _sheetUseDictionary);

                EditorPrefs.SetString(ExcelImportPaths.PendingFilePathKey, _selectedExcelPath);
                EditorPrefs.SetString(PendingUseDictMapKey, SerializeDictMap(_sheetUseDictionary));
                AddLog("[Step 1] Pending asset export registered. Refreshing assets...");

                AssetDatabase.Refresh();
                // #region agent log
                ExcelImportDebugLog.Write("H1", "ExcelEditorWindow.BakeData", "step1_done", "{\"sheetCount\":" + sheets.Count + "}");
                // #endregion
                AddLog("[Step 1] Done. Script compile 후 Step 2(asset)가 자동 실행됩니다.");
            }
            catch (Exception ex)
            {
                AddLog($"Bake failed: {ex.Message}");
                Debug.LogError($"[Excel Importer] Bake failed: {ex}\n{ex.StackTrace}");
            }
        }

        private void AddLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logs.Add(line);
            Debug.Log("[Excel Importer] " + message);
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
