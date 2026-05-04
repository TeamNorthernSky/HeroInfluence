using UnityEngine;

namespace ASB.Work.Battle.Core
{
    public static class CombatCalculator
    {
        public static float CalculateDamage(DamageContext context)
        {
            // TODO: 크리티컬 확장
            // TODO: 방어 관통 확장
            // TODO: 속성 상성 확장
            float multiplier = context.SkillValue > 0f
                ? Mathf.Max(0.01f, context.SkillValue)
                : Mathf.Max(0.01f, context.SkillMultiplier);
            float baseDamage = context.Caster.FinalStats.Atk * multiplier;
            float finalDamage = Mathf.Max(1f, baseDamage - context.Target.FinalStats.DEF);
            return finalDamage;
        }
    }
}
