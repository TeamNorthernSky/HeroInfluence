using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(LFLMasterPreset))]
    public class LFLMasterPresetEditor : Editor
    {
        const string LFLPrefab = "Assets/RenderFX/HeroSkill/Nekoming/LetsFightingLove/Prefabs/LetsFightingLove.prefab";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (LFLMasterPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 LetsFightingLove 프리팹의 오케스트레이터에 반영.\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        static readonly string[] Floats =
        {
            "handChargeTime","convergeTime","flashSizeMul","launchDelay","hitYOffset",
            "chainDelay","chainSpawnYOffset","chainStagger","cellSize","axisTol","maxChainTargets",
        };

        void Apply(LFLMasterPreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(LFLPrefab);
            var fx = root.GetComponentInChildren<LetsFightingLoveVfx>(true);
            if (fx == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[LFLMasterPreset] LetsFightingLoveVfx 없음"); return; }
            var so = new SerializedObject(fx);
            var pso = new SerializedObject(p);
            foreach (var f in Floats) CopyFloatLike(pso, so, f);
            so.FindProperty("mergeLocalOffset").vector3Value = p.mergeLocalOffset;
            so.FindProperty("mergeFlashColor").colorValue = p.mergeFlashColor;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, LFLPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[LFLMasterPreset] 프리팹에 적용 완료");
        }

        void Capture(LFLMasterPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(LFLPrefab);
            var fx = root ? root.GetComponentInChildren<LetsFightingLoveVfx>(true) : null;
            if (fx == null) { Debug.LogError("[LFLMasterPreset] LetsFightingLoveVfx 없음"); return; }
            Undo.RecordObject(p, "Capture LFL Master");
            var so = new SerializedObject(fx);
            var pso = new SerializedObject(p);
            foreach (var f in Floats) CopyFloatLike(so, pso, f);
            pso.FindProperty("mergeLocalOffset").vector3Value = so.FindProperty("mergeLocalOffset").vector3Value;
            pso.FindProperty("mergeFlashColor").colorValue = so.FindProperty("mergeFlashColor").colorValue;
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(p);
            Debug.Log("[LFLMasterPreset] 현재값 캡처 완료");
        }

        // int/float 겸용 복사(같은 이름 프로퍼티)
        static void CopyFloatLike(SerializedObject from, SerializedObject to, string name)
        {
            var pf = from.FindProperty(name);
            var pt = to.FindProperty(name);
            if (pf == null || pt == null) return;
            if (pf.propertyType == SerializedPropertyType.Integer) pt.intValue = pf.intValue;
            else pt.floatValue = pf.floatValue;
        }
    }
}
