using UnityEngine;
using ASB.Work.Battle.SkillExecution;
/// <summary>
/// 구조 예시: 기본 데미지 공식만 사용하는 커스텀 핸들러 샘플.
/// </summary>
public sealed class DefaultDamageSkillHandler : ISkillEffectHandler
{
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
        Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} dmg={dmg:F1} (×{multiplier:0.##})");
        return true;
    }
}
