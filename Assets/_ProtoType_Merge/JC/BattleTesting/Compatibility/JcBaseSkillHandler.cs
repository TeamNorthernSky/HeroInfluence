// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs
// 원본 객체: BaseSkillHandler -> JcBaseSkillHandler
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System.Collections.Generic;
using ASB.Work.BattleGrid;
using UnityEngine;
using ASB.Work.Battle.Core;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.BattleGridManager;

namespace ASB.Work.Battle.SkillExecution
{
    public abstract class JcBaseSkillHandler : ISkillEffectHandler
    {
        // 원본 함수 대응: BaseSkillHandler.Execute (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)
        public virtual SkillExecutionResult Execute(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillData additionalSkillData)
        {
            if (caster == null || target == null || skillData == null || caster.IsDead)
            {
                return SkillExecutionResult.Failed();
            }

            ASBGridManager gm = ASBGridManager.Instance;
            ASBGridCell selectedCell = target.OccupiedCell;
            if (selectedCell == null && gm != null)
            {
                selectedCell = gm.FindCellByUnit(target);
            }

            var context = new JcSkillExecutionContext
            {
                Caster = caster,
                Skill = skillData,
                SelectedTarget = target,
                SelectedCell = selectedCell
            };

            ResolvePrimaryTarget(context);
            ResolveAffectedArea(context);
            ResolveFinalTargets(context);

            if (context.ResolvedTargets == null || context.ResolvedTargets.Count == 0)
            {
                return SkillExecutionResult.Failed();
            }

            var result = SkillExecutionResult.SuccessResult(context.Caster, context.Skill);
            ApplySkill(context, result);
            return result;
        }

        /// <summary>데미지 적용 없이 최종 타격 대상만 해석합니다. 발판 프리뷰용.</summary>
        // 원본 함수 대응: BaseSkillHandler.TryResolvePreviewContext (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)
        public bool TryResolvePreviewContext(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            out JcSkillExecutionContext context)
        {
            context = null;
            if (caster == null || target == null || skillData == null || caster.IsDead)
            {
                return false;
            }

            ASBGridManager gm = ASBGridManager.Instance;
            ASBGridCell selectedCell = target.OccupiedCell;
            if (selectedCell == null && gm != null)
            {
                selectedCell = gm.FindCellByUnit(target);
            }

            context = new JcSkillExecutionContext
            {
                Caster = caster,
                Skill = skillData,
                SelectedTarget = target,
                SelectedCell = selectedCell
            };

            ResolvePrimaryTarget(context);
            ResolveAffectedArea(context);
            ResolveFinalTargets(context);

            return context.ResolvedTargets != null && context.ResolvedTargets.Count > 0;
        }

        // [1] 중심 타겟 해석
        // 원본 함수 대응: BaseSkillHandler.ResolvePrimaryTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)
        protected virtual void ResolvePrimaryTarget(JcSkillExecutionContext context)
        {
            if (context == null || context.Skill == null)
            {
                return;
            }

            JcITargetSelector selector = JcSkillTargetSelectorRegistry.GetSelector(context.Skill.skillKey);
            context.PrimaryTarget = selector.SelectTarget(context) ?? context.SelectedTarget;

            ASBGridManager gm = ASBGridManager.Instance;
            context.PrimaryCell = context.PrimaryTarget != null
                ? context.PrimaryTarget.OccupiedCell ?? (gm != null ? gm.FindCellByUnit(context.PrimaryTarget) : null)
                : null;
        }

        // [2] AoE 확장 (단일 기본 구현)
        // 원본 함수 대응: BaseSkillHandler.ResolveAffectedArea (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)
        protected virtual void ResolveAffectedArea(JcSkillExecutionContext context)
        {
            if (context == null || context.PrimaryCell == null)
            {
                return;
            }

            if (!context.ResolvedCells.Contains(context.PrimaryCell))
            {
                context.ResolvedCells.Add(context.PrimaryCell);
            }
        }

        // [3] 최종 유닛 확정
        // 원본 함수 대응: BaseSkillHandler.ResolveFinalTargets (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)
        protected virtual void ResolveFinalTargets(JcSkillExecutionContext context)
        {
            if (context == null || context.ResolvedCells == null)
            {
                return;
            }

            var dedupe = new HashSet<BattleCharactor>();
            for (int i = 0; i < context.ResolvedCells.Count; i++)
            {
                ASBGridCell cell = context.ResolvedCells[i];
                if (cell == null)
                {
                    continue;
                }

                BattleCharactor unit = cell.OccupyingUnit;
                if (!IsValidTarget(context, unit))
                {
                    continue;
                }

                if (dedupe.Add(unit))
                {
                    context.ResolvedTargets.Add(unit);
                }
            }
        }

        // 원본 함수 대응: BaseSkillHandler.IsValidTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)

