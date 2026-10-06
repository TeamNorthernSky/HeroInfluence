// [JC 테스트 씬 전용 / 기준 0705af74]
// 원본: Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/TargetingVisualController.cs
// 원본 객체 TargetingVisualController → JcTargetingVisualController. 동일 이름 함수는 원본 함수와 1:1 대응합니다.
// 차이: JC 전투 흐름/입력을 참조하며 원본 씬·컴포넌트·데이터를 수정하지 않습니다.
using System.Collections.Generic;
using UnityEngine;
using ASBGridManager = ASB.Work.BattleGrid.JcBattleGridManager;

public class JcTargetingVisualController : MonoBehaviour
{
    private readonly HashSet<BattleCharactor> selectableTargets = new();
    private readonly HashSet<HostageBattleActor> selectableHostageTargets = new();
    private BattleCharactor hoveredTarget;
    private HostageBattleActor hoveredHostageTarget;

    public void ShowSelectableTargets(IEnumerable<BattleCharactor> targets)
    {
        ClearAll();
        if (targets == null) return;

        foreach (BattleCharactor target in targets)
        {
            if (target == null) continue;
            selectableTargets.Add(target);
            ApplyVisualState(target);
        }
        RefreshCellSelection();
    }

    public void ShowSelectableHostageTargets(IEnumerable<HostageBattleActor> targets)
    {
        if (targets == null) return;

        foreach (HostageBattleActor target in targets)
        {
            if (target == null || !target.IsSafe) continue;
            selectableHostageTargets.Add(target);
            ApplyHostageVisualState(target);
        }
        RefreshCellSelection();
    }

    public void RemoveSelectableTarget(BattleCharactor target)
    {
        if (target == null) return;

        selectableTargets.Remove(target);
        if (hoveredTarget == target)
            hoveredTarget = null;

        ApplyVisualState(target);
        RefreshCellSelection();
    }

    public void RemoveSelectableHostageTarget(HostageBattleActor target)
    {
        if (target == null) return;

        selectableHostageTargets.Remove(target);
        if (hoveredHostageTarget == target)
            hoveredHostageTarget = null;

        ApplyHostageVisualState(target);
        RefreshCellSelection();
    }

    public void SetHoveredTarget(BattleCharactor target)
    {
        if (target != null && !selectableTargets.Contains(target))
            target = null;

        if (hoveredTarget == target) return;

        BattleCharactor previous = hoveredTarget;
        hoveredTarget = target;

        if (previous != null) ApplyVisualState(previous);
        if (hoveredTarget != null) ApplyVisualState(hoveredTarget);
    }

    public void SetHoveredHostageTarget(HostageBattleActor target)
    {
        if (target != null && !selectableHostageTargets.Contains(target))
            target = null;

        if (hoveredHostageTarget == target) return;

        HostageBattleActor previous = hoveredHostageTarget;
        hoveredHostageTarget = target;

        if (previous != null) ApplyHostageVisualState(previous);
        if (hoveredHostageTarget != null) ApplyHostageVisualState(hoveredHostageTarget);
    }

    public void ClearAll()
    {
        ASBGridManager.Instance?.ClearSelectableCells();
        var unitsToClear = new List<BattleCharactor>(selectableTargets);
        if (hoveredTarget != null && !selectableTargets.Contains(hoveredTarget))
            unitsToClear.Add(hoveredTarget);

        var hostagesToClear = new List<HostageBattleActor>(selectableHostageTargets);
        if (hoveredHostageTarget != null && !selectableHostageTargets.Contains(hoveredHostageTarget))
            hostagesToClear.Add(hoveredHostageTarget);

        selectableTargets.Clear();
        selectableHostageTargets.Clear();
        hoveredTarget = null;
        hoveredHostageTarget = null;

        foreach (BattleCharactor target in unitsToClear)
            ApplyVisualState(target);
        foreach (HostageBattleActor target in hostagesToClear)
            ApplyHostageVisualState(target);
    }

    private void RefreshCellSelection()
    {
        ASBGridManager.Instance?.SetSelectableUnits(selectableTargets);
        ASBGridManager.Instance?.AddSelectableHostages(selectableHostageTargets);
    }

    private void ApplyVisualState(BattleCharactor target)
    {
        if (target == null) return;

        bool isSelectable = selectableTargets.Contains(target);
        bool isHovered = hoveredTarget == target;

        IReadOnlyList<ITargetSelectableVisual> visuals = TargetingVisualRegistry.GetVisuals(target);
        foreach (ITargetSelectableVisual visual in visuals)
        {
            if (visual == null) continue;

            if (!isSelectable && !isHovered)
                visual.Clear();
            else
            {
                visual.SetSelectable(isSelectable);
                visual.SetHovered(isHovered);
            }
        }
    }

    private void ApplyHostageVisualState(HostageBattleActor target)
    {
        if (target == null) return;

        HostageTargetingVisual visual = HostageTargetingVisual.Ensure(target);
        if (visual == null) return;

        bool isSelectable = selectableHostageTargets.Contains(target);
        bool isHovered = hoveredHostageTarget == target;
        if (!isSelectable && !isHovered)
            visual.Clear();
        else
        {
            visual.SetSelectable(isSelectable);
            visual.SetHovered(isHovered);
        }
    }
}
