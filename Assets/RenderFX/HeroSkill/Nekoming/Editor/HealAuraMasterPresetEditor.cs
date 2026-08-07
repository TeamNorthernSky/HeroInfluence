using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealAuraMasterPreset))]
    public class HealAuraMasterPresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Heal";
        // ★공용 프리셋(타이밍+묶음 트랜스폼) — 같은 스킬의 Basic·Alter 두 프리팹 모두에 적용.
        //   LFL 독립 사본(260807)도 같은 목록에 두되, UsesMaster 가드가 「이 에셋을 masterPreset 으로
        //   참조하는 프리팹」만 통과시킨다 — 힐 9번과 LFL L9 이 서로를 덮지 않는다(완전 절연).
        static readonly string[] PrefabPaths =
        {
            DIR + "/Prefabs/HealOrbit_Basic.prefab",
            DIR + "/Prefabs/HealOrbit_Alter.prefab",
            "Assets/RenderFX/HeroSkill/Nekoming/LetsFightingLove/Prefabs/LFL_LandAura_Basic.prefab",
            "Assets/RenderFX/HeroSkill/Nekoming/LetsFightingLove/Prefabs/LFL_LandAura_Alter.prefab",
        };

        static bool UsesMaster(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("masterPreset")?.objectReferenceValue == presetAsset;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (HealAuraMasterPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
            }
            EditorGUILayout.HelpBox("적용: 이펙트 전체 타이밍을 HealOrbit 프리팹의 HealOrbitVfx(오케스트레이터)에 반영.\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        void Apply(HealAuraMasterPreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                var vfx = root.GetComponent<HealOrbitVfx>();
                if (vfx == null || !UsesMaster(vfx, p)) { PrefabUtility.UnloadPrefabContents(root); continue; }
                var so = new SerializedObject(vfx);
                so.FindProperty("duration").floatValue = p.duration;
                so.FindProperty("fadeInTime").floatValue = p.fadeInTime;
                so.FindProperty("fadeOutTime").floatValue = p.fadeOutTime;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                applied++;
            }
            if (applied == 0) Debug.LogError("[HealAuraMasterPreset] 이 에셋을 masterPreset 으로 참조하는 프리팹 없음 — 결선을 확인하세요");
            else Debug.Log($"[HealAuraMasterPreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(HealAuraMasterPreset p)
        {
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var vfx = root != null ? root.GetComponent<HealOrbitVfx>() : null;
                if (vfx == null || !UsesMaster(vfx, p)) continue;
                var so = new SerializedObject(vfx);
                Undo.RecordObject(p, "Capture Heal Aura Master");
                p.duration = so.FindProperty("duration").floatValue;
                p.fadeInTime = so.FindProperty("fadeInTime").floatValue;
                p.fadeOutTime = so.FindProperty("fadeOutTime").floatValue;
                EditorUtility.SetDirty(p);
                Debug.Log($"[HealAuraMasterPreset] 현재값 캡처 완료 ({root.name})");
                return;
            }
            Debug.LogError("[HealAuraMasterPreset] 이 에셋을 masterPreset 으로 참조하는 프리팹 없음 — 결선을 확인하세요");
        }
    }
}
