using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealArcRingPreset))]
    public class HealArcRingPresetEditor : Editor
    {
        const string DIR = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/HealSkill";
        static string OrbitPrefab => DIR + "/HealOrbit.prefab";   // ArcRing이 이 프리팹의 자식

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (HealArcRingPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 HealOrbit 프리팹의 ArcRing(HealArcRing)에 반영.\nHealArcRing.livePreview가 켜져 있으면 플레이 중에도 새 원호부터 즉시 반영됩니다.", MessageType.Info);
        }

        // 프리팹 내 여러 HealArcRing 중, preset 필드가 이 애셋을 가리키는 것을 타깃. (메인 3_ / 서브 4_ 공존 대응)
        static HealArcRing FindTargetArc(GameObject root, HealArcRingPreset p)
        {
            var arcs = root.GetComponentsInChildren<HealArcRing>(true);
            foreach (var a in arcs)
            {
                var aso = new SerializedObject(a);
                if (aso.FindProperty("preset").objectReferenceValue == p) return a;
            }
            return arcs.Length > 0 ? arcs[0] : null;   // 폴백
        }

        void Apply(HealArcRingPreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(OrbitPrefab);
            var arc = FindTargetArc(root, p);
            if (arc == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[HealArcRingPreset] 이 프리셋을 참조하는 ArcRing 없음"); return; }
            var so = new SerializedObject(arc);
            so.FindProperty("spawnIntervalMin").floatValue = p.spawnIntervalMin;
            so.FindProperty("spawnIntervalMax").floatValue = p.spawnIntervalMax;
            so.FindProperty("maxArcs").intValue = p.maxArcs;
            so.FindProperty("sweepDurationMin").floatValue = p.sweepDurationMin;
            so.FindProperty("sweepDurationMax").floatValue = p.sweepDurationMax;
            so.FindProperty("arcSpanMinDeg").floatValue = p.arcSpanMinDeg;
            so.FindProperty("arcSpanMaxDeg").floatValue = p.arcSpanMaxDeg;
            so.FindProperty("radius").floatValue = p.radius;
            so.FindProperty("radiusNoise").floatValue = p.radiusNoise;
            so.FindProperty("orbitHeight").floatValue = p.orbitHeight;
            so.FindProperty("tiltMaxDeg").floatValue = p.tiltMaxDeg;
            so.FindProperty("bidirectional").boolValue = p.bidirectional;
            so.FindProperty("widthStartMin").floatValue = p.widthStartMin;
            so.FindProperty("widthStartMax").floatValue = p.widthStartMax;
            so.FindProperty("trailTimeMin").floatValue = p.trailTimeMin;
            so.FindProperty("trailTimeMax").floatValue = p.trailTimeMax;
            if (p.colorGradient != null) so.FindProperty("colorGradient").gradientValue = p.colorGradient;
            so.FindProperty("alphaMin").floatValue = p.alphaMin;
            so.FindProperty("alphaMax").floatValue = p.alphaMax;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, OrbitPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[HealArcRingPreset] 프리팹에 적용 완료");
        }

        void Capture(HealArcRingPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitPrefab);
            var arc = FindTargetArc(root, p);
            if (arc == null) { Debug.LogError("[HealArcRingPreset] 이 프리셋을 참조하는 ArcRing 없음"); return; }
            Undo.RecordObject(p, "Capture Heal Arc Ring");
            var so = new SerializedObject(arc);
            p.spawnIntervalMin = so.FindProperty("spawnIntervalMin").floatValue;
            p.spawnIntervalMax = so.FindProperty("spawnIntervalMax").floatValue;
            p.maxArcs = so.FindProperty("maxArcs").intValue;
            p.sweepDurationMin = so.FindProperty("sweepDurationMin").floatValue;
            p.sweepDurationMax = so.FindProperty("sweepDurationMax").floatValue;
            p.arcSpanMinDeg = so.FindProperty("arcSpanMinDeg").floatValue;
            p.arcSpanMaxDeg = so.FindProperty("arcSpanMaxDeg").floatValue;
            p.radius = so.FindProperty("radius").floatValue;
            p.radiusNoise = so.FindProperty("radiusNoise").floatValue;
            p.orbitHeight = so.FindProperty("orbitHeight").floatValue;
            p.tiltMaxDeg = so.FindProperty("tiltMaxDeg").floatValue;
            p.bidirectional = so.FindProperty("bidirectional").boolValue;
            p.widthStartMin = so.FindProperty("widthStartMin").floatValue;
            p.widthStartMax = so.FindProperty("widthStartMax").floatValue;
            p.trailTimeMin = so.FindProperty("trailTimeMin").floatValue;
            p.trailTimeMax = so.FindProperty("trailTimeMax").floatValue;
            p.colorGradient = so.FindProperty("colorGradient").gradientValue;
            p.alphaMin = so.FindProperty("alphaMin").floatValue;
            p.alphaMax = so.FindProperty("alphaMax").floatValue;
            EditorUtility.SetDirty(p);
            Debug.Log("[HealArcRingPreset] 현재값 캡처 완료");
        }
    }
}
