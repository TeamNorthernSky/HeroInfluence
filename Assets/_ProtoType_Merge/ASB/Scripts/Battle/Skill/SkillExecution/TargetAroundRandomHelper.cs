using System.Collections.Generic;
using ASB.Work.BattleGrid;
using UnityEngine;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.BattleGridManager;

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

        /// <summary>
        /// 패턴(중심 제외, 중심과 같은 진영) 칸에 있는 안전한 인질. 플레이어의 공격 스킬일 때만 후보가 된다.
        /// </summary>
        public static List<HostageBattleActor> CollectHostageCandidates(
            BattleCharactor caster,
            SkillData skill,
            ASBGridCell centerCell)
        {
            var hostages = new List<HostageBattleActor>();
            ASBGridManager gridManager = ASBGridManager.Instance;
            if (centerCell == null || gridManager == null || !HostageFriendlyFireResolver.CanTargetHostages(caster, skill))
            {
                return hostages;
            }

            var splashCells = new List<ASBGridCell>();
            CollectSplashCells(centerCell.Coords, skill, gridManager, centerCell, splashCells);
            for (int i = 0; i < splashCells.Count; i++)
            {
                HostageBattleActor hostage = splashCells[i].GetComponentInChildren<HostageBattleActor>(true);
                if (hostage != null && hostage.IsSafe && !hostages.Contains(hostage))
                {
                    hostages.Add(hostage);
                }
            }

            return hostages;
        }

        public static List<T> SelectAdditionalTargets<T>(
            List<T> candidates,
            SkillData skill)
        {
            if (candidates == null || candidates.Count == 0 || skill == null)
            {
                return new List<T>();
            }

            if (skill.multiTargetType != MultiTargetTypeRandom)
            {
                return new List<T>(candidates);
            }

            int pickCount = Mathf.Max(1, skill.multiTargetCount);
            pickCount = Mathf.Min(pickCount, candidates.Count);
            var pool = new List<T>(candidates);
            var picked = new List<T>(pickCount);
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
