using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(PawSpritePreset))]
    public class PawSpritePresetEditor : Editor
    {
        // ★분할 프리팹(PawSpawn/Warp/Beam ×B·G)이 실사용 — 통짜 2종만 있던 죽은 목록을 교정(260805).
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/PawForYou/Prefabs";
        static readonly string[] PrefabPaths =
        {
            DIR + "/PawSpawn.prefab",  DIR + "/PawSpawn_Gold.prefab",
            DIR + "/PawWarp.prefab",   DIR + "/PawWarp_Gold.prefab",
            DIR + "/PawBeam.prefab",   DIR + "/PawBeam_Gold.prefab",
            DIR + "/PawBeam_Miss.prefab", DIR + "/PawBeam_Gold_Miss.prefab",
            DIR + "/PawForYou.prefab", DIR + "/_Legacy/PawForYouMistake.prefab",
        };

        // 따름 잠금 대상 = 색·밝기를 제외한 전부(위치·크기·형태·움직임)
        static readonly string[] TransformProps =
        {
            "spawnSocketName","spawnOffset","headSocketName","headOffset",
            "size","canvasScale","billboardMode","anchorPin","anchorLocal","bobAmp","bobFreq",
            "toeCount","toeSpreadDeg","toeDist","toeRadius",
            "palmRadiusX","palmRadiusY","palmOffsetY","fusion",
            "padMainRadius","padMainSquash","padToeRadius","padToeDistMul",
            "rimWidth","glowRange","wobbleAmp","wobbleSpeed",
        };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.PawSprite.Fold", TransformProps);
            var p = (PawSpritePreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("▶ 프리팹에 적용","이 프리셋을 참조하는 대상 프리팹에 현재 값을 확정합니다."), GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button(new GUIContent("● 현재값 캡처","대상 프리팹의 값을 현재 프리셋으로 읽습니다. 프리셋 파일 저장은 별도입니다."), GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
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
            foreach (var path in JcPresetPartTargets.Paths(p, PrefabPaths))
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
                    so.FindProperty("billboardMode").enumValueIndex = (int)p.billboardMode;
                    so.FindProperty("anchorPin").boolValue = p.anchorPin;
                    so.FindProperty("anchorLocal").vector2Value = p.anchorLocal;
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
            foreach (var path in JcPresetPartTargets.Paths(p, PrefabPaths))
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
                    pso.FindProperty("billboardMode").enumValueIndex = so.FindProperty("billboardMode").enumValueIndex;
                    pso.FindProperty("anchorPin").boolValue = so.FindProperty("anchorPin").boolValue;
                    pso.FindProperty("anchorLocal").vector2Value = so.FindProperty("anchorLocal").vector2Value;
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
