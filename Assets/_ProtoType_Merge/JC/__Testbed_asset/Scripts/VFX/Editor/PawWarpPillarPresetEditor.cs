using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(PawWarpPillarPreset))]
    public class PawWarpPillarPresetEditor : Editor
    {
        static readonly string[] PrefabPaths =
        {
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/N_PawForYou/PawForYou.prefab",
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/N_PawForYou/PawForYouMistake.prefab",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (PawWarpPillarPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 WarpPillar(PawWarpPillar)에 반영.\n단면/경계 항목은 프리셋+livePreview로만 구동됩니다.", MessageType.Info);
        }

        static readonly string[] Floats = { "width", "height", "bottomOffset", "duration", "flashFrac", "flashBoost", "narrow", "intensity" };
        static readonly string[] Colors = { "coreColor", "glowColor" };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(PawWarpPillarPreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                foreach (var ws in root.GetComponentsInChildren<PawWarpPillar>(true))
                {
                    if (!UsesPreset(ws, p)) continue;
                    var so = new SerializedObject(ws);
                    var pso = new SerializedObject(p);
                    foreach (var f in Floats) CopyFloat(pso, so, f);
                    foreach (var c in Colors) so.FindProperty(c).colorValue = pso.FindProperty(c).colorValue;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    applied++;
                }
                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            if (applied == 0) Debug.LogError("[PawWarpPillarPreset] 이 에셋을 참조하는 PawWarpPillar 없음");
            else Debug.Log($"[PawWarpPillarPreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(PawWarpPillarPreset p)
        {
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var ws in root.GetComponentsInChildren<PawWarpPillar>(true))
                {
                    if (!UsesPreset(ws, p)) continue;
                    Undo.RecordObject(p, "Capture Paw Warp Pillar");
                    var so = new SerializedObject(ws);
                    var pso = new SerializedObject(p);
                    foreach (var f in Floats) CopyFloat(so, pso, f);
                    foreach (var c in Colors) pso.FindProperty(c).colorValue = so.FindProperty(c).colorValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log("[PawWarpPillarPreset] 현재값 캡처 완료");
                    return;
                }
            }
            Debug.LogError("[PawWarpPillarPreset] 이 에셋을 참조하는 PawWarpPillar 없음");
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
