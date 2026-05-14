using System.Collections.Generic;
using UnityEngine;

public enum EnemyEncounterPathMode
{
    Ignore,
    BlockEncounterZones,
    AllowSingleEncounterZonePassage
}

public class AStarPathfinder : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;

    public List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        Transform selfTransform = null,
        bool ignoreFogVisibility = false,
        EnemyEncounterPathMode enemyEncounterPathMode = EnemyEncounterPathMode.Ignore)
    {
        if (start == goal)
            return new List<Vector2Int> { start };

        if (gridManager != null)
        {
            if (!CanEnterPathCell(goal, goal, selfTransform, ignoreFogVisibility, enemyEncounterPathMode))
                return null;
        }

        var openSet = new List<Vector2Int> { start };
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [start] = 0f };

        while (openSet.Count > 0)
        {
            Vector2Int current = GetLowestF(openSet, gScore, goal);
            if (current == goal)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            Vector2Int[] orderedDirections = GetOrderedDirections(current, goal);
            for (int i = 0; i < orderedDirections.Length; i++)
            {
                Vector2Int dir = orderedDirections[i];
                Vector2Int neighbor = current + dir;

                if (closedSet.Contains(neighbor))
                    continue;

                if (gridManager != null && !CanEnterPathCell(neighbor, goal, selfTransform, ignoreFogVisibility, enemyEncounterPathMode))
                    continue;

                float tentativeG = GetOrInfinity(gScore, current) + 1f;

                if (!openSet.Contains(neighbor))
                    openSet.Add(neighbor);
                else if (tentativeG >= GetOrInfinity(gScore, neighbor))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
            }
        }

        return null;
    }

    private bool CanEnterPathCell(
        Vector2Int grid,
        Vector2Int goal,
        Transform selfTransform,
        bool ignoreFogVisibility,
        EnemyEncounterPathMode enemyEncounterPathMode)
    {
        if (gridManager == null)
            return true;

        if (enemyEncounterPathMode != EnemyEncounterPathMode.Ignore)
        {
            EnemyEncounterZoneState zoneState = gridManager.GetEnemyEncounterZoneState(grid, out _);
            if (zoneState == EnemyEncounterZoneState.EnemyOccupied
                || zoneState == EnemyEncounterZoneState.OverlappedEnemyZone)
            {
                return false;
            }

            if (zoneState == EnemyEncounterZoneState.SingleEnemyZone
                && enemyEncounterPathMode == EnemyEncounterPathMode.BlockEncounterZones
                && grid != goal)
            {
                return false;
            }
        }

        return gridManager.CanEnterCell(grid, goal, selfTransform, ignoreFogVisibility);
    }

    private static Vector2Int[] GetOrderedDirections(Vector2Int current, Vector2Int goal)
    {
        Vector2Int[] ordered = (Vector2Int[])GridManager.Directions8.Clone();
        System.Array.Sort(ordered, (a, b) =>
        {
            Vector2Int nextA = current + a;
            Vector2Int nextB = current + b;

            int distanceCompare = GridManager.GridDistance(nextA, goal).CompareTo(GridManager.GridDistance(nextB, goal));
            if (distanceCompare != 0)
                return distanceCompare;

            bool aIsDiagonal = a.x != 0 && a.y != 0;
            bool bIsDiagonal = b.x != 0 && b.y != 0;
            if (aIsDiagonal != bIsDiagonal)
                return aIsDiagonal ? 1 : -1;

            int manhattanCompare =
                (Mathf.Abs(goal.x - nextA.x) + Mathf.Abs(goal.y - nextA.y))
                .CompareTo(Mathf.Abs(goal.x - nextB.x) + Mathf.Abs(goal.y - nextB.y));
            if (manhattanCompare != 0)
                return manhattanCompare;

            return 0;
        });

        return ordered;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return GridManager.GridDistance(a, b);
    }

    private static Vector2Int GetLowestF(List<Vector2Int> openSet, Dictionary<Vector2Int, float> gScore, Vector2Int goal)
    {
        Vector2Int best = openSet[0];
        float bestF = GetOrInfinity(gScore, best) + Heuristic(best, goal);

        for (int i = 1; i < openSet.Count; i++)
        {
            Vector2Int node = openSet[i];
            float f = GetOrInfinity(gScore, node) + Heuristic(node, goal);
            if (f < bestF)
            {
                bestF = f;
                best = node;
            }
        }

        return best;
    }

    private static float GetOrInfinity(Dictionary<Vector2Int, float> gScore, Vector2Int node)
    {
        return gScore.TryGetValue(node, out float score) ? score : float.PositiveInfinity;
    }

    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.TryGetValue(current, out Vector2Int prev))
        {
            current = prev;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
