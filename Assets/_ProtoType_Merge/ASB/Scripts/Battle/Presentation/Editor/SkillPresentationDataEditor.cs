using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillPresentationData를 프로젝트에서 직접 선택했을 때의 인스펙터를 옛/새 방식 접이식으로 표시.
/// 표시 전용(직렬화 무변경). 편집 전용 창(SkillPresentationEditorWindow)과 동일한 레이아웃.
/// </summary>
[CustomEditor(typeof(SkillPresentationData))]
public class SkillPresentationDataEditor : Editor
{
    private bool _foldNew = true;

    private void OnEnable()
    {
        var data = target as SkillPresentationData;
        _foldNew = data == null || data.PresentationSchemaVersion >= 1;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillIndex"));

        SerializedProperty schema = serializedObject.FindProperty("PresentationSchemaVersion");
        bool isPhaseCue = schema != null && schema.intValue >= 1;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presentation Schema", isPhaseCue ? "PhaseCue (1)" : "Legacy (0)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(isPhaseCue))
        {
            if (GUILayout.Button("Upgrade To Phase Cue (Schema = 1)") && schema != null)
            {
                schema.intValue = 1;
                isPhaseCue = true;
                _foldNew = true;
            }
        }
        EditorGUILayout.HelpBox(isPhaseCue
            ? "PhaseCue: 이펙트/사운드는 아래 Presentation Phases의 Cue로만. 빈 Cue = 의도적 무연출."
            : "Legacy: 기존 director 경로. Upgrade 시 Cue 경로로 전환.", MessageType.Info);

        // ── 공용 (전 스키마) ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation (공용)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("AnimationStateName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetAnimationTriggerOverride"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit Timing (공용)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("UseAnimEvent"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("HitDelay"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hit / 타겟 피격 (공용: 전 스키마)", EditorStyles.boldLabel);
        SerializedProperty enHit = serializedObject.FindProperty("EnableHitEffect");
        EditorGUILayout.PropertyField(enHit);
        using (new EditorGUI.DisabledScope(enHit != null && !enHit.boolValue))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("HitEffectId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("HitSoundId"));
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("SfxVolume"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Projectile Impact (공용)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ProjectileVisual"), true);

        // ── 신 방식 (Phase Cue, Schema=1) ──
        EditorGUILayout.Space();
        _foldNew = EditorGUILayout.Foldout(_foldNew, "신 방식 — Presentation Phases (Schema=1)", true);
        if (_foldNew)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("MovePrepare"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Move"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AttackPrepare"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Attack"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Return"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Post"), true);
        
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Moving Attack (진입 이동 + 공격)", EditorStyles.boldLabel);
                            EditorGUILayout.PropertyField(serializedObject.FindProperty("MovingAttack"), true);
                            serializedObject.ApplyModifiedProperties();
                            MovingAttackPathSceneTool.DrawInspectorControls(target as SkillPresentationData);
                            serializedObject.Update();
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
