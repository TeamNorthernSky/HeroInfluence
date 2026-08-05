using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(PawBeamPreset))]
    public class PawBeamPresetEditor : Editor
    {
        static readonly string[] PrefabPaths =
        {
            "Assets/RenderFX/HeroSkill/Nekoming/PawForYou/Prefabs/PawForYou.prefab",
            "Assets/RenderFX/HeroSkill/Nekoming/PawForYou/Prefabs/_Legacy/PawForYouMistake.prefab",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (PawBeamPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 PawBeam(수직 빔/워프 스트릭)에 반영.\n단면/노이즈/캡 항목은 프리셋+livePreview로만 구동됩니다.", MessageType.Info);
        }

        static readonly string[] Floats = { "width", "intensity" };
        static readonly string[] Colors = { "coreColor", "glowColor", "hitFlashColor" };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(PawBeamPreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                foreach (var pb in root.GetComponentsInChildren<PawBeam>(true))
                {
                    if (!UsesPreset(pb, p)) continue;
                    var so = new SerializedObject(pb);
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
            if (applied == 0) Debug.LogError("[PawBeamPreset] 이 에셋을 참조하는 PawBeam 없음");
            else Debug.Log($"[PawBeamPreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(PawBeamPreset p)
        {
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var pb in root.GetComponentsInChildren<PawBeam>(true))
                {
                    if (!UsesPreset(pb, p)) continue;
                    Undo.RecordObject(p, "Capture Paw Beam");
                    var so = new SerializedObject(pb);
                    var pso = new SerializedObject(p);
                    foreach (var f in Floats) CopyFloat(so, pso, f);
                    foreach (var c in Colors) pso.FindProperty(c).colorValue = so.FindProperty(c).colorValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log("[PawBeamPreset] 현재값 캡처 완료");
                    return;
                }
            }
            Debug.LogError("[PawBeamPreset] 이 에셋을 참조하는 PawBeam 없음");
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
