using UnityEditor;
using UnityEngine;

namespace JC.VFX
{
    [CustomEditor(typeof(LflMergePreset))]
    public sealed class LflMergePresetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(new GUIContent("▶ 프리팹에 적용","이 프리셋을 참조하는 합류 부품에 값을 확정합니다."),GUILayout.Height(30)))Apply((LflMergePreset)target);
                if(GUILayout.Button(new GUIContent("● 현재값 캡처","실제 합류 부품의 저장된 값을 이 프리셋으로 가져옵니다."),GUILayout.Height(30)))Capture((LflMergePreset)target);
                JcPresetEditorUtil.DrawSaveButton(target);
            }
        }
        public static void Apply(LflMergePreset preset)
        {
            foreach(var asset in JcPresetPartTargets.Roots(preset))
            {
                var path=AssetDatabase.GetAssetPath(asset);var root=PrefabUtility.LoadPrefabContents(path);
                try{foreach(var fx in root.GetComponentsInChildren<LflMergeVfx>(true))if(JcPresetPartTargets.References(fx,preset))fx.PullFromPreset();PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        public static void Capture(LflMergePreset preset)
        {
            foreach(var root in JcPresetPartTargets.Roots(preset))foreach(var fx in root.GetComponentsInChildren<LflMergeVfx>(true))
            {
                if(!JcPresetPartTargets.References(fx,preset))continue;
                Undo.RecordObject(preset,"합류 프리셋 캡처");var from=new SerializedObject(fx);var to=new SerializedObject(preset);
                foreach(var name in new[]{"convergeTime","holdTime","flashColor","flashSizeMul"})to.CopyFromSerializedProperty(from.FindProperty(name));
                to.ApplyModifiedProperties();return;
            }
        }
    }
}
