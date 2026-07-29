using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

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

    [Header("Projectile Impact (Skill-specific)")]
    [Tooltip("Actual projectile presentation used by ranged offensive skills. This lives on the presentation asset, never in CSV SkillData.")]
    public ProjectileVisualData ProjectileVisual = new ProjectileVisualData();

    /// <summary>
    /// 스킬별 투사체 데이터를 반환한다. 유효 조건은 "Prefab != null"뿐이며 JC 컴포넌트를 확인하지 않는다.
    /// 프리팹이 없으면 null을 반환해 기존 즉시 히트/레거시 화살 흐름으로 폴백한다.
    /// (레거시 ProjectilePrefab 자동 합성은 JC 의존을 없애기 위해 제거함. 필요하면 ProjectileVisual을 명시 설정.)
    /// </summary>
    public ProjectileVisualData GetProjectileVisual()
    {
        return (ProjectileVisual != null && ProjectileVisual.Prefab != null) ? ProjectileVisual : null;
    }

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

    [Header("Moving Attack (Schema=1, optional)")]
    [Tooltip("활성 시 Move/AttackPrepare/Combo 대신 곡선 이동과 회전 공격을 하나의 액션으로 실행합니다.")]
    [FormerlySerializedAs("SpinSweep")]
    public MovingAttackPresentation MovingAttack = new MovingAttackPresentation();

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
        if (MovingAttack != null && MovingAttack.Enabled)
        {
            ValidateCues("MovingAttack", MovingAttack.AnimationStateName, MovingAttack.Cues);
        }

        if (Attack != null && Attack.Enabled && Attack.Beats != null)
        {

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
        var heldInstanceKeys = new HashSet<string>();
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

            CueBinding cue = cues[i];
            string instanceKey = cue.NormalizedInstanceKey;
            int effectCount = cue.EffectIds != null ? cue.EffectIds.Count : 0;
            if (cue.Operation == CueOperation.Spawn && !string.IsNullOrEmpty(instanceKey) && effectCount != 1)
            {
                Debug.LogWarning($"[SkillPresentation] {name}: {label}.Cues[{i}] Spawn+InstanceKey는 EffectIds를 정확히 1개 가져야 합니다.", this);
            }
            if (cue.Operation == CueOperation.Spawn && !string.IsNullOrEmpty(instanceKey) && !heldInstanceKeys.Add(instanceKey))
            {
                Debug.LogWarning($"[SkillPresentation] {name}: {label}에 Held InstanceKey '{instanceKey}'가 중복 Spawn됩니다.", this);
            }
            if (cue.Operation != CueOperation.Spawn && string.IsNullOrEmpty(instanceKey))
            {
                Debug.LogWarning($"[SkillPresentation] {name}: {label}.Cues[{i}] {cue.Operation}에는 InstanceKey가 필요합니다.", this);
            }
            if (cue.Operation != CueOperation.Spawn && effectCount > 0)
            {
                Debug.LogWarning($"[SkillPresentation] {name}: {label}.Cues[{i}] {cue.Operation}은 EffectIds를 생성하지 않습니다. 별도 Spawn Cue로 분리하세요.", this);
            }
        }
    }
}


/// <summary>
/// Unity-object-backed visual data for one skill's projectile. Keep this on
/// SkillPresentationData; SkillData is CSV-loaded and must remain reference-free.
/// </summary>
[Serializable]
public class ProjectileVisualData
{
    public GameObject Prefab;
    [Min(0.01f)] public float Speed = 6f;
    public ProjectileTrajectoryType Trajectory = ProjectileTrajectoryType.Arc;
    [Min(0f)] public float ArcHeight = 1.2f;
    [Min(0.01f)] public float TrailTime = 0.4f;
    [Tooltip("Visual endpoint follows the locked target while it remains valid.")]
    public bool TrackTarget;
    [Tooltip("이동 방향으로 투사체를 회전시킬지 여부.")]
    public bool AlignToVelocity = true;
    [Tooltip("도착 후 폭발/페이드/트레일 잔상을 유지할 배속-시간(초). 이후 자동 정리. 피해 타이밍과 무관.")]
    [Min(0f)] public float ImpactVisualLifetime = 0.3f;
    [Tooltip("Homing 전용 도착 반경. Straight/Arc는 거리 완료(t>=1)로 판정하며 이 값을 사용하지 않는다.")]
    [Min(0f)] public float ArrivalRadius = 0.2f;
    [Tooltip("Additional battle-time allowance after distance / speed. Increase for tracking projectiles that can chase a moving target.")]
    [Min(0.1f)] public float TimeoutGraceSeconds = 1f;
    [Tooltip("Deprecated compatibility field for existing assets. It is not used to determine arrival or timeout.")]
    [HideInInspector]
    [Min(0.1f)] public float TimeoutSeconds = 5f;

