using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(PawMasterPreset))]
    public class PawMasterPresetEditor : Editor
    {
        // ★분할 프리팹(PawSpawn/Warp/Beam ×B·G)이 실사용 — 통짜 2종만 있던 죽은 목록을 교정(260805).
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/PawForYou/Prefabs";
        static readonly string[] PrefabPaths =
        {
            DIR + "/PawSpawn.prefab",  DIR + "/PawSpawn_Gold.prefab",
            DIR + "/PawWarp.prefab",   DIR + "/PawWarp_Gold.prefab",
            DIR + "/PawBeam.prefab",   DIR + "/PawBeam_Gold.prefab",
            DIR + "/PawForYou.prefab", DIR + "/_Legacy/PawForYouMistake.prefab",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (PawMasterPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("▶ 프리팹에 적용","이 프리셋을 참조하는 대상 프리팹에 현재 값을 확정합니다."), GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button(new GUIContent("● 현재값 캡처","대상 프리팹의 값을 현재 프리셋으로 읽습니다. 프리셋 파일 저장은 별도입니다."), GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 PawForYou 프리팹의 PawForYouVfx(전체 타이밍/배치)에 반영.\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        // ★위치(casterOffset/targetHeadOffset/beamEndOffsetY)는 P1/P2 부품 프리셋 소유로 이관(260805).
        static readonly string[] Fields =
        {
            "appearTime","holdTime","warpOutTime","warpTravelTime","warpInTime",
            "beamExtendTime","beamSustainTime","fadeOutTime",
            "popOvershoot","pawBeamGap","flashSizeMul",
        };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("masterPreset")?.objectReferenceValue == presetAsset;

        void Apply(PawMasterPreset p)
        {
            int applied = 0;
            foreach (var path in JcPresetPartTargets.Paths(p, PrefabPaths))
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                var fx = root.GetComponentInChildren<PawForYouVfx>(true);
                if (fx != null && UsesPreset(fx, p))
                {
                    var so = new SerializedObject(fx);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloat(pso, so, f);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    applied++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            if (applied == 0) Debug.LogError("[PawMasterPreset] 이 에셋을 참조하는 PawForYouVfx 없음");
            else Debug.Log($"[PawMasterPreset] 프리팹에 적용 완료 ({applied}곳)");
        }

        void Capture(PawMasterPreset p)
        {
            foreach (var path in JcPresetPartTargets.Paths(p, PrefabPaths))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var fx = root ? root.GetComponentInChildren<PawForYouVfx>(true) : null;
                if (fx == null || !UsesPreset(fx, p)) continue;
                Undo.RecordObject(p, "Capture Paw Master");
                var so = new SerializedObject(fx);
                var pso = new SerializedObject(p);
                foreach (var f in Fields) CopyFloat(so, pso, f);
                pso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(p);
                Debug.Log("[PawMasterPreset] 현재값 캡처 완료");
                return;
            }
            Debug.LogError("[PawMasterPreset] 이 에셋을 참조하는 PawForYouVfx 없음");
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
