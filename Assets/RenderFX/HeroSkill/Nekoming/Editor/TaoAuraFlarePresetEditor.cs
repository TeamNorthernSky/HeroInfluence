using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(TaoAuraFlarePreset))]
    public class TaoAuraFlarePresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Taosenaiyo/Prefabs";
        // ★변종 인식(260807) — _Basic/_Alter 프리셋은 제 부품 프리팹(Tao_Revive±)만 만진다.
        string Sfx => JcPresetEditorUtil.VariantSuffix(target) ?? "_Alter";
        string TaoPrefab => DIR + "/Tao_Revive" + Sfx + ".prefab";

        // 따름 잠금 — 색·밝기·불투명도 외 전부는 Basic 이 정본.
        static readonly string[] TransformProps =
        {
            "width", "height", "groundOffsetY", "riseGrow", "flare", "flareCurve", "baseBoost",
            "bottomFade", "verticalBias", "topMin", "topMax", "topSoft", "topNoiseScale", "topNoiseSpeed",
            "facePower", "lineCount", "lineWidth", "lineSharp", "dashFreq", "dashDuty", "scrollSpeed",
        };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.TaoFlare.Fold", TransformProps);
            var p = (TaoAuraFlarePreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
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
