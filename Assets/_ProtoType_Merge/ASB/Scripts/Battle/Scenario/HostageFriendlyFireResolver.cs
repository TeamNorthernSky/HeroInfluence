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

    /// <summary>
    /// 부수피해의 중심 칸을 <b>피해 확정 전에</b> 스냅샷한다.
    /// <para>
    /// 확정 후에 판정하면 <c>primaryTarget.IsDead</c>가 "이번 공격으로 죽었는지"를 뜻하게 되어
    /// <b>적을 죽이면 옆 인질이 무사하고, 살려두면 다치는</b> 역전이 생긴다.
    /// 시전자가 반격으로 사망하는 경우도 마찬가지라 <see cref="CanTargetHostages"/> 판정을 여기서 끝낸다.
    /// </para>
    /// </summary>
    /// <returns>부수피해를 적용해야 하면 중심 칸, 아니면 null.</returns>
    public static GridCellRef CaptureCollateralCenter(BattleCharactor actor, BattleCharactor primaryTarget, SkillData skill)
    {
        if (!CanTargetHostages(actor, skill) || primaryTarget == null || primaryTarget.IsDead)
            return null;

        return primaryTarget.OccupiedCell;
    }

    /// <summary>
    /// <see cref="CaptureCollateralCenter"/>로 미리 잡아둔 중심 칸에 부수피해를 적용한다.
    /// 이 시점의 전투 상태(대상·시전자 생사)는 판정에 쓰지 않는다.
    /// </summary>
    public static int ApplyCollateralDamage(BattleCharactor actor, GridCellRef centerCell, SkillData skill)
    {
        if (actor == null || skill == null || centerCell == null)
            return 0;

        GridManagerRef gridManager = GridManagerRef.Instance;
        if (gridManager == null)
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

    /// <summary>
    /// Returns the cells that should be highlighted while a hostage is targeted.
    /// Random-around skills expose every possible splash cell, matching enemy targeting previews;
    /// actual hostage damage remains resolved by TryGetAffectedCells.
    /// </summary>
    public static bool TryGetPreviewCells(
        HostageBattleActor primaryTarget,
        SkillData skill,
        out GridCellRef centerCell,
        out List<GridCellRef> previewCells)
    {
        centerCell = primaryTarget != null ? primaryTarget.GetComponentInParent<GridCellRef>() : null;
        previewCells = new List<GridCellRef>();

        GridManagerRef gridManager = GridManagerRef.Instance;
        if (centerCell == null || skill == null || gridManager == null)
            return false;

        previewCells.Add(centerCell);

        if (SkillExecutionRegistry.TryGetHandler(skill.skillKey, out ISkillEffectHandler handler) &&
            handler is TargetAroundRandom)
        {
            TargetAroundRandomHelper.CollectSplashCells(
                centerCell.Coords,
                skill,
                gridManager,
                centerCell,
                previewCells);
            return true;
        }

        previewCells = ResolveAffectedCells(centerCell, skill, gridManager);
        return previewCells.Count > 0;
    }

    public static bool TryGetAffectedCells(
        HostageBattleActor primaryTarget,
        SkillData skill,
        out GridCellRef centerCell,
        out List<GridCellRef> affectedCells)
    {
        centerCell = primaryTarget != null ? primaryTarget.GetComponentInParent<GridCellRef>() : null;
        affectedCells = new List<GridCellRef>();
        GridManagerRef gridManager = GridManagerRef.Instance;
        if (centerCell == null || skill == null || gridManager == null)
            return false;

        affectedCells = ResolveAffectedCells(centerCell, skill, gridManager);
        return affectedCells.Count > 0;
    }

    public static List<HostageBattleActor> ApplySkillDamage(BattleCharactor actor, HostageBattleActor primaryTarget, SkillData skill)
    {
        var hitHostages = new List<HostageBattleActor>();
        if (!CanTargetHostages(actor, skill) || primaryTarget == null || !primaryTarget.IsSafe)
            return hitHostages;

        if (!TryGetAffectedCells(primaryTarget, skill, out _, out List<GridCellRef> cells))
            return hitHostages;

        float rawDamage = Mathf.Max(0f, actor.FinalStats.Atk * Mathf.Max(0.01f, skill.skillValue));
        var seen = new HashSet<HostageBattleActor>();

        for (int i = 0; i < cells.Count; i++)
        {
            HostageBattleActor hostage = cells[i].GetComponentInChildren<HostageBattleActor>(true);
            if (hostage == null || !hostage.IsSafe || !seen.Add(hostage))
                continue;

            hostage.ApplyFriendlyDamage(rawDamage);
            hitHostages.Add(hostage);
        }

        return hitHostages;
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
