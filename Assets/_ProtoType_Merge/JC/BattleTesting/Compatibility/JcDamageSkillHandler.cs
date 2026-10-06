// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs
// 원본 객체: DamageSkillHandler -> JcDamageSkillHandler
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using ASB.Work.Battle.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.Battle.SkillExecution
{
    // 데미지 스킬 핸들러
    //싱글 공격일 경우 : JcBaseSingleSkillHandler
    //광역 공격일 경우 : JcBaseAoESkillHandler


    //---------------------- 단일 공격!!!


    // 기본 데미지 스킬 핸들러 (스킬값 무시, 단순 데미지 블록만 호출)
    public sealed class JcDefaultDamageSkillHandler : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, 1.0f, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue=1.0)");
        }
    }


    // 단일 공격
    public sealed class JcDamageSkillHandler : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // 데미지 계산/체력 감소는 BattleManager가 담당합니다.
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/Damage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }


    // 치명타 확률 증가 버전
    public sealed class JcMoreCriticDmg : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange, false, false, 0.2f));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }





    //-------------------------------체력 관련 조건부 데미지 핸들러

    // 대상이 높은 체력일수록 데미지 증가
    public sealed class JcTargetMoreHPMoreDmg : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = (target.CurrentHp / (float)target.MaxHp);
            float snapped = Mathf.Floor(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = snapped + skillData.skillValue;

            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }


    // 대상이 낮은 체력일수록 데미지 증가
    public sealed class JcTargetLowerHPMoreDmg : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = 1.0f - (target.CurrentHp / (float)target.MaxHp);
            int steps = Mathf.Clamp(Mathf.FloorToInt(hpRatio * 10f + 0.0001f), 0, 9);
            float bonus = HeroSkillRules.IsFamily(skillData, 3040) ? skillData.skillSubValue : 0.1f;
            float totalSkillValue = skillData.skillValue + steps * bonus;

            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }


    // 대상이 70% 이하 체력이라면 치명타 확률 증가
    public sealed class JcTargetLowerHPMoreCriticDmg : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = (target.CurrentHp / (float)target.MaxHp);
            float additionalCritRate = 0f;

            if (hpRatio <= 0.7f)
                additionalCritRate = 0.2f; // 대상의 체력이 70% 이하라면 치명타 확률 20% 증가

            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange, false, false, additionalCritRate));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }




    // 시전자의HP가 낮을수록 데미지가 증가하는 스킬 핸들러
    public sealed class JcCasterLowHPMoreDmg : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 스킬값에 더함
            float hpRatio = (1.0f - caster.CurrentHp / (float)caster.MaxHp);
            float snapped = Mathf.Floor(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = snapped + skillData.skillValue;

            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }



    // --------------------------------------위치 관련 조건부 데미지 핸들러

    // 전방 위치에 있을수록 데미지가 증가하는 스킬 핸들러
    public sealed class JcTargetFrontPosMoreDmg : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue;

            // 전열 대상에만 추가 배율. 추가량은 데이터(ClassSkillSubValueLv*)에서 온다.
            // 중괄호가 없어 두 줄이 모두 실행되던 버그를 수정: 전열이면 2배가 붙고 후열에도 붙었다.
            if (target.IsInFrontRow)
            {
                totalSkillValue += skillData.skillSubValue;
            }

            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }


    // 후방 위치에 있을수록 데미지가 증가하는 스킬 핸들러
    public sealed class JcTargetBackPosMoreDmg : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue;

            if (!target.IsInFrontRow)
                totalSkillValue = skillData.skillSubValue; // 예: 후방 위치에 있을 경우 50% 추가 데미지

            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }


    // 타겟이 후방 위치에 있다면, 일시적으로 치명타 확률이 증가하는 스킬 핸들러
    public sealed class JcTargetBackPosMoreCriticDmg : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = skillData.skillValue;
            float additionalCritRate = 0f;
            if (!target.IsInFrontRow)
                additionalCritRate = 0.2f; // 예: 후방 위치에 있을 경우 치명타 확률 20% 증가

            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange, false, false, additionalCritRate));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }





    /// 생존 아군 1명 + 생존 적군 1명(1v1)일 때, 이번에 추가된 단일 스킬의 배율만 2.0배 보정합니다.
    public sealed class JcDuelistSkillHandler : JcBaseSingleSkillHandler
    {
        private const float DuelDamageMultiplier = 2.0f;

        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)

        protected override void ApplyAdditionaDamage(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            if (result == null) return;

            // 1. 배율 결정 (기본은 1.0f)
            float finalMultiplier = 1.0f;

            JcBattleFlowManager flowManager = UnityEngine.Object.FindFirstObjectByType<JcBattleFlowManager>();
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
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue * finalMultiplier, skillData.skillIndex, skillData.classSkillRange));

        }
    }




    // 두 번 공격하는 스킬 핸들러
    public sealed class JcDoubleAttackSkillHandler : ISkillEffectHandler
    {
        private const float FirstHitDelaySeconds = 0.5f;

        // 원본 함수 대응: DamageSkillHandler.Execute (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)

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

            float hitMultiplier = Mathf.Max(0.01f, skillData.skillValue);

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
            hit1.IsCritical = JcCombatCalculator.RollCritical(hit1);

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
            hit2.IsCritical = JcCombatCalculator.RollCritical(hit2);

            return SkillExecutionResult.SuccessResult(caster, skillData)
                .AddDamage(hit1)
                .AddDamage(hit2);
        }
    }



    ///////////////////////광역공격////////////////////////////////

    // 광역 공격
    public sealed class JcAoEDamageSkillHandler : JcBaseAoESkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result, bool? sharedIsCritical = null)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange, sharedIsCritical: sharedIsCritical));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");
        }
    }


    public sealed class JcPiercingDashSkillHandler : JcBaseAoESkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplySkill (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplySkill(JcSkillExecutionContext context, SkillExecutionResult result)
        {
            foreach (var target in context.ResolvedTargets)
            {
                float value = target == context.PrimaryTarget ? context.Skill.skillValue : context.Skill.skillSubValue;
                result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(context.Caster, target, value,
                    context.Skill.skillIndex, context.Skill.classSkillRange));
            }
        }
    }

    public sealed class JcPrismExplosionSkillHandler : JcBaseAoESkillHandler
    {
        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData,
            int count, SkillExecutionResult result, bool? sharedIsCritical = null)
        {
            float value = skillData.skillValue + (target.CurrentHp < target.MaxHp * 0.5f ? skillData.skillSubValue : 0f);
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, value, skillData.skillIndex,
                skillData.classSkillRange, sharedIsCritical: sharedIsCritical));
        }
    }

    // 피격된 적 수에 따라 데미지 감소
    public sealed class JcHitNumLowerDamageHandler : JcBaseAoESkillHandler
    {
        /// <summary>
        /// 배율 하한. 대상이 아무리 많아도 여기까지만 깎인다.
        /// 데이터에 대응 컬럼이 없어 임시값이며, 기획 수치 확정 시 교체할 것.
        /// </summary>
        private const float MinTotalSkillValue = 0.1f;

        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result, bool? sharedIsCritical = null)
        {
            // 첫 대상은 감소 없음. 초과 대상 1명마다 데이터(ClassSkillSubValueLv*)만큼 배율이 깎인다.
            // 기존 코드는 부호가 반대라 대상이 많을수록 1인당 피해가 '증가'했다(총 피해가 제곱으로 폭증).
            int extraTargets = Mathf.Max(0, Count - 1);
            float totalSkillValue = Mathf.Max(
                MinTotalSkillValue,
                skillData.skillValue - skillData.skillSubValue * extraTargets);
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, totalSkillValue, skillData.skillIndex, skillData.classSkillRange, sharedIsCritical: sharedIsCritical));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (skillValue={totalSkillValue:F2})");
        }
    }



    //------------------- 단일 + 랜덤
    public sealed class JcHitTargetAroundRandomHandler : JcTargetAroundRandom
    {
        private static readonly HashSet<int> MissingSubValueWarnings = new HashSet<int>();

        // 원본 함수 대응: DamageSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs)

        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float multiplier = skillData.skillSubValue;
            if (multiplier <= 0f)
            {
                if (MissingSubValueWarnings.Add(skillData.skillIndex))
                    Debug.LogWarning($"[Skill/DefaultDamage] Missing skillSubValue; using skillValue. " +
                        $"key={skillData.skillKey}, index={skillData.skillIndex}");
                multiplier = skillData.skillValue;
            }
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target,
                multiplier, skillData.skillIndex, skillData.classSkillRange, true));
            Debug.Log($"[Skill/DefaultDamage] {caster.UnitName} -> {target.UnitName} (multiplier={multiplier:F2})");
        }
    }

}
