using UnityEngine;

/// <summary>
/// 스킬 연출 데이터 (ScriptableObject).
/// CSV SkillData와 별도로 Inspector에서 설정하며, SkillPresentationCatalog를 통해 skillIndex로 매핑합니다.
/// </summary>
[CreateAssetMenu(fileName = "SkillPresentation_New", menuName = "Battle/Skill Presentation Data")]
public class SkillPresentationData : ScriptableObject
{
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
}
