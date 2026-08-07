using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(TaoMagicCirclePreset))]
    public class TaoMagicCirclePresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Taosenaiyo/Prefabs";
        // ★변종 인식(260807) — _Basic/_Alter 프리셋은 제 부품 프리팹(Tao_Revive±)만 만진다.
        //   레거시 통짜(_Legacy/Taosenaiyo)는 대상에서 제외 — ASB 결선 유지용 동결.
        string Sfx => JcPresetEditorUtil.VariantSuffix(target) ?? "_Alter";
        string TaoPrefab => DIR + "/Tao_Revive" + Sfx + ".prefab";

        // 따름 잠금 — 색·밝기 외 전부(형태·회전·펄스)는 Basic 이 정본.
        static readonly string[] TransformProps =
        {
            "radius", "groundOffsetY", "forwardOffset",
            "centerSpread", "centerPulseAmp", "centerPulsePeriod",
            "appearTime", "appearStartScale", "appearStartAlpha",
            "ring1", "ring2", "ring3", "ringIntensity", "lineWidth", "lineSoft",
            "hexRadius", "hexWidth", "hexIntensity",
            "squareRadius", "squareWidth", "squareIntensity",
            "satOrbit", "satCount", "satRadius", "satIntensity",
            "spokeCount", "spokeInner", "spokeOuter", "spokeIntensity",
            "tickRadius", "tickWidth", "tickCount", "tickDuty", "tickIntensity",
            "rotInner", "rotOuter", "edgeFade",
        };

        // ★T10(배리지 손앞 마법진)은 프리팹에 컴포넌트가 없다(TaoBarrageVfx 가 절차 생성) —
        //   적용/캡처 대상이 없으므로 버튼을 숨기고 라이브 전용임을 안내한다(260807).
        bool IsBarrageCircle => target != null && target.name.Contains("BarrageCircle");

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.TaoCircle.Fold", TransformProps);
            var p = (TaoMagicCirclePreset)target;
            EditorGUILayout.Space();
            if (IsBarrageCircle)
            {
                JcPresetEditorUtil.DrawSaveButton(target, wide: true);
                EditorGUILayout.HelpBox("배리지 손앞 마법진(T10) — 시각물이 절차 생성이라 프리팹 베이크(적용/캡처)가 없습니다.\n" +
                    "조절값은 메모리에 유지되고 💾 클릭 시에만 파일로 확정됩니다. 위치는 T9 의 손앞 섹션이 소유.", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 MagicCircle(TaoMagicCircle)에 반영.\n문양(링/육망성/눈금/회전) 항목은 프리셋+livePreview로만 구동됩니다.", MessageType.Info);
        }

        static readonly string[] Floats = { "radius", "groundOffsetY", "intensity" };

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
