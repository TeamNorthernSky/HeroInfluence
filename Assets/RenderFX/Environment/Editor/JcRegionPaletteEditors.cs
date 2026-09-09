using UnityEditor;
using UnityEngine;

namespace JC.Env.EditorTools
{
    /// <summary>
    /// 지역 팔레트 컨트롤러 인스펙터 — 프로파일을 <b>초안(draft) 사본</b>으로 인라인 편집하고 즉시 씬에 반영한다.
    ///
    /// 저장 규격은 [[reference-unity-so-live-tuning]] + 260904 초안 방식 그대로:
    ///   슬라이더를 만지는 동안 원본 에셋은 변하지 않고(사본만), [💾 저장] 에서만 <c>CopySerialized(draft → profile)</c> 후 디스크 확정.
    ///   [↺ 되돌리기] 는 사본을 버리고 원본을 다시 밀어 넣는다.
    /// ★260908: 프로파일 ④(나무 재질 값)도 같은 초안 흐름 — 슬라이더가 재질에 라이브 기록되고, 💾 에서 프로파일+재질을 함께 저장한다.
    /// 씬뷰: 존을 원으로 그리고 중심을 드래그로 옮길 수 있다(XZ 평면).
    /// </summary>
    [CustomEditor(typeof(JcRegionPaletteController))]
    public class JcRegionPaletteControllerEditor : Editor
    {
        private static readonly System.Collections.Generic.Dictionary<int, JcRegionPaletteProfile> Drafts = new();

        private JcRegionPaletteProfile _draft;
        private SerializedObject _draftSO;

        private JcRegionPaletteController C => (JcRegionPaletteController)target;

        private JcRegionPaletteProfile SourceProfile =>
            (JcRegionPaletteProfile)serializedObject.FindProperty("profile").objectReferenceValue;

        private void OnEnable() => EnsureDraft();

        private void OnDisable()
        {
            // 인스펙터가 닫혀도 씬은 초안 상태를 유지한다(정적 캐시). overrideSource 도 그대로 둔다.
        }

        private void EnsureDraft()
        {
            var src = SourceProfile;
            if (src == null) { _draft = null; _draftSO = null; C.overrideSource = null; return; }

            int key = C.GetInstanceID();
            if (!Drafts.TryGetValue(key, out _draft) || _draft == null)
            {
                _draft = Instantiate(src);
                _draft.name = src.name + " (draft)";
                _draft.hideFlags = HideFlags.DontSave;
                Drafts[key] = _draft;
            }
            _draftSO = new SerializedObject(_draft);
            C.overrideSource = _draft;
        }

        private bool IsDirtyVsSource()
        {
            var src = SourceProfile;
            if (src == null || _draft == null) return false;
            return EditorJsonUtility.ToJson(src) != EditorJsonUtility.ToJson(_draft);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("treeMaterials"), true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                Drafts.Remove(C.GetInstanceID());
                EnsureDraft();
                C.Push();
            }

            var src = SourceProfile;
            if (src == null)
            {
                EditorGUILayout.HelpBox("프로파일을 꽂으면 여기서 존을 편집할 수 있습니다.", MessageType.Info);
                return;
            }
            if (_draftSO == null) EnsureDraft();

            bool dirty = IsDirtyVsSource();
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(dirty ? "● 편집 중 (원본 미반영)" : "원본과 동일", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!dirty))
                {
                    if (GUILayout.Button("💾 저장", GUILayout.Width(80)))
                    {
                        Undo.RecordObject(src, "지역 팔레트 저장");
                        string keepName = src.name;
                        EditorUtility.CopySerialized(_draft, src);
                        src.name = keepName;
                        src.hideFlags = HideFlags.None;
                        EditorUtility.SetDirty(src);
                        foreach (var m in C.TreeMaterials) if (m != null) EditorUtility.SetDirty(m);   // 재질 = 출력물, 같이 확정
                        AssetDatabase.SaveAssets();
                    }
                    if (GUILayout.Button("↺ 되돌리기", GUILayout.Width(90)))
                    {
                        Drafts.Remove(C.GetInstanceID());
                        EnsureDraft();
                        C.Push();
                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.Space(2);
            _draftSO.Update();
            EditorGUI.BeginChangeCheck();
            var it = _draftSO.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script") continue;
                EditorGUILayout.PropertyField(it, true);
            }
            if (EditorGUI.EndChangeCheck())
            {
                _draftSO.ApplyModifiedPropertiesWithoutUndo();
                C.Push();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("재베이크 · 적용")) { C.Push(); SceneView.RepaintAll(); }

            var tex = C.BakedTexture;
            if (tex != null)
            {
                EditorGUILayout.LabelField("베이크 미리보기 (RGB=색조, 어두운 곳=존 없음)", EditorStyles.miniLabel);
                var r = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxHeight(180));
                EditorGUI.DrawPreviewTexture(r, tex, null, ScaleMode.ScaleToFit);
            }
        }

        private void OnSceneGUI()
        {
            if (_draft == null) return;
            var b = _draft.worldBounds;
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawWireCube(new Vector3(b.center.x, 0.05f, b.center.y), new Vector3(b.width, 0f, b.height));

            bool changed = false;
            for (int i = 0; i < _draft.zones.Count; i++)
            {
                var z = _draft.zones[i];
                if (z == null) continue;
                var pos = new Vector3(z.center.x, 0.05f, z.center.y);
                var col = z.color; col.a = 0.9f;
                Handles.color = col;
                Handles.DrawWireDisc(pos, Vector3.up, z.radius, 2f);
                Handles.color = new Color(col.r, col.g, col.b, 0.35f);
                Handles.DrawWireDisc(pos, Vector3.up, z.radius * (1f - z.feather));
                Handles.Label(pos + Vector3.up * 0.5f, $"{z.label}  r={z.radius:0.#}");

                EditorGUI.BeginChangeCheck();
                float size = HandleUtility.GetHandleSize(pos) * 0.15f;
                var np = Handles.FreeMoveHandle(pos, size, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    z.center = new Vector2(np.x, np.z);
                    changed = true;
                }
            }
            if (changed) { C.Push(); Repaint(); }
        }
    }
}
