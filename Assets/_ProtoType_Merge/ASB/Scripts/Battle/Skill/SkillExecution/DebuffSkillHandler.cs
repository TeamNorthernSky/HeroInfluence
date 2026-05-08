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

            // 데미지 적용 이후(사망 여부 포함)에 출혈을 부여합니다.
            result.OnPostExecution += (totalDamage) =>
            {
                if (target == null || target.IsDead)
                {
                    return;
                }

                bool bleedApplied = SkillEffectHelper.TryApplyStatusEffect(target, "Bleed", BleedChance);
                if (bleedApplied)
                {
                    Debug.Log($"[Skill/BleedStrike] 출혈 부여 시도 (성공, 대상={target.UnitName})");
                }
            };
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

            // 도발 부여는 데미지 적용 이후(사망 여부 포함)에 결정합니다.
            result.OnPostExecution += (totalDamage) =>
            {
                if (target == null || target.IsDead)
                {
                    return;
                }

                SkillEffectHelper.SetTaunt(caster, target, TauntDurationTurns);
                Debug.Log($"[Skill/TauntStrike] Taunt applied: target={target.UnitName}, source={caster.UnitName}, turns={TauntDurationTurns}");
            };
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
        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData)
        {
            SkillEffectHelper.SetStun(caster, caster, 1); // 자신에게 1턴 스턴 적용
            Debug.Log($"[Skill/AtkAfterRest] Stun applied: target={caster.UnitName}, source={caster.UnitName}, turns=1");
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
        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData)
        {
            SkillEffectHelper.SetHealBan(caster, target, 100); // 대상에게 1턴 힐 밴 적용
            Debug.Log($"[Skill/TargetHealBanSkill] applied: target={caster.UnitName}, source={caster.UnitName}");
        }
    }



    //--------------------- 광역 도발

    // 광역 도발
    public sealed class AoETauntStrikeSkillHandler : BaseAoESkillHandler
    {
        private const int TauntDurationTurns = 1;

        // 추가 효과: 타겟에게 도발 부여
        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count)
        {
            SkillEffectHelper.SetTaunt(caster, target, TauntDurationTurns);
            Debug.Log($"[Skill/TauntStrike] Taunt applied: target={target.UnitName}, source={caster.UnitName}, turns={TauntDurationTurns}");
        }
    }


}
