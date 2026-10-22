// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/CombatCalculator.cs
// 원본 객체: CombatCalculator -> JcCombatCalculator
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using UnityEngine;

namespace ASB.Work.Battle.Core
{
    public static class JcCombatCalculator
    {
        private const float MaxCriticalRate = 0.7f;
        private const float ReducedDamageMultiplier = 0.5f;

        // 원본 함수 대응: CombatCalculator.RollCritical (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/CombatCalculator.cs)
        public static bool RollCritical(BattleCharactor caster)
        {
            if (caster == null)
            {
                return false;
            }

            float roll = JC.BattleTesting.JcBattleRandom.Range(0f, 1f);
            return roll < Mathf.Clamp(caster.FinalStats.CriticalRate, 0f, MaxCriticalRate);
        }

        // 원본 함수 대응: CombatCalculator.RollCritical (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/CombatCalculator.cs)

        public static bool RollCritical(DamageContext context)
        {
            if (context == null || context.Caster == null)
            {
                return false;
            }

            float finalCritRate = context.Caster.FinalStats.CriticalRate + context.BonusCritRate;
            finalCritRate = Mathf.Clamp(finalCritRate, 0f, MaxCriticalRate);
            float roll = JC.BattleTesting.JcBattleRandom.Range(0f, 1f);
            return roll < finalCritRate;
        }

        // 원본 함수 대응: CombatCalculator.RollCounter (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/CombatCalculator.cs)

        public static bool RollCounter(BattleCharactor defender)
        {
            if (defender == null)
            {
                return false;
            }

            float rate = Mathf.Clamp01(defender.FinalStats.CounterRate);
            return JC.BattleTesting.JcBattleRandom.value < rate;
        }

        public static bool RollDamageReduction(DamageContext context)
        {
            if (context?.Target == null || context.IsCounterAttack ||
                !TargetingHelper.IsUnitInFixedBackRow(context.Target))
            {
                return false;
            }

            float rate = Mathf.Clamp01(context.Target.FinalStats.AvoidRate - context.TargetAvoidRateReduction);
            return rate >= 1f || JC.BattleTesting.JcBattleRandom.value < rate;
        }

        // 원본 대응: CombatCalculator.ResolveHitRolls. JC 테스트 전투의 난수 소스를 사용한다.
        public static void ResolveHitRolls(DamageContext context)
        {
            if (context == null) return;
            if (context.IsCounterAttack)
            {
                context.IsDamageReduced = false;
                context.IsCritical = false;
                context.IsCriticalResolved = true;
                return;
            }

            if (context.Target == null || !TargetingHelper.IsUnitInFixedBackRow(context.Target))
                context.IsDamageReduced = false;
            else
                context.IsDamageReduced ??= RollDamageReduction(context);

            if (context.IsDamageReduced == true)
            {
                context.IsCritical = false;
                context.IsCriticalResolved = true;
                return;
            }

            if (context.IsCriticalResolved) return;
            if (context.SharedCriticalRoll != null)
            {
                context.SharedCriticalRoll.Result ??= RollCritical(context.Caster);
                context.IsCritical = context.SharedCriticalRoll.Result.Value;
            }
            else
                context.IsCritical = RollCritical(context);
            context.IsCriticalResolved = true;
        }

        // 원본 함수 대응: CombatCalculator.CalculateDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/CombatCalculator.cs)

        public static float CalculateDamage(DamageContext context)
        {
            if (context.IsCounterAttack)
            {
                float counterDamage = Mathf.Max(2f, context.Caster.FinalStats.Atk * 0.5f);
                return Mathf.Floor(counterDamage + 0.5f);
            }

            float atk = context.Caster.FinalStats.Atk;
            float def = context.Target.FinalStats.DEF;
            float baseStatDiff = Mathf.Max(0f, atk - def);

            float multiplier = context.SkillValue > 0f
                ? Mathf.Max(0.01f, context.SkillValue)
                : Mathf.Max(0.01f, context.SkillMultiplier);

            bool damageReduced = TargetingHelper.IsUnitInFixedBackRow(context.Target) && context.IsDamageReduced == true;
            float mitigationRate = damageReduced ? ReducedDamageMultiplier : 1f;
            if (context.Target.IsInFrontRow && context.IsRangedAttack)
            {
                mitigationRate *= 0.9f;
            }
            else if (context.Target.IsInBackRow && !context.IsRangedAttack)
            {
                mitigationRate *= 1.1f;
            }

            float critMultiplier = context.IsCritical && !damageReduced ? 1.5f : 1.0f;
            float baseDamage = baseStatDiff * multiplier * mitigationRate * critMultiplier;
            float influence = Mathf.Max(0f, context.Caster.CurrentInfluence);
            float finalDamage = baseDamage * (100f + influence) / 100f * context.Target.SkillDamageTakenMultiplier;
            float roundedDamage = Mathf.Floor(finalDamage + 0.5f);
            return Mathf.Max(2f, roundedDamage);
        }
    }
}
