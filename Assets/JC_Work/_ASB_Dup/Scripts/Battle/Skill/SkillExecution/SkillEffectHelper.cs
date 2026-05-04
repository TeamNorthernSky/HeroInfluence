using UnityEngine;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 스킬 최소 단위 효과를 재사용 가능한 조립 블록으로 제공하는 헬퍼.
    /// </summary>
    public static class SkillEffectHelper
    {
        public static float ApplyStandardDamage(BattleCharactor caster, BattleCharactor target, float skillValue)
        {
            if (caster == null || target == null)
            {
                return 0f;
            }

            float multiplier = Mathf.Max(0.01f, skillValue);
            float raw = (caster.FinalStats.Atk * multiplier) - target.FinalStats.DEF;
            float damage = Mathf.Max(1f, raw);
            target.TakeDamage(damage);
            return damage;
        }

        public static float ApplyStandardHeal(BattleCharactor caster, BattleCharactor target, float skillValue)
        {
            if (caster == null || target == null)
            {
                return 0f;
            }

            float multiplier = Mathf.Max(0.01f, skillValue);
            float heal = Mathf.Max(0f, caster.FinalStats.Atk * multiplier);
            target.ApplyHeal(heal);
            return heal;
        }

        public static bool TryApplyStatusEffect(BattleCharactor target, string effectType, float chance)
        {
            if (target == null || target.IsDead)
            {
                return false;
            }

            string safeEffectType = string.IsNullOrWhiteSpace(effectType) ? "Unknown" : effectType.Trim();
            float safeChance = Mathf.Clamp01(chance);
            bool success = Random.value < safeChance;
            if (success)
            {
                Debug.Log($"[SkillEffect] 상태이상 부여 성공: target={target.UnitName}, effect={safeEffectType}, chance={safeChance:0.##}");
            }

            return success;
        }

        public static float ResolveReviveHpRatio(float skillValue)
        {
            if (skillValue <= 0f)
            {
                return 0.5f;
            }

            return skillValue <= 1f ? Mathf.Clamp01(skillValue) : Mathf.Clamp01(skillValue * 0.01f);
        }


        public static void SetTaunt(BattleCharactor caster, BattleCharactor target, int DurationTurn)
        {
            var tauntEffect = new StatusEffectInstance
            {
                effectType = StatusEffectType.taunt,
                category = StatusEffectCategory.debuff,
                value = 0f,
                remainingTurns = DurationTurn,
                source = caster
            };

            target.ApplyStatusEffect(tauntEffect);
        }
        
    }
}
