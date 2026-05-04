using System.Collections.Generic;
using UnityEngine;

public class CastleRegistry : MonoBehaviour
{
    private readonly List<CastleUnit> castles = new List<CastleUnit>();

    public IReadOnlyList<CastleUnit> Castles => castles;

    private void Awake()
    {
        RefreshSceneCastles();
    }

    public void Register(CastleUnit castle)
    {
        if (castle == null || castles.Contains(castle))
            return;

        castles.Add(castle);
    }

    public void Unregister(CastleUnit castle)
    {
        if (castle == null)
            return;

        castles.Remove(castle);
    }

    [ContextMenu("Refresh Scene Castles")]
    public void RefreshSceneCastles()
    {
        castles.Clear();

        CastleUnit[] sceneCastles = FindObjectsByType<CastleUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneCastles.Length; i++)
        {
            CastleUnit castle = sceneCastles[i];
            if (castle == null)
                continue;

            Register(castle);
        }
    }

    public CastleUnit GetClosestCastle(Vector2Int grid)
    {
        CastleUnit closest = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < castles.Count; i++)
        {
            CastleUnit castle = castles[i];
            if (castle == null)
                continue;

            int distance = GridManager.GridDistance(grid, castle.GetCurrentGrid());
            if (distance >= closestDistance)
                continue;

            closest = castle;
            closestDistance = distance;
        }

        return closest;
    }
}
