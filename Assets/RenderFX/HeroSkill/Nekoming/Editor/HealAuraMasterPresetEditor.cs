using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealAuraMasterPreset))]
    public class HealAuraMasterPresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Heal";
        // ★공용 프리셋(타이밍+묶음 트랜스폼) — Basic·Alter 두 프리팹 모두에 적용
        static readonly string[] PrefabPaths =
        {
            DIR + "/Prefabs/HealOrbit_Basic.prefab",
            DIR + "/Prefabs/HealOrbit_Alter.prefab",
        };

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
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                var so = new SerializedObject(root.GetComponent<HealOrbitVfx>());
                so.FindProperty("duration").floatValue = p.duration;
                so.FindProperty("fadeInTime").floatValue = p.fadeInTime;
                so.FindProperty("fadeOutTime").floatValue = p.fadeOutTime;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[HealAuraMasterPreset] 프리팹에 적용 완료 (Basic·Alter)");
        }

        void Capture(HealAuraMasterPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[0])
                    ?? AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[1]);
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