        protected virtual bool IsValidTarget(JcSkillExecutionContext context, BattleCharactor unit)
        {
            if (context == null || context.Caster == null || context.Skill == null || unit == null)
            {
                return false;
            }

            switch (context.Skill.classSkillEffect)
            {
                case 0: // Attack
                    return !unit.IsDead && unit.IsPlayer != context.Caster.IsPlayer;
                case 1: // Heal
                    return !unit.IsDead && unit.IsPlayer == context.Caster.IsPlayer;
                case 2: // Revive
                    return unit.IsDead && unit.IsPlayer == context.Caster.IsPlayer;
                case 3: // Buff(버프): 아군 대상
                    return !unit.IsDead && unit.IsPlayer == context.Caster.IsPlayer;
                case 4: // Debuff(디버프): 적 대상
                    return !unit.IsDead && unit.IsPlayer != context.Caster.IsPlayer;
                default:
                    return false;
            }
        }

        protected abstract void ApplySkill(JcSkillExecutionContext context, SkillExecutionResult result);
    }


    // 단일 대상 공격은 이걸 사용함!!
    public abstract class JcBaseSingleSkillHandler : JcBaseSkillHandler
    {
        // 원본 함수 대응: BaseSkillHandler.ApplySkill (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)
        protected override void ApplySkill(JcSkillExecutionContext context, SkillExecutionResult result)
        {
            if (context == null || context.Caster == null || context.Skill == null || context.ResolvedTargets == null)
            {
                return;
            }

            for (int i = 0; i < context.ResolvedTargets.Count; i++)
            {
                BattleCharactor target = context.ResolvedTargets[i];
                if (target == null)
                {
                    continue;
                }

                if (context.Skill.classSkillEffect == 0)
                {
                    ApplyAdditionaDamage(context.Caster, target, context.Skill, result);
                }
                else if (context.Skill.classSkillEffect == 1 || context.Skill.classSkillEffect == 2)
                {
                    ApplyHeal(context.Caster, target, context.Skill, result);
                }

                ApplyAdditionalEffect(context.Caster, target, context.Skill, result);
            }
        }

