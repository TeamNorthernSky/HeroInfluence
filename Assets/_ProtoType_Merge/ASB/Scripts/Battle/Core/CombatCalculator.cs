using UnityEngine;

namespace ASB.Work.Battle.Core
{
    public static class CombatCalculator
    {
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

        public static float CalculateDamage(DamageContext context)
        {
            float atk = context.Caster.FinalStats.Atk;
            float def = context.Target.FinalStats.DEF;
            float baseStatDiff = Mathf.Max(0f, atk - def);

            float multiplier = context.SkillValue > 0f
                ? Mathf.Max(0.01f, context.SkillValue)
                : Mathf.Max(0.01f, context.SkillMultiplier);

            float effectiveAvoidRate = Mathf.Max(0f, context.Target.FinalStats.AvoidRate - context.TargetAvoidRateReduction);
            float mitigationRate = Mathf.Clamp01(1f - (effectiveAvoidRate ));
            if (context.Target.IsInFrontRow && context.IsRangedAttack)
            {
                mitigationRate *= 0.9f;
            }
            else if (context.Target.IsInBackRow && !context.IsRangedAttack)
            {
                mitigationRate *= 1.1f;
            }

            float critMultiplier = context.IsCritical ? 1.5f : 1.0f;
            float influenceMultiplier = context.Caster.FinalStats.Influence / 100f;
            float finalDamage = baseStatDiff * multiplier * mitigationRate * critMultiplier * influenceMultiplier;
            float roundedDamage = Mathf.Floor(finalDamage + 0.5f);
            return Mathf.Max(2f, roundedDamage);
        }
    }
}
