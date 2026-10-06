// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs
// 원본 객체: DebuffSkillHandler -> JcDebuffSkillHandler
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using UnityEngine;
using System;
using System.IO;

namespace ASB.Work.Battle.SkillExecution
{
    //디버프 스킬 핸들러
    //싱글 공격일 경우 : JcBaseSingleSkillHandler
    //광역 공격일 경우 : JcBaseAoESkillHandler

    public sealed class JcBleeedSkillHandler : JcBaseSingleSkillHandler
    {
        private const float BleedChance = 0.35f;

        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/BleedStrike] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");

            bool bleedApplied = JcSkillEffectHelper.TryApplyStatusEffect(target, StatusEffectType.bleed, BleedChance);
            if (bleedApplied)
            {
                result.AddStatusEffect(caster, target, StatusEffectType.bleed, 1);
                Debug.Log($"[Skill/BleedStrike] 출혈 부여 예약 (성공, 대상={target.UnitName})");
            }
        }
    }

    // 도발배기_1010
    public sealed class JcTauntStrikeSkillHandler : JcBaseSingleSkillHandler
    {
        private const int TauntDurationTurns = 1;


        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)


        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/TauntStrike] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");

            result.AddStatusEffect(caster, target, StatusEffectType.taunt, TauntDurationTurns);
            Debug.Log($"[Skill/TauntStrike] Taunt reserved: target={target.UnitName}, source={caster.UnitName}, turns={TauntDurationTurns}");
        }

    }



    // 일렬배기_1020
    public sealed class JcColumnsStrikeSkillHandler : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/ColumnsStrike] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }



    // 강한 공격 이후 1턴 쉼
    public sealed class JcAtkAfterRest : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/JcAtkAfterRest] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }


        // 추가 효과 구현
        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionalEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)
        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, caster, StatusEffectType.stun, 1);
            Debug.Log($"[Skill/JcAtkAfterRest] Stun reserved: target={caster.UnitName}, source={caster.UnitName}, turns=1");
        }
    }


    // 단일 공격 후 힐 밴
    public sealed class JcTargetHealBanSkill : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/JcTargetHealBanSkill] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }


        // 추가 효과 구현
        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionalEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)
        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, target, StatusEffectType.healBan, 100);
            Debug.Log($"[Skill/JcTargetHealBanSkill] reserved: target={target.UnitName}, source={caster.UnitName}");
        }
    }



    //--------------------- 광역 도발

    // 광역 도발
    public sealed class JcAoETauntStrikeSkillHandler : JcBaseAoESkillHandler
    {
        private const int TauntDurationTurns = 1;

        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData,
            int count, SkillExecutionResult result, bool? sharedIsCritical = null)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue,
                skillData.skillIndex, skillData.classSkillRange, sharedIsCritical: sharedIsCritical));
        }

        // 추가 효과: 타겟에게 도발 부여
        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionalEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)
        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, target, StatusEffectType.taunt, TauntDurationTurns);
            Debug.Log($"[Skill/TauntStrike] Taunt reserved: target={target.UnitName}, source={caster.UnitName}, turns={TauntDurationTurns}");
        }
    }


    // 받피감 추가 감소 (방어 버프) - 무기 스킬 HCS003
    // WeaponSkillEffect=3(버프) 전제. 대상(아군)에게 defense_up을 부여해 받는 피해를 줄인다.
    public sealed class JcDamageTakenReductionSkillHandler : JcBaseSingleSkillHandler
    {
        private const int BuffDurationTurns = 1;

        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionalEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)

        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, caster, StatusEffectType.damage_taken_down, BuffDurationTurns, skillData.skillValue);
            Debug.Log($"[Skill/DamageTakenReduction] defense_up reserved: target={target.UnitName}, source={caster.UnitName}, turns={BuffDurationTurns}");
        }
    }


    // 방어력 감소 (디버프) - 무기 스킬 HCS005
    // WeaponSkillEffect=4(디버프) 전제. 대상(적)에게 defense_down을 부여한다.
    public sealed class JcDefenseDownSkillHandler : JcBaseAoESkillHandler
    {
        private const int DebuffDurationTurns = 1;

        // 원본 함수 대응: DebuffSkillHandler.ApplyAdditionalEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs)

        protected override void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, int count, SkillExecutionResult result)
        {
            result?.AddStatusEffect(caster, target, StatusEffectType.defense_down, DebuffDurationTurns, skillData.skillValue);
            Debug.Log($"[Skill/DefenseDown] defense_down reserved: target={target.UnitName}, source={caster.UnitName}, turns={DebuffDurationTurns}");
        }
    }


}
