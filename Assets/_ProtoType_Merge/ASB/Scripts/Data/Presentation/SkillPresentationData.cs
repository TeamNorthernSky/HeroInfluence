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
}
