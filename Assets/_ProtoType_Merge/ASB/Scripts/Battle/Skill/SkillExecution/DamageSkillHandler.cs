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
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, 1.0f));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue=1.0)");
        }
    }




    // 단일 공격
    public sealed class DamageSkillHandler : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // 데미지 계산/체력 감소는 BattleManager가 담당합니다.
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue));
            Debug.Log($"[Skill/Damage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }


    // 대상이 높은 체력일수록 데미지 증가
    public sealed class TargetMoreHPMoreDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = (target.CurrentHp / (float)target.MaxHp);
            float snapped = Mathf.Round(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = snapped + skillData.skillValue;

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }



    // HP가 낮을수록 데미지가 증가하는 스킬 핸들러
    public sealed class CasterLowHPMoreDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = (1.0f - caster.CurrentHp / (float)caster.MaxHp);
            float snapped = Mathf.Round(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = snapped + skillData.skillValue;

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }

    // 전방 위치에 있을수록 데미지가 증가하는 스킬 핸들러
    public sealed class TargetFrontPosMoreDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue;

            if (target.IsInFrontRow)
                totalSkillValue += 0.2f; // 예: 전방 위치에 있을 경우 50% 추가 데미지

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }



    public sealed class TargetBackPosMoreDmg : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue;

            if (!target.IsInFrontRow)
                totalSkillValue  = skillData.skillSubValue; // 예: 전방 위치에 있을 경우 50% 추가 데미지

            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }




    ///////////////////////광역공격////////////////////////////////

    // 광역 공격
    public sealed class AoEDamageSkillHandler : BaseAoESkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }


    // 피격된 적 수에 따라 데미지 감소
    public sealed class HitNumLowerDamageHandler : BaseAoESkillHandler
    {

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue + 0.2f * Count;
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }



    //------------------- 단일 + 랜덤
    public sealed class HitTargetAroundRandomHandler : TargetAroundRandom
    {

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }

}
