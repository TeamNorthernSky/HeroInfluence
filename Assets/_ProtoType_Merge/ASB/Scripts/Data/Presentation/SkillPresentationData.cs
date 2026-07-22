using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// skillIndex로 매핑되는 스킬 연출 데이터.
/// PresentationSchemaVersion으로 신/구 경로를 구분한다: 0=Legacy(기존 director), 1=PhaseCue(페이즈+Cue).
/// </summary>
[CreateAssetMenu(fileName = "SkillPresentation_New", menuName = "Battle/Skill Presentation Data")]
public class SkillPresentationData : ScriptableObject
{
    [Header("Skill Binding")]
    [Tooltip("SkillData.skillIndex. SkillPresentationCatalog가 이 값으로 조회.")]
    public int SkillIndex;

    [Tooltip("0 = Legacy(기존 director 경로), 1 = PhaseCue(새 Cue 경로). 자동 변경 금지 — 에디터 Upgrade 버튼으로만 전환.")]
    public int PresentationSchemaVersion = 0;

    /// <summary>새 페이즈/Cue 구조가 활성인지.</summary>
    public bool IsPhaseCue => PresentationSchemaVersion >= 1;

    [Header("Animation State/Slot Override (empty uses SkillData/fallback)")]
    [Tooltip("이 스킬의 Animator state/slot. 비면 SkillData.StateName → 산술(ClassSkill_N/WeaponSkill_N) 폴백.")]
    [AnimatorStateDropdown]
    public string AnimationStateName;
    [Tooltip("Legacy. 신규는 AnimationStateName 사용.")]
    public string AnimationTriggerOverride;
    [Tooltip("비면 SkillData.TargetAnimationTrigger 사용.")]
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

    [Header("Hit Timing")]
    [Tooltip("true: AniEvent_OnHit 대기. false: HitDelay 사용.")]
    public bool UseAnimEvent = false;
    [Tooltip("UseAnimEvent가 false일 때 히트 지연(초).")]
    public float HitDelay = 0.25f;

    // ─────────────────────────────────────────────
    // Legacy (Schema=0) — 기존 BattleVisualDirector 경로용. Schema=1에선 사용하지 않음.
    // ─────────────────────────────────────────────
    [Header("Sound (Legacy, Schema=0)")]
    public int AttackSoundId;
    public int HitSoundId;
    [Range(0f, 1f)] public float SfxVolume = 1f;

    [Header("Attack/Hit Effect (Legacy, Schema=0)")]
    public bool EnableAttackEffect = true;
    public int AttackEffectId;
    public bool EnableHitEffect = true;
    public int HitEffectId;

    [Header("Projectile (Legacy, Schema=0)")]
    public GameObject ProjectilePrefab;
    public float FlightTime = 0.3f;
    public ProjectileTrajectoryType TrajectoryType = ProjectileTrajectoryType.Straight;
    public float ArcHeight = 2f;
    public bool ScaleByCellSize;

    // 더 오래된 직접참조 필드(마이그레이션 잔재). 레지스트리 조회 실패 시 폴백으로만.
    [HideInInspector] public AudioClip AttackSfxClip;
    [HideInInspector] public AudioClip HitSfxClip;
    [HideInInspector] public GameObject AttackEffectPrefab;
    [HideInInspector] public Vector3 AttackEffectPositionOffset;
    [HideInInspector] public Vector3 AttackEffectRotationOffset;
    [HideInInspector] public GameObject HitEffectPrefab;
    [HideInInspector] public Vector3 HitEffectPositionOffset;
    [HideInInspector] public Vector3 HitEffectRotationOffset;

    // ─────────────────────────────────────────────
    // Presentation Phases (Schema=1) — 애니+Cue를 갖는 페이즈: MovePrepare/AttackPrepare/Attack/Post.
    // Move/Return은 이동 로코모션이라 Enabled만.
    // ─────────────────────────────────────────────
    [Header("Presentation Phases (Schema=1)")]
    public MovePreparePhase MovePrepare = new MovePreparePhase();
    public MovePhase Move = new MovePhase();
    public AttackPreparePhase AttackPrepare = new AttackPreparePhase();
    public AttackPhase Attack = new AttackPhase();
    public ReturnPhase Return = new ReturnPhase();
    public PostPhase Post = new PostPhase();

    /// <summary>페이즈를 정의 순서대로 순회.</summary>
    public IEnumerable<PhaseBase> GetPhases()
    {
        yield return MovePrepare;
        yield return Move;
        yield return AttackPrepare;
        yield return Attack;
        yield return Return;
        yield return Post;
    }

    /// <summary>첫 Attack Beat. 레거시 state 폴백과 기존 단일 공격 호환에만 사용한다.</summary>
    public AttackBeat PrimaryAttackBeat =>
        (Attack != null && Attack.Beats != null && Attack.Beats.Count > 0) ? Attack.Beats[0] : null;

    private void OnValidate()
    {
        if (!IsPhaseCue)
        {
            return;
        }

        // Schema=1일 때만 Cue/state 검증.
        ValidateCuePhase("MovePrepare", MovePrepare);
        ValidateCuePhase("AttackPrepare", AttackPrepare);
        ValidateCuePhase("Post", Post);

        if (Attack != null && Attack.Enabled && Attack.Beats != null)
        {
            if (Attack.Beats.Count > 1)
            {
                Debug.LogWarning($"[SkillPresentation] {name}: 이번 버전은 Attack Beat 단일만 실행합니다. Beats[0] 외에는 무시됩니다.", this);
            }
            for (int i = 0; i < Attack.Beats.Count; i++)
            {
                ValidateCues($"Attack.Beats[{i}]", Attack.Beats[i]?.AnimationStateName, Attack.Beats[i]?.Cues);
            }
        }
    }

