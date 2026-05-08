using System.Collections.Generic;
using ASB.Work.BattleGrid;
using UnityEngine;
using ASB.Work.Battle.Core;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.GridManager;

namespace ASB.Work.Battle.SkillExecution
{
    public abstract class BaseSkillHandler : ISkillEffectHandler
    {
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

            var context = new SkillExecutionContext
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

            var result = SkillExecutionResult.SuccessResult();
            ApplySkill(context, result);
            return result;
        }

        // [1] 중심 타겟 해석
        protected virtual void ResolvePrimaryTarget(SkillExecutionContext context)
        {
            if (context == null || context.Skill == null)
            {
                return;
            }

            ITargetSelector selector = SkillTargetSelectorRegistry.GetSelector(context.Skill.skillIndex);
            context.PrimaryTarget = selector.SelectTarget(context) ?? context.SelectedTarget;

            ASBGridManager gm = ASBGridManager.Instance;
            context.PrimaryCell = context.PrimaryTarget != null
                ? context.PrimaryTarget.OccupiedCell ?? (gm != null ? gm.FindCellByUnit(context.PrimaryTarget) : null)
                : null;
        }

        // [2] AoE 확장 (단일 기본 구현)
        protected virtual void ResolveAffectedArea(SkillExecutionContext context)
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
        protected virtual void ResolveFinalTargets(SkillExecutionContext context)
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

        protected virtual bool IsValidTarget(SkillExecutionContext context, BattleCharactor unit)
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
                default:
                    return false;
            }
        }

        protected abstract void ApplySkill(SkillExecutionContext context, SkillExecutionResult result);
    }


    // 단일 대상 공격은 이걸 사용함!!
    public abstract class BaseSingleSkillHandler : BaseSkillHandler
    {
        protected override void ApplySkill(SkillExecutionContext context, SkillExecutionResult result)
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

                ApplyAdditionalEffect(context.Caster, target, context.Skill);
            }
        }

        // 추가 효과 구현
        protected virtual void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData) { }
        // 추가 데미지 구현
        protected virtual void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result) { }
        // 힐 구현
        protected virtual void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result) { }
    }



    // 2개 이상의 광역 공격은 이걸 사용함!!
    public abstract class BaseAoESkillHandler : BaseSkillHandler
    {
        protected override void ResolveAffectedArea(SkillExecutionContext context)
        {
            if (context == null || context.Skill == null || context.PrimaryCell == null)
            {
                return;
            }

            if (context.Skill.classSkillTarget == 0)
            {
                return;
            }

            List<int> pattern = BuildPatternIncludingCenter(context.Skill.boundary);
            HashSet<Vector2Int> coords = SkillTargetingMapper.GetMultiTargetCoordinates(context.PrimaryCell.Coords, pattern);
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

        protected override void ApplySkill(SkillExecutionContext context, SkillExecutionResult result)
        {
            if (context == null || context.Caster == null || context.Skill == null || context.ResolvedTargets == null)
            {
                return;
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
                    ApplyAdditionaDamage(context.Caster, target, context.Skill, count, result);
                }
                else if (context.Skill.classSkillEffect == 1 || context.Skill.classSkillEffect == 2)
                {
                    ApplyHeal(context.Caster, target, context.Skill, count, result);
                }

                ApplyAdditionalEffect(context.Caster, target, context.Skill, count);
            }
        }

        protected virtual void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count) { }
        protected virtual void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, int Count, SkillExecutionResult result) { }
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


    // 메인 타겟 타격 후 주변 1칸 랜덤 1명 타깃
    public abstract class TargetAroundRandom : ISkillEffectHandler
    {
        public SkillExecutionResult Execute(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillData additionalSkillData)
        {
            if (caster == null || target == null || skillData == null || caster.IsDead)
            {
                return SkillExecutionResult.Failed();
            }

            var result = SkillExecutionResult.SuccessResult();
            // 메인 타격 데미지는 항상 result에 추가합니다.
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(caster, target, skillData.skillValue, skillData.skillIndex, skillData.classSkillRange));

            ASB.Work.BattleGrid.GridManager gridManager = ASB.Work.BattleGrid.GridManager.Instance;
            if (gridManager == null)
            {
                return SkillExecutionResult.Failed();
            }

            ASB.Work.BattleGrid.GridCell centerCell = target.OccupiedCell ?? gridManager.FindCellByUnit(target);
            if (centerCell == null)
            {
                return SkillExecutionResult.Failed();
            }

            int range = Mathf.Max(0, skillData.multiTargetCount);
            List<BattleCharactor> validTargets = new List<BattleCharactor>();

            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    if (x == 0 && y == 0)
                    {
                        continue;
                    }

                    Vector2Int checkCoord = centerCell.Coords + new Vector2Int(x, y);
                    if (!gridManager.TryGetCell(checkCoord, out ASB.Work.BattleGrid.GridCell cell) || cell == null)
                    {
                        continue;
                    }

                    BattleCharactor aroundUnit = cell.OccupyingUnit;
                    if (aroundUnit == null || aroundUnit.IsDead || aroundUnit.TeamType == caster.TeamType || aroundUnit == target)
                    {
                        continue;
                    }

                    // 리스트에 담기
                    validTargets.Add(aroundUnit);
                }
            }
            if (validTargets.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, validTargets.Count);
                BattleCharactor luckyTarget = validTargets[randomIndex];

                if (skillData.classSkillEffect == 0)
                {
                    ApplyAdditionaDamage(caster, luckyTarget, skillData, result);
                }
                if (skillData.classSkillEffect == 1)
                {
                    ApplyHeal(caster, luckyTarget, skillData, result);
                }

                ApplyAdditionalEffect(caster, luckyTarget, skillData);
            }

            return result;
        }


        // 추가 효과 구현
        protected virtual void ApplyAdditionalEffect(BattleCharactor caster, BattleCharactor target, SkillData skillData)
        {
        }

        // 추가 데미지 구현
        protected virtual void ApplyAdditionaDamage(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
        }

        // 힐 구현
        protected virtual void ApplyHeal(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillExecutionResult result)
        {
        }
    }
}



