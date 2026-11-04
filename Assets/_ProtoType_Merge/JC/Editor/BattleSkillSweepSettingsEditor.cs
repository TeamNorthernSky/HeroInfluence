using UnityEditor;
using UnityEngine;

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
}

