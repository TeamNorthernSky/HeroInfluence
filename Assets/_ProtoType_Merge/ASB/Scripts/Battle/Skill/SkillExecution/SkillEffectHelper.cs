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
        public static DamageContext ApplyStandardDamage(
            BattleCharactor caster,
            BattleCharactor target,
            float skillValue,
            int skillIndex = 0,
            int skillRange = -1,
            bool isAdditionalHit = false,
            bool isCounterAttack = false,
            float bonusCritRate = 0f,
            float targetAvoidRateReduction = 0f,
            bool? sharedIsCritical = null)
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
                SkillIndex = skillIndex,
                IsRangedAttack = skillRange > 0,
                BonusCritRate = bonusCritRate,
                TargetAvoidRateReduction = targetAvoidRateReduction,
                CanTriggerCounter = !isAdditionalHit && !isCounterAttack && skillRange == 0 && target.IsInFrontRow,
                IsCounterAttack = isCounterAttack
            };
            context.IsCritical = sharedIsCritical ?? CombatCalculator.RollCritical(context);
            return context;
        }

        public static DamageContext ApplySkillDamage(
            BattleCharactor caster,
            BattleCharactor target,
            float skillValue,
            int skillIndex = 0,
            int skillRange = -1,
            bool isAdditionalHit = false,
            bool isCounterAttack = false,
            float bonusCritRate = 0f,
            float targetAvoidRateReduction = 0f)
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
                SkillIndex = skillIndex,
                IsRangedAttack = skillRange > 0,
                BonusCritRate = bonusCritRate,
                TargetAvoidRateReduction = targetAvoidRateReduction,
                CanTriggerCounter = !isAdditionalHit && !isCounterAttack && skillRange == 0 && target.IsInFrontRow,
                IsCounterAttack = isCounterAttack
            };
            context.IsCritical = CombatCalculator.RollCritical(context);
            return context;
        }



        public static float ApplyStandardHeal(BattleCharactor caster, BattleCharactor target, float skillValue)
        {
            if (caster == null || target == null)
            {
                return 0f;
            }

            float heal = CalculateStandardHealAmount(skillValue);
            target.ApplyHeal(heal);
            return heal;
        }

        public static float CalculateStandardHealAmount(float skillValue)
        {
            return Mathf.Max(0.01f, skillValue);
        }


        // 타겟 HP 비례 회복: skillValue가 0.2면 20% HP 회복, 50이면 50% HP 회복
        public static float ApplyTargetHPPerHeal(BattleCharactor caster, BattleCharactor target, float skillValue)
        {
            if (caster == null || target == null)
            {
                return 0f;
            }

            float heal = CalculateTargetHPPerHealAmount(target, skillValue);
            target.ApplyHeal(heal);
            return heal;
        }

        public static float CalculateTargetHPPerHealAmount(BattleCharactor target, float skillValue)
        {
            if (target == null)
            {
                return 0f;
            }

            float multiplier = Mathf.Max(0.01f, skillValue);
            return Mathf.Max(0f, target.FinalStats.HP * multiplier);
        }



        public static bool TryApplyStatusEffect(BattleCharactor target, StatusEffectType effectType, float chance)
        {
            if (target == null || target.IsDead)
            {
                return false;
            }

            float safeChance = Mathf.Clamp01(chance);
            bool success = Random.value < safeChance;
            if (success)
            {
                Debug.Log($"[SkillEffect] 상태이상 부여 성공: target={target.UnitName}, effect={effectType}, chance={safeChance:0.##}");
            }

            return success;
        }

        public static void ApplyStatusEffect(StatusEffectContext context)
        {
            if (context.Caster == null || context.Target == null || context.Target.IsDead)
            {
                return;
            }

            switch (context.EffectType)
            {
                case StatusEffectType.taunt:
                    SetTaunt(context.Caster, context.Target, context.DurationTurn);
                    break;
                case StatusEffectType.stun:
                    SetStun(context.Caster, context.Target, context.DurationTurn);
                    break;
                case StatusEffectType.healBan:
                    SetHealBan(context.Caster, context.Target, context.DurationTurn);
                    break;
                case StatusEffectType.bleed:
                case StatusEffectType.poison:
                case StatusEffectType.attack_up:
                case StatusEffectType.attack_down:
                case StatusEffectType.defense_up:
                case StatusEffectType.defense_down:
                    context.Target.ApplyStatusEffect(CreateStatusEffectInstance(context));
                    break;
                default:
                    Debug.LogWarning($"[SkillEffect] 지원하지 않는 상태이상 타입: {context.EffectType}");
                    break;
            }
        }

        private static StatusEffectInstance CreateStatusEffectInstance(StatusEffectContext context)
        {
            return new StatusEffectInstance
            {
                effectType = context.EffectType,
                category = ResolveStatusEffectCategory(context.EffectType),
                value = 0f,
                remainingTurns = Mathf.Max(1, context.DurationTurn),
                source = context.Caster
            };
        }

        private static StatusEffectCategory ResolveStatusEffectCategory(StatusEffectType effectType)
        {
            switch (effectType)
            {
                case StatusEffectType.attack_up:
                case StatusEffectType.defense_up:
                    return StatusEffectCategory.buff;
                case StatusEffectType.poison:
                case StatusEffectType.bleed:
                    return StatusEffectCategory.dot;
                default:
                    return StatusEffectCategory.debuff;
            }
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
