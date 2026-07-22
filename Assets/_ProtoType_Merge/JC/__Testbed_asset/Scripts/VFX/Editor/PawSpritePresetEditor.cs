using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(PawSpritePreset))]
    public class PawSpritePresetEditor : Editor
    {
        static readonly string[] PrefabPaths =
        {
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/PawForYou/PawForYou.prefab",
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/PawForYou/PawForYouMistake.prefab",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (PawSpritePreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 에셋을 preset으로 참조하는 Paw(PawSprite)에 반영.\n형태(SDF) 항목은 프리셋+livePreview로만 구동됩니다(컴포넌트에 동명 필드 없음).", MessageType.Info);
        }

        static readonly string[] Floats = { "size", "bobAmp", "bobFreq", "fillOpacity", "rimIntensity", "glowIntensity", "padIntensity" };
        static readonly string[] Colors = { "fillColor", "rimColor", "glowColor", "padColor", "warpFlashColor" };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("preset")?.objectReferenceValue == presetAsset;

        void Apply(PawSpritePreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                bool dirty = false;
                foreach (var ps in root.GetComponentsInChildren<PawSprite>(true))
                {
                    if (!UsesPreset(ps, p)) continue;
                    var so = new SerializedObject(ps);
                    var pso = new SerializedObject(p);
                    foreach (var f in Floats) CopyFloat(pso, so, f);
                    foreach (var c in Colors) so.FindProperty(c).colorValue = pso.FindProperty(c).colorValue;
                    so.FindProperty("billboardYOnly").boolValue = p.billboardYOnly;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    applied++;
                }
                if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            if (applied == 0) Debug.LogError("[PawSpritePreset] 이 에셋을 참조하는 PawSprite 없음");
            else Debug.Log($"[PawSpritePreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(PawSpritePreset p)
        {
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var ps in root.GetComponentsInChildren<PawSprite>(true))
                {
                    if (!UsesPreset(ps, p)) continue;
                    Undo.RecordObject(p, "Capture Paw Sprite");
                    var so = new SerializedObject(ps);
                    var pso = new SerializedObject(p);
                    foreach (var f in Floats) CopyFloat(so, pso, f);
                    foreach (var c in Colors) pso.FindProperty(c).colorValue = so.FindProperty(c).colorValue;
                    pso.FindProperty("billboardYOnly").boolValue = so.FindProperty("billboardYOnly").boolValue;
                    pso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(p);
                    Debug.Log("[PawSpritePreset] 현재값 캡처 완료");
                    return;
                }
            }
            Debug.LogError("[PawSpritePreset] 이 에셋을 참조하는 PawSprite 없음");
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
