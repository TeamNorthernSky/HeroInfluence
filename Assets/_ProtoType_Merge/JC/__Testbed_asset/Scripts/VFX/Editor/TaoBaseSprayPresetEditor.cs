using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(TaoBaseSprayPreset))]
    public class TaoBaseSprayPresetEditor : Editor
    {
        const string TaoPrefab = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/Taosenaiyo/Taosenaiyo.prefab";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (TaoBaseSprayPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 BaseSpray(TaoBaseSpray)에 반영.\n스커트 기울기/갈퀴 항목은 프리셋+livePreview로만 구동됩니다.", MessageType.Info);
        }

        static readonly string[] Floats = { "width", "height", "groundOffsetY", "riseGrow", "sprayIntensity" };
        static readonly string[] Colors = { "sprayColor" };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(TaoBaseSprayPreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(TaoPrefab);
            bool dirty = false;
            foreach (var bs in root.GetComponentsInChildren<TaoBaseSpray>(true))
            {
                if (!UsesPreset(bs, p)) continue;
                var so = new SerializedObject(bs);
                var pso = new SerializedObject(p);
                foreach (var f in Floats) CopyFloat(pso, so, f);
                foreach (var c in Colors) so.FindProperty(c).colorValue = pso.FindProperty(c).colorValue;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
            }
            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, TaoPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            if (dirty) Debug.Log("[TaoBaseSprayPreset] 프리팹에 적용 완료");
            else Debug.LogError("[TaoBaseSprayPreset] 이 에셋을 참조하는 TaoBaseSpray 없음");
        }

        void Capture(TaoBaseSprayPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(TaoPrefab);
            if (root != null)
            {
                foreach (var bs in root.GetComponentsInChildren<TaoBaseSpray>(true))
                {
                    if (!UsesPreset(bs, p)) continue;
                    Undo.RecordObject(p, "Capture Tao Base Spray");
                    var so = new SerializedObject(bs);
                    var pso = new SerializedObject(p);
                    foreach (var f in Floats) CopyFloat(so, pso, f);
                    foreach (var c in Colors) pso.FindProperty(c).colorValue = so.FindProperty(c).colorValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log("[TaoBaseSprayPreset] 현재값 캡처 완료");
                    return;
                }
            }
            Debug.LogError("[TaoBaseSprayPreset] 이 에셋을 참조하는 TaoBaseSpray 없음");
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
