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
    private bool _foldLegacy;

    private void OnEnable()
    {
        var data = target as SkillPresentationData;
        bool phaseCue = data != null && data.PresentationSchemaVersion >= 1;
        _foldNew = phaseCue;
        _foldLegacy = !phaseCue;
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
                _foldLegacy = false;
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
            EditorGUI.indentLevel--;
        }

        // ── 옛 방식 (Legacy, Schema=0) ──
        EditorGUILayout.Space();
        _foldLegacy = EditorGUILayout.Foldout(_foldLegacy, "옛 방식 — Legacy (Schema=0, director 경로)", true);
        if (_foldLegacy)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AnimationTriggerOverride"));

            EditorGUILayout.LabelField("Attack Effect", EditorStyles.miniBoldLabel);
            SerializedProperty enAtk = serializedObject.FindProperty("EnableAttackEffect");
            EditorGUILayout.PropertyField(enAtk);
            using (new EditorGUI.DisabledScope(enAtk != null && !enAtk.boolValue))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AttackEffectId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AttackEffectPrefab"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AttackEffectPositionOffset"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("AttackEffectRotationOffset"));
            }

            EditorGUILayout.LabelField("Attack Sound", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AttackSoundId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("AttackSfxClip"));

            EditorGUILayout.LabelField("Hit Effect/Sound (legacy prefab/clip)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("HitEffectPrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("HitEffectPositionOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("HitEffectRotationOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("HitSfxClip"));

            EditorGUILayout.LabelField("Projectile", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ProjectilePrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("FlightTime"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TrajectoryType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ArcHeight"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ScaleByCellSize"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
