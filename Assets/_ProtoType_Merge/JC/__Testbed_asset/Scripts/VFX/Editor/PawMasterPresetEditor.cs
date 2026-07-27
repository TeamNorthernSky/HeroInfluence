using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(PawMasterPreset))]
    public class PawMasterPresetEditor : Editor
    {
        static readonly string[] PrefabPaths =
        {
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/N_PawForYou/PawForYou.prefab",
            "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Skill/N_PawForYou/PawForYouMistake.prefab",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (PawMasterPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 PawForYou 프리팹의 PawForYouVfx(전체 타이밍/배치)에 반영.\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        static readonly string[] Fields =
        {
            "appearTime","holdTime","warpOutTime","warpTravelTime","warpInTime",
            "beamExtendTime","beamSustainTime","fadeOutTime",
            "popOvershoot","targetHeadOffset","beamEndOffsetY","pawBeamGap","flashSizeMul",
        };

        static bool UsesPreset(Component c, Object presetAsset)
            => new SerializedObject(c).FindProperty("masterPreset")?.objectReferenceValue == presetAsset;

        void Apply(PawMasterPreset p)
        {
            int applied = 0;
            foreach (var path in PrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                var fx = root.GetComponentInChildren<PawForYouVfx>(true);
                if (fx != null && UsesPreset(fx, p))
                {
                    var so = new SerializedObject(fx);
                    var pso = new SerializedObject(p);
                    foreach (var f in Fields) CopyFloat(pso, so, f);
                    so.FindProperty("casterOffset").vector3Value = p.casterOffset;
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
            foreach (var path in PrefabPaths)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var fx = root ? root.GetComponentInChildren<PawForYouVfx>(true) : null;
                if (fx == null || !UsesPreset(fx, p)) continue;
                Undo.RecordObject(p, "Capture Paw Master");
                var so = new SerializedObject(fx);
                var pso = new SerializedObject(p);
                foreach (var f in Fields) CopyFloat(so, pso, f);
                pso.FindProperty("casterOffset").vector3Value = so.FindProperty("casterOffset").vector3Value;
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
