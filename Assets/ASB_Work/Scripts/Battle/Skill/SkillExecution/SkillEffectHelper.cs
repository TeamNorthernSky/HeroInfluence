using UnityEngine;
using ASB.Work.Battle.Core;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 스킬 최소 단위 효과를 재사용 가능한 조립 블록으로 제공하는 헬퍼.
    /// </summary>
    public static class SkillEffectHelper
    {
        // 데미지 적용/계산은 BattleManager에서만 수행합니다.
        // 이 메서드는 DamageContext(명세서)만 생성해 반환합니다.
        public static DamageContext ApplyStandardDamage(BattleCharactor caster, BattleCharactor target, float skillValue)
        {
            if (caster == null || target == null)
            {
                return default;
            }

            var context = new DamageContext
            {
                Caster = caster,
                Target = target,
                SkillMultiplier = 0f,
                SkillValue = skillValue,
                SkillIndex = 0,
                IsCritical = false
            };
            return context;
        }

        public static DamageContext ApplySkillDamage(BattleCharactor caster, BattleCharactor target, float skillValue)
        {
            if (caster == null || target == null)
            {
                return default;
            }

            var context = new DamageContext
            {
                Caster = caster,
                Target = target,
                SkillMultiplier = 0f,
                SkillValue = skillValue,
                SkillIndex = 0,
                IsCritical = false
            };
            return context;
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


        // 부활
        public static float ResolveReviveHpRatio(float skillValue )
        {
            if (skillValue <= 0f)
            {
                return 0.2f;
            }

            return skillValue <= 1f ? Mathf.Clamp01(skillValue) : Mathf.Clamp01(skillValue * 0.01f);
        }


        //스턴
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

        public static void SetStun(BattleCharactor caster, BattleCharactor target, int durationTurn)
        {
            if (target == null || target.IsDead)
            {
                return;
            }

            var stunEffect = new StatusEffectInstance
            {
                effectType = StatusEffectType.stun,
                category = StatusEffectCategory.debuff,
                value = 0f,
                remainingTurns = Mathf.Max(1, durationTurn),
                source = caster
            };

            target.ApplyStatusEffect(stunEffect);
        }

        public static void SetHealBan(BattleCharactor caster, BattleCharactor target, int durationTurn)
        {
            if (target == null || target.IsDead)
            {
                return;
            }

            var healBanEffect = new StatusEffectInstance
            {
                effectType = StatusEffectType.healBan,
                category = StatusEffectCategory.debuff,
                value = 0f,
                remainingTurns = Mathf.Max(1, durationTurn),
                source = caster
            };

            target.ApplyStatusEffect(healBanEffect);
        }

    }
}
