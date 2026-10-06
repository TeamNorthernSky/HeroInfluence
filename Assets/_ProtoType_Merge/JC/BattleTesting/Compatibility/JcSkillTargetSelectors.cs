// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillTargetSelectors.cs
// 원본 객체: SkillTargetSelectors -> JcSkillTargetSelectors
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System;
using System.Collections.Generic;
using ASB.Work.BattleGrid;
using UnityEngine;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.BattleGridManager;

namespace ASB.Work.Battle.SkillExecution
{
    public sealed class JcDefaultTargetSelector : JcITargetSelector
    {
        public static readonly JcDefaultTargetSelector Instance = new JcDefaultTargetSelector();
        private JcDefaultTargetSelector() { }

        // 원본 함수 대응: SkillTargetSelectors.SelectTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillTargetSelectors.cs)

        public BattleCharactor SelectTarget(JcSkillExecutionContext context)
        {
            return context != null ? context.SelectedTarget : null;
        }
    }

    public sealed class JcLowestHpSelector : JcITargetSelector
    {
        public static readonly JcLowestHpSelector Instance = new JcLowestHpSelector();
        private JcLowestHpSelector() { }

        // 원본 함수 대응: SkillTargetSelectors.SelectTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillTargetSelectors.cs)

        public BattleCharactor SelectTarget(JcSkillExecutionContext context)
        {
            if (context == null || context.Caster == null)
            {
                return null;
            }

            BattleCharactor[] all = UnityEngine.Object.FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            BattleCharactor best = null;
            float bestHp = float.MaxValue;
            bool attackSkill = context.Skill != null && context.Skill.classSkillEffect == 0;
            bool healSkill = context.Skill != null && context.Skill.classSkillEffect == 1;
            bool reviveSkill = context.Skill != null && context.Skill.classSkillEffect == 2;

            for (int i = 0; i < all.Length; i++)
            {
                BattleCharactor unit = all[i];
                if (unit == null)
                {
                    continue;
                }

                if (attackSkill)
                {
                    if (unit.IsDead || unit.IsPlayer == context.Caster.IsPlayer) { continue; }
                }
                else if (healSkill)
                {
                    if (unit.IsDead || unit.IsPlayer != context.Caster.IsPlayer) { continue; }
                }
                else if (reviveSkill)
                {
                    if (!unit.IsDead || unit.IsPlayer != context.Caster.IsPlayer) { continue; }
                }

                if (unit.CurrentHp < bestHp)
                {
                    bestHp = unit.CurrentHp;
                    best = unit;
                }
            }

            return best ?? context.SelectedTarget;
        }
    }

    public sealed class JcSameRowLowestHpSelector : JcITargetSelector
    {
        public static readonly JcSameRowLowestHpSelector Instance = new JcSameRowLowestHpSelector();
        private JcSameRowLowestHpSelector() { }

        // 원본 함수 대응: SkillTargetSelectors.SelectTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillTargetSelectors.cs)

        public BattleCharactor SelectTarget(JcSkillExecutionContext context)
        {
            if (context == null || context.SelectedTarget == null)
            {
                return null;
            }

            ASBGridManager gm = ASBGridManager.Instance;
            if (gm == null)
            {
                return context.SelectedTarget;
            }

            ASBGridCell selectedCell = context.SelectedCell ?? context.SelectedTarget.OccupiedCell ?? gm.FindCellByUnit(context.SelectedTarget);
            if (selectedCell == null)
            {
                return context.SelectedTarget;
            }

            List<Vector2Int> allCoords = gm.GetAllCoordsSnapshot();
            BattleCharactor best = context.SelectedTarget;
            float bestHp = context.SelectedTarget.CurrentHp;
            for (int i = 0; i < allCoords.Count; i++)
            {
                Vector2Int coord = allCoords[i];
                if (coord.x != selectedCell.Coords.x)
                {
                    continue;
                }

                if (!gm.TryGetCell(coord, out ASBGridCell cell) || cell == null || cell.OccupyingUnit == null)
                {
                    continue;
                }

                BattleCharactor unit = cell.OccupyingUnit;
                if (unit.IsDead || unit.IsPlayer == context.Caster.IsPlayer)
                {
                    continue;
                }

                if (unit.CurrentHp < bestHp)
                {
                    bestHp = unit.CurrentHp;
                    best = unit;
                }
            }

            return best;
        }
    }

    public sealed class JcRandomTargetSelector : JcITargetSelector
    {
        public static readonly JcRandomTargetSelector Instance = new JcRandomTargetSelector();
        private JcRandomTargetSelector() { }

        // 원본 함수 대응: SkillTargetSelectors.SelectTarget (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillTargetSelectors.cs)

        public BattleCharactor SelectTarget(JcSkillExecutionContext context)
        {
            if (context == null || context.Caster == null)
            {
                return null;
            }

            var candidates = new List<BattleCharactor>();
            BattleCharactor[] all = UnityEngine.Object.FindObjectsByType<BattleCharactor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++) 
            {
                BattleCharactor unit = all[i];
                if (unit == null || unit.IsDead)
                {
                    continue;
                }

                if (unit.IsPlayer == context.Caster.IsPlayer)
                {
                    continue;
                }

                candidates.Add(unit);
            }

            if (candidates.Count == 0)
            {
                return context.SelectedTarget;
            }

            int idx = JC.BattleTesting.JcBattleRandom.Range(0, candidates.Count);
            return candidates[idx];
        }
    }
}
