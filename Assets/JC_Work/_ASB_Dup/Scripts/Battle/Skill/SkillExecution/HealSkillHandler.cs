using UnityEngine;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 기본 힐 블록만 호출
    /// </summary>
    public sealed class HealSkillHandler : ISkillEffectHandler
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

            float heal = SkillEffectHelper.ApplyStandardHeal(caster, target, skillData.skillValue);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
            return true;
        }
    }

    //부활 스킬
    public sealed class RebirthSkillHandler : ISkillEffectHandler
    {
        public bool Execute(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillData additionalSkillData)
        {
            if (caster == null || target == null || skillData == null)
            {
                return false;
            }

            if (caster.IsDead)
            {
                return false;
            }

            if (!target.IsDead)
            {
                return false;
            }

            float ratio = SkillEffectHelper.ResolveReviveHpRatio(skillData.skillValue);
            target.Revive(ratio);
            Debug.Log($"[Skill/Rebirth] {caster.UnitName} -> {target.UnitName} hpRatio={ratio:0.###}");
            return true;
        }


    }


}
