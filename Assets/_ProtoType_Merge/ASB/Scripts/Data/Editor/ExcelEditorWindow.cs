using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    public class ExcelEditorWindow : EditorWindow
    {
        private const string PendingUseDictKey = "ExcelParser_PendingUseDict";
        private bool _useDictionary = false;

        private string _selectedExcelPath = string.Empty;
        private List<ExcelSheetParseResult> _previewSheets = new List<ExcelSheetParseResult>();
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
            bool useDictionary = EditorPrefs.GetBool(PendingUseDictKey, false);
            EditorPrefs.DeleteKey(PendingUseDictKey);

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
                ScriptableExporter.ExportAll(sheets, useDictionary);
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
            _sheetScroll = EditorGUILayout.BeginScrollView(_sheetScroll, GUILayout.Height(120f));
            if (_previewSheets.Count == 0)
            {
                EditorGUILayout.HelpBox("Select an .xlsx file to preview sheets.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < _previewSheets.Count; i++)
                {
                    ExcelSheetParseResult sheet = _previewSheets[i];
                    EditorGUILayout.LabelField(
                        $"• {sheet.SheetName}  (columns: {sheet.Names.Count}, rows: {sheet.Rows.Count})");
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);

            EditorGUILayout.LabelField("Data Structure", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = !_useDictionary ? Color.cyan : Color.white;
            if (GUILayout.Button("List", GUILayout.Height(24f))) _useDictionary = false;
            GUI.backgroundColor = _useDictionary ? Color.cyan : Color.white;
            if (GUILayout.Button("Dictionary", GUILayout.Height(24f))) _useDictionary = true;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (_useDictionary && _previewSheets.Count > 0)
            {
                for (int i = 0; i < _previewSheets.Count; i++)
                {
                    var s = _previewSheets[i];
                    string keyField = s.Names.Count > 0 ? s.Names[0] : "?";
                    string keyType  = s.Types.Count > 0 ? s.Types[0] : "?";
                    EditorGUILayout.HelpBox($"Key: {keyField} ({keyType})  —  {s.SheetName}", MessageType.None);
                }
            }
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
                CodeGenerator.GenerateAll(sheets, _useDictionary);

                EditorPrefs.SetString(ExcelImportPaths.PendingFilePathKey, _selectedExcelPath);
                EditorPrefs.SetBool(PendingUseDictKey, _useDictionary);
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