    [Tooltip("전달 방식. Single=일반 원거리. ChainAdditionalTargets=주 타깃 도착 지점에서 추가 타깃으로 이어지는 체인.")]
    public ProjectileDeliveryMode DeliveryMode = ProjectileDeliveryMode.Single;

    [Tooltip("준비(차징) 단계에서 미리 만든 인스턴스를 발사에 재사용(2040 낙하형). 첫 이벤트에 held Cue로 스폰·차징, 둘째 이벤트에 그 인스턴스를 발사.")]
    public bool UsePreparedCharge = false;
    [Tooltip("UsePreparedCharge일 때, 차징 held 이펙트의 InstanceKey. 발사 시점에 이 인스턴스를 넘겨받아 발사한다.")]
    public string ChargeInstanceKey;
    [Tooltip("도착점을 개별 타깃이 아니라 대상 진형 중심으로. 낙하형 전체 공격에 사용.")]
    public bool TargetFormationCenter = false;
}
public enum ProjectileTrajectoryType
{
    Straight,
    Arc,
    // 적 바로 위로 로프트한 뒤 수직으로 낙하한다(낙하형 단일 대상). ArcHeight를 대상 위 체공 높이로 사용.
    OverheadDrop
}

/// <summary>투사체 전달 방식. Single=시전자 원점에서 각 타깃으로. ChainAdditionalTargets=1차 도착점에서 2차로 체인.</summary>
public enum ProjectileDeliveryMode
{
    Single,
    ChainAdditionalTargets
}

/// <summary>이펙트를 어디에 생성할지(배치 정보). 재료 동작이 아니라 레시피가 결정.</summary>
public enum CueOperation
{
    Spawn,
    Signal,
    Stop
}
public enum SpawnAnchor
{
    Caster,       // 시전자 루트
    CasterSocket, // 시전자 소켓(Socket)
    Target,       // 대상 위치
    TargetCell,   // 대상 셀(위치 스냅샷)
    EachTarget    // AoE 각 타깃마다 하나씩 생성(대상 수만큼). Targets 리스트를 순회.
}

/// <summary>
/// 하나의 연출 Cue. 클립의 AniEvent_PresentationCue(cueName)가 이 CueName과 매칭되면
/// EffectIds/SoundIds를 실행한다. 이펙트/사운드는 id 참조만, 동작은 프리팹, 배치는 Anchor/Socket.
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

    [Tooltip("Spawn: 새 이펙트 생성, Signal: 유지 이펙트에 신호, Stop: 유지 이펙트 중단.")]
    public CueOperation Operation = CueOperation.Spawn;
    [Tooltip("Held 이펙트 식별 키. Spawn+키는 이펙트 1개, Signal/Stop은 필수.")]
    public string InstanceKey;

    public SpawnAnchor Anchor = SpawnAnchor.CasterSocket;
    [Tooltip("Anchor=CasterSocket일 때 사용할 소켓. None이면 기본 공격 소켓(AttackEffectSocket).")]
    public UnitSocket Socket = UnitSocket.None;

    /// <summary>trim + 소문자 정규화된 CueName. 매칭/맵 키에 사용.</summary>
    public string NormalizedCueName =>
        string.IsNullOrWhiteSpace(CueName) ? string.Empty : CueName.Trim().ToLowerInvariant();

    public string NormalizedInstanceKey =>
        string.IsNullOrWhiteSpace(InstanceKey) ? string.Empty : InstanceKey.Trim().ToLowerInvariant();
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
    [Min(0f)]
    [Tooltip("이전 상태에서 이 페이즈 애니로 전환할 블렌드 시간(초). 0이면 즉시 전환.")]
    public float BlendInSeconds = 0.1f;
    public List<CueBinding> Cues = new List<CueBinding>();
}

[Serializable]
public class MovePreparePhase : CuePhase { }

[Serializable]
public class MovePhase : PhaseBase
{
    [Tooltip("유닛 이동 프로필로 접근/복귀. 실제 이동 여부는 런타임 조건과 AND.")]
    public bool UseUnitMovement = true;

    [AnimatorStateDropdown]
    [Tooltip("접근 이동 중 재생할 Animator state. 비어 있으면 기존 MoveForward를 사용.")]
    public string AnimationStateName;
    [Min(0f)]
    [Tooltip("현재 상태에서 접근 이동 애니로 전환할 블렌드 시간(초). 0이면 즉시 전환.")]
    public float BlendInSeconds = 0.1f;
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
    [Tooltip("켜면 AniEvent_AdvanceCombo 이벤트가 클립 종료보다 먼저 왔을 때 다음 Beat로 조기 전환합니다. 마지막 Beat에서는 무시됩니다.")]
    public bool AdvanceOnEvent;
    public bool WaitForHitEvent = true;
    public List<CueBinding> Cues = new List<CueBinding>();
}

