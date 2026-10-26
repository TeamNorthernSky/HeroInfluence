using UnityEditor;
using UnityEngine;

// 씬 컴포넌트와 에셋 Inspector 모두 같은 프로필을 직접 수정합니다.
[CustomEditor(typeof(MoveRangeController))]
public sealed class JcMoveRangeControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var profile = ((MoveRangeController)target).GridProfile;
        if (profile == null)
        {
            EditorGUILayout.HelpBox("공통 이동범위 그리드 프로필이 없습니다.", MessageType.Error);
            return;
        }
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField(new GUIContent("공통 그리드 프로필", "탐사와 튜토리얼이 항상 함께 사용하는 프로필입니다. 씬별로 교체하지 않습니다."), profile, typeof(JcMoveRangeGridProfile), false);
        JcMoveRangeGridProfileEditor.DrawSettings(new SerializedObject(profile));
    }
}

[CustomEditor(typeof(JcMoveRangeGridProfile))]
public sealed class JcMoveRangeGridProfileEditor : Editor
{
    public override void OnInspectorGUI() => DrawSettings(serializedObject);

    internal static void DrawSettings(SerializedObject data)
    {
        EditorGUILayout.HelpBox("탐사·튜토리얼 공통 설정입니다. 변경하면 플레이 화면에 즉시 반영되고 프로필에 자동 저장되어 플레이 종료 후에도 유지됩니다. Ctrl+Z로 되돌릴 수 있습니다.", MessageType.Info);
        data.Update();
        var property = data.GetIterator();
        bool children = true;
        while (property.NextVisible(children))
        {
            children = false;
            if (property.propertyPath == "m_Script") continue;
            EditorGUILayout.PropertyField(property, new GUIContent(property.displayName, property.tooltip), true);
        }
        if (data.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(data.targetObject);
            AssetDatabase.SaveAssetIfDirty(data.targetObject);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button(new GUIContent("프로필 저장", "현재 공통 프로필만 디스크에 저장합니다. Undo로 되돌린 값도 저장할 수 있습니다. 씬과 다른 에셋은 저장하지 않습니다.")))
            AssetDatabase.SaveAssetIfDirty(data.targetObject);
    }
}
