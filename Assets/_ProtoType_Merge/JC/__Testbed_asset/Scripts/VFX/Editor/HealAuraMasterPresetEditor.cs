using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealAuraMasterPreset))]
    public class HealAuraMasterPresetEditor : Editor
    {
        const string DIR = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/N_HealSkill";
        static string OrbitPrefab => DIR + "/HealOrbit.prefab";   // 오케스트레이터 HealOrbitVfx가 루트

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (HealAuraMasterPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이펙트 전체 타이밍을 HealOrbit 프리팹의 HealOrbitVfx(오케스트레이터)에 반영.\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        void Apply(HealAuraMasterPreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(OrbitPrefab);
            var vfx = root.GetComponent<HealOrbitVfx>();
            var so = new SerializedObject(vfx);
            so.FindProperty("duration").floatValue = p.duration;
            so.FindProperty("fadeInTime").floatValue = p.fadeInTime;
            so.FindProperty("fadeOutTime").floatValue = p.fadeOutTime;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, OrbitPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[HealAuraMasterPreset] 프리팹에 적용 완료");
        }

        void Capture(HealAuraMasterPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitPrefab);
            var vfx = root.GetComponent<HealOrbitVfx>();
            var so = new SerializedObject(vfx);
            Undo.RecordObject(p, "Capture Heal Aura Master");
            p.duration = so.FindProperty("duration").floatValue;
            p.fadeInTime = so.FindProperty("fadeInTime").floatValue;
            p.fadeOutTime = so.FindProperty("fadeOutTime").floatValue;
            EditorUtility.SetDirty(p);
            Debug.Log("[HealAuraMasterPreset] 현재값 캡처 완료");
        }
    }
}
