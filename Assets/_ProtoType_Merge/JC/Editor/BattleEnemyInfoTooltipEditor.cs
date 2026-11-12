using UnityEditor;
using UnityEngine;

// JC | 2026-11-12 | 기존 정보창 인스펙터에 스타일 비교와 명시적 공용 프로필 저장을 제공합니다.
[CustomEditor(typeof(BattleEnemyInfoTooltip))]
public sealed class BattleEnemyInfoTooltipEditor : Editor
{
    private void OnEnable()
    {
        // 재선택으로 플레이 중 미리보기 값을 덮지 않습니다. 편집 모드에서는 저장값을 표시합니다.
        if (!Application.isPlaying) ((BattleEnemyInfoTooltip)target).LoadArrowStyle();
    }

    public override void OnInspectorGUI()
    {
        var controller = (BattleEnemyInfoTooltip)target;
        // 실행 중 읽기 전용으로 유입된 GUI 상태도 이 조절 구간에서는 해제하고, 끝나면 원복합니다.
        bool previousEnabled = GUI.enabled;
        GUI.enabled = true;
        try
        {
            EditorGUILayout.LabelField("턴 슬롯 화살표 스타일", EditorStyles.boldLabel);
            bool value = EditorGUILayout.Toggle(new GUIContent("이전 입체 스타일 사용",
                "켜기: 이전 색상·하이라이트·그림자. 끄기: 현재 평면·점멸 테두리. 현재 크기와 왕복 이동은 유지합니다. 플레이 중 즉시 반영되며 저장 전에는 임시값입니다."), controller.UseLegacyArrowStyle);
            if (value != controller.UseLegacyArrowStyle)
            {
                Undo.RecordObject(controller, "화살표 스타일 미리보기");
                controller.UseLegacyArrowStyle = value;
            }
            var profile = controller.ArrowStyleProfile;
            EditorGUILayout.HelpBox("플레이 중 비교한 값은 종료 전에 저장하세요. 본게임·튜토리얼·테스트씬이 같은 프로필을 사용합니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(profile == null))
            {
                if (GUILayout.Button(new GUIContent("현재 스타일을 공용 프로필에 저장", "현재 토글 값을 공용 프로필에 저장합니다. 다음 전투부터 모든 씬에 적용되며 씬 자체는 저장하지 않습니다.")))
                {
                    Undo.RecordObject(profile, "화살표 스타일 저장");
                    profile.useLegacyStyle = controller.UseLegacyArrowStyle;
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssetIfDirty(profile);
                }
                if (GUILayout.Button(new GUIContent("저장값 다시 적용", "현재 임시 조절값을 버리고 공용 프로필에 저장된 스타일을 다시 적용합니다.")))
                {
                    Undo.RecordObject(controller, "화살표 저장값 적용");
                    controller.LoadArrowStyle();
                }
            }
            if (profile == null) EditorGUILayout.HelpBox("공용 화살표 스타일 프로필을 찾지 못했습니다.", MessageType.Error);
        }
        finally { GUI.enabled = previousEnabled; }
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
