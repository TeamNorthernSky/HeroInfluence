using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// 원호 링 프리셋 에디터 — 260805 변종 인식 개편.
    /// ★적용/캡처 대상은 「preset 필드가 이 에셋을 참조하는 HealArcRing」만.
    ///   과거의 arcs[0] 폴백은 _Basic 적용이 _Alter 프리팹을 덮는 사고 경로라 제거했다 — 없으면 에러.
    /// 공용 프리셋(7_Sub)은 두 프리팹(Basic·Alter) 모두에 적용된다.
    /// </summary>
    [CustomEditor(typeof(HealArcRingPreset))]
    public class HealArcRingPresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Heal";
        static readonly string[] PrefabPaths =
        {
            DIR + "/Prefabs/HealOrbit_Basic.prefab",
            DIR + "/Prefabs/HealOrbit_Alter.prefab",
            // ★LFL 독립 사본(260807) — UsesPreset 가드 덕에 L4/L7 프리셋만 여기 닿는다(힐과 절연).
            "Assets/RenderFX/HeroSkill/Nekoming/LetsFightingLove/Prefabs/LFL_LandAura_Basic.prefab",
            "Assets/RenderFX/HeroSkill/Nekoming/LetsFightingLove/Prefabs/LFL_LandAura_Alter.prefab",
        };

        static readonly string[] TransformProps =
        {
            "spawnIntervalMin", "spawnIntervalMax", "maxArcs", "sweepDurationMin", "sweepDurationMax",
            "arcSpanMinDeg", "arcSpanMaxDeg", "radius", "radiusNoise", "orbitHeight", "tiltMaxDeg",
            "bidirectional", "widthStartMin", "widthStartMax", "trailTimeMin", "trailTimeMax",
        };

        static readonly string[] Fields =
        {
            "spawnIntervalMin", "spawnIntervalMax", "maxArcs", "sweepDurationMin", "sweepDurationMax",
            "arcSpanMinDeg", "arcSpanMaxDeg", "radius", "radiusNoise", "orbitHeight", "tiltMaxDeg",
            "widthStartMin", "widthStartMax", "trailTimeMin", "trailTimeMax", "alphaMin", "alphaMax",
        };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.ArcRing.Fold", TransformProps);
            var p = (HealArcRingPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 ArcRing 에만 반영(Basic·Alter 프리팹 자동 탐색 — 다른 변종을 덮지 않음).\nlivePreview가 켜져 있으면 플레이 중에도 새 원호부터 즉시 반영됩니다.", MessageType.Info);
        }

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(HealArcRingPreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                foreach (var arc in root.GetComponentsInChildren<HealArcRing>(true))
                {
                    if (!UsesPreset(arc, p)) continue;
                    var so = new SerializedObject(arc);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloatLike(pso, so, f);
                    so.FindProperty("bidirectional").boolValue = p.bidirectional;
                    if (p.colorGradient != null) so.FindProperty("colorGradient").gradientValue = p.colorGradient;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    applied++;
                }
                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            if (applied == 0) Debug.LogError("[HealArcRingPreset] 이 프리셋을 참조하는 ArcRing 없음 — 결선을 확인하세요(폴백 없음)");
            else Debug.Log($"[HealArcRingPreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(HealArcRingPreset p)
        {
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var arc in root.GetComponentsInChildren<HealArcRing>(true))
                {
                    if (!UsesPreset(arc, p)) continue;
                    Undo.RecordObject(p, "Capture Heal Arc Ring");
                    var so = new SerializedObject(arc);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloatLike(so, pso, f);
                    pso.FindProperty("bidirectional").boolValue = so.FindProperty("bidirectional").boolValue;
                    pso.FindProperty("colorGradient").gradientValue = so.FindProperty("colorGradient").gradientValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log($"[HealArcRingPreset] 현재값 캡처 완료 ({root.name})");
                    return;
                }
            }
            Debug.LogError("[HealArcRingPreset] 이 프리셋을 참조하는 ArcRing 없음 — 결선을 확인하세요(폴백 없음)");
        }

        static void CopyFloatLike(SerializedObject from, SerializedObject to, string name)
        {
            var pf = from.FindProperty(name);
            var pt = to.FindProperty(name);
            if (pf == null || pt == null) return;
            if (pf.propertyType == SerializedPropertyType.Integer) pt.intValue = pf.intValue;
            else if (pf.propertyType == SerializedPropertyType.Boolean) pt.boolValue = pf.boolValue;
            else pt.floatValue = pf.floatValue;
        }
    }
}
