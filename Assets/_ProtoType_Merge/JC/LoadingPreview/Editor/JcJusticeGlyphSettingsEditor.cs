using UnityEditor;
using UnityEngine;

namespace JC.LoadingPreview.Editor
{
    // JC | 2026-11-12 | 위치·배율을 즉시 비교하고 명시적으로 저장하는 전용 조절 Inspector입니다.
    [CustomEditor(typeof(JcJusticeGlyphSettings))]
    public sealed class JcJusticeGlyphSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (JcJusticeGlyphSettings)target;
            EditorGUILayout.HelpBox("저스티스 수정안만 변경합니다. 위치는 원형 중심 기준 X/Y입니다. 크기 1배는 현재 시안 기준이며, 원 밖 잘림 방지 때문에 확대가 제한될 수 있습니다.", MessageType.Info);
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("startCenter"), new GUIContent("시작 중심 위치"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("endCenter"), new GUIContent("종료 중심 위치"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("initialSize"), new GUIContent("초기 크기 배율"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("finalSize"), new GUIContent("최종 크기 배율"));
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed) settings.ApplyPreview();
            EditorGUILayout.HelpBox("플레이 종료 또는 씬을 닫기 전에 ‘현재 값 저장’을 누르세요. 저장하지 않은 초안은 다시 열 때 저장값으로 돌아갑니다. 이 비교 씬의 전용 프로필에만 저장됩니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!settings.Profile))
            {
                if (GUILayout.Button("현재 값 저장")) settings.SaveCurrent();
                if (GUILayout.Button("저장값 다시 적용")) settings.LoadSaved();
            }
        }
    }
}
