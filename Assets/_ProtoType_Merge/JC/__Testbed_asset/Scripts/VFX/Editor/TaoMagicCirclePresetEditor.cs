using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(TaoMagicCirclePreset))]
    public class TaoMagicCirclePresetEditor : Editor
    {
        const string TaoPrefab = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/N_Taosenaiyo/Taosenaiyo.prefab";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (TaoMagicCirclePreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 MagicCircle(TaoMagicCircle)에 반영.\n문양(링/육망성/눈금/회전) 항목은 프리셋+livePreview로만 구동됩니다.", MessageType.Info);
        }

        static readonly string[] Floats = { "radius", "groundOffsetY", "spreadGrow", "intensity" };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(TaoMagicCirclePreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(TaoPrefab);
            bool dirty = false;
            foreach (var mc in root.GetComponentsInChildren<TaoMagicCircle>(true))
            {
                if (!UsesPreset(mc, p)) continue;
                var so = new SerializedObject(mc);
                var pso = new SerializedObject(p);
                foreach (var f in Floats) CopyFloat(pso, so, f);
                so.FindProperty("color").colorValue = p.color;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
            }
            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, TaoPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            if (dirty) Debug.Log("[TaoMagicCirclePreset] 프리팹에 적용 완료");
            else Debug.LogError("[TaoMagicCirclePreset] 이 에셋을 참조하는 TaoMagicCircle 없음");
        }

        void Capture(TaoMagicCirclePreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(TaoPrefab);
            if (root != null)
            {
                foreach (var mc in root.GetComponentsInChildren<TaoMagicCircle>(true))
                {
                    if (!UsesPreset(mc, p)) continue;
                    Undo.RecordObject(p, "Capture Tao Magic Circle");
                    var so = new SerializedObject(mc);
                    var pso = new SerializedObject(p);
                    foreach (var f in Floats) CopyFloat(so, pso, f);
                    pso.FindProperty("color").colorValue = so.FindProperty("color").colorValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log("[TaoMagicCirclePreset] 현재값 캡처 완료");
                    return;
                }
            }
            Debug.LogError("[TaoMagicCirclePreset] 이 에셋을 참조하는 TaoMagicCircle 없음");
        }

        static void CopyFloat(SerializedObject from, SerializedObject to, string name)
        {
            var pf = from.FindProperty(name);
            var pt = to.FindProperty(name);
            if (pf == null || pt == null) return;
            pt.floatValue = pf.floatValue;
        }
    }
}
