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

        // 체크박스로 양방향 전환한다. 어느 쪽으로 가도 데이터는 지워지지 않는다 —
        // Legacy 슬롯과 Cue는 각각 다른 자리에 그대로 남고, '런타임이 어느 쪽을 읽는지'만 바뀐다.
        // (BeginChangeCheck로 감싸 값이 실제로 바뀔 때만 쓴다. 그냥 열어보기만 해도 dirty가 되면 자산 YAML이 흔들린다.)
        using (new EditorGUI.DisabledScope(schema == null))
        {
            EditorGUI.BeginChangeCheck();
            bool next = EditorGUILayout.ToggleLeft("Phase Cue 사용 (Schema = 1)", isPhaseCue);
            if (EditorGUI.EndChangeCheck() && schema != null)
            {
                schema.intValue = next ? 1 : 0;
                isPhaseCue = next;
                _foldNew = next;
            }
        }

        EditorGUILayout.HelpBox(isPhaseCue
            ? "PhaseCue: 이펙트/사운드는 아래 Presentation Phases의 Cue로만. 빈 Cue = 의도적 무연출.\n" +
              "체크를 해제하면 Legacy 슬롯을 다시 읽습니다(Cue 데이터는 지워지지 않습니다)."
            : "Legacy: 기존 director 경로(Attack/Hit Effect·Sound id). 체크하면 Cue 경로로 전환됩니다.\n" +
              "전환은 값을 옮기지 않습니다 — Cue를 채우기 전까지 이펙트·사운드가 재생되지 않습니다.",
            MessageType.Info);

        // ── Animation Rail (Path A — opt-in) ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation Rail (Path A)", EditorStyles.boldLabel);
        SerializedProperty rail = serializedObject.FindProperty("AnimationRail");
        EditorGUILayout.PropertyField(rail);
        if (rail != null && rail.enumValueIndex == (int)AnimationRail.Timeline)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillTimelines"), true);
            EditorGUILayout.HelpBox(
                "Timeline 레일(Path A): Character Key가 시전자 unitName(예: '블래스터')과 일치하는 Timeline을 재생합니다.\n" +
                "Character Key를 비워두면 모든 시전자에 적용됩니다(와일드카드) — 단일 캐릭터 파일럿이면 그냥 비워두세요.",
                MessageType.Info);
        }

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
