using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Orora.ImageObjectForge
{
    internal class ForgePrefabPickerWindow : EditorWindow
    {
        System.Action<List<GameObject>> _onConfirm;
        List<GameObject> _all = new List<GameObject>();
        HashSet<int> _sel = new HashSet<int>();
        string _search = "";
        Vector2 _scroll;

        DefaultAsset _folderAsset;
        string _folderPath = ForgePrefabFactory.PrefabsDir;
        string _folderError;

        public static void Show(string initialFolderPath, IEnumerable<GameObject> preSelected, System.Action<List<GameObject>> onConfirm)
        {
            var w = CreateInstance<ForgePrefabPickerWindow>();
            w.titleContent = new GUIContent("Select Prefabs");
            w._onConfirm = onConfirm;
            w.minSize = new Vector2(420, 560);
            string folder = !string.IsNullOrEmpty(initialFolderPath) ? initialFolderPath : ForgePrefabFactory.PrefabsDir;
            w.SetFolder(folder);
            w.RefreshList(preSelected);
            w.ShowUtility();
            w.Focus();
        }

        void SetFolder(string folderPath)
        {
            _folderPath = folderPath;
            _folderAsset = string.IsNullOrEmpty(folderPath) ? null : AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        }

        void RefreshList(IEnumerable<GameObject> preSelected = null)
        {
            _folderError = null;
            if (!ForgeIO.IsValidAssetsFolder(_folderPath))
            {
                _all = new List<GameObject>();
                _sel.Clear();
                _folderError = "Assets/ 이하의 유효한 폴더를 지정하세요.";
                return;
            }
            _all = ForgeIO.EnumeratePrefabsIn(_folderPath);
            _sel.Clear();
            if (preSelected != null)
            {
                var keepSet = new HashSet<GameObject>(preSelected);
                for (int i = 0; i < _all.Count; i++)
                    if (keepSet.Contains(_all[i])) _sel.Add(i);
            }
        }

        bool Passes(int idx)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            return _all[idx].name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void OnGUI()
        {
            // 폴더 선택
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Folder", GUILayout.Width(50));
                var newAsset = (DefaultAsset)EditorGUILayout.ObjectField(_folderAsset, typeof(DefaultAsset), false);
                if (newAsset != _folderAsset)
                {
                    string newPath = newAsset != null ? AssetDatabase.GetAssetPath(newAsset) : ForgePrefabFactory.PrefabsDir;
                    if (newAsset == null || ForgeIO.IsValidAssetsFolder(newPath))
                    {
                        SetFolder(newPath);
                        RefreshList(GetSelected());
                    }
                    else
                    {
                        _folderError = "Assets/ 이하 폴더만 가능";
                    }
                }
                if (GUILayout.Button("Reset", GUILayout.Width(60)))
                {
                    SetFolder(ForgePrefabFactory.PrefabsDir);
                    RefreshList(GetSelected());
                }
            }
            EditorGUILayout.LabelField(string.IsNullOrEmpty(_folderPath) ? ForgePrefabFactory.PrefabsDir : _folderPath, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(_folderError))
            {
                EditorGUILayout.HelpBox(_folderError, MessageType.Warning);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Prefabs ({_all.Count}개)", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField("Search", _search);
                if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshList(GetSelected());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All (필터 결과)"))
                    for (int i = 0; i < _all.Count; i++) if (Passes(i)) _sel.Add(i);
                if (GUILayout.Button("Deselect All"))
                    _sel.Clear();
            }

            EditorGUILayout.Space(4);

            if (_all.Count == 0)
            {
                EditorGUILayout.HelpBox("선택된 폴더에 Prefab이 없습니다.\n다른 폴더를 지정하거나, Create Prefabs로 먼저 생성하세요.", MessageType.Info);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUI.skin.box);
            for (int i = 0; i < _all.Count; i++)
            {
                if (!Passes(i)) continue;
                var go = _all[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool was = _sel.Contains(i);
                    bool now = EditorGUILayout.Toggle(was, GUILayout.Width(18));
                    if (now && !was) _sel.Add(i);
                    else if (!now && was) _sel.Remove(i);

                    var previewRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
                    var preview = AssetPreview.GetAssetPreview(go);
                    if (preview == null) preview = AssetPreview.GetMiniThumbnail(go);
                    if (preview != null) GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);

                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(go.name, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(go), EditorStyles.miniLabel);
                    }

                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        EditorGUIUtility.PingObject(go);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"선택됨: {_sel.Count}개");
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(30))) Close();
                GUI.enabled = _sel.Count > 0;
                if (GUILayout.Button($"Continue ({_sel.Count})", GUILayout.Height(30)))
                {
                    var cb = _onConfirm;
                    _onConfirm = null;
                    cb?.Invoke(GetSelected());
                    Close();
                }
                GUI.enabled = true;
            }
        }

        List<GameObject> GetSelected()
        {
            var r = new List<GameObject>();
            foreach (var idx in _sel) r.Add(_all[idx]);
            return r;
        }
    }
}
