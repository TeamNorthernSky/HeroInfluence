using UnityEditor;
using UnityEngine;

// JC | 2026-11-13 | 실행 중 읽기 전용 GUI 상태를 이 조절기에서만 해제하며 저장 버튼 외에는 원본 프로필을 쓰지 않습니다.
[CustomEditor(typeof(JcBattleTurnSlotSettings))]
public sealed class JcBattleTurnSlotSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool before = GUI.enabled; GUI.enabled = true;
        try
        {
            var controls = (JcBattleTurnSlotSettings)target;
            _ = controls.Draft;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
            if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); controls.LoadProfile(); serializedObject.Update(); }
            var draft = serializedObject.FindProperty("draft");
            string[] fields = { "style", "innerDiameter", "outerDiameter", "centerOffset", "innerSeparatorWidth", "innerSeparatorColor", "innerSeparatorOpacity", "arrowOffset", "arrowScale", "arrowTravel", "arrowOutlineWidth", "arrowCornerRadius", "cycleSeconds", "showHealingTargets", "enemyColor", "enemyFlash", "healingColor", "healingFlash" };
            string[] labels = { "화살표 스타일", "원 내경 배율", "원 외경 배율", "원 중심 오프셋(px)", "안쪽 구분선 두께(px)", "안쪽 구분선 색상", "안쪽 구분선 불투명도", "화살표 추가 오프셋(px)", "화살표 크기 배율", "화살표 이동량(px)", "화살표 윤곽선 두께", "화살표 모서리 라운딩", "점멸·왕복 주기(초)", "회복 대상 턴슬롯 표시", "공격 기본 색", "공격 점멸 색", "회복 기본 색", "회복 점멸 색" };
            for (int i=0;i<fields.Length;i++) { var p=draft.FindPropertyRelative(fields[i]); EditorGUILayout.PropertyField(p,new GUIContent(labels[i],p.tooltip),true); }
            serializedObject.ApplyModifiedProperties(); controls.Draft.Validate();
            EditorGUILayout.HelpBox("플레이 중 즉시 반영되는 임시값입니다. 종료 전에 저장하면 본게임·튜토리얼·테스트씬이 공유합니다. 씬은 저장하지 않습니다.",MessageType.Info);
            using (new EditorGUI.DisabledScope(controls.profile == null))
            {
                if (GUILayout.Button(new GUIContent("현재 값을 공용 프로필에 저장", "배지·화살표·회복 표시 설정을 연결 프로필에 저장합니다.")))
                {
                    Undo.RecordObject(controls.profile,"턴 배지 프로필 저장");
                    controls.profile.settings = controls.Draft.Copy();
                    controls.profile.useLegacyStyle = controls.Draft.style == JcBattleArrowStyle.Legacy;
                    EditorUtility.SetDirty(controls.profile); AssetDatabase.SaveAssetIfDirty(controls.profile);
                }
                if (GUILayout.Button(new GUIContent("저장값 다시 적용", "저장하지 않은 임시 조절값을 버리고 연결 프로필을 다시 읽습니다.")))
                { Undo.RecordObject(controls,"턴 배지 초안 복원"); controls.LoadProfile(); }
            }
        }
        finally { GUI.enabled = before; }
    }
}

// JC | 2026-11-13 | 기존 CellBox에서 회복 상태를 포함한 전 항목을 즉시 조절하며 명시적 저장만 공유합니다.
[CustomEditor(typeof(BattleCellVisualSettings))]
public sealed class BattleCellVisualSettingsEditor : Editor
{
    private int state;
    private static readonly string[] States = { "현재 턴 · 노랑", "선택 가능 · 녹색", "확정 대상 · 빨강", "선택 불가 · 회색", "치료·부활 · 흰색" };
    public override void OnInspectorGUI()
    {
        bool before=GUI.enabled; GUI.enabled=true;
        try
        {
            var controls=(BattleCellVisualSettings)target;
            _ = controls.Draft;
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"));
            if(EditorGUI.EndChangeCheck()){serializedObject.ApplyModifiedProperties();controls.LoadProfile();serializedObject.Update();}
            state=EditorGUILayout.Popup(new GUIContent("편집 상태","해당 상태의 형태·색·채움을 조절합니다. 실제 타겟 선택 상태를 바꾸지는 않습니다."),state,States);
            BattleCellBorderWorkbenchEditor.DrawConfiguration(serializedObject.FindProperty("draft"),state);
            serializedObject.ApplyModifiedProperties();
            if(GUILayout.Button(new GUIContent("현재 형태를 모든 상태에 적용","현재 상태의 모양만 다른 상태에 복사합니다. 색상과 채움은 유지합니다.")))
            { Undo.RecordObject(controls,"셀 외형 통일");controls.Draft.UnifyShape((BattleCellVisualState)(state+1)); }
            EditorGUILayout.HelpBox("플레이 중 초안이 즉시 반영됩니다. 종료 전 저장 버튼을 누르면 기존 BattleCellBorderSettings와 모든 연결 전투씬이 같은 값을 사용합니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(controls.profile==null))
            {
                if(GUILayout.Button(new GUIContent("현재 값을 공용 프로필에 저장","형태·색·스윕·운동·대상 모델 윤곽선 토글을 기존 프로필에 저장합니다. 씬은 저장하지 않습니다.")))
                { Undo.RecordObject(controls.profile,"전투 셀 프로필 저장");controls.profile.settings=controls.Draft.Copy();EditorUtility.SetDirty(controls.profile);AssetDatabase.SaveAssetIfDirty(controls.profile); }
                if(GUILayout.Button(new GUIContent("저장값 다시 적용","임시 초안을 버리고 기존 프로필을 다시 불러옵니다.")))
                { Undo.RecordObject(controls,"셀 초안 복원");controls.LoadProfile(); }
            }
            EditorGUILayout.Space();
            // 기존 연결/상태별 불투명도도 계속 제공하며 프로필과 별개인 씬 값임을 명시합니다.
            EditorGUILayout.LabelField("씬 연결·표시 불투명도",EditorStyles.boldLabel);
            serializedObject.Update();
            foreach(var name in new[]{"currentTurnAlpha","selectableAlpha","confirmedAreaAlpha","unavailableAlpha","currentTurnPrefab","selectablePrefab","confirmedAreaPrefab","unavailablePrefab","cellRenderers"})
                EditorGUILayout.PropertyField(serializedObject.FindProperty(name),true);
            serializedObject.ApplyModifiedProperties();
        }
        finally { GUI.enabled=before; }
    }
}
