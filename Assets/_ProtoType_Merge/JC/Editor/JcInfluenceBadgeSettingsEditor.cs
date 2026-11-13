using UnityEditor;
using UnityEngine;

// JC | 2026-11-13 | 플레이 중에도 초안을 편집하며 명시적 저장만 모든 전투의 공용 프로필에 반영합니다.
[CustomEditor(typeof(JcInfluenceBadgeSettings))]
public sealed class JcInfluenceBadgeSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool enabled = GUI.enabled;
        try
        {
            GUI.enabled = true;
            var control = (JcInfluenceBadgeSettings)target;
            _ = control.Draft;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (changed) { control.LoadProfile(); serializedObject.Update(); }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("draft"), new GUIContent("IP 칭호별 색상"), true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("현재 씬에 즉시 미리보기합니다. 플레이 종료 전에 저장하면 본게임·튜토리얼·테스트씬이 같은 색상을 사용합니다. 배지 크기와 기존 광택 연출은 유지합니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!control.profile))
                if (GUILayout.Button("현재 색상을 공용 프로필에 저장"))
                {
                    Undo.RecordObject(control.profile, "JC IP 칭호 색상");
                    control.profile.settings = control.Draft.Copy();
                    EditorUtility.SetDirty(control.profile); AssetDatabase.SaveAssetIfDirty(control.profile);
                }
            if (GUILayout.Button("저장한 색상 다시 불러오기")) { Undo.RecordObject(control, "JC IP 색상 불러오기"); control.LoadProfile(); }
        }
        finally { GUI.enabled = enabled; }
    }
}