        // 추가 효과 구현
        // 원본 함수 대응: BaseSkillHandler.ApplyAdditionalEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)
        protected virtual void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result) { }
        // 추가 데미지 구현
        protected virtual void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result) { }
        // 힐 구현
        protected virtual void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result) { }
    }



    // 2개 이상의 광역 공격은 이걸 사용함!!
    public abstract class JcBaseAoESkillHandler : JcBaseSkillHandler
    {
        protected override void ResolveAffectedArea(JcSkillExecutionContext context)
        {
            if (context == null || context.Skill == null || context.PrimaryCell == null)
            {
                return;
            }

            if (context.Skill.classSkillTarget == 0 && !HeroSkillRules.IsFamily(context.Skill, 1030) && context.Skill.skillKey != "HCS005" && context.Skill.skillKey != "HCS002")
            {
                base.ResolveAffectedArea(context);
                return;
            }

            HashSet<Vector2Int> coords;
            if (HeroSkillRules.IsFamily(context.Skill, 2030))
            {
                coords = SkillTargetingMapper.GetFullSideBoardCoordinates(context.PrimaryCell.Coords);
                coords.RemoveWhere(c => c.x != context.PrimaryCell.Coords.x);
            }
            else if (HeroSkillRules.IsFamily(context.Skill, 1030))
            {
                coords = new HashSet<Vector2Int> { context.PrimaryCell.Coords };
                if (context.PrimaryTarget.IsInFrontRow)
                    coords.Add(context.PrimaryCell.Coords + new Vector2Int(context.Caster.IsPlayer ? 1 : -1, 0));
            }
            else if (context.Skill.classSkillTarget == 2 || context.Skill.skillKey == "HCS005" || context.Skill.skillKey == "HCS002")
            {
                coords = SkillTargetingMapper.GetFullSideBoardCoordinates(context.PrimaryCell.Coords);
            }
            else
            {
                List<int> pattern = BuildPatternIncludingCenter(context.Skill.boundary);
                coords = SkillTargetingMapper.GetMultiTargetCoordinates(context.PrimaryCell.Coords, pattern);
            }

            if (coords == null || coords.Count == 0)
            {
                return;
            }

            ASBGridManager gm = ASBGridManager.Instance;
            if (gm == null)
            {
                return;
            }

            bool isTargetEnemySide = context.PrimaryCell.Coords.x >= 2;
            foreach (Vector2Int coord in coords)
            {
                if (!gm.TryGetCell(coord, out ASBGridCell cell) || cell == null)
                {
                    continue;
                }

                // 중심 타겟 보드와 같은 쪽만 유지
                bool isSplashEnemySide = cell.Coords.x >= 2;
                if (isTargetEnemySide != isSplashEnemySide)
                {
                    continue;
                }

                if (!context.ResolvedCells.Contains(cell))
                {
                    context.ResolvedCells.Add(cell);
                }
            }
        }

        // 원본 함수 대응: BaseSkillHandler.ApplySkill (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)

        protected override void ApplySkill(JcSkillExecutionContext context, SkillExecutionResult result)
        {
            if (context == null || context.Caster == null || context.Skill == null || context.ResolvedTargets == null)
            {
                return;
            }

            bool? sharedCrit = null;
            if (context.Skill.classSkillEffect == 0)
            {
                var rollCtx = new DamageContext { Caster = context.Caster };
                sharedCrit = JcCombatCalculator.RollCritical(rollCtx);
            }

            int count = context.ResolvedTargets.Count;
            for (int i = 0; i < count; i++)
            {
                BattleCharactor target = context.ResolvedTargets[i];
                if (target == null)
                {
                    continue;
                }

                if (context.Skill.classSkillEffect == 0)
                {
                    ApplyAdditionaDamage(context.Caster, target, context.Skill, count, result, sharedCrit);
                }
                else if (context.Skill.classSkillEffect == 1 || context.Skill.classSkillEffect == 2)
                {
                    ApplyHeal(context.Caster, target, context.Skill, count, result);
                }

                ApplyAdditionalEffect(context.Caster, target, context.Skill, count, result);
            }
        }

        // 원본 함수 대응: BaseSkillHandler.ApplyAdditionalEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)

        protected virtual void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result) { }
        protected virtual void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result, bool? sharedIsCritical = null) { }
        protected virtual void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result) { }

        private static List<int> BuildPatternIncludingCenter(List<int> sourcePattern)
        {
            List<int> pattern = sourcePattern != null ? new List<int>(sourcePattern) : new List<int>();
            if (!pattern.Contains(0))
            {
                pattern.Insert(0, 0);
            }

            return pattern;
        }
    }


    // 메인 타겟 타격 후 boundary(ClassSkillMultiTarget) 패턴 내 추가 타겟
    public abstract class JcTargetAroundRandom : ISkillEffectHandler
    {
        // 원본 함수 대응: BaseSkillHandler.Execute (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)
        public SkillExecutionResult Execute(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillData additionalSkillData)
        {
            if (caster == null || target == null || skillData == null || caster.IsDead)
            {
                return SkillExecutionResult.Failed();
            }

            var result = SkillExecutionResult.SuccessResult(caster, skillData);

            int mainBefore = result.DamageContexts != null ? result.DamageContexts.Count : 0;
            ApplyMainEffect(caster, target, skillData, result);
            // 주 타깃으로 생성된 컨텍스트는 명시적으로 Primary.
            if (result.DamageContexts != null)
            {
                for (int k = mainBefore; k < result.DamageContexts.Count; k++)
                {
                    if (result.DamageContexts[k] != null)
                    {
                        result.DamageContexts[k].Role = ASB.Work.Battle.Core.DamageRole.Primary;
                    }
                }
            }

            ASB.Work.BattleGrid.BattleGridManager gridManager = ASB.Work.BattleGrid.BattleGridManager.Instance;
            if (gridManager == null)
            {
                return SkillExecutionResult.Failed();
            }

            ASB.Work.BattleGrid.GridCell centerCell = target.OccupiedCell ?? gridManager.FindCellByUnit(target);
            if (centerCell == null)
            {
                return SkillExecutionResult.Failed();
            }

            List<BattleCharactor> candidates = JcTargetAroundRandomHelper.CollectValidAdditionalTargets(
                caster, target, skillData, centerCell);
            List<BattleCharactor> selectedTargets = JcTargetAroundRandomHelper.SelectAdditionalTargets(candidates, skillData);
            for (int i = 0; i < selectedTargets.Count; i++)
            {
                BattleCharactor extraTarget = selectedTargets[i];
                if (extraTarget == null)
                {
                    continue;
                }

                int addBefore = result.DamageContexts != null ? result.DamageContexts.Count : 0;

                if (skillData.classSkillEffect == 0)
                {
                    ApplyAdditionaDamage(caster, extraTarget, skillData, result);
                }
                else if (skillData.classSkillEffect == 1)
                {
                    ApplyHeal(caster, extraTarget, skillData, result);
                }

                ApplyAdditionalEffect(caster, extraTarget, skillData, result);

                // 추가 타깃으로 생성된 컨텍스트는 명시적으로 Additional.
                if (result.DamageContexts != null)
                {
                    for (int k = addBefore; k < result.DamageContexts.Count; k++)
                    {
                        if (result.DamageContexts[k] != null)
                        {
                            result.DamageContexts[k].Role = ASB.Work.Battle.Core.DamageRole.Additional;
                        }
                    }
                }
            }

            return result;
        }

        // 원본 함수 대응: BaseSkillHandler.ApplyMainEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)

        protected virtual void ApplyMainEffect(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skillData,
            SkillExecutionResult result)
        {
            if (skillData.classSkillEffect == 0)
            {
                result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(
                    caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));
            }
            else if (skillData.classSkillEffect == 1)
            {
                ApplyHeal(caster, target, skillData, result);
            }
        }

        // 추가 효과 구현
        // 원본 함수 대응: BaseSkillHandler.ApplyAdditionalEffect (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs)
        protected virtual void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
        }

        // 추가 데미지 구현
        protected virtual void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
        }

        // 메인/추가 힐 구현
        protected virtual void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
        }
    }
}



