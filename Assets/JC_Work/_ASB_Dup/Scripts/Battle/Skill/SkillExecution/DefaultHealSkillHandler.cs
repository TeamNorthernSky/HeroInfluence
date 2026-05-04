using UnityEngine;
using ASB.Work.Battle.SkillExecution;
/// <summary>
/// 구조 예시: 기본 힐 공식만 사용하는 커스텀 핸들러 샘플.
/// </summary>
public sealed class DefaultHealSkillHandler : ISkillEffectHandler
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
        float heal = Mathf.Max(0f, caster.FinalStats.Atk * multiplier);
        target.ApplyHeal(heal);
        Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1} (×{multiplier:0.##})");
        return true;
    }
}
