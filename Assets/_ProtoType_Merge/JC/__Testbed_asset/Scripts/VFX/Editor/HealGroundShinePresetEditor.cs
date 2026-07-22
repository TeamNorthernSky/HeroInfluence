using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealGroundShinePreset))]
    public class HealGroundShinePresetEditor : Editor
    {
        // 이 프리셋 SO를 쓰는 컴포넌트가 있을 수 있는 프리팹들(힐 오라 + PawForYou 타격 재사용).
        // 적용 대상은 "preset 필드가 이 에셋을 참조하는" 컴포넌트만 — 색 변형 에셋이 서로를 덮지 않게.
        static readonly string[] PrefabPaths =
        {
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/HealSkill/HealOrbit.prefab",
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/PawForYou/PawForYou.prefab",
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/PawForYou/PawForYouMistake.prefab",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (HealGroundShinePreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 GroundShine(HealGroundShine)에 반영(힐/Paw 프리팹 자동 탐색).\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        static readonly string[] Fields =
        {
            "discRadius","rayRadius","groundOffsetY","intensity","opacity",
            "edgeSoft","sideSoft","centerFalloff",
            "rayDensity","raySharp","rayRotSpeed","valleyWidth",
            "spreadGrow",
        };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(HealGroundShinePreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                foreach (var gs in root.GetComponentsInChildren<HealGroundShine>(true))
                {
                    if (!UsesPreset(gs, p)) continue;
                    var so = new SerializedObject(gs);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloat(pso, so, f);
                    so.FindProperty("color").colorValue = p.color;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    applied++;
                }
                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            if (applied == 0) Debug.LogError("[HealGroundShinePreset] 이 에셋을 참조하는 GroundShine 없음");
            else Debug.Log($"[HealGroundShinePreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(HealGroundShinePreset p)
        {
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var gs in root.GetComponentsInChildren<HealGroundShine>(true))
                {
                    if (!UsesPreset(gs, p)) continue;
                    Undo.RecordObject(p, "Capture Heal Ground Shine");
                    var so = new SerializedObject(gs);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloat(so, pso, f);
                    pso.FindProperty("color").colorValue = so.FindProperty("color").colorValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log("[HealGroundShinePreset] 현재값 캡처 완료");
                    return;
                }
            }
            Debug.LogError("[HealGroundShinePreset] 이 에셋을 참조하는 GroundShine 없음");
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