    private void ValidateCuePhase(string label, CuePhase phase)
    {
        if (phase == null || !phase.Enabled)
        {
            return;
        }
        ValidateCues(label, phase.AnimationStateName, phase.Cues);
    }

    private void ValidateCues(string label, string stateName, List<CueBinding> cues)
    {
        bool hasCue = cues != null && cues.Count > 0;
        if (hasCue && string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogWarning($"[SkillPresentation] {name}: {label}에 Cue가 있는데 AnimationStateName이 비어 Cue를 낼 클립이 없습니다.", this);
        }

        if (!hasCue)
        {
            return;
        }

        var seen = new HashSet<string>();
        for (int i = 0; i < cues.Count; i++)
        {
            string norm = cues[i] != null ? cues[i].NormalizedCueName : string.Empty;
            if (string.IsNullOrEmpty(norm))
            {
                Debug.LogWarning($"[SkillPresentation] {name}: {label}.Cues[{i}] CueName이 비어 있습니다(실행되지 않음).", this);
                continue;
            }
            if (!seen.Add(norm))
            {
                Debug.LogWarning($"[SkillPresentation] {name}: {label}에 CueName '{norm}' 중복. 페이즈/Beat 내 CueName은 유일해야 합니다.", this);
            }
        }
    }
}

public enum ProjectileTrajectoryType
{
    Straight,
    Arc
}

/// <summary>이펙트를 어디에 생성할지(배치 정보). 재료 동작이 아니라 레시피가 결정.</summary>
public enum SpawnAnchor
{
    Caster,       // 시전자 루트
    CasterSocket, // 시전자 소켓(SocketName)
    Target,       // 대상 위치
    TargetCell    // 대상 셀(위치 스냅샷)
}

/// <summary>
/// 하나의 연출 Cue. 클립의 AniEvent_PresentationCue(cueName)가 이 CueName과 매칭되면
/// EffectIds/SoundIds를 실행한다. 이펙트/사운드는 id 참조만, 동작은 프리팹, 배치는 Anchor/SocketName.
/// </summary>
[Serializable]
public class CueBinding
{
    [CueDropdown]
    [Tooltip("클립 AniEvent_PresentationCue 인자와 일치. trim+소문자로 정규화됨. 빈 값은 실행 안 함.")]
    public string CueName;

    [Tooltip("EffectRegistry id 목록(0..N).")]
    public List<int> EffectIds = new List<int>();
    [Tooltip("SoundRegistry id 목록(0..N).")]
    public List<int> SoundIds = new List<int>();

    public SpawnAnchor Anchor = SpawnAnchor.CasterSocket;
    [Tooltip("Anchor=CasterSocket일 때 사용할 소켓 이름(선택).")]
    public string SocketName;

    /// <summary>trim + 소문자 정규화된 CueName. 매칭/맵 키에 사용.</summary>
    public string NormalizedCueName =>
        string.IsNullOrWhiteSpace(CueName) ? string.Empty : CueName.Trim().ToLowerInvariant();
}

[Serializable]
public class PhaseBase
{
    [Tooltip("이 페이즈를 실행할지 여부. false면 스킵.")]
    public bool Enabled = true;
}

/// <summary>애니메이션 슬롯 + Cue 목록을 갖는 페이즈 공통 형태.</summary>
[Serializable]
public class CuePhase : PhaseBase
{
    [AnimatorStateDropdown]
    [Tooltip("이 페이즈에서 재생할 Animator state/slot.")]
    public string AnimationStateName;
    public List<CueBinding> Cues = new List<CueBinding>();
}

[Serializable]
public class MovePreparePhase : CuePhase { }

[Serializable]
public class MovePhase : PhaseBase
{
    [Tooltip("유닛 이동 프로필로 접근/복귀. 실제 이동 여부는 런타임 조건과 AND.")]
    public bool UseUnitMovement = true;
}

[Serializable]
public class AttackPreparePhase : CuePhase { }

/// <summary>공격 타격 단위. 각 Beat는 시퀀서가 CrossFade로 재생하고 Cue 컨텍스트를 개별 등록한다.</summary>
[Serializable]
public class AttackBeat
{
    [AnimatorStateDropdown]
    [Tooltip("이 타격의 Animator state/slot. 비면 SkillData.StateName → 산술 폴백.")]
    public string AnimationStateName;
    [Min(0f)]
    public float BlendInSeconds = 0.1f;
    public bool WaitForHitEvent = true;
    public List<CueBinding> Cues = new List<CueBinding>();
}

[Serializable]
public class AttackPhase : PhaseBase
{
    [Tooltip("콤보 Beat 목록. non-last Beat 클립에는 AniEvent_AdvanceCombo 이벤트가 필요합니다.")]
    public List<AttackBeat> Beats = new List<AttackBeat>();
}

[Serializable]
public class ReturnPhase : PhaseBase { }

[Serializable]
public class PostPhase : CuePhase
{
    [Tooltip("연출 종료 후 추가 대기(초).")]
    public float ExtraDelay;
}
