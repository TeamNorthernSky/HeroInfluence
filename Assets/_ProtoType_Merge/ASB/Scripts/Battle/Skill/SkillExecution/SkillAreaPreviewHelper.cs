using System.Collections.Generic;
using ASB.Work.BattleGrid;
using UnityEngine;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.BattleGridManager;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 스킬 공격 범위 좌표 패턴 기준으로 하이라이트할 발판을 해석합니다.
    /// 플레이어 AoE 프리뷰·자동전투·적 AI 타깃 표시에 공통 사용합니다.
    /// </summary>
    public static class SkillAreaPreviewHelper
    {
        public static bool IsFullSideAttack(SkillData skill) =>
            skill != null && skill.classSkillTarget == 2;

        /// <summary>
        /// 범위 공격: 중심 Main + 범위 Additional.
        /// 전체 공격(classSkillTarget==2): 범위 전체 Main.
        /// </summary>
        public static void ApplyAreaHighlights(
            SkillData skill,
            ASBGridCell mainCell,
            List<ASBGridCell> splashCells,
            List<ASBGridCell> highlightedCellsOut,
            ref ASBGridCell highlightedMainTargetCellOut)
        {
            if (highlightedCellsOut == null)
            {
                return;
            }

            if (IsFullSideAttack(skill))
            {
                highlightedMainTargetCellOut = null;
                ApplyMainHighlight(mainCell, highlightedCellsOut);
                for (int i = 0; i < splashCells.Count; i++)
                {
                    ApplyMainHighlight(splashCells[i], highlightedCellsOut);
                }

                return;
            }

            for (int i = 0; i < splashCells.Count; i++)
            {
                ASBGridCell cell = splashCells[i];
                if (cell == null)
                {
                    continue;
                }

                cell.SetAdditionalHighlight();
                highlightedCellsOut.Add(cell);
            }

            if (mainCell != null)
            {
                mainCell.SetMainTargetHighlight();
                highlightedMainTargetCellOut = mainCell;
            }
        }

        private static void ApplyMainHighlight(ASBGridCell cell, List<ASBGridCell> highlightedCellsOut)
        {
            if (cell == null)
            {
                return;
            }

            cell.SetMainTargetHighlight();
            highlightedCellsOut.Add(cell);
        }

        public static bool TryGetAreaCells(
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

            if (SkillExecutionRegistry.TryGetHandler(skill.skillKey, out ISkillEffectHandler handler)
                && handler is TargetAroundRandom)
            {
                return TryResolveTargetAroundRandomArea(selectedTarget, skill, gridManager, out mainCell, splashCells);
            }

            ASBGridCell centerCell = ResolveCenterCell(caster, selectedTarget, skill, gridManager);
            if (centerCell == null)
            {
                return false;
            }

            if (IsSingleTargetSkill(skill))
            {
                mainCell = centerCell;
                return true;
            }

            HashSet<Vector2Int> hitCoords = ResolveHitCoordinates(skill, centerCell);
            if (hitCoords == null || hitCoords.Count == 0)
            {
                return false;
            }

            CollectSplashCellsFromCoords(hitCoords, centerCell, gridManager, splashCells);
            mainCell = centerCell;
            return true;
        }

        private static ASBGridCell ResolveCenterCell(
            BattleCharactor caster,
            BattleCharactor selectedTarget,
            SkillData skill,
            ASBGridManager gridManager)
        {
            var previewContext = new SkillExecutionContext
            {
                Caster = caster,
                Skill = skill,
                SelectedTarget = selectedTarget,
                SelectedCell = selectedTarget.OccupiedCell ?? gridManager.FindCellByUnit(selectedTarget)
            };

            ITargetSelector selector = SkillTargetSelectorRegistry.GetSelector(skill.skillKey);
            BattleCharactor primaryTarget = selector.SelectTarget(previewContext) ?? selectedTarget;
            if (primaryTarget == null)
            {
                return null;
            }

            return primaryTarget.OccupiedCell ?? gridManager.FindCellByUnit(primaryTarget);
        }

        private static bool IsSingleTargetSkill(SkillData skill)
        {
            return skill.classSkillTarget == 0
                   || (skill.classSkillTarget != 2
                       && (skill.boundary == null || skill.boundary.Count == 0));
        }

        private static HashSet<Vector2Int> ResolveHitCoordinates(SkillData skill, ASBGridCell centerCell)
        {
            if (skill.classSkillTarget == 2)
            {
                return SkillTargetingMapper.GetFullSideBoardCoordinates(centerCell.Coords);
            }

            List<int> previewPattern = BuildPatternIncludingCenter(skill.boundary);
            return SkillTargetingMapper.GetMultiTargetCoordinates(centerCell.Coords, previewPattern);
        }

        private static void CollectSplashCellsFromCoords(
            HashSet<Vector2Int> hitCoords,
            ASBGridCell centerCell,
            ASBGridManager gridManager,
            List<ASBGridCell> splashCells)
        {
            bool isTargetEnemySide = centerCell.Coords.x >= 2;
            foreach (Vector2Int coord in hitCoords)
            {
                if (!gridManager.TryGetCell(coord, out ASBGridCell cell) || cell == null)
                {
                    continue;
                }

                bool isSplashEnemySide = cell.Coords.x >= 2;
                if (isTargetEnemySide != isSplashEnemySide)
                {
                    continue;
                }

                if (cell == centerCell)
                {
                    continue;
                }

                splashCells.Add(cell);
            }
        }

        private static bool TryResolveTargetAroundRandomArea(
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

            TargetAroundRandomHelper.CollectSplashCells(mainCell.Coords, skill, gridManager, mainCell, splashCells);
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
}
