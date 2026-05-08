using UnityEngine;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 기본 힐 블록만 호출
    /// </summary>

    public sealed class HealSkillHandler : BaseSingleSkillHandler
    {
        protected override void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float heal = SkillEffectHelper.ApplyStandardHeal(caster, target, skillData.skillValue);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }

    public sealed class TargetLowerHPMoreHeal : BaseSingleSkillHandler
    {
        protected override void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            // HP 비율을 20% 단위로 끊어서 15의 추가 힐량을 얻음
            float hpRatio = (1.0f - target.CurrentHp / (float)target.MaxHp);
            float snapped = Mathf.Round(hpRatio / 0.2f) * 0.2f;
            float totalSkillValue = Mathf.Clamp(snapped * 15 + 5, 5, 50);
            float heal = SkillEffectHelper.ApplyStandardHeal(caster, target, totalSkillValue);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }


    //부활 스킬
    public sealed class RebirthSkillHandler : BaseSingleSkillHandler
    {
        protected override void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            if (!target.IsDead)
            {
                return;
            }
            //float ratio = SkillEffectHelper.ResolveReviveHpRatio(skillData.skillValue);
            target.Revive(0.2f);
            Debug.Log($"[Skill/Rebirth] {caster.UnitName} -> {target.UnitName} hpRatio={0.2f:0.###}");
        }
    }

    // 타깃 + 랜덤 주변힐
    public sealed class HealTargetAroundRandomHandler : TargetAroundRandom
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
            float heal = SkillEffectHelper.ApplyStandardHeal(caster, target, skillData.skillValue);
            Debug.Log($"[Skill/DefaultHeal] {caster.UnitName} -> {target.UnitName} heal={heal:F1}");
        }
    }

    /// <summary>
    /// 광역 흡혈 스킬:
    /// - 범위 내 각 대상에게 데미지를 주고
    /// - 이번 스킬로 가한 총 데미지 합만큼 시전자를 회복합니다.
    /// </summary>
    public sealed class AoEVampiricSkillHandler : BaseAoESkillHandler
    {
        protected override void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result)
        {
            // 데미지 적용/계산은 BattleManager로만 중앙화합니다.
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
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
