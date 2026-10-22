using UnityEngine;

namespace ASB.Work.Battle.Core
{
    public static class CombatCalculator
    {
        /// <summary>경감 발동 시 받는 피해 배율.</summary>
        private const float ReducedDamageMultiplier = 0.5f;

        public static bool RollCritical(BattleCharactor caster)
        {
            if (caster == null)
            {
                return false;
            }

            float roll = UnityEngine.Random.Range(0f, 1f);
            return roll < caster.FinalStats.CriticalRate;
        }

        public static bool RollCritical(DamageContext context)
        {
            if (context == null || context.Caster == null)
            {
                return false;
            }

            float finalCritRate = context.Caster.FinalStats.CriticalRate + context.BonusCritRate;
            finalCritRate = Mathf.Clamp(finalCritRate, 0f, 1f);
            float roll = UnityEngine.Random.Range(0f, 1f);
            return roll < finalCritRate;
        }

        public static bool RollCounter(BattleCharactor defender)
        {
            if (defender == null)
            {
                return false;
            }

            float rate = Mathf.Clamp01(defender.FinalStats.CounterRate);
            return UnityEngine.Random.value < rate;
        }

        /// <summary>대상의 경감 확률(ReduceRate → AvoidRate)로 이번 타격의 경감 발동 여부를 굴린다.</summary>
        public static bool RollDamageReduction(DamageContext context)
        {
            if (context?.Target == null)
            {
                return false;
            }

            float rate = Mathf.Clamp01(context.Target.FinalStats.AvoidRate - context.TargetAvoidRateReduction);
            // Random.value는 1을 포함하므로 100%는 따로 보장한다.
            return rate >= 1f || UnityEngine.Random.value < rate;
        }

        public static float CalculateDamage(DamageContext context)
            => CalculateDamage(context, out _);

        /// <summary>피해를 계산하고 경감이 발동했는지 함께 반환한다. 경감 판정은 context.IsDamageReduced를 따른다.</summary>
        public static float CalculateDamage(DamageContext context, out bool damageReduced)
        {
            if (context.IsCounterAttack)
            {
                damageReduced = false;
                return CalculateCounterDamage(context.Caster.FinalStats.Atk);
            }

            float atk = context.Caster.FinalStats.Atk;
            float def = context.Target.FinalStats.DEF;
            float baseStatDiff = Mathf.Max(0f, atk - def);

            float multiplier = context.SkillValue > 0f
                ? Mathf.Max(0.01f, context.SkillValue)
                : Mathf.Max(0.01f, context.SkillMultiplier);

            damageReduced = context.IsDamageReduced == true;
            float mitigationRate = damageReduced ? ReducedDamageMultiplier : 1f;
            if (context.Target.IsInFrontRow && context.IsRangedAttack)
            {
                mitigationRate *= 0.9f;
            }
            else if (context.Target.IsInBackRow && !context.IsRangedAttack)
            {
                mitigationRate *= 1.1f;
            }

            float critMultiplier = context.IsCritical ? 1.5f : 1.0f;
            float baseDamage = baseStatDiff * multiplier * mitigationRate * critMultiplier;
            float influence = Mathf.Max(0f, context.Caster.CurrentInfluence);
            float finalDamage = baseDamage * (100f + influence) / 100f * context.Target.SkillDamageTakenMultiplier;
            float roundedDamage = Mathf.Floor(finalDamage + 0.5f);
            return Mathf.Max(2f, roundedDamage);
        }

        /// <summary>반격은 상대 방어력과 일반 공격의 모든 계수를 제외하고 공격력의 50%만 사용한다.</summary>
        public static float CalculateCounterDamage(float counterAttackerAttack)
        {
            float finalDamage = Mathf.Max(2f, counterAttackerAttack * 0.5f);
            return Mathf.Floor(finalDamage + 0.5f);
        }
    }
}
