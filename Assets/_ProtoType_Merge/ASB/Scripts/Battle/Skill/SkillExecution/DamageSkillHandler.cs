using ASB.Work.Battle.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.Battle.SkillExecution
{
    // 데미지 스킬 핸들러
    //싱글 공격일 경우 : BaseSingleSkillHandler
    //광역 공격일 경우 : BaseAoESkillHandler


    //---------------------- 단일 공격!!!


    // 기본 데미지 스킬 핸들러 (스킬값 무시, 단순 데미지 블록만 호출)
    public sealed class DefaultDamageSkillHandler : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, 1.0f, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue=1.0)");
        }
    }


    // 단일 공격
    public sealed class DamageSkillHandler : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // 데미지 계산/체력 감소는 BattleManager가 담당합니다.
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/Damage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }


    // 치명타 확률 증가 버전
    public sealed class MoreCriticDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange, false, false, 0.2f));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }





    //-------------------------------체력 관련 조건부 데미지 핸들러

    // 대상이 높은 체력일수록 데미지 증가
    public sealed class TargetMoreHPMoreDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = (target.CurrentHp / (float)target.MaxHp);
            float snapped = Mathf.Floor(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = snapped + skillData.skillValue;

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }


    // 대상이 낮은 체력일수록 데미지 증가
    public sealed class TargetLowerHPMoreDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = 1.0f - (target.CurrentHp / (float)target.MaxHp);
            float snapped = Mathf.Floor(hpRatio / 0.1f) * 0.1f;
            float totalSkillValue = snapped + skillData.skillValue;

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }


    // 대상이 70% 이하 체력이라면 치명타 확률 증가
    public sealed class TargetLowerHPMoreCriticDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = (target.CurrentHp / (float)target.MaxHp);
            float additionalCritRate = 0f;

            if (hpRatio <= 0.7f)
                additionalCritRate = 0.2f; // 대상의 체력이 70% 이하라면 치명타 확률 20% 증가

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange, false, false, additionalCritRate));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }




    // 시전자의HP가 낮을수록 데미지가 증가하는 스킬 핸들러
    public sealed class CasterLowHPMoreDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = (1.0f - caster.CurrentHp / (float)caster.MaxHp);
            float snapped = Mathf.Floor(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = snapped + skillData.skillValue;

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }



    // --------------------------------------위치 관련 조건부 데미지 핸들러

    // 전방 위치에 있을수록 데미지가 증가하는 스킬 핸들러
    public sealed class TargetFrontPosMoreDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue;

            if (target.IsInFrontRow)
                totalSkillValue += 0.2f; // 예: 전방 위치에 있을 경우 50% 추가 데미지

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }


    // 후방 위치에 있을수록 데미지가 증가하는 스킬 핸들러
    public sealed class TargetBackPosMoreDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue;

            if (!target.IsInFrontRow)
                totalSkillValue = skillData.skillSubValue; // 예: 후방 위치에 있을 경우 50% 추가 데미지

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }


    // 타겟이 후방 위치에 있다면, 일시적으로 치명타 확률이 증가하는 스킬 핸들러
    public sealed class TargetBackPosMoreCriticDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue;
            float additionalCritRate = 0f;
            if (!target.IsInFrontRow)
                additionalCritRate = 0.2f; // 예: 후방 위치에 있을 경우 치명타 확률 20% 증가

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange, false, false, additionalCritRate));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }





    /// 생존 아군 1명 + 생존 적군 1명(1v1)일 때, 이번에 추가된 단일 스킬의 배율만 2.0배 보정합니다.
    public sealed class DuelistSkillHandler : BaseSingleSkillHandler
    {
        private const float DuelDamageMultiplier = 2.0f;

        protected override void ApplyAdditionaDamage(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            if (result == null) return;

            // 1. 배율 결정 (기본은 1.0f)
            float finalMultiplier = 1.0f;

            BattleFlowManager flowManager = UnityEngine.Object.FindFirstObjectByType<BattleFlowManager>();
            if (flowManager != null)
            {
                // 2. 적과 아군이 각각 1명씩만 남았는지 체크
                bool isDuel = flowManager.GetAlivePlayerCount() == 1 && flowManager.GetAliveEnemyCount() == 1;
                if (isDuel)
                {
                    finalMultiplier = 2.0f; // 조건 만족 시 2배로 변경
                }
            }

            // 3. 데미지 추가 (평소에는 skillData.skillValue * 1.0이 들어감)
            int countBefore = result.DamageContexts != null ? result.DamageContexts.Count : 0;

            // 기본 수치에 결정된 배율을 곱해서 적용
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue * finalMultiplier, skillData.skillIndex, skillData.classSkillRange));

        }
    }




    // 두 번 공격하는 스킬 핸들러
    public sealed class DoubleAttackSkillHandler : ISkillEffectHandler
    {
        private const float FirstHitDelaySeconds = 0.5f;

        public SkillExecutionResult Execute(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillData additionalSkillData)
        {
            if (caster == null || target == null || skillData == null)
            {
                return SkillExecutionResult.Failed();
            }

            if (target.IsDead || target.CurrentHp <= 0f)
            {
                return SkillExecutionResult.Failed();
            }

            float hitMultiplier = Mathf.Max(0.01f, skillData.skillValue * 0.5f);

            var hit1 = new DamageContext
            {
                Caster = caster,
                Target = target,
                SkillMultiplier = hitMultiplier,
                SkillIndex = skillData.skillIndex,
                IsRangedAttack = skillData.classSkillRange > 0,
                CanTriggerCounter = false,
                IsCounterAttack = false,
                DelayAfter = FirstHitDelaySeconds
            };
            hit1.IsCritical = CombatCalculator.RollCritical(hit1);

            var hit2 = new DamageContext
            {
                Caster = caster,
                Target = target,
                SkillMultiplier = hitMultiplier,
                SkillIndex = skillData.skillIndex,
                IsRangedAttack = skillData.classSkillRange > 0,
                CanTriggerCounter = false,
                IsCounterAttack = false,
                DelayAfter = 0f
            };
            hit2.IsCritical = CombatCalculator.RollCritical(hit2);

            return SkillExecutionResult.SuccessResult()
                .AddDamage(hit1)
                .AddDamage(hit2);
        }
    }



    ///////////////////////광역공격////////////////////////////////

    // 광역 공격
    public sealed class AoEDamageSkillHandler : BaseAoESkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }


    // 피격된 적 수에 따라 데미지 감소
    public sealed class HitNumLowerDamageHandler : BaseAoESkillHandler
    {

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue + 0.2f * Count;
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }



    //------------------- 단일 + 랜덤
    public sealed class HitTargetAroundRandomHandler : TargetAroundRandom
    {

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange, true));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }

}
