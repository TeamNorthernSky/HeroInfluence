using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation data mapped by SkillIndex through SkillPresentationCatalog.
/// Skill runtime values remain separate; this asset stores animation slot, effect, sound,
/// hit timing, and projectile presentation overrides.
/// </summary>
[CreateAssetMenu(fileName = "SkillPresentation_New", menuName = "Battle/Skill Presentation Data")]
public class SkillPresentationData : ScriptableObject
{
    [Header("Skill Binding")]
    [Tooltip("SkillData.skillIndex used by SkillPresentationEditorWindow and SkillPresentationCatalog.")]
    public int SkillIndex;

    [Header("Animation State/Slot Override (empty uses SkillData/fallback)")]
    [Tooltip("Animator state/slot used by this skill, such as ClassSkill_1 or WeaponSkill_1. Empty uses SkillData.StateName, then the legacy index fallback.")]
    public string AnimationStateName;
    [Tooltip("Legacy slot override. Prefer AnimationStateName for new data.")]
    public string AnimationTriggerOverride;
    [Tooltip("Empty uses SkillData.TargetAnimationTrigger.")]
    public string TargetAnimationTriggerOverride;

    public string ResolvedAnimationStateName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AnimationStateName))
            {
                return AnimationStateName.Trim();
            }

            return !string.IsNullOrWhiteSpace(AnimationTriggerOverride)
                ? AnimationTriggerOverride.Trim()
                : string.Empty;
        }
    }

    [Header("Sound (Registry id; 0 = none)")]
    [Tooltip("SoundRegistry id for the attack sound. 0 = none.")]
    public int AttackSoundId;
    [Tooltip("SoundRegistry id for the hit sound. 0 = none.")]
    public int HitSoundId;
    [Range(0f, 1f)] public float SfxVolume = 1f;

    [Header("Attack Effect (Registry id; 0 = none)")]
    [Tooltip("Whether to spawn the attack effect.")]
    public bool EnableAttackEffect = true;
    [Tooltip("EffectRegistry id spawned at the actor attack socket. 0 = none.")]
    public int AttackEffectId;

    [Header("Hit Effect (Registry id; 0 = none)")]
    [Tooltip("Whether to spawn the hit effect.")]
    public bool EnableHitEffect = true;
    [Tooltip("EffectRegistry id spawned at the target hit socket. 0 = none.")]
    public int HitEffectId;

    // --- Legacy direct-reference fields (마이그레이션용, 데이터 이전 후 제거 예정) ---
    // 신규 데이터는 위 *Id 필드를 사용하세요. 아래 필드는 레지스트리 조회 실패 시 폴백으로만 참조됩니다.
    [HideInInspector] public AudioClip AttackSfxClip;
    [HideInInspector] public AudioClip HitSfxClip;
    [HideInInspector] public GameObject AttackEffectPrefab;
    [HideInInspector] public Vector3 AttackEffectPositionOffset;
    [HideInInspector] public Vector3 AttackEffectRotationOffset;
    [HideInInspector] public GameObject HitEffectPrefab;
    [HideInInspector] public Vector3 HitEffectPositionOffset;
    [HideInInspector] public Vector3 HitEffectRotationOffset;

    [Header("Hit Timing")]
    [Tooltip("true: wait for AniEvent_OnHit. false: use HitDelay.")]
    public bool UseAnimEvent = false;
    [Tooltip("Hit delay in seconds when UseAnimEvent is false.")]
    public float HitDelay = 0.25f;

    [Header("Projectile")]
    [Tooltip("Empty uses the normal instant hit sequence.")]
    public GameObject ProjectilePrefab;
    [Tooltip("Projectile travel time in seconds before battle speed scaling.")]
    public float FlightTime = 0.3f;
    public ProjectileTrajectoryType TrajectoryType = ProjectileTrajectoryType.Straight;
    [Tooltip("Max height used when TrajectoryType is Arc.")]
    public float ArcHeight = 2f;
    [Tooltip("Scale projectile to grid cell size.")]
    public bool ScaleByCellSize;

    // ─────────────────────────────────────────────
    // Presentation Phases (연출 페이즈)
    // 각 페이즈의 Enabled로 스킵/사용을 데이터로 제어한다.
    // "무엇을 하나(데미지/힐)"는 SkillExecutionResult가 단일 기준이며, 여기서 중복 정의하지 않는다.
    // ─────────────────────────────────────────────
    [Header("Presentation Phases")]
    public MovePreparePhase MovePrepare = new MovePreparePhase();
    public MovePhase Move = new MovePhase();
    public AttackPreparePhase AttackPrepare = new AttackPreparePhase();
    public AttackPhase Attack = new AttackPhase();
    public ReturnPhase Return = new ReturnPhase();
    public PostPhase Post = new PostPhase();

    /// <summary>페이즈를 정의 순서대로 순회 (검증·에디터 등 크로스커팅용).</summary>
    public IEnumerable<PhaseBase> GetPhases()
    {
        yield return MovePrepare;
        yield return Move;
        yield return AttackPrepare;
        yield return Attack;
        yield return Return;
        yield return Post;
    }

    private void OnValidate()
    {
        // beat가 0개인 경우는 flat/산술 폴백으로 정상 동작하므로 경고하지 않는다(마이그레이션 중 노이즈 방지).
        // 명시적으로 추가된 beat가 완전히 비어 있을 때만 경고한다.
        if (Attack?.Beats == null)
        {
            return;
        }

        for (int i = 0; i < Attack.Beats.Count; i++)
        {
            AttackBeat b = Attack.Beats[i];
            if (b != null
                && string.IsNullOrWhiteSpace(b.AnimationStateName)
                && b.EffectId == 0 && b.SoundId == 0)
            {
                Debug.LogWarning(
                    $"[SkillPresentation] {name}: Attack.Beats[{i}]에 아무 데이터도 없습니다(상태/이펙트/사운드 모두 비어있음).",
                    this);
            }
        }
    }
}

