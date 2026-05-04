using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Orora.ImageObjectForge
{
    internal class ForgeSpritePickerWindow : EditorWindow
    {
        System.Action<List<Sprite>> _onConfirm;
        List<Sprite> _all = new List<Sprite>();
        HashSet<int> _sel = new HashSet<int>();
        string _search = "";
        Vector2 _scroll;

        DefaultAsset _folderAsset;
        string _folderPath = ForgeIO.OutputDir;
        string _folderError;

        public static void Show(IEnumerable<Sprite> preSelected, System.Action<List<Sprite>> onConfirm)
        {
            var w = CreateInstance<ForgeSpritePickerWindow>();
            w.titleContent = new GUIContent("Select Sprites");
            w._onConfirm = onConfirm;
            w.minSize = new Vector2(420, 560);
            w.SetFolder(ForgeIO.OutputDir);
            w.RefreshList(preSelected);
            w.ShowUtility();
            w.Focus();
        }

        void SetFolder(string folderPath)
        {
            _folderPath = folderPath;
            _folderAsset = string.IsNullOrEmpty(folderPath) ? null : AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
        }

        void RefreshList(IEnumerable<Sprite> preSelected = null)
        {
            _folderError = null;
            if (!ForgeIO.IsValidAssetsFolder(_folderPath))
            {
                _all = new List<Sprite>();
                _sel.Clear();
                _folderError = "Assets/ 이하의 유효한 폴더를 지정하세요.";
                return;
            }
            _all = ForgeIO.EnumerateSpritesIn(_folderPath);
            _sel.Clear();
            if (preSelected != null)
            {
                var keepSet = new HashSet<Sprite>(preSelected);
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
                    string newPath = newAsset != null ? AssetDatabase.GetAssetPath(newAsset) : ForgeIO.OutputDir;
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
                    SetFolder(ForgeIO.OutputDir);
                    RefreshList(GetSelected());
                }
            }
            EditorGUILayout.LabelField(string.IsNullOrEmpty(_folderPath) ? ForgeIO.OutputDir : _folderPath, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(_folderError))
            {
                EditorGUILayout.HelpBox(_folderError, MessageType.Warning);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Sprites ({_all.Count}개)", EditorStyles.boldLabel);

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
                EditorGUILayout.HelpBox("선택된 폴더에 Sprite가 없습니다.\n다른 폴더를 지정하거나, 이미지를 저장/일괄 변환하세요.", MessageType.Info);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUI.skin.box);
            for (int i = 0; i < _all.Count; i++)
            {
                if (!Passes(i)) continue;
                var sp = _all[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool was = _sel.Contains(i);
                    bool now = EditorGUILayout.Toggle(was, GUILayout.Width(18));
                    if (now && !was) _sel.Add(i);
                    else if (!now && was) _sel.Remove(i);

                    var previewRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
                    var preview = AssetPreview.GetAssetPreview(sp);
                    if (preview == null) preview = AssetPreview.GetMiniThumbnail(sp);
                    if (preview != null) GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);

                    var r = sp.rect;
                    using (new EditorGUILayout.VerticalScope())
                    {
                        EditorGUILayout.LabelField(sp.name, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"{(int)r.width} × {(int)r.height} px", EditorStyles.miniLabel);
                    }

                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        EditorGUIUtility.PingObject(sp);
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

        List<Sprite> GetSelected()
        {
            var r = new List<Sprite>();
            foreach (var idx in _sel) r.Add(_all[idx]);
            return r;
        }
    }
}
