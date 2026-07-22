using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(TaoMasterPreset))]
    public class TaoMasterPresetEditor : Editor
    {
        const string TaoPrefab = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/Taosenaiyo/Taosenaiyo.prefab";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (TaoMasterPreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 Taosenaiyo 프리팹의 오케스트레이터에 반영.\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        static readonly string[] Floats = { "launchDelay", "hitYOffset", "flashSizeMul", "fadeInTime", "sustainTime", "fadeOutTime" };
        static readonly string[] Colors = { "handFlashColor", "impactFlashColor" };

        void Apply(TaoMasterPreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(TaoPrefab);
            var fx = root.GetComponentInChildren<TaosenaiyoVfx>(true);
            if (fx == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[TaoMasterPreset] TaosenaiyoVfx 없음"); return; }
            var so = new SerializedObject(fx);
            var pso = new SerializedObject(p);
            foreach (var f in Floats) CopyFloat(pso, so, f);
            foreach (var c in Colors) so.FindProperty(c).colorValue = pso.FindProperty(c).colorValue;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, TaoPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[TaoMasterPreset] 프리팹에 적용 완료");
        }

        void Capture(TaoMasterPreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(TaoPrefab);
            var fx = root ? root.GetComponentInChildren<TaosenaiyoVfx>(true) : null;
            if (fx == null) { Debug.LogError("[TaoMasterPreset] TaosenaiyoVfx 없음"); return; }
            Undo.RecordObject(p, "Capture Tao Master");
            var so = new SerializedObject(fx);
            var pso = new SerializedObject(p);
            foreach (var f in Floats) CopyFloat(so, pso, f);
            foreach (var c in Colors) pso.FindProperty(c).colorValue = so.FindProperty(c).colorValue;
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(p);
            Debug.Log("[TaoMasterPreset] 현재값 캡처 완료");
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
