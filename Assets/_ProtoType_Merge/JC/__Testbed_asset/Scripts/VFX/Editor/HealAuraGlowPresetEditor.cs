using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealAuraGlowPreset))]
    public class HealAuraGlowPresetEditor : Editor
    {
        const string DIR = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/N_HealSkill";
        static string OrbitPrefab => DIR + "/HealOrbit.prefab";   // AuraGlow가 이 프리팹의 자식

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (HealAuraGlowPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 HealOrbit 프리팹의 AuraGlow(HealAuraGlow)에 반영.\nHealAuraGlow.livePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        static HealAuraGlow FindTarget(GameObject root) => root.GetComponentInChildren<HealAuraGlow>(true);

        void Apply(HealAuraGlowPreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(OrbitPrefab);
            var fg = FindTarget(root);
            if (fg == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[HealAuraGlowPreset] AuraGlow(HealAuraGlow) 없음"); return; }
            var so = new SerializedObject(fg);
            so.FindProperty("width").floatValue = p.width;
            so.FindProperty("height").floatValue = p.height;
            so.FindProperty("groundOffsetY").floatValue = p.groundOffsetY;
            so.FindProperty("color").colorValue = p.color;
            so.FindProperty("intensity").floatValue = p.intensity;
            so.FindProperty("opacity").floatValue = p.opacity;
            so.FindProperty("bottomFade").floatValue = p.bottomFade;
            so.FindProperty("verticalBias").floatValue = p.verticalBias;
            so.FindProperty("topMin").floatValue = p.topMin;
            so.FindProperty("topMax").floatValue = p.topMax;
            so.FindProperty("topSoft").floatValue = p.topSoft;
            so.FindProperty("topNoiseScale").floatValue = p.topNoiseScale;
            so.FindProperty("topNoiseSpeed").floatValue = p.topNoiseSpeed;
            so.FindProperty("facePower").floatValue = p.facePower;
            so.FindProperty("streakTiling").floatValue = p.streakTiling;
            so.FindProperty("streakStrength").floatValue = p.streakStrength;
            so.FindProperty("streakScroll").floatValue = p.streakScroll;
            so.FindProperty("wobbleAmount").floatValue = p.wobbleAmount;
            so.FindProperty("wobbleSpeed").floatValue = p.wobbleSpeed;
            so.FindProperty("riseGrow").floatValue = p.riseGrow;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, OrbitPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[HealAuraGlowPreset] 프리팹에 적용 완료");
        }

        void Capture(HealAuraGlowPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitPrefab);
            var fg = FindTarget(root);
            if (fg == null) { Debug.LogError("[HealAuraGlowPreset] AuraGlow(HealAuraGlow) 없음"); return; }
            Undo.RecordObject(p, "Capture Heal Floor Glow");
            var so = new SerializedObject(fg);
            p.width = so.FindProperty("width").floatValue;
            p.height = so.FindProperty("height").floatValue;
            p.groundOffsetY = so.FindProperty("groundOffsetY").floatValue;
            p.color = so.FindProperty("color").colorValue;
            p.intensity = so.FindProperty("intensity").floatValue;
            p.opacity = so.FindProperty("opacity").floatValue;
            p.bottomFade = so.FindProperty("bottomFade").floatValue;
            p.verticalBias = so.FindProperty("verticalBias").floatValue;
            p.topMin = so.FindProperty("topMin").floatValue;
            p.topMax = so.FindProperty("topMax").floatValue;
            p.topSoft = so.FindProperty("topSoft").floatValue;
            p.topNoiseScale = so.FindProperty("topNoiseScale").floatValue;
            p.topNoiseSpeed = so.FindProperty("topNoiseSpeed").floatValue;
            p.facePower = so.FindProperty("facePower").floatValue;
            p.streakTiling = so.FindProperty("streakTiling").floatValue;
            p.streakStrength = so.FindProperty("streakStrength").floatValue;
            p.streakScroll = so.FindProperty("streakScroll").floatValue;
            p.wobbleAmount = so.FindProperty("wobbleAmount").floatValue;
            p.wobbleSpeed = so.FindProperty("wobbleSpeed").floatValue;
            p.riseGrow = so.FindProperty("riseGrow").floatValue;
            EditorUtility.SetDirty(p);
            Debug.Log("[HealAuraGlowPreset] 현재값 캡처 완료");
        }
    }
}
