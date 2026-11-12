using UnityEditor;
using UnityEngine;

namespace JC.LoadingPreview.Editor
{
    // JC | 2026-11-12 | 플레이 진입 없이 정지 프레임·동시 재생을 볼 수 있는 비교 전용 조작입니다. 게임 로딩 설정을 저장하지 않습니다.
    [CustomEditor(typeof(JcLoadingGlyphComparison))]
    public sealed class JcLoadingGlyphComparisonEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("원본 / 수정안이 같은 시간으로 움직입니다. Game 뷰에서 확인하세요. 비교 씬 전용이며 실제 로딩 효과는 유지됩니다.",MessageType.Info);
            var player=(JcLoadingGlyphComparison)target;
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("재생"))player.SetPlaying(true);
            if(GUILayout.Button("정지"))player.SetPlaying(false);
            if(GUILayout.Button("처음부터"))player.Restart();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("speed"),new GUIContent("비교 배속"));
            var time=serializedObject.FindProperty("previewTime");
            EditorGUI.BeginChangeCheck();float value=EditorGUILayout.Slider("비교 시간",time.floatValue,0,6.6f);
            if(EditorGUI.EndChangeCheck()){player.SetPlaying(false);player.Sample(value);}
            serializedObject.ApplyModifiedProperties();
        }
        public override bool RequiresConstantRepaint()=>true;
    }
}
