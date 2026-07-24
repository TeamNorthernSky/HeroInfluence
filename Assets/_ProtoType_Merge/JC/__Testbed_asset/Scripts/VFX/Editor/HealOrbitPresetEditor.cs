using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealOrbitPreset))]
    public class HealOrbitPresetEditor : Editor
    {
        const string DIR = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/HealSkill";
        static string MatCoreInner => DIR + "/HealOrbitCoreInner.mat";   // 전용 코어(스파이키 가능)
        static string MatRimShell  => DIR + "/HealOrbitRimShell.mat";    // 전용 외곽선(매끈)
        static string OrbitPrefab  => DIR + "/HealOrbit.prefab";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (HealOrbitPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 HealOrbitCoreInner/RimShell.mat(전용) + HealOrbit 프리팹(HealOrbitVfx, Orb 크기, 코어/림 사이즈)에 반영.\n발사체 재질과 분리되어 있어 발사체에 영향 없음.", MessageType.Info);
        }

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        void Apply(HealOrbitPreset p)
        {
            // CoreInner(전용): 코어 색/밝기 + 스파이크. 림은 꺼둠.
            var inner = Load<Material>(MatCoreInner);
            if (inner)
            {
                inner.SetColor("_CoreColor", p.coreColor);
                inner.SetFloat("_CoreIntensity", p.coreIntensity); inner.SetFloat("_CorePower", p.corePower);
                inner.SetFloat("_CoreSpikeAmount", p.coreSpikeAmount); inner.SetFloat("_RimSpikeAmount", p.rimSpikeAmount);
                inner.SetFloat("_SpikeFreq", p.spikeFreq); inner.SetFloat("_SpikeSpeed", p.spikeSpeed); inner.SetFloat("_SpikeSharp", p.spikeSharp);
                inner.SetFloat("_RimIntensity", 0f); inner.SetFloat("_GapStrength", 0f); inner.SetFloat("_FadeMul", 1f);
                EditorUtility.SetDirty(inner);
            }
            // RimShell(전용): 림 색/밝기. 코어·스파이크 꺼둠.
            var shell = Load<Material>(MatRimShell);
            if (shell)
            {
                shell.SetColor("_RimColor", p.rimColor);
                shell.SetFloat("_RimIntensity", p.rimIntensity); shell.SetFloat("_RimPower", p.rimPower);
                shell.SetFloat("_CoreIntensity", 0f); shell.SetFloat("_CoreSpikeAmount", 0f); shell.SetFloat("_RimSpikeAmount", 0f);
                shell.SetFloat("_GapStrength", 0f); shell.SetFloat("_FadeMul", 1f);
                EditorUtility.SetDirty(shell);
            }
            AssetDatabase.SaveAssets();

            var root = PrefabUtility.LoadPrefabContents(OrbitPrefab);
            var so = new SerializedObject(root.GetComponent<HealOrbitVfx>());
            so.FindProperty("orbitRadius").floatValue = p.orbitRadius;
            so.FindProperty("orbitHeight").floatValue = p.orbitHeight;
            so.FindProperty("angularSpeed").floatValue = p.angularSpeed;
            so.FindProperty("startAngle").floatValue = p.startAngle;
            so.FindProperty("tiltDeg").floatValue = p.tiltDeg;
            so.FindProperty("orbWorldSize").floatValue = p.orbWorldSize;
            so.ApplyModifiedPropertiesWithoutUndo();
            var vInner = root.transform.Find("Orb/CoreInner");
            var vShell = root.transform.Find("Orb/RimShell");
            if (vInner) vInner.localScale = Vector3.one * p.coreSize;
            if (vShell) vShell.localScale = Vector3.one * p.rimSize;
            var orb = root.transform.Find("Orb");
            if (orb) orb.localScale = Vector3.one * p.orbWorldSize;
            PrefabUtility.SaveAsPrefabAsset(root, OrbitPrefab);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log("[HealOrbitPreset] 프리팹에 적용 완료");
        }

        void Capture(HealOrbitPreset p)
        {
            Undo.RecordObject(p, "Capture Heal Orbit");
            var inner = Load<Material>(MatCoreInner);
            if (inner)
            {
                p.coreColor = inner.GetColor("_CoreColor");
                p.coreIntensity = inner.GetFloat("_CoreIntensity"); p.corePower = inner.GetFloat("_CorePower");
                p.coreSpikeAmount = inner.GetFloat("_CoreSpikeAmount"); p.rimSpikeAmount = inner.GetFloat("_RimSpikeAmount");
                p.spikeFreq = inner.GetFloat("_SpikeFreq"); p.spikeSpeed = inner.GetFloat("_SpikeSpeed"); p.spikeSharp = inner.GetFloat("_SpikeSharp");
            }
            var shell = Load<Material>(MatRimShell);
            if (shell)
            {
                p.rimColor = shell.GetColor("_RimColor"); p.rimIntensity = shell.GetFloat("_RimIntensity"); p.rimPower = shell.GetFloat("_RimPower");
            }

            var root = Load<GameObject>(OrbitPrefab);
            var vfx = root.GetComponent<HealOrbitVfx>();
            var so = new SerializedObject(vfx);
            p.orbitRadius = so.FindProperty("orbitRadius").floatValue;
            p.orbitHeight = so.FindProperty("orbitHeight").floatValue;
            p.angularSpeed = so.FindProperty("angularSpeed").floatValue;
            p.startAngle = so.FindProperty("startAngle").floatValue;
            p.tiltDeg = so.FindProperty("tiltDeg").floatValue;
            p.orbWorldSize = so.FindProperty("orbWorldSize").floatValue;
            var vInner = root.transform.Find("Orb/CoreInner");
            var vShell = root.transform.Find("Orb/RimShell");
            if (vInner) p.coreSize = vInner.localScale.x;
            if (vShell) p.rimSize = vShell.localScale.x;

            EditorUtility.SetDirty(p);
            Debug.Log("[HealOrbitPreset] 현재값 캡처 완료");
        }
    }
}
