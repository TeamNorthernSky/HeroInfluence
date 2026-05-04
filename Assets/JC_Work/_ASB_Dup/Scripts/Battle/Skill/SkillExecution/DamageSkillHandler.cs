using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 기본 데미지 블록만 호출하는 샘플 핸들러.
    /// </summary>
    public sealed class DamageSkillHandler : ISkillEffectHandler
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

            float dmg = SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue);
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} dmg={dmg:F1}");
            return true;
        }
    }



    public sealed class CasterLowHPMoreDmg : ISkillEffectHandler
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

            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = 1.0f - caster.CurrentHp / (float)caster.MaxHp;
            float snapped = Mathf.Round(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = snapped + skillData.skillValue;

            float dmg = SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue);
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} dmg={dmg:F1}");
            return true;
        }
    }

    public sealed class TargetFrontPosMoreDmg : ISkillEffectHandler
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

            float totalSkillValue = skillData.skillValue;

            if (target.IsInFrontRow)
                totalSkillValue += 0.2f; // 예: 전방 위치에 있을 경우 50% 추가 데미지


            float dmg = SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue);
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} dmg={dmg:F1}");
            return true;
        }
    }

    
    public sealed class TauntStrikeSkillHanr : ISkillEffectHandler
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

            var tauntEffect = new StatusEffectInstance
            {
                effectType = StatusEffectType.taunt,
                category = StatusEffectCategory.debuff,
                value = 0f,
                remainingTurns = TauntDurationTurns,
                source = caster
            };

            target.ApplyStatusEffect(tauntEffect);
            Debug.Log($"[Skill/TauntStrike] Taunt applied: target={target.UnitName}, source={caster.UnitName}, turns={TauntDurationTurns}");
            return true;
        }
    }
}
