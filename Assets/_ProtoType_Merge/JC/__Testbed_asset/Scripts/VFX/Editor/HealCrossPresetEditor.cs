using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealCrossPreset))]
    public class HealCrossPresetEditor : Editor
    {
        const string DIR = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/HealSkill";
        static string OrbitPrefab => DIR + "/HealOrbit.prefab";   // CrossBurst가 이 프리팹의 자식

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (HealCrossPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 HealOrbit 프리팹의 CrossBurst(HealCrossBurst)에 반영.\nlivePreview가 켜져 있으면 플레이 중에도 새 십자부터 즉시 반영됩니다.", MessageType.Info);
        }

        static HealCrossBurst FindTarget(GameObject root) => root.GetComponentInChildren<HealCrossBurst>(true);

        static readonly string[] Fields =
        {
            "maxCrosses","spawnIntervalMin","spawnIntervalMax","lifetimeMin","lifetimeMax",
            "radius","baseYOffset","riseSpeedMin","riseSpeedMax","riseAccelMul","riseAccelTime",
            "popOvershoot","popFrac","endFrac","bobAmp","bobFreq","spinMax",
            "sizeMin","sizeMax","fadeInFrac","fadeOutFrac",
            "intensity","barWidth","barLength","softness",
        };

        void Apply(HealCrossPreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(OrbitPrefab);
            var cb = FindTarget(root);
            if (cb == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[HealCrossPreset] CrossBurst(HealCrossBurst) 없음"); return; }
            var so = new SerializedObject(cb);
            var pso = new SerializedObject(p);
            foreach (var f in Fields) CopyFloatLike(pso, so, f);
            so.FindProperty("color").colorValue = p.color;
            so.FindProperty("billboardYOnly").boolValue = p.billboardYOnly;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, OrbitPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[HealCrossPreset] 프리팹에 적용 완료");
        }

        void Capture(HealCrossPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitPrefab);
            var cb = FindTarget(root);
            if (cb == null) { Debug.LogError("[HealCrossPreset] CrossBurst(HealCrossBurst) 없음"); return; }
            Undo.RecordObject(p, "Capture Heal Cross");
            var so = new SerializedObject(cb);
            var pso = new SerializedObject(p);
            foreach (var f in Fields) CopyFloatLike(so, pso, f);
            pso.FindProperty("color").colorValue = so.FindProperty("color").colorValue;
            pso.FindProperty("billboardYOnly").boolValue = so.FindProperty("billboardYOnly").boolValue;
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(p);
            Debug.Log("[HealCrossPreset] 현재값 캡처 완료");
        }

        // int/float 겸용 복사(같은 이름 프로퍼티)
        static void CopyFloatLike(SerializedObject from, SerializedObject to, string name)
        {
            var pf = from.FindProperty(name);
            var pt = to.FindProperty(name);
            if (pf == null || pt == null) return;
            if (pf.propertyType == SerializedPropertyType.Integer) pt.intValue = pf.intValue;
            else pt.floatValue = pf.floatValue;
        }
    }
}