[Serializable]
public class AttackPhase : PhaseBase
{
    [Tooltip("콤보 Beat 목록. 기본은 각 Beat 클립 종료 후 다음 Beat로 진행합니다. 조기 전환이 필요한 Beat만 Advance On Event를 켜고 AniEvent_AdvanceCombo를 배치하세요.")]
    public List<AttackBeat> Beats = new List<AttackBeat>();
}

[Serializable]
public class ReturnPhase : PhaseBase
{
    [AnimatorStateDropdown]
    [Tooltip("복귀 이동 중 재생할 Animator state. 비어 있으면 기존 MoveReturn을 사용.")]
    public string AnimationStateName;
    [Min(0f)]
    [Tooltip("현재 상태에서 복귀 이동 애니로 전환할 블렌드 시간(초). 0이면 즉시 전환.")]
    public float BlendInSeconds = 0.1f;
}

[Serializable]
public class PostPhase : CuePhase
{
    [Tooltip("연출 종료 후 추가 대기(초).")]
    public float ExtraDelay;
}

        
        [System.Serializable]
public enum MovingAttackHitMode
{
    SingleTarget,
    SingleAoE,
    SequentialAoE
}

/// <summary>
/// Move 단계로 Entry에 진입한 뒤 Entry → Mid → Exit를 스핀 공격으로 통과하는 연출 설정.
/// 위치는 적 진영 중심 기준 로컬 offset으로 저장한다.
/// </summary>
[System.Serializable]
[MovedFrom(true, sourceClassName: "SpinSweepPresentation")]
public class MovingAttackPresentation : PhaseBase
{
    public const int CurrentPathSchemaVersion = 2;

    public MovingAttackPresentation()
    {
        Enabled = false;
        PathSchemaVersion = CurrentPathSchemaVersion;
    }

    [AnimatorStateDropdown]
    public string AnimationStateName;

    [Min(0f)]
    public float AnimationBlendInSeconds = 0.1f;

    [Tooltip("x=시전자 기준 오른쪽/왼쪽, y=적 진영 방향 앞/뒤. Move가 끝나는 스핀 진입점.")]
    public Vector2 EntryOffset = new Vector2(-1.5f, 0f);

    [Tooltip("x=시전자 기준 오른쪽/왼쪽, y=적 진영 방향 앞/뒤. 스핀 곡선이 지나는 중간점.")]
    public Vector2 MidOffset = Vector2.zero;

    [Tooltip("x=시전자 기준 오른쪽/왼쪽, y=적 진영 방향 앞/뒤. 스핀 종료 및 Return 시작점.")]
    public Vector2 ExitOffset = new Vector2(1.5f, 0f);

    [HideInInspector] public int PathSchemaVersion = CurrentPathSchemaVersion;

    // Schema=1의 프로토타입 데이터 보존용. 런타임 경로의 직접 입력으로 사용하지 않는다.
    [HideInInspector] public float TopOffset = 1.5f;
    [HideInInspector] public float BottomOffset = 1.5f;
    [HideInInspector] public float SweepClearance;

    [Tooltip("켜면 이동 Transform을 곡선 접선 방향으로 회전합니다.")]
    public bool FaceCurveTangent;

    public MovingAttackHitMode HitMode = MovingAttackHitMode.SingleTarget;

    [Range(-1f, 1f)]
    [Tooltip("SingleAoE에서 0 이상이면 경로 진행도에서 전체 타격합니다. -1이면 AniEvent_OnHit을 사용합니다.")]
    public float SingleAoEHitPathProgress = -1f;

    [Min(48)]
    public int PathSampleCount = 64;

    public List<CueBinding> Cues = new List<CueBinding>();

    public bool IsLegacyPath => PathSchemaVersion < CurrentPathSchemaVersion;

    public void GetPathOffsets(out Vector2 entry, out Vector2 mid, out Vector2 exit)
    {
        if (!IsLegacyPath)
        {
            entry = EntryOffset;
            mid = MidOffset;
            exit = ExitOffset;
            return;
        }

        // 구형 데이터는 에디터 Upgrade 전에도 안전하게 표시/실행할 수 있도록 임시 변환한다.
        entry = new Vector2(TopOffset + SweepClearance, 0f);
        mid = Vector2.zero;
        exit = new Vector2(-(BottomOffset + SweepClearance), 0f);
    }

    public void UpgradeLegacyPath()
    {
        GetPathOffsets(out EntryOffset, out MidOffset, out ExitOffset);
        PathSchemaVersion = CurrentPathSchemaVersion;
    }
}

