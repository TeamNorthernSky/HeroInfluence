using UnityEditor;
using UnityEngine;

// JC | 2026-11-10 | 플레이 중에도 초안을 편집하고 저장 버튼에서만 원본을 갱신합니다.
[CustomEditor(typeof(JcAssociationIndicatorController))]
public sealed class JcAssociationIndicatorControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var controller = (JcAssociationIndicatorController)target;
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("저장 프로필", controller.Source, typeof(JcAssociationIndicatorProfile), false);
        EditorGUILayout.HelpBox("메인 탐사씬 협회 방문 표시 전용입니다. 최대 배율 1은 기존 크기입니다. 상승량·진폭은 기존 표시 높이의 비율이며 월드 위쪽으로 움직입니다. 플레이 종료 전 ‘현재 값 저장’을 눌러 보존하세요.", MessageType.Info);
        if (!controller.Draft) return;
        // JC | 2026-11-10 | Controller가 NotEditable을 제거한 초안을 사용합니다. 직접 그리는 것만으로 편집 금지는 해제되지 않습니다.
        using (var serialized = new SerializedObject(controller.Draft))
        {
            serialized.Update();
            var property = serialized.GetIterator();
            bool children = true;
            while (property.NextVisible(children))
            {
                children = false;
                if (property.name != "m_Script") EditorGUILayout.PropertyField(property, true);
            }
            if (serialized.ApplyModifiedProperties()) controller.Draft.Sanitize();
        }
        if (GUILayout.Button("현재 값 저장"))
        {
            Undo.RecordObject(controller.Source, "협회 인디케이터 값 저장");
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(controller.Draft), controller.Source);
            // JC | 2026-11-10 | 복사 시 임시 오브젝트 이름이 원본 프로필 이름을 덮지 않도록 자산 파일명을 유지합니다.
            controller.Source.name = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(controller.Source));
            EditorUtility.SetDirty(controller.Source);
            AssetDatabase.SaveAssetIfDirty(controller.Source);
        }
        if (GUILayout.Button("저장값 다시 적용")) controller.ReloadProfile();
    }
}
