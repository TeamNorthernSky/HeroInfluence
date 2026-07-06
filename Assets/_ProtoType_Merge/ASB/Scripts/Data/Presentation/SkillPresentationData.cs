using UnityEngine;

/// <summary>
/// 스킬 연출 데이터 (ScriptableObject).
/// CSV SkillData와 별도로 Inspector에서 설정하며, SkillPresentationCatalog를 통해 skillIndex로 매핑합니다.
/// </summary>
[CreateAssetMenu(fileName = "SkillPresentation_New", menuName = "Battle/Skill Presentation Data")]
public class SkillPresentationData : ScriptableObject
{
    [Header("Skill Binding")]
    [Tooltip("이 프리셋이 적용될 SkillData.skillIndex. SkillPresentationEditorWindow가 카탈로그 등록에 사용합니다.")]
    public int SkillIndex;

    [Header("Animation Override (비우면 CSV 값 사용)")]
    [Tooltip("비어 있으면 CSV SkillData.AnimationTrigger를 그대로 사용합니다.")]
    public string AnimationTriggerOverride;
    [Tooltip("비어 있으면 CSV SkillData.TargetAnimationTrigger를 그대로 사용합니다.")]
    public string TargetAnimationTriggerOverride;

    [Header("Sound")]
    public AudioClip AttackSfxClip;
    public AudioClip HitSfxClip;
    [Range(0f, 1f)] public float SfxVolume = 1f;

    [Header("Attack Effect")]
    [Tooltip("스킬 시전 시 공격자 소켓에 생성할 이펙트 프리팹")]
    public GameObject AttackEffectPrefab;

    [Header("Hit Effect")]
    [Tooltip("피격 시 타겟 소켓에 생성할 이펙트 프리팹")]
    public GameObject HitEffectPrefab;

    // [CSV 미지원 임시] UseAnimEvent / HitDelay를 CSV 대신 여기서 관리합니다.
    // CSV 스키마에 해당 컬럼이 추가되면 아래 섹션과 BattleManager의 ApplyPresentationOverride를 제거하세요.
    [Header("Hit Timing (CSV 미지원 임시)")]
    [Tooltip("true: 애니메이션 이벤트(AniEvent_OnHit) 프레임에 히트 처리. false: HitDelay 고정 시간 사용.")]
    public bool UseAnimEvent = false;
    [Tooltip("UseAnimEvent = false일 때 사용하는 히트 딜레이(초). UseAnimEvent = true면 무시됩니다.")]
    public float HitDelay = 0.25f;

    [Header("Projectile (선택)")]
    [Tooltip("비어 있으면 투사체 없이 기존 즉시 히트 처리(ResolveHitAction)를 사용합니다.")]
    public GameObject ProjectilePrefab;
    [Tooltip("발사~도착까지 걸리는 시간(초). 배속(_currentBattleSpeed)이 곱해져 실제 소요 시간이 줄어듭니다.")]
    public float FlightTime = 0.3f;
    public ProjectileTrajectoryType TrajectoryType = ProjectileTrajectoryType.Straight;
    [Tooltip("TrajectoryType이 Arc일 때 포물선 최고 높이.")]
    public float ArcHeight = 2f;
    [Tooltip("그리드 셀 크기에 맞춰 투사체 스케일을 조정할지 여부.")]
    public bool ScaleByCellSize;
}

public enum ProjectileTrajectoryType
{
    Straight,
    Arc
}
