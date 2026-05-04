using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 메인 타겟 셀을 중심으로 패턴 인덱스에 해당하는 좌표의 적에게 동일 배율의 표준 피해를 적용한다.
    /// (전역 <see cref="GridManager"/>와 구분하기 위해 ASB.Work.BattleGrid 타입을 전부 한정한다.)
    /// </summary>
    public sealed class AoEDamageSkillHandler : ISkillEffectHandler
    {
        private static readonly int[] DefaultFullPattern = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };

        public bool Execute(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillData additionalSkillData)
        {
            if (caster == null || target == null || skillData == null)
            {
                return false;
            }

            if (caster.IsDead)
            {
                return false;
            }

            ASB.Work.BattleGrid.GridCell centerCell = target.OccupiedCell;
            if (centerCell == null && ASB.Work.BattleGrid.GridManager.Instance != null)
            {
                centerCell = ASB.Work.BattleGrid.GridManager.Instance.FindCellByUnit(target);
            }

            if (centerCell == null)
            {
                Debug.LogWarning("[Skill/AoE] 중심 셀을 찾지 못했습니다.");
                return false;
            }

            Vector2Int centerCoords = centerCell.Coords;

            IEnumerable<int> pattern = skillData.aoePatternIndices != null && skillData.aoePatternIndices.Count > 0
                ? skillData.aoePatternIndices
                : DefaultFullPattern;

            HashSet<Vector2Int> hitCoords = ASB.Work.BattleGrid.SkillTargetingMapper.GetMultiTargetCoordinates(centerCoords, pattern);
            if (hitCoords == null || hitCoords.Count == 0)
            {
                return false;
            }

            int hits = 0;
            if (ASB.Work.BattleGrid.GridManager.Instance == null)
            {
                Debug.LogWarning("[Skill/AoE] GridManager.Instance가 없습니다.");
                return false;
            }

            foreach (Vector2Int coord in hitCoords)
            {
                if (!ASB.Work.BattleGrid.GridManager.Instance.TryGetCell(coord, out ASB.Work.BattleGrid.GridCell cell) || cell == null)
                {
                    continue;
                }

                BattleCharactor hitUnit = cell.OccupyingUnit;
                if (hitUnit == null || hitUnit.IsDead)
                {
                    continue;
                }

                if (hitUnit.TeamType == caster.TeamType)
                {
                    continue;
                }

                SkillEffectHelper.ApplyStandardDamage(caster, hitUnit, skillData.skillValue);
                hits++;
                // TODO: 거리(맨해튼 등) 기반 데미지 감쇠
            }

            Debug.Log($"[Skill/AoE] {caster.UnitName} center={centerCoords} hits={hits}");
            return hits > 0;
        }
    }
}
