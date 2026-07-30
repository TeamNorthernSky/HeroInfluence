using System.Collections.Generic;
using ASB.Work.BattleGrid;
using ASB.Work.Battle.SkillExecution;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;
using GridManagerRef = ASB.Work.BattleGrid.BattleGridManager;

public static class HostageFriendlyFireResolver
{
    public static HashSet<HostageBattleActor> GetSelectableTargets(BattleCharactor actor, SkillData skill)
    {
        var result = new HashSet<HostageBattleActor>();
        if (!CanTargetHostages(actor, skill))
            return result;

        HostageScenarioController scenario = HostageScenarioController.Active;
        IReadOnlyList<HostageBattleActor> hostages = scenario.Hostages;
        for (int i = 0; i < hostages.Count; i++)
        {
            HostageBattleActor hostage = hostages[i];
            if (hostage != null && hostage.IsSafe)
                result.Add(hostage);
        }

        return result;
    }

    public static bool IsStillValidTarget(BattleCharactor actor, PendingActionType actionType, HostageBattleActor target)
    {
        if (target == null || !target.IsSafe || actor == null)
            return false;

        SkillData skill = null;
        switch (actionType)
        {
            case PendingActionType.ClassSkill:
                actor.ResolveSelectedSkill(false);
                skill = actor.SelectedSkillData;
                break;
            case PendingActionType.WeaponSkill:
                skill = actor.EquippedWeaponData != null ? actor.EquippedWeaponData.ToSkillData() : null;
                break;
        }

        return CanTargetHostages(actor, skill);
    }

    public static bool CanTargetHostages(BattleCharactor actor, SkillData skill)
    {
        return actor != null
               && actor.IsPlayer
               && !actor.IsDead
               && skill != null
               && skill.classSkillEffect == 0
               && HostageScenarioController.Active != null;
    }

    public static int ApplyCollateralDamage(BattleCharactor actor, BattleCharactor primaryTarget, SkillData skill)
    {
        if (!CanTargetHostages(actor, skill) || primaryTarget == null || primaryTarget.IsDead)
            return 0;

        GridCellRef centerCell = primaryTarget.OccupiedCell;
        GridManagerRef gridManager = GridManagerRef.Instance;
        if (centerCell == null || gridManager == null)
            return 0;

        float rawDamage = Mathf.Max(0f, actor.FinalStats.Atk * Mathf.Max(0.01f, skill.skillValue));
        List<GridCellRef> cells = ResolveAffectedCells(centerCell, skill, gridManager);
        var hitHostages = new HashSet<HostageBattleActor>();
        int hitCount = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            HostageBattleActor hostage = cells[i].GetComponentInChildren<HostageBattleActor>(true);
            if (hostage == null || !hostage.IsSafe || !hitHostages.Add(hostage))
                continue;

            hostage.ApplyFriendlyDamage(rawDamage);
            hitCount++;
        }

        return hitCount;
    }

    public static int ApplySkillDamage(BattleCharactor actor, HostageBattleActor primaryTarget, SkillData skill)
    {
        if (!CanTargetHostages(actor, skill) || primaryTarget == null || !primaryTarget.IsSafe)
            return 0;

        GridCellRef centerCell = primaryTarget.GetComponentInParent<GridCellRef>();
        GridManagerRef gridManager = GridManagerRef.Instance;
        if (centerCell == null || gridManager == null)
            return 0;

        float rawDamage = Mathf.Max(0f, actor.FinalStats.Atk * Mathf.Max(0.01f, skill.skillValue));
        List<GridCellRef> cells = ResolveAffectedCells(centerCell, skill, gridManager);
        var hitHostages = new HashSet<HostageBattleActor>();
        int hitCount = 0;

        for (int i = 0; i < cells.Count; i++)
        {
            HostageBattleActor hostage = cells[i].GetComponentInChildren<HostageBattleActor>(true);
            if (hostage == null || !hostage.IsSafe || !hitHostages.Add(hostage))
                continue;

            hostage.ApplyFriendlyDamage(rawDamage);
            hitCount++;
        }

        return hitCount;
    }

    private static List<GridCellRef> ResolveAffectedCells(GridCellRef centerCell, SkillData skill, GridManagerRef gridManager)
    {
        var cells = new List<GridCellRef>();
        bool singleTarget = skill.classSkillTarget == 0 ||
            (skill.classSkillTarget != 2 && (skill.boundary == null || skill.boundary.Count == 0));
        if (singleTarget)
        {
            cells.Add(centerCell);
            return cells;
        }

        HashSet<Vector2Int> coordinates;
        if (skill.classSkillTarget == 2)
        {
            coordinates = SkillTargetingMapper.GetFullSideBoardCoordinates(centerCell.Coords);
        }
        else
        {
            var pattern = skill.boundary != null ? new List<int>(skill.boundary) : new List<int>();
            if (!pattern.Contains(0))
                pattern.Insert(0, 0);
            coordinates = SkillTargetingMapper.GetMultiTargetCoordinates(centerCell.Coords, pattern);
        }

        bool targetEnemySide = centerCell.Coords.x >= 2;
        foreach (Vector2Int coordinate in coordinates)
        {
            if (!gridManager.TryGetCell(coordinate, out GridCellRef cell) || cell == null)
                continue;
            if ((cell.Coords.x >= 2) == targetEnemySide)
                cells.Add(cell);
        }

        return cells;
    }
}
