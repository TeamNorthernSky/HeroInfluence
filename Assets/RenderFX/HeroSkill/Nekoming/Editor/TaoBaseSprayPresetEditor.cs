using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(TaoBaseSprayPreset))]
    public class TaoBaseSprayPresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Taosenaiyo/Prefabs";
        // ★변종 인식(260807) — _Basic/_Alter 프리셋은 제 부품 프리팹(Tao_Revive±)만 만진다.
        string Sfx => JcPresetEditorUtil.VariantSuffix(target) ?? "_Alter";
        string TaoPrefab => DIR + "/Tao_Revive" + Sfx + ".prefab";

        // 따름 잠금 — 색·밝기·불투명도 외 전부는 Basic 이 정본.
        static readonly string[] TransformProps =
        {
            "width", "height", "groundOffsetY", "riseGrow", "flare", "flareCurve",
            "bottomFade", "verticalBias", "topMin", "topMax", "topSoft", "topNoiseScale", "topNoiseSpeed",
            "facePower", "sprayHeight", "sprayCount", "sprayWidth", "spraySharp",
            "sprayDashFreq", "sprayDashDuty", "spraySpeed", "sprayTaper", "sprayRandom",
        };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.TaoSpray.Fold", TransformProps);
            var p = (TaoBaseSprayPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
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
