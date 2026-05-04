using UnityEngine;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 특수 스킬 예시: 기본 피해 후 출혈 효과 부여 시도.
    /// 상태이상 시스템이 없는 현재는 로그만 출력합니다.
    /// </summary>
    public sealed class BleeedSkillHandler : ISkillEffectHandler
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

            float dmg = SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue);
            Debug.Log($"[Skill/BleedStrike] {caster.UnitName} -> {target.UnitName} dmg={dmg:F1}");

            if (target.IsDead)
            {
                return true;
            }

            bool bleedApplied = SkillEffectHelper.TryApplyStatusEffect(target, "Bleed", BleedChance);
            if (bleedApplied)
            {
                // TODO: 상태이상 시스템 연동 — 예: target.ApplyBleed(stacks: 1, duration: 3f);
                Debug.Log($"[Skill/BleedStrike] 출혈 부여 시도 (성공, 대상={target.UnitName})");
            }

            return true;
        }
    }

    // 도발배기_1010
    public sealed class TauntStrikeSkillHandler : ISkillEffectHandler
    {
        private const int TauntDurationTurns = 2;

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

            float damage = SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue);
            Debug.Log($"[Skill/TauntStrike] {caster.UnitName} -> {target.UnitName} dmg={damage:F1}");

            if (target.IsDead)
            {
                return true;
            }

            SkillEffectHelper.SetTaunt(caster, target, TauntDurationTurns);

            Debug.Log($"[Skill/TauntStrike] Taunt applied: target={target.UnitName}, source={caster.UnitName}, turns={TauntDurationTurns}");
            return true;
        }
    }

    // 일렬배기_1020
    public sealed class ColumnsStrikeSkillHandler : ISkillEffectHandler
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

            float damage = SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue);
            Debug.Log($"[Skill/TauntStrike] {caster.UnitName} -> {target.UnitName} dmg={damage:F1}");

            if (target.IsDead)
            {
                return true;
            }

            return true;
        }
    }



}
