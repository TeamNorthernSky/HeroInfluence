// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs
// 원본 객체: HealSkillHandler -> JcHealSkillHandler
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using UnityEngine;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 기본 힐 블록만 호출
    /// </summary>

    public sealed class JcHealSkillHandler : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: HealSkillHandler.ApplyHeal (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)
        protected override void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float heal = JcSkillEffectHelper.CalculateStandardHealAmount(skillData.skillKey == "HCS004" ? caster.MaxHp * skillData.skillValue : skillData.skillValue);
            result.AddHeal(caster, target, heal, skillData != null ? skillData.skillIndex : 0);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }

    public sealed class JcTargetLowerHPMoreHeal : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: HealSkillHandler.ApplyHeal (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)
        protected override void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 15의 추가 힐량을 얻음
            float hpRatio = (1.0f - target.CurrentHp / (float)target.MaxHp);
            float snapped = Mathf.Round(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = Mathf.Clamp(snapped * 15 + 5, 5, 50);
            float heal = JcSkillEffectHelper.CalculateStandardHealAmount(totalSkillValue);
            result.AddHeal(caster, target, heal, skillData != null ? skillData.skillIndex : 0);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }



    // 타겟 HP 비례 힐: 타깃에 HP에 비례해서 힐을 함
    public sealed class JcTargetHPPerHeal : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: HealSkillHandler.ApplyHeal (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)
        protected override void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float totalSkillValue = Mathf.Ceil(caster.MaxHp * Mathf.Clamp01(skillData.skillValue));
            float heal = JcSkillEffectHelper.CalculateStandardHealAmount(totalSkillValue);
            result.AddHeal(caster, target, heal, skillData != null ? skillData.skillIndex : 0);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }


    //부활 스킬
    /// <summary>
    /// 4020: Deals normal damage, then restores HP to the living allied unit with
    /// randomly selected living ally by the actual damage dealt.
    /// </summary>
    public sealed class JcHolyBulletHpRecoveryHandler : JcBaseSingleSkillHandler
    {
        // 원본 함수 대응: HealSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)
        protected override void ApplyAdditionaDamage(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(
                caster,
                target,
                skillData.skillValue,
                skillData.skillIndex,
                skillData.classSkillRange));

            result.OnPostExecution += totalDamageDealt =>
            {
                float healAmount = Mathf.Max(0f, totalDamageDealt);
                if (healAmount <= 0f)
                    return;

                BattleCharactor healTarget = FindRandomLivingAlly(caster);
                if (healTarget != null)
                {
                    healTarget.ApplyHeal(healAmount);
                    // 공격 후 실제로 회복한 아군에게 기존 단일 치유 표시를 재사용합니다.
                    JcBattleManager.Instance?.VisualDirector?.PlayHitEffect(
                        healTarget, skillData.skillIndex == 4021 ? 4011 : 4010);
                }
            };
        }

        // 원본 함수 대응: HealSkillHandler.FindRandomLivingAlly (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)

        private static BattleCharactor FindRandomLivingAlly(BattleCharactor caster)
        {
            var allies = new System.Collections.Generic.List<BattleCharactor>();
            foreach (var unit in Object.FindObjectsByType<BattleCharactor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (unit != null && !unit.IsDead && unit.IsPlayer == caster.IsPlayer) allies.Add(unit);
            return allies.Count > 0 ? allies[JC.BattleTesting.JcBattleRandom.Range(0, allies.Count)] : null;
        }
    }
    /// <summary>
    /// 4040: 죽은 아군 1명을 전투당 1회 부활(20%)시키고, 적 전체에 데미지.
    /// 부활 대상이 없으면 부활을 건너뛰고 적 전체 공격만 한다.
    /// 연출은 한 번의 시전으로 AttackPrepare(ClassSkill_4=부활) → Attack(WeaponSkill_3=전체공격)을 재생한다.
    /// 부활 아군에 이펙트를 꽂기 위해 caster.PendingReviveTarget을 설정한다(SpawnAnchor.ReviveTarget이 참조).
    /// AoE 핸들러로 두어 적 데미지가 동시(RunAoESkillSequence) 연출된다.
    /// </summary>
    public sealed class JcRebirthSkillHandler : JcBaseAoESkillHandler
    {
        // 입력 게이트와 같은 판정을 사용해, 부활 가능한데 전체 공격만 고르는 우회를 차단합니다.
        // 원본 함수 대응: HealSkillHandler.Execute (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)
        public override SkillExecutionResult Execute(BattleCharactor caster, BattleCharactor target, SkillData skill, SkillData additional)
        {
            if (caster == null || caster.IsDead || skill == null || target == null ||
                !TargetingHelper.GetValidTargetsForSkillData(caster, skill).Contains(target))
                return SkillExecutionResult.Failed();
            var result = SkillExecutionResult.SuccessResult(caster, skill);
            ApplySkill(new JcSkillExecutionContext { Caster = caster, Skill = skill, SelectedTarget = target }, result);
            return result;
        }

        // 원본 함수 대응: HealSkillHandler.ApplySkill (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)

        protected override void ApplySkill(JcSkillExecutionContext context, SkillExecutionResult result)
        {
            var caster = context.Caster;
            var skill = context.Skill;
            caster.PendingReviveTarget = null;
            if (SkillActivationRules.RequiresReviveTarget(caster, skill))
            {
                caster.PendingReviveTarget = context.SelectedTarget;
                result.AddRevive(caster, context.SelectedTarget, skill.skillSubValue, skill.skillIndex);
            }
            foreach (var enemy in Object.FindObjectsByType<BattleCharactor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (enemy != null && !enemy.IsDead && enemy.IsPlayer != caster.IsPlayer)
                    result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, enemy, skill.skillValue,
                        skill.skillIndex, skill.classSkillRange));
        }
    }
    public sealed class JcHealTargetAroundRandomHandler : JcTargetAroundRandom
    {
        // 원본 함수 대응: HealSkillHandler.ApplyMainEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)
        protected override void ApplyMainEffect(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            AddCasterBasedHeal(caster, target, skillData, result, skillData.skillValue);
        }

        // 원본 함수 대응: HealSkillHandler.ApplyHeal (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)

        protected override void ApplyHeal(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            AddCasterBasedHeal(caster, target, skillData, result, skillData.skillSubValue);
        }

        // 원본 함수 대응: HealSkillHandler.AddCasterBasedHeal (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)

        private static void AddCasterBasedHeal(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result, float coefficient)
        {
            float heal = caster.MaxHp * coefficient;
            result.AddHeal(caster, target, heal, skillData.skillIndex);
            Debug.Log($"[Skill/TargetAroundRandomHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }
    /// <summary>
    /// 광역 흡혈 스킬:
    /// - 범위 내 각 대상에게 데미지를 주고
    /// - 이번 스킬로 가한 총 데미지 합만큼 시전자를 회복합니다.
    /// </summary>
    public sealed class JcAoEVampiricSkillHandler : JcBaseAoESkillHandler
    {
        // 원본 함수 대응: HealSkillHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs)
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result, bool? sharedIsCritical = null)
        {
            // 데미지 적용/계산은 BattleManager로만 중앙화합니다.
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange, sharedIsCritical: sharedIsCritical));
            Debug.Log($"[Skill/AoEVampiric] hit {caster.UnitName} -> {target.UnitName} (skillValue={skillData.skillValue:F2})");

            // 모든 DamageContext 적용이 끝난 직후, 총 피해량만큼 흡혈 회복합니다.
            if (result.OnPostExecution == null)
            {
                result.OnPostExecution = (totalDamageDealt) =>
                {
                    caster.ApplyHeal(totalDamageDealt);
                    Debug.Log($"[Skill/AoEVampiric] heal caster={caster.UnitName} totalHeal={totalDamageDealt:F1}");
                };
            }
        }
    }

}
