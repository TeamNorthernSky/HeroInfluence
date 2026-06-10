using System.Collections.Generic;
using UnityEngine;

public static class TargetingVisualRegistry
{
    private static readonly Dictionary<BattleCharactor, List<ITargetSelectableVisual>> visualsByOwner = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => visualsByOwner.Clear();

    public static void Register(ITargetSelectableVisual visual)
    {
        if (visual == null || visual.Owner == null) return;

        if (!visualsByOwner.TryGetValue(visual.Owner, out var list))
        {
            list = new List<ITargetSelectableVisual>();
            visualsByOwner[visual.Owner] = list;
        }

        if (!list.Contains(visual))
            list.Add(visual);
    }

    public static void Unregister(ITargetSelectableVisual visual)
    {
        if (visual == null || visual.Owner == null) return;

        if (!visualsByOwner.TryGetValue(visual.Owner, out var list)) return;

        list.Remove(visual);
        if (list.Count == 0)
            visualsByOwner.Remove(visual.Owner);
    }

    public static IReadOnlyList<ITargetSelectableVisual> GetVisuals(BattleCharactor owner)
    {
        if (owner == null) return System.Array.Empty<ITargetSelectableVisual>();
        if (!visualsByOwner.TryGetValue(owner, out var list)) return System.Array.Empty<ITargetSelectableVisual>();
        return list;
    }
}
