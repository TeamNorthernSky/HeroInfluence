using System.Collections.Generic;
using ASB.Work.BattleGrid;
using UnityEngine;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.GridManager;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 스킬 실행 파이프라인과 동일한 규칙으로 실제 타격 대상 유닛의 발판 좌표를 해석합니다.
    /// </summary>
    public static class SkillHitPreviewResolver
    {
        private sealed class DefaultPreviewSkillHandler : BaseAoESkillHandler
        {
            public static readonly DefaultPreviewSkillHandler Instance = new DefaultPreviewSkillHandler();

            protected override void ApplyAdditionaDamage(
                BattleCharactor caster,
                BattleCharactor target,
                SkillData skillData,
                int count,
                SkillExecutionResult result)
            {
            }

            protected override void ApplyHeal(
                BattleCharactor caster,
                BattleCharactor target,
                SkillData skillData,
                int count,
                SkillExecutionResult result)
            {
            }
        }

        public static bool TryGetHitUnitCells(
            BattleCharactor caster,
            BattleCharactor selectedTarget,
            SkillData skill,
            out ASBGridCell mainCell,
            out List<ASBGridCell> splashCells)
        {
            mainCell = null;
            splashCells = new List<ASBGridCell>();

            if (selectedTarget == null)
            {
                return false;
            }

            ASBGridManager gridManager = ASBGridManager.Instance;
            if (gridManager == null)
            {
                return false;
            }

            if (caster == null || skill == null)
            {
                return TryResolveSingleTargetCell(selectedTarget, gridManager, out mainCell);
            }

            if (SkillExecutionRegistry.TryGetHandler(skill.skillIndex, out ISkillEffectHandler handler))
            {
                if (handler is BaseSkillHandler baseHandler
                    && baseHandler.TryResolvePreviewContext(caster, selectedTarget, skill, out SkillExecutionContext context))
                {
                    CollectCellsFromResolvedTargets(context, gridManager, out mainCell, splashCells);
                    return mainCell != null || splashCells.Count > 0;
                }

                if (handler is TargetAroundRandom)
                {
                    return TryResolveTargetAroundRandom(caster, selectedTarget, skill, gridManager, out mainCell, splashCells);
                }
            }
            else if (DefaultPreviewSkillHandler.Instance.TryResolvePreviewContext(caster, selectedTarget, skill, out SkillExecutionContext context))
            {
                CollectCellsFromResolvedTargets(context, gridManager, out mainCell, splashCells);
                return mainCell != null || splashCells.Count > 0;
            }

            return TryResolveSingleTargetCell(selectedTarget, gridManager, out mainCell);
        }

        private static void CollectCellsFromResolvedTargets(
            SkillExecutionContext context,
            ASBGridManager gridManager,
            out ASBGridCell mainCell,
            List<ASBGridCell> splashCells)
        {
            mainCell = context?.PrimaryCell;
            if (mainCell == null && context?.PrimaryTarget != null)
            {
                mainCell = context.PrimaryTarget.OccupiedCell ?? gridManager.FindCellByUnit(context.PrimaryTarget);
            }

            if (context?.ResolvedTargets == null)
            {
                return;
            }

            var seenCells = new HashSet<ASBGridCell>();
            if (mainCell != null)
            {
                seenCells.Add(mainCell);
            }

            for (int i = 0; i < context.ResolvedTargets.Count; i++)
            {
                BattleCharactor unit = context.ResolvedTargets[i];
                if (unit == null)
                {
                    continue;
                }

                ASBGridCell cell = unit.OccupiedCell ?? gridManager.FindCellByUnit(unit);
                if (cell == null || !seenCells.Add(cell))
                {
                    continue;
                }

                splashCells.Add(cell);
            }

            if (mainCell != null)
            {
                splashCells.Remove(mainCell);
            }
            else if (splashCells.Count > 0)
            {
                mainCell = splashCells[0];
                splashCells.RemoveAt(0);
            }
        }

        private static bool TryResolveTargetAroundRandom(
            BattleCharactor caster,
            BattleCharactor target,
            SkillData skill,
            ASBGridManager gridManager,
            out ASBGridCell mainCell,
            List<ASBGridCell> splashCells)
        {
            mainCell = target.OccupiedCell ?? gridManager.FindCellByUnit(target);
            if (mainCell == null)
            {
                return false;
            }

            int range = Mathf.Max(0, skill.multiTargetCount);
            var seenCells = new HashSet<ASBGridCell> { mainCell };

            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    if (x == 0 && y == 0)
                    {
                        continue;
                    }

                    Vector2Int checkCoord = mainCell.Coords + new Vector2Int(x, y);
                    if (!gridManager.TryGetCell(checkCoord, out ASBGridCell cell) || cell == null)
                    {
                        continue;
                    }

                    BattleCharactor aroundUnit = cell.OccupyingUnit;
                    if (aroundUnit == null
                        || aroundUnit.IsDead
                        || aroundUnit.TeamType == caster.TeamType
                        || aroundUnit == target
                        || !seenCells.Add(cell))
                    {
                        continue;
                    }

                    splashCells.Add(cell);
                }
            }

            return true;
        }

        private static bool TryResolveSingleTargetCell(
            BattleCharactor target,
            ASBGridManager gridManager,
            out ASBGridCell mainCell)
        {
            mainCell = target.OccupiedCell ?? gridManager.FindCellByUnit(target);
            return mainCell != null;
        }
    }
}
