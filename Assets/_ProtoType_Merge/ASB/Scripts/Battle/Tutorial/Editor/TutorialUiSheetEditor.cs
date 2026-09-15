using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TutorialUiSheet를 "한눈에 표"로 보여주는 커스텀 인스펙터.
/// 컬럼: order | key | purpose | when | view | message | highlight | requiredNote
/// </summary>
[CustomEditor(typeof(TutorialUiSheet))]
public sealed class TutorialUiSheetEditor : Editor
{
    private static readonly (string field, string header, float width)[] Columns =
    {
        ("displayOrder", "order", 42f),
        ("key", "key", 90f),
        ("purpose", "purpose", 90f),
        ("whenNote", "when", 100f),
        ("viewId", "view", 55f),
        ("message", "message", 170f),
        ("highlightActionId", "highlight", 80f),
        ("requiredActionNote", "requiredNote", 100f),
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("zoneId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("battleKey"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("UI 엔트리 (한눈에 보기)", EditorStyles.boldLabel);

        SerializedProperty entries = serializedObject.FindProperty("entries");

        // 헤더 행
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        for (int c = 0; c < Columns.Length; c++)
        {
            EditorGUILayout.LabelField(Columns[c].header, EditorStyles.miniBoldLabel, GUILayout.Width(Columns[c].width));
        }
        GUILayout.Space(24f); // 삭제 버튼 자리
        EditorGUILayout.EndHorizontal();

        // 데이터 행
        int removeIndex = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            for (int c = 0; c < Columns.Length; c++)
            {
                SerializedProperty field = e.FindPropertyRelative(Columns[c].field);
                if (Columns[c].field == "message")
                {
                    // message는 [TextArea]라 기본 PropertyField가 여러 줄로 커진다.
                    // 표에서는 다른 칸과 높이를 맞추기 위해 한 줄 텍스트필드로 표시(내용은 그대로 편집 가능).
                    field.stringValue = EditorGUILayout.TextField(field.stringValue, GUILayout.Width(Columns[c].width));
                }
                else
                {
                    EditorGUILayout.PropertyField(field, GUIContent.none, GUILayout.Width(Columns[c].width));
                }
            }
            if (GUILayout.Button("−", GUILayout.Width(22f)))
            {
                removeIndex = i;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
        {
            entries.DeleteArrayElementAtIndex(removeIndex);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("+ 엔트리 추가"))
        {
            entries.arraySize++;
        }

        serializedObject.ApplyModifiedProperties();

        // 검증 경고(공용 메서드 재사용)
        var issues = new List<string>();
        ((TutorialUiSheet)target).CollectValidationIssues(issues);
        if (issues.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Warning);
        }
    }
}
