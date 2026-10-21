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
        bool isTimelineRail = rail != null && rail.enumValueIndex == (int)AnimationRail.Timeline;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("PresentationArchetype"));
        if (isTimelineRail)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ImpactTiming"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("SkillTimelines"), true);
            EditorGUILayout.HelpBox(
                "다중 대상: TargetCount별 바인딩을 선택합니다. 분리형은 Segments를 순서대로 채우세요. " +
                "Impact/Move 마커의 TargetSlot 0은 세그먼트 슬롯을 따릅니다.",
                MessageType.Info);

            // 근접 Timeline도 접근/복귀 이동은 Move/Return 페이즈 액션이 담당한다(아래 Phases는 비활성 표시라 키만 따로 노출).
            DrawOptionalProperty("Move.SfxKey", "Move Sfx Key (접근 시작음)");
            DrawOptionalProperty("Return.SfxKey", "Return Sfx Key (복귀 시작음)");
        }

        // ── 공용 (전 스키마) ──
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation (공용)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(isTimelineRail))
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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("HitEffectAtTargetRoot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("HitEffectOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ScaleHitEffectWithBattleSpeed"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Projectile Impact (공용)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ProjectileVisual"), true);

        EditorGUILayout.Space();
        DrawEventSfxSection(serializedObject);

        // ── 신 방식 (Phase Cue, Schema=1) ──
        EditorGUILayout.Space();
        _foldNew = EditorGUILayout.Foldout(_foldNew,
            isTimelineRail ? "Legacy Migration Data — Presentation Phases" : "Presentation Phases (Animator Rail)", true);
        if (_foldNew)
        {
            using (new EditorGUI.DisabledScope(isTimelineRail))
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

            DrawLegacyHitSound(serializedObject);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Moving Attack 경로", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("MovingAttack"), true);
        serializedObject.ApplyModifiedProperties();
        MovingAttackPathSceneTool.DrawInspectorControls(target as SkillPresentationData);
        serializedObject.Update();

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 코드 시점 효과음(Event SFX). Hit/Impact 섹션 끝에 둔다 — 위의 명중 시점·임팩트 전달 설정이 이 소리들의 시점을 정한다.
    /// 편집 창(SkillPresentationEditorWindow)도 같은 함수를 쓴다.
    /// </summary>
    internal static void DrawEventSfxSection(SerializedObject so)
    {
        EditorGUILayout.LabelField("명중 사운드 (Event SFX)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("EventSfx"), true);
        EditorGUILayout.HelpBox(
            "Hit = 피해 확정(대상별, 같은 시전자의 동시 피격은 1회)\n" +
            "AdditionalHit = 추가 대상 피해(체인 연쇄·스플래시 등). 줄이 없으면 Hit 사용\n" +
            "EffectImpact = VFX 착탄 순간. Occurrence로 착탄 순번 지정(-1 = 매 착탄)\n" +
            "Cast = 시전 시점(캐스트당 1회). Timeline이 없는 스킬 전용\n" +
            "시전·발사·휘두름 소리는 Timeline SFX 마커에 넣으세요. 같은 순간을 양쪽에 넣으면 두 번 울립니다.",
            MessageType.Info);
    }

    /// <summary>재생되지 않는 레거시 Hit 사운드 슬롯. 직렬화 호환을 위해 필드는 남기고 Legacy 접이식 안에 비활성 표시한다.</summary>
    internal static void DrawLegacyHitSound(SerializedObject so)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("레거시 Hit 사운드 (현재 재생되지 않음 — Event SFX 사용)", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(so.FindProperty("HitSoundId"));
            EditorGUILayout.PropertyField(so.FindProperty("SfxVolume"));
        }
    }

    private void DrawOptionalProperty(string path, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(path);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label));
    }
}
