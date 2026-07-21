using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    [CustomEditor(typeof(HealGroundShinePreset))]
    public class HealGroundShinePresetEditor : Editor
    {
        const string DIR = "Assets/_ProtoType_Merge/JC/__Testbed_asset/VFX/HealSkill";
        static string OrbitPrefab => DIR + "/HealOrbit.prefab";   // GroundShine이 이 프리팹의 자식

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
            EditorGUILayout.HelpBox("적용: 이 값을 HealOrbit 프리팹의 GroundShine(HealGroundShine)에 반영.\nlivePreview가 켜져 있으면 플레이 중에도 즉시 반영됩니다.", MessageType.Info);
        }

        static HealGroundShine FindTarget(GameObject root) => root.GetComponentInChildren<HealGroundShine>(true);

        static readonly string[] Fields =
        {
            "discRadius","rayRadius","groundOffsetY","intensity","opacity",
            "edgeSoft","sideSoft","centerFalloff",
            "rayDensity","raySharp","rayRotSpeed","valleyWidth",
            "spreadGrow",
        };

        void Apply(HealGroundShinePreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(OrbitPrefab);
            var gs = FindTarget(root);
            if (gs == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[HealGroundShinePreset] GroundShine(HealGroundShine) 없음"); return; }
            var so = new SerializedObject(gs);
            var pso = new SerializedObject(p);
            foreach (var f in Fields) CopyFloat(pso, so, f);
            so.FindProperty("color").colorValue = p.color;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, OrbitPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[HealGroundShinePreset] 프리팹에 적용 완료");
        }

        void Capture(HealGroundShinePreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitPrefab);
            var gs = FindTarget(root);
            if (gs == null) { Debug.LogError("[HealGroundShinePreset] GroundShine(HealGroundShine) 없음"); return; }
            Undo.RecordObject(p, "Capture Heal Ground Shine");
            var so = new SerializedObject(gs);
            var pso = new SerializedObject(p);
            foreach (var f in Fields) CopyFloat(so, pso, f);
            pso.FindProperty("color").colorValue = so.FindProperty("color").colorValue;
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(p);
            Debug.Log("[HealGroundShinePreset] 현재값 캡처 완료");
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
