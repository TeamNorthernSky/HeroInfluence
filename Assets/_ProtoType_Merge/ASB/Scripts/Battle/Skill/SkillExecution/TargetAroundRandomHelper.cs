using System.Collections.Generic;
using ASB.Work.BattleGrid;
using UnityEngine;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.GridManager;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// TargetAroundRandom: boundary(ClassSkillMultiTarget) 패턴 기준 추가 타겟 해석.
    /// multiTargetType 0=후보 전체, 1=multiTargetCount명 랜덤.
    /// </summary>
    public static class TargetAroundRandomHelper
    {
        public const int MultiTargetTypeAll = 0;
        public const int MultiTargetTypeRandom = 1;

        private static readonly int[] DefaultAdjacentPattern = { 1, 2, 3, 4, 5, 6, 7, 8 };

        public static HashSet<Vector2Int> ResolveSplashCoordinates(Vector2Int centerCoords, SkillData skill)
        {
            List<int> pattern = GetBoundaryPattern(skill);
            HashSet<Vector2Int> coords = SkillTargetingMapper.GetMultiTargetCoordinates(centerCoords, pattern);
            coords.Remove(centerCoords);
            return coords;
        }

        public static void CollectSplashCells(
            Vector2Int centerCoords,
            SkillData skill,
            ASBGridManager gridManager,
            ASBGridCell centerCell,
            List<ASBGridCell> splashCellsOut)
        {
            if (gridManager == null || splashCellsOut == null)
            {
                return;
            }

            bool isTargetEnemySide = centerCoords.x >= 2;
            HashSet<Vector2Int> coords = ResolveSplashCoordinates(centerCoords, skill);
            foreach (Vector2Int coord in coords)
            {
                if (!gridManager.TryGetCell(coord, out ASBGridCell cell) || cell == null)
                {
                    continue;
                }

                if (centerCell != null && cell == centerCell)
                {
                    continue;
                }

                bool isSplashEnemySide = cell.Coords.x >= 2;
                if (isTargetEnemySide != isSplashEnemySide)
                {
                    continue;
                }

                splashCellsOut.Add(cell);
            }
        }

        public static List<BattleCharactor> CollectValidAdditionalTargets(
            BattleCharactor caster,
            BattleCharactor mainTarget,
            SkillData skill,
            ASBGridCell centerCell)
        {
            var validTargets = new List<BattleCharactor>();
            if (caster == null || mainTarget == null || skill == null || centerCell == null)
            {
                return validTargets;
            }

            ASBGridManager gridManager = ASBGridManager.Instance;
            if (gridManager == null)
            {
                return validTargets;
            }

            var seen = new HashSet<BattleCharactor>();
            HashSet<Vector2Int> coords = ResolveSplashCoordinates(centerCell.Coords, skill);
            foreach (Vector2Int coord in coords)
            {
                if (!gridManager.TryGetCell(coord, out ASBGridCell cell) || cell == null)
                {
                    continue;
                }

                BattleCharactor unit = cell.OccupyingUnit;
                if (!IsValidAdditionalTarget(caster, mainTarget, unit, skill) || !seen.Add(unit))
                {
                    continue;
                }

                validTargets.Add(unit);
            }

            return validTargets;
        }

        public static List<BattleCharactor> SelectAdditionalTargets(
            List<BattleCharactor> candidates,
            SkillData skill)
        {
            if (candidates == null || candidates.Count == 0 || skill == null)
            {
                return new List<BattleCharactor>();
            }

            if (skill.multiTargetType != MultiTargetTypeRandom)
            {
                return new List<BattleCharactor>(candidates);
            }

            int pickCount = Mathf.Max(1, skill.multiTargetCount);
            pickCount = Mathf.Min(pickCount, candidates.Count);
            var pool = new List<BattleCharactor>(candidates);
            var picked = new List<BattleCharactor>(pickCount);
            for (int i = 0; i < pickCount; i++)
            {
                int index = Random.Range(0, pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return picked;
        }

        private static List<int> GetBoundaryPattern(SkillData skill)
        {
            if (skill?.boundary != null && skill.boundary.Count > 0)
            {
                return new List<int>(skill.boundary);
            }

            return new List<int>(DefaultAdjacentPattern);
        }

        private static bool IsValidAdditionalTarget(
            BattleCharactor caster,
            BattleCharactor mainTarget,
            BattleCharactor unit,
            SkillData skill)
        {
            if (unit == null || unit.IsDead || unit == mainTarget || caster == null || skill == null)
            {
                return false;
            }

            switch (skill.classSkillEffect)
            {
                case 0:
                    return unit.TeamType != caster.TeamType;
                case 1:
                    return unit.TeamType == caster.TeamType;
                default:
                    return false;
            }
        }
    }
}
