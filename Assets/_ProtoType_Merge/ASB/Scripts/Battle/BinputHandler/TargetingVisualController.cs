using System.Collections.Generic;
using UnityEngine;

public class TargetingVisualController : MonoBehaviour
{
    private readonly HashSet<BattleCharactor> selectableTargets = new();
    private BattleCharactor hoveredTarget;

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
    }

    public void RemoveSelectableTarget(BattleCharactor target)
    {
        if (target == null) return;

        selectableTargets.Remove(target);
        if (hoveredTarget == target)
            hoveredTarget = null;

        ApplyVisualState(target);
    }

    public void SetHoveredTarget(BattleCharactor target)
    {
        if (target != null && !selectableTargets.Contains(target))
            target = null;

        if (hoveredTarget == target) return;

        BattleCharactor prev = hoveredTarget;
        hoveredTarget = target;

        if (prev != null) ApplyVisualState(prev);
        if (hoveredTarget != null) ApplyVisualState(hoveredTarget);
    }

    public void ClearAll()
    {
        var toClear = new List<BattleCharactor>(selectableTargets);
        if (hoveredTarget != null && !selectableTargets.Contains(hoveredTarget))
            toClear.Add(hoveredTarget);

        selectableTargets.Clear();
        hoveredTarget = null;

        foreach (BattleCharactor target in toClear)
            ApplyVisualState(target);
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
}
