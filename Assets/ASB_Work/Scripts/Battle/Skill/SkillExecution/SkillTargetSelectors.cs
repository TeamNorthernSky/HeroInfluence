using System;
using System.Collections.Generic;
using ASB.Work.BattleGrid;
using UnityEngine;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.GridManager;

namespace ASB.Work.Battle.SkillExecution
{
    public sealed class DefaultTargetSelector : ITargetSelector
    {
        public static readonly DefaultTargetSelector Instance = new DefaultTargetSelector();
        private DefaultTargetSelector() { }

        public BattleCharactor SelectTarget(SkillExecutionContext context)
        {
            return context != null ? context.SelectedTarget : null;
        }
    }

    public sealed class LowestHpSelector : ITargetSelector
    {
        public static readonly LowestHpSelector Instance = new LowestHpSelector();
        private LowestHpSelector() { }

        public BattleCharactor SelectTarget(SkillExecutionContext context)
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

    public sealed class SameRowLowestHpSelector : ITargetSelector
    {
        public static readonly SameRowLowestHpSelector Instance = new SameRowLowestHpSelector();
        private SameRowLowestHpSelector() { }

        public BattleCharactor SelectTarget(SkillExecutionContext context)
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

    public sealed class RandomTargetSelector : ITargetSelector
    {
        public static readonly RandomTargetSelector Instance = new RandomTargetSelector();
        private RandomTargetSelector() { }

        public BattleCharactor SelectTarget(SkillExecutionContext context)
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

            int idx = UnityEngine.Random.Range(0, candidates.Count);
            return candidates[idx];
        }
    }
}
