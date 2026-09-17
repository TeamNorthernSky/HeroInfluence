using UnityEngine;
using System;
using System.IO;

namespace ASB.Work.Battle.SkillExecution
{
    //디버프 스킬 핸들러
    //싱글 공격일 경우 : BaseSingleSkillHandler
    //광역 공격일 경우 : BaseAoESkillHandler

    public sealed class BleeedSkillHandler : BaseSingleSkillHandler
    {
        private const float BleedChance = 0.35f;

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/BleedStrike] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");

            bool bleedApplied = SkillEffectHelper.TryApplyStatusEffect(target, StatusEffectType.bleed, BleedChance);
            if (bleedApplied)
            {
                result.AddStatusEffect(caster, target, StatusEffectType.bleed, 1);
                Debug.Log($"[Skill/BleedStrike] 출혈 부여 예약 (성공, 대상={target.UnitName})");
            }
        }
    }

    // 도발배기_1010
    public sealed class TauntStrikeSkillHandler : BaseSingleSkillHandler
    {
        private const int TauntDurationTurns = 1;


        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/TauntStrike] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");

            result.AddStatusEffect(caster, target, StatusEffectType.taunt, TauntDurationTurns);
            Debug.Log($"[Skill/TauntStrike] Taunt reserved: target={target.UnitName}, source={caster.UnitName}, turns={TauntDurationTurns}");
        }

    }



    // 일렬배기_1020
    public sealed class ColumnsStrikeSkillHandler : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/ColumnsStrike] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }



    // 강한 공격 이후 1턴 쉼
    public sealed class AtkAfterRest : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/AtkAfterRest] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }


        // 추가 효과 구현
        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, caster, StatusEffectType.stun, 1);
            Debug.Log($"[Skill/AtkAfterRest] Stun reserved: target={caster.UnitName}, source={caster.UnitName}, turns=1");
        }
    }


    // 단일 공격 후 힐 밴
    public sealed class TargetHealBanSkill : BaseSingleSkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/TargetHealBanSkill] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }


        // 추가 효과 구현
        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, target, StatusEffectType.healBan, 100);
            Debug.Log($"[Skill/TargetHealBanSkill] reserved: target={target.UnitName}, source={caster.UnitName}");
        }
    }



    //--------------------- 광역 도발

    // 광역 도발
    public sealed class AoETauntStrikeSkillHandler : BaseAoESkillHandler
    {
        private const int TauntDurationTurns = 1;

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData,
            int count, SkillExecutionResult result, bool? sharedIsCritical = null)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue,
                skillData.skillIndex, skillData.classSkillRange, sharedIsCritical: sharedIsCritical));
        }

        // 추가 효과: 타겟에게 도발 부여
        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, target, StatusEffectType.taunt, TauntDurationTurns);
            Debug.Log($"[Skill/TauntStrike] Taunt reserved: target={target.UnitName}, source={caster.UnitName}, turns={TauntDurationTurns}");
        }
    }


    // 받피감 추가 감소 (방어 버프) - 무기 스킬 HCS003
    // WeaponSkillEffect=3(버프) 전제. 대상(아군)에게 defense_up을 부여해 받는 피해를 줄인다.
    public sealed class DamageTakenReductionSkillHandler : BaseSingleSkillHandler
    {
        private const int BuffDurationTurns = 1;

        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, caster, StatusEffectType.damage_taken_down, BuffDurationTurns, skillData.skillValue);
            Debug.Log($"[Skill/DamageTakenReduction] defense_up reserved: target={target.UnitName}, source={caster.UnitName}, turns={BuffDurationTurns}");
        }
    }


    // 방어력 감소 (디버프) - 무기 스킬 HCS005
    // WeaponSkillEffect=4(디버프) 전제. 대상(적)에게 defense_down을 부여한다.
    public sealed class DefenseDownSkillHandler : BaseAoESkillHandler
    {
        private const int DebuffDurationTurns = 1;

        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, int count, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, target, StatusEffectType.defense_down, DebuffDurationTurns, skillData.skillValue);
            Debug.Log($"[Skill/DefenseDown] defense_down reserved: target={target.UnitName}, source={caster.UnitName}, turns={DebuffDurationTurns}");
        }
    }


}
