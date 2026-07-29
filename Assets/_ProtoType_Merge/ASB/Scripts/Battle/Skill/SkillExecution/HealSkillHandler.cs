using UnityEngine;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 기본 힐 블록만 호출
    /// </summary>

    public sealed class HealSkillHandler : BaseSingleSkillHandler
    {
        protected override void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float heal = SkillEffectHelper.CalculateStandardHealAmount(skillData.skillValue);
            result.AddHeal(caster, target, heal, skillData != null ? skillData.skillIndex : 0);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }

    public sealed class TargetLowerHPMoreHeal : BaseSingleSkillHandler
    {
        protected override void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 15의 추가 힐량을 얻음
            float hpRatio = (1.0f - target.CurrentHp / (float)target.MaxHp);
            float snapped = Mathf.Round(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = Mathf.Clamp(snapped * 15 + 5, 5, 50);
            float heal = SkillEffectHelper.CalculateStandardHealAmount(totalSkillValue);
            result.AddHeal(caster, target, heal, skillData != null ? skillData.skillIndex : 0);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }



    // 타겟 HP 비례 힐: 타깃에 HP에 비례해서 힐을 함
    public sealed class TargetHPPerHeal : BaseSingleSkillHandler
    {
        protected override void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = Mathf.Ceil(target.FinalStats.HP * Mathf.Clamp01(skillData.skillValue));
            float heal = SkillEffectHelper.CalculateStandardHealAmount(totalSkillValue);
            result.AddHeal(caster, target, heal, skillData != null ? skillData.skillIndex : 0);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }


    //부활 스킬
    /// <summary>
    /// 4020: Deals normal damage, then restores HP to the living allied unit with
    /// the lowest HP ratio by the actual damage dealt (capped at 20).
    /// </summary>
    public sealed class HolyBulletHpRecoveryHandler : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(
                caster,
                target,
                skillData.skillValue,
                skillData.skillIndex,
                skillData.classSkillRange));

            result.OnPostExecution += totalDamageDealt =>
            {
                float healAmount = Mathf.Min(20f, Mathf.Max(0f, totalDamageDealt));
                if (healAmount <= 0f)
                    return;

                BattleCharactor healTarget = FindLowestHpRatioAlly(caster);
                if (healTarget != null)
                    healTarget.ApplyHeal(healAmount);
            };
        }

        private static BattleCharactor FindLowestHpRatioAlly(BattleCharactor caster)
        {
            BattleCharactor best = null;
            float lowestRatio = float.MaxValue;
            BattleCharactor[] units = UnityEngine.Object.FindObjectsByType<BattleCharactor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (BattleCharactor unit in units)
            {
                if (unit == null || unit.IsDead || unit.IsPlayer != caster.IsPlayer || unit.MaxHp <= 0f)
                    continue;

                float hpRatio = unit.CurrentHp / unit.MaxHp;
                if (hpRatio < lowestRatio)
                {
                    lowestRatio = hpRatio;
                    best = unit;
                }
            }

            return best;
        }
    }
    public sealed class RebirthSkillHandler : BaseSingleSkillHandler
    {
        protected override void ApplyHeal(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            if (!target.IsDead)
                return;

            target.Revive(0.2f);
            Debug.Log($"[Skill/Rebirth] {caster.UnitName} -> {target.UnitName} hpRatio={0.2f:0.###}");

            BattleCharactor[] units = UnityEngine.Object.FindObjectsByType<BattleCharactor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (BattleCharactor enemy in units)
            {
                if (enemy == null || enemy.IsDead || enemy.IsPlayer == caster.IsPlayer)
                    continue;

                result.AddDamage(SkillEffectHelper.ApplyStandardDamage(
                    caster,
                    enemy,
                    skillData.skillValue,
                    skillData.skillIndex,
                    skillData.classSkillRange,
                    isAdditionalHit: true));
            }
        }
    }
    public sealed class HealTargetAroundRandomHandler : TargetAroundRandom
    {
        protected override void ApplyMainEffect(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            AddCasterBasedHeal(caster, target, skillData, result);
        }

        protected override void ApplyHeal(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            AddCasterBasedHeal(caster, target, skillData, result);
        }

        private static void AddCasterBasedHeal(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            float heal = caster.MaxHp * skillData.skillValue;
            result.AddHeal(caster, target, heal, skillData.skillIndex);
            Debug.Log($"[Skill/TargetAroundRandomHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }
    /// <summary>
    /// 광역 흡혈 스킬:
    /// - 범위 내 각 대상에게 데미지를 주고
    /// - 이번 스킬로 가한 총 데미지 합만큼 시전자를 회복합니다.
    /// </summary>
    public sealed class AoEVampiricSkillHandler : BaseAoESkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result, bool? sharedIsCritical = null)
        {
            // 데미지 적용/계산은 BattleManager로만 중앙화합니다.
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange, sharedIsCritical: sharedIsCritical));
            Debug.Log($"[Skill/AoEVampiric] hit {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");

            // 모든 DamageContext 적용이 끝난 직후, 총 피해량만큼 흡혈 회복합니다.
            if (result.OnPostExecution == null)
            {
                result.OnPostExecution = (totalDamageDealt) =>
                {
                    caster.ApplyHeal(totalDamageDealt);
                    Debug.Log($"[Skill/AoEVampiric] heal caster={caster.UnitName} totalHeal={totalDamageDealt:F1}");
                };
            }
        }
    }

}
