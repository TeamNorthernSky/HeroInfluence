using UnityEditor;
using UnityEngine;
namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-09 | 같은 저장 형식을 가정하지 않고 각 초안의 직렬화 필드를 직접 편집합니다.
    [CustomEditor(typeof(JcIndoorLightControl))]
    public sealed class JcIndoorLightControlEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var c=(JcIndoorLightControl)target; if(!c.Draft)return;
            using(new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("저장 프로필",c.Source,typeof(JcIndoorEnvironmentProfile),false);
            var so=new SerializedObject(c.Draft); so.Update(); var array=so.FindProperty("lights");
            if(c.Index>=array.arraySize)return;
            EditorGUILayout.PropertyField(array.GetArrayElementAtIndex(c.Index),new GUIContent("이 조명의 초안"),true);
            if(so.ApplyModifiedProperties())c.Apply();
            EditorGUILayout.HelpBox("전체 조명 목록과 같은 초안입니다. 저장은 JC_IndoorEnvironment의 조명 · 환경 항목에서 수행하세요. 원본 Light를 직접 편집하지 마세요.",MessageType.Info);
        }
    }
}
