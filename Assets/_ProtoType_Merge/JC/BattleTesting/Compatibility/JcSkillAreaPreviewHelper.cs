// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs
// 원본 객체: SkillAreaPreviewHelper -> JcSkillAreaPreviewHelper
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
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
    public static class JcSkillAreaPreviewHelper
    {
        /// <summary>실행 핸들러가 무작위 보조 대상을 고르는 경우 후보 범위는 표시하지 않습니다.</summary>
        // 원본 함수 대응: SkillAreaPreviewHelper.HasRandomSecondaryTargets (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)
        public static bool HasRandomSecondaryTargets(SkillData skill) => skill != null &&
            JcSkillExecutionRegistry.TryGetHandler(skill.skillKey, out ISkillEffectHandler handler) && handler is JcTargetAroundRandom;

        // 원본 함수 대응: SkillAreaPreviewHelper.IsFullSideAttack (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

        public static bool IsFullSideAttack(SkillData skill) =>
            skill != null && skill.classSkillTarget == 2;

        /// <summary>
        /// 범위 공격: 중심 Main + 범위 Additional.
        /// 전체 공격(classSkillTarget==2): 범위 전체 Main.
        /// </summary>
        // 원본 함수 대응: SkillAreaPreviewHelper.ApplyAreaHighlights (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)
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

            if (ASBGridManager.Instance != null && ASBGridManager.Instance.UsesBattleTilePresentation)
            {
                highlightedMainTargetCellOut = mainCell;
                ApplyMainHighlight(mainCell, highlightedCellsOut);
                if (!HasRandomSecondaryTargets(skill) && splashCells != null)
                    foreach (var cell in splashCells) ApplyMainHighlight(cell, highlightedCellsOut);
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

        // 원본 함수 대응: SkillAreaPreviewHelper.ApplyMainHighlight (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

        private static void ApplyMainHighlight(ASBGridCell cell, List<ASBGridCell> highlightedCellsOut)
        {
            if (cell == null)
            {
                return;
            }

            cell.SetMainTargetHighlight();
            highlightedCellsOut.Add(cell);
        }

        // 원본 함수 대응: SkillAreaPreviewHelper.TryGetAreaCells (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

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

            if (JcSkillExecutionRegistry.TryGetHandler(skill.skillKey, out ISkillEffectHandler handler)
                && handler is JcTargetAroundRandom)
            {
                if (gridManager.UsesBattleTilePresentation)
                    return TryResolveSingleTargetCell(selectedTarget, gridManager, out mainCell);
                return TryResolveTargetAroundRandomArea(selectedTarget, skill, gridManager, out mainCell, splashCells);
            }

            ASBGridCell centerCell = ResolveCenterCell(caster, selectedTarget, skill, gridManager);
            if (centerCell == null)
            {
                return false;
            }

            if (HeroSkillRules.IsFamily(skill, 4040))
            {
                mainCell = centerCell;
                int enemyX = caster.IsPlayer ? 2 : 0;
                foreach (var coord in SkillTargetingMapper.GetFullSideBoardCoordinates(new Vector2Int(enemyX, 0)))
                    if (gridManager.TryGetCell(coord, out var cell) && cell != null && cell != mainCell) splashCells.Add(cell);
                return true;
            }
            if (IsSingleTargetSkill(skill) && !HeroSkillRules.IsFamily(skill, 1030) && skill.skillKey != "HCS005" && skill.skillKey != "HCS002")
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

        // 원본 함수 대응: SkillAreaPreviewHelper.ResolveCenterCell (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

        private static ASBGridCell ResolveCenterCell(
            BattleCharactor caster,
            BattleCharactor selectedTarget,
            SkillData skill,
            ASBGridManager gridManager)
        {
            var previewContext = new JcSkillExecutionContext
            {
                Caster = caster,
                Skill = skill,
                SelectedTarget = selectedTarget,
                SelectedCell = selectedTarget.OccupiedCell ?? gridManager.FindCellByUnit(selectedTarget)
            };

            JcITargetSelector selector = JcSkillTargetSelectorRegistry.GetSelector(skill.skillKey);
            BattleCharactor primaryTarget = selector.SelectTarget(previewContext) ?? selectedTarget;
            if (primaryTarget == null)
            {
                return null;
            }

            return primaryTarget.OccupiedCell ?? gridManager.FindCellByUnit(primaryTarget);
        }

        // 원본 함수 대응: SkillAreaPreviewHelper.IsSingleTargetSkill (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

        private static bool IsSingleTargetSkill(SkillData skill)
        {
            return skill.classSkillTarget == 0
                   || (skill.classSkillTarget != 2
                       && (skill.boundary == null || skill.boundary.Count == 0));
        }

        // 원본 함수 대응: SkillAreaPreviewHelper.ResolveHitCoordinates (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

        private static HashSet<Vector2Int> ResolveHitCoordinates(SkillData skill, ASBGridCell centerCell)
        {
            if (HeroSkillRules.IsFamily(skill, 2030))
            {
                var column = SkillTargetingMapper.GetFullSideBoardCoordinates(centerCell.Coords);
                column.RemoveWhere(c => c.x != centerCell.Coords.x);
                return column;
            }
            if (HeroSkillRules.IsFamily(skill, 1030))
            {
                var cells = new HashSet<Vector2Int> { centerCell.Coords };
                if (centerCell.OccupyingUnit != null && centerCell.OccupyingUnit.IsInFrontRow)
                    cells.Add(centerCell.Coords + new Vector2Int(centerCell.Coords.x >= 2 ? 1 : -1, 0));
                return cells;
            }
            if (skill.classSkillTarget == 2 || skill.skillKey == "HCS005" || skill.skillKey == "HCS002")
            {
                return SkillTargetingMapper.GetFullSideBoardCoordinates(centerCell.Coords);
            }

            List<int> previewPattern = BuildPatternIncludingCenter(skill.boundary);
            return SkillTargetingMapper.GetMultiTargetCoordinates(centerCell.Coords, previewPattern);
        }

        // 원본 함수 대응: SkillAreaPreviewHelper.CollectSplashCellsFromCoords (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

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

        // 원본 함수 대응: SkillAreaPreviewHelper.TryResolveTargetAroundRandomArea (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

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

            JcTargetAroundRandomHelper.CollectSplashCells(mainCell.Coords, skill, gridManager, mainCell, splashCells);
            return true;
        }

        // 원본 함수 대응: SkillAreaPreviewHelper.TryResolveSingleTargetCell (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

        private static bool TryResolveSingleTargetCell(
            BattleCharactor target,
            ASBGridManager gridManager,
            out ASBGridCell mainCell)
        {
            mainCell = target.OccupiedCell ?? gridManager.FindCellByUnit(target);
            return mainCell != null;
        }

        // 원본 함수 대응: SkillAreaPreviewHelper.BuildPatternIncludingCenter (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs)

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
