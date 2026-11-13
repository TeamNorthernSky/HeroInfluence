using UnityEditor;
using UnityEngine;

// JC | 2026-11-04 | 사용 가능한 스킬의 스윕과 선택된 스킬의 테두리를 서로 독립적으로 조절합니다.
[CustomEditor(typeof(BattleSkillSweepSettings))]
public sealed class BattleSkillSweepSettingsEditor : Editor
{
    private void Field(string name, string label)
    {
        var p = serializedObject.FindProperty(name);
        EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip));
    }

    public override void OnInspectorGUI()
    {
        // JC | 2026-11-12 | 기존 공통 설정 위치에서 모션 초안과 명시적인 저장·복원을 제공합니다.
        DrawMotionControls((BattleSkillSweepSettings)target);
        serializedObject.Update();
        if (!((BattleSkillSweepSettings)target).IsSkillButtonStyle)
        {
            DrawPropertiesExcluding(serializedObject, "m_Script", "selectedInset", "insetDepth");
            serializedObject.ApplyModifiedProperties();
            return;
        }

        Field("showAvailableEffect", "사용 가능 스윕 표시");
        if (serializedObject.FindProperty("showAvailableEffect").boolValue)
        {
            Field("gleamTexture", "빛띠 텍스처");
            Field("sweepColor", "빛띠 색");
            Field("sweepIntensity", "밝기");
            Field("gleamTilt", "기울기");
            Field("gleamScale", "이동 범위");
            Field("gleamWidth", "폭");
            Field("gleamSoftness", "경계 부드러움");
            Field("sweepDelay", "시작 지연");
            Field("sweepDuration", "이동 시간");
            Field("sweepCycle", "반복 주기");
            Field("bloomIntensity", "블룸 강도");
            if (serializedObject.FindProperty("bloomIntensity").floatValue > 0f)
            {
                Field("bloomColor", "블룸 색");
                Field("bloomTint", "색 혼합");
                Field("bloomThreshold", "밝기 기준");
                Field("bloomRadius", "반경");
                Field("bloomSoftKnee", "밝기 압축");
            }
        }
        Field("selectedColor", "테두리 색");
        Field("selectedWidth", "두께");
        Field("selectedIntensity", "밝기");
        var orbit = serializedObject.FindProperty("selectedOrbit");
        orbit.boolValue = EditorGUILayout.Toggle("테두리 순환", orbit.boolValue);
        if (orbit.boolValue)
        {
            Field("orbitPeriod", "한 바퀴 시간");
            Field("orbitTrail", "꼬리 길이");
        }
        EditorGUILayout.Space();
        Field("selectedInset", "선택 시 음각");
        if (serializedObject.FindProperty("selectedInset").boolValue)
            Field("insetDepth", "음각 깊이");
        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawMotionControls(BattleSkillSweepSettings settings)
    {
        if (!settings.SupportsMotionControls) return;
        EditorGUILayout.LabelField("스킬 버튼 너비 · 확대 · 피봇", EditorStyles.boldLabel);
        var profile = settings.MotionProfile;
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("6개 씬 공유 프로필", profile, typeof(BattleSkillButtonMotionProfile), false);
        if (profile == null)
        {
            EditorGUILayout.HelpBox("JC_BattleSkillButtonMotion 프로필을 찾을 수 없습니다. Resources의 연결을 확인하세요.", MessageType.Error);
            return;
        }
        settings.TryGetMotionDraft(out float scale, out float pivotX, out float pivotY, out float widthRatio);
        EditorGUI.BeginChangeCheck();
        // JC | 2026-11-12 | 비확대 상태의 왼쪽 경계를 고정하는 기본 너비 슬라이더입니다. 기존 확대 배율과 독립적입니다.
        widthRatio = EditorGUILayout.Slider(new GUIContent("기본 너비 비율", "1=현재 너비(최대), 0.6=60%. 기본 버튼의 왼쪽 경계를 고정하고 오른쪽만 줄입니다. 아이콘·글자의 가로 배율은 바꾸지 않습니다."), widthRatio, .6f, 1f);
        scale = EditorGUILayout.Slider(new GUIContent("호버·선택 확대 배율", "기본 크기에 곱할 배율입니다. 1은 확대 없음, 1.04는 4% 확대입니다. 호버와 선택 유지에 함께 적용합니다."), scale, 1f, 1.12f);
        // JC | 2026-11-12 | 좌우 피봇도 기본 정렬을 유지한 채 조절하고 기존 저장 버튼으로 함께 보존합니다.
        pivotX = EditorGUILayout.Slider(new GUIContent("피봇 좌우 위치", "0=왼쪽, 0.5=중앙, 1=오른쪽. 기본 버튼의 중심을 유지하고 확대 기준점만 바꿉니다."), pivotX, 0f, 1f);
        pivotY = EditorGUILayout.Slider(new GUIContent("피봇 상하 위치", "0=아래, 0.5=중앙, 1=위. 기본 버튼의 중심과 간격은 유지하고 확대 기준점만 바꿉니다."), pivotY, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) settings.SetMotionDraft(scale, pivotX, pivotY, widthRatio);
        EditorGUILayout.HelpBox("플레이 중 즉시 반영됩니다. 종료 전에 ‘현재 값 저장’을 눌러야 보존됩니다. 저장값은 일반·튜토리얼·테스트 4종에 공통 적용됩니다. 비플레이 중에는 저장값을 다음 실행부터 적용합니다.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("현재 값 저장")) SaveMotionProfile(settings);
            if (GUILayout.Button("저장값 다시 적용")) settings.ReloadMotionDraft();
        }
        EditorGUILayout.Space();
    }

    // JC | 2026-11-12 | 현재 모션 초안만 저장하며 씬 저장이나 기존 선택 테두리·스윕 설정 저장을 동반하지 않습니다.
    public static void SaveMotionProfile(BattleSkillSweepSettings settings)
    {
        var profile = settings.MotionProfile;
        if (profile == null || !settings.TryGetMotionDraft(out float scale, out float pivotX, out float pivotY, out float widthRatio)) return;
        Undo.RecordObject(profile, "스킬 버튼 확대·피봇 프로필 저장");
        profile.hoverScale = scale;
        profile.pivotX = pivotX;
        profile.pivotY = pivotY;
        profile.widthRatio = widthRatio;
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
    }
}

