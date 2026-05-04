using System.Collections.Generic;
using UnityEngine;

public class VillainUnionBaseRegistry : MonoBehaviour
{
    private readonly List<VillainUnionBase> villainUnionBases = new List<VillainUnionBase>();

    public IReadOnlyList<VillainUnionBase> VillainUnionBases => villainUnionBases;

    private void Awake()
    {
        RefreshSceneBases();
    }

    public void Register(VillainUnionBase villainUnionBase)
    {
        if (villainUnionBase == null || villainUnionBases.Contains(villainUnionBase))
            return;

        villainUnionBases.Add(villainUnionBase);
    }

    public void Unregister(VillainUnionBase villainUnionBase)
    {
        if (villainUnionBase == null)
            return;

        villainUnionBases.Remove(villainUnionBase);
    }

    [ContextMenu("Refresh Scene Bases")]
    public void RefreshSceneBases()
    {
        villainUnionBases.Clear();

        VillainUnionBase[] sceneBases = FindObjectsByType<VillainUnionBase>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneBases.Length; i++)
        {
            VillainUnionBase villainUnionBase = sceneBases[i];
            if (villainUnionBase == null)
                continue;

            Register(villainUnionBase);
        }
    }

    public VillainUnionBase GetClosestBase(Vector2Int grid)
    {
        VillainUnionBase closest = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < villainUnionBases.Count; i++)
        {
            VillainUnionBase villainUnionBase = villainUnionBases[i];
            if (villainUnionBase == null)
                continue;

            int distance = GridManager.GridDistance(grid, villainUnionBase.GetCurrentGrid());
            if (distance >= closestDistance)
                continue;

            closest = villainUnionBase;
            closestDistance = distance;
        }

        return closest;
    }
}