public enum ProjectileTrajectoryType
{
    Straight,
    Arc
}

// ─────────────────────────────────────────────
// Phase / Beat 정의
// struct는 class를 상속할 수 없으므로 모두 class로 정의한다.
// SkillPresentationData에는 구체 타입 필드로만 두고, List<PhaseBase>/SerializeReference 다형 직렬화는 쓰지 않는다.
// ─────────────────────────────────────────────
[Serializable]
public class HitTimingSettings
{
    [Tooltip("true: AniEvent_OnHit 대기. false: HitDelay 사용.")]
    public bool UseAnimEvent = false;
    [Tooltip("UseAnimEvent가 false일 때 히트 지연(초).")]
    public float HitDelay = 0.25f;
}

[Serializable]
public class PhaseBase
{
    [Tooltip("이 페이즈를 실행할지 여부. false면 스킵.")]
    public bool Enabled = true;
}

[Serializable]
public class MovePreparePhase : PhaseBase { }

[Serializable]
public class MovePhase : PhaseBase
{
    [Tooltip("유닛 이동 프로필(UnitMovementProfile)로 접근/복귀. 실제 이동 여부는 런타임 조건과 AND된다.")]
    public bool UseUnitMovement = true;
}

[Serializable]
public class AttackPreparePhase : PhaseBase
{
    [Tooltip("윈드업 이펙트 EffectRegistry id. 0 = none.")]
    public int WindupEffectId;
}

[Serializable]
public class AttackBeat
{
    [Tooltip("이 타격에 사용할 Animator state/slot. 비면 폴백(SkillData.StateName → 산술).")]
    public string AnimationStateName;
    [Tooltip("EffectRegistry id. 0 = none.")]
    public int EffectId;
    [Tooltip("SoundRegistry id. 0 = none.")]
    public int SoundId;
    public HitTimingSettings HitTiming = new HitTimingSettings();
}

[Serializable]
public class AttackPhase : PhaseBase
{
    [Tooltip("연출 타격 단위. 실제 데미지/힐 적용 횟수·시점은 SkillExecutionResult(DamageContext/onHitCallback)를 단일 기준으로 한다. beat 수만큼 데미지를 중복 적용하지 말 것.")]
    public List<AttackBeat> Beats = new List<AttackBeat>();
}

[Serializable]
public class ReturnPhase : PhaseBase { }

[Serializable]
public class PostPhase : PhaseBase
{
    [Tooltip("연출 종료 후 추가 대기(초).")]
    public float ExtraDelay;
}
