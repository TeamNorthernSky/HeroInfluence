using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(TaoAuraFlarePreset))]
    public class TaoAuraFlarePresetEditor : Editor
    {
        const string TaoPrefab = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/Taosenaiyo/Taosenaiyo.prefab";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (TaoAuraFlarePreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 AuraFlare(TaoAuraFlare)에 반영.\n플레어/펜선/상단 경계 항목은 프리셋+livePreview로만 구동됩니다.", MessageType.Info);
        }

        static readonly string[] Floats = { "width", "height", "groundOffsetY", "riseGrow", "baseIntensity", "lineIntensity" };
        static readonly string[] Colors = { "baseColor", "lineColor" };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(TaoAuraFlarePreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(TaoPrefab);
            bool dirty = false;
            foreach (var af in root.GetComponentsInChildren<TaoAuraFlare>(true))
            {
                if (!UsesPreset(af, p)) continue;
                var so = new SerializedObject(af);
                var pso = new SerializedObject(p);
                foreach (var f in Floats) CopyFloat(pso, so, f);
                foreach (var c in Colors) so.FindProperty(c).colorValue = pso.FindProperty(c).colorValue;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
            }
            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, TaoPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            if (dirty) Debug.Log("[TaoAuraFlarePreset] 프리팹에 적용 완료");
            else Debug.LogError("[TaoAuraFlarePreset] 이 에셋을 참조하는 TaoAuraFlare 없음");
        }

        void Capture(TaoAuraFlarePreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(TaoPrefab);
            if (root != null)
            {
                foreach (var af in root.GetComponentsInChildren<TaoAuraFlare>(true))
                {
                    if (!UsesPreset(af, p)) continue;
                    Undo.RecordObject(p, "Capture Tao Aura Flare");
                    var so = new SerializedObject(af);
                    var pso = new SerializedObject(p);
                    foreach (var f in Floats) CopyFloat(so, pso, f);
                    foreach (var c in Colors) pso.FindProperty(c).colorValue = so.FindProperty(c).colorValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log("[TaoAuraFlarePreset] 현재값 캡처 완료");
                    return;
                }
            }
            Debug.LogError("[TaoAuraFlarePreset] 이 에셋을 참조하는 TaoAuraFlare 없음");
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
