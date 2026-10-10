// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs
// 원본 객체: SkillEffectHelper -> JcSkillEffectHelper
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using UnityEngine;
using ASB.Work.Battle.Core;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 스킬 최소 단위 효과를 재사용 가능한 조립 블록으로 제공하는 헬퍼.
    /// </summary>
    public static class JcSkillEffectHelper
    {
        // 데미지 적용/계산은 BattleManager에서만 수행합니다.
        // 이 메서드는 DamageContext(명세서)만 생성해 반환합니다.
        // 원본 함수 대응: SkillEffectHelper.ApplyStandardDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)
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
                CanTriggerCounter = !isAdditionalHit && !isCounterAttack && skillRange == 0 && target.IsFrontRow(),
                IsCounterAttack = isCounterAttack
            };
            context.IsCritical = sharedIsCritical ?? JcCombatCalculator.RollCritical(context);
            return context;
        }

        // 원본 함수 대응: SkillEffectHelper.ApplySkillDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)

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
                CanTriggerCounter = !isAdditionalHit && !isCounterAttack && skillRange == 0 && target.IsFrontRow(),
                IsCounterAttack = isCounterAttack
            };
            context.IsCritical = JcCombatCalculator.RollCritical(context);
            return context;
        }



        // 원본 함수 대응: SkillEffectHelper.ApplyStandardHeal (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)



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

        // 원본 함수 대응: SkillEffectHelper.CalculateStandardHealAmount (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)

        public static float CalculateStandardHealAmount(float skillValue)
        {
            return Mathf.Max(0.01f, skillValue);
        }


        // 타겟 HP 비례 회복: skillValue가 0.2면 20% HP 회복, 50이면 50% HP 회복
        // 원본 함수 대응: SkillEffectHelper.ApplyTargetHPPerHeal (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)
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

        // 원본 함수 대응: SkillEffectHelper.CalculateTargetHPPerHealAmount (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)

        public static float CalculateTargetHPPerHealAmount(BattleCharactor target, float skillValue)
        {
            if (target == null)
            {
                return 0f;
            }

            float multiplier = Mathf.Max(0.01f, skillValue);
            return Mathf.Max(0f, target.FinalStats.HP * multiplier);
        }



        // 원본 함수 대응: SkillEffectHelper.TryApplyStatusEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)



        public static bool TryApplyStatusEffect(BattleCharactor target, StatusEffectType effectType, float chance)
        {
            if (target == null || target.IsDead)
            {
                return false;
            }

            float safeChance = Mathf.Clamp01(chance);
            bool success = JC.BattleTesting.JcBattleRandom.value < safeChance;
            if (success)
            {
                Debug.Log($"[SkillEffect] 상태이상 부여 성공: target={target.UnitName}, effect={effectType}, chance={safeChance:0.##}");
            }

            return success;
        }

        // 원본 함수 대응: SkillEffectHelper.ApplyStatusEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)

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
                case StatusEffectType.damage_taken_down:
                case StatusEffectType.defense_up:
                case StatusEffectType.defense_down:
                    context.Target.ApplyStatusEffect(CreateStatusEffectInstance(context));
                    break;
                default:
                    Debug.LogWarning($"[SkillEffect] 지원하지 않는 상태이상 타입: {context.EffectType}");
                    break;
            }
        }

        // 원본 함수 대응: SkillEffectHelper.CreateStatusEffectInstance (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)

        private static StatusEffectInstance CreateStatusEffectInstance(StatusEffectContext context)
        {
            return new StatusEffectInstance
            {
                effectType = context.EffectType,
                category = ResolveStatusEffectCategory(context.EffectType),
                value = context.Value,
                remainingTurns = Mathf.Max(1, context.DurationTurn),
                source = context.Caster
            };
        }

        // 원본 함수 대응: SkillEffectHelper.ResolveStatusEffectCategory (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)

        private static StatusEffectCategory ResolveStatusEffectCategory(StatusEffectType effectType)
        {
            switch (effectType)
            {
                case StatusEffectType.attack_up:
                case StatusEffectType.damage_taken_down:
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
        // 원본 함수 대응: SkillEffectHelper.ResolveReviveHpRatio (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)
        public static float ResolveReviveHpRatio(float skillValue )
        {
            if (skillValue <= 0f)
            {
                return 0.2f;
            }

            return skillValue <= 1f ? Mathf.Clamp01(skillValue) : Mathf.Clamp01(skillValue * 0.01f);
        }


        //스턴
        // 원본 함수 대응: SkillEffectHelper.SetTaunt (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)
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

        // 원본 함수 대응: SkillEffectHelper.SetStun (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)

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

        // 원본 함수 대응: SkillEffectHelper.SetHealBan (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs)

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
