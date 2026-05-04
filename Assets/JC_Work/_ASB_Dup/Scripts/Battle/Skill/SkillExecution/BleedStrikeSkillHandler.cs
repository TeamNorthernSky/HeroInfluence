using UnityEngine;
using ASB.Work.Battle.SkillExecution;
/// <summary>
/// 특수 스킬 예시: 기본 피해 후 출혈 시도(상태이상 시스템 미구현 시 로그만).
/// </summary>
public sealed class BleedStrikeSkillHandler : ISkillEffectHandler
{
    private const float BleedChance = 0.35f;

    public bool Execute(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillData additionalSkillData)
    {
        if (caster == null || target == null || skillData == null)
        {
            return false;
        }

        if (caster.IsDead || target.IsDead)
        {
            return false;
        }

        float multiplier = Mathf.Max(0.01f, skillData.skillValue);
        float raw = (caster.FinalStats.Atk * multiplier) - target.FinalStats.DEF;
        float dmg = Mathf.Max(1f, raw);
        target.TakeDamage(dmg);
        Debug.Log($"[Skill/BleedStrike] {caster.UnitName} -> {target.UnitName} dmg={dmg:F1}");

        if (target.IsDead)
        {
            return true;
        }

        if (UnityEngine.Random.value < BleedChance)
        {
            // TODO: 상태이상 시스템 연동 — 예: target.ApplyBleed(stacks: 1, duration: 3f);
            Debug.Log($"[Skill/BleedStrike] 출혈 부여 시도 (성공, 대상={target.UnitName}) — 상태이상 파이프라인 연결 전");
        }

        return true;
    }
}
