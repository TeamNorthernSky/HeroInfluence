using UnityEditor;
using UnityEngine;

// JC | 2026-11-04 | 플레이 초안과 저장 프로필을 분리하고 명시적 저장/복원만 수행합니다.
[CustomEditor(typeof(JcEventOcclusionController))]
public sealed class JcEventOcclusionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var c = (JcEventOcclusionController)target;
        serializedObject.Update();
        Field("profile", "저장 프로필"); Field("opacity", "가림 표시 불투명도");
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("건물에 가려진 NPC·이벤트 메시만 반투명하게 표시합니다. 건물 투명도는 바꾸지 않습니다. 플레이 조절값은 종료 전에 ‘현재 값 저장’으로 보존하세요. 일반·튜토리얼 탐사는 같은 프로필을 공유합니다.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!c.Profile))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("저장값 다시 적용", "현재 초안을 버리고 저장 프로필을 불러옵니다.")))
            { Undo.RecordObject(c, "이벤트 가림 저장값 적용"); c.LoadProfile(); EditorUtility.SetDirty(c); }
            if (GUILayout.Button(new GUIContent("현재 값 저장", "현재 불투명도를 프로필에 저장합니다. 씬과 원본 재질은 저장하지 않습니다.")))
            { Undo.RecordObject(c.Profile, "이벤트 가림 프로필 저장"); c.Profile.opacity = c.Opacity; EditorUtility.SetDirty(c.Profile); AssetDatabase.SaveAssetIfDirty(c.Profile); }
        }
        void Field(string name, string label) { var p = serializedObject.FindProperty(name); EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip)); }
    }
}
