using System.Collections.Generic;
using UnityEngine;

public enum EnemyEncounterPathMode
{
    Ignore,
    BlockEncounterZones,
    AllowSingleEncounterZonePassage
}

public enum MainEventInteractionPathMode
{
    Ignore,
    BlockInteractionCells,
    AllowSingleInteractionCellPassage
}

public class AStarPathfinder : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;

    public List<Vector2Int> FindPath(
        Vector2Int start,
        Vector2Int goal,
        Transform selfTransform = null,
        bool ignoreVisibilityRules = false,
        EnemyEncounterPathMode enemyEncounterPathMode = EnemyEncounterPathMode.Ignore,
        bool allowItemCells = false,
        MainEventInteractionPathMode mainEventInteractionPathMode = MainEventInteractionPathMode.Ignore,
        int maxVisitedNodes = 0,
        int maxPathCost = -1)
    {
        if (start == goal)
            return new List<Vector2Int> { start };

        if (gridManager != null)
        {
            if (!CanEnterPathCell(goal, goal, selfTransform, ignoreVisibilityRules, enemyEncounterPathMode, allowItemCells, mainEventInteractionPathMode))
                return null;
        }

        var openSet = new MinHeap();
        openSet.Enqueue(start, Heuristic(start, goal), 0);
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [start] = 0f };
        int insertionOrder = 1;
        Vector2Int[] directionBuffer = new Vector2Int[GridManager.Directions8.Length];

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();
            if (closedSet.Contains(current))
                continue;

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);
            if (maxVisitedNodes > 0 && closedSet.Count >= maxVisitedNodes)
                return null;

            Vector2Int[] orderedDirections = GetOrderedDirections(current, goal, directionBuffer);
            for (int i = 0; i < orderedDirections.Length; i++)
            {
                Vector2Int dir = orderedDirections[i];
                Vector2Int neighbor = current + dir;

                if (closedSet.Contains(neighbor))
                    continue;

                float tentativeG = GetOrInfinity(gScore, current) + 1f;
                if (maxPathCost >= 0 && tentativeG > maxPathCost)
                    continue;

                if (tentativeG >= GetOrInfinity(gScore, neighbor))
                    continue;

                if (gridManager != null && !CanEnterPathCell(neighbor, goal, selfTransform, ignoreVisibilityRules, enemyEncounterPathMode, allowItemCells, mainEventInteractionPathMode))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                openSet.Enqueue(neighbor, tentativeG + Heuristic(neighbor, goal), insertionOrder++);
            }
        }

        return null;
    }

    public List<Vector2Int> FindPathToAdjacent(
        Vector2Int start,
        Vector2Int target,
        Transform selfTransform = null,
        bool ignoreVisibilityRules = false,
        EnemyEncounterPathMode enemyEncounterPathMode = EnemyEncounterPathMode.Ignore,
        bool allowItemCells = false,
        MainEventInteractionPathMode mainEventInteractionPathMode = MainEventInteractionPathMode.Ignore,
        int maxVisitedNodes = 0,
        System.Func<Vector2Int, bool> isValidAdjacentCell = null,
        int maxPathCost = -1)
    {
        if (IsValidAdjacentDestination(start, target, isValidAdjacentCell))
            return new List<Vector2Int> { start };

        var openSet = new MinHeap();
        openSet.Enqueue(start, Heuristic(start, target), 0);
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [start] = 0f };
        int insertionOrder = 1;
        Vector2Int[] directionBuffer = new Vector2Int[GridManager.Directions8.Length];

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();
            if (closedSet.Contains(current))
                continue;

            if (current != start && IsValidAdjacentDestination(current, target, isValidAdjacentCell))
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);
            if (maxVisitedNodes > 0 && closedSet.Count >= maxVisitedNodes)
                return null;

            Vector2Int[] orderedDirections = GetOrderedDirections(current, target, directionBuffer);
            for (int i = 0; i < orderedDirections.Length; i++)
            {
                Vector2Int dir = orderedDirections[i];
                Vector2Int neighbor = current + dir;

                if (closedSet.Contains(neighbor) || neighbor == target)
                    continue;

                float tentativeG = GetOrInfinity(gScore, current) + 1f;
                if (maxPathCost >= 0 && tentativeG > maxPathCost)
                    continue;

                if (tentativeG >= GetOrInfinity(gScore, neighbor))
                    continue;

                bool isTerminalAdjacent = IsValidAdjacentDestination(neighbor, target, isValidAdjacentCell);
                Vector2Int pathGoalForRules = isTerminalAdjacent ? neighbor : target;
                if (gridManager != null && !CanEnterPathCell(neighbor, pathGoalForRules, selfTransform, ignoreVisibilityRules, enemyEncounterPathMode, allowItemCells, mainEventInteractionPathMode))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                openSet.Enqueue(neighbor, tentativeG + Heuristic(neighbor, target), insertionOrder++);
            }
        }

        return null;
    }

    private static bool IsValidAdjacentDestination(
        Vector2Int grid,
        Vector2Int target,
        System.Func<Vector2Int, bool> isValidAdjacentCell)
    {
        return GridManager.GridDistance(grid, target) == 1 &&
            (isValidAdjacentCell == null || isValidAdjacentCell(grid));
    }

    public List<Vector2Int> FindPathToAny(
        Vector2Int start,
        IReadOnlyList<Vector2Int> goals,
        Vector2Int heuristicTarget,
        Transform selfTransform = null,
        bool ignoreVisibilityRules = false,
        EnemyEncounterPathMode enemyEncounterPathMode = EnemyEncounterPathMode.Ignore,
        bool allowItemCells = false,
        MainEventInteractionPathMode mainEventInteractionPathMode = MainEventInteractionPathMode.Ignore,
        int maxVisitedNodes = 0,
        int maxPathCost = -1,
        System.Func<Vector2Int, bool> isAllowedSearchCell = null)
    {
        if (goals == null || goals.Count == 0)
            return null;

        if (ContainsGrid(goals, start))
            return new List<Vector2Int> { start };

        var openSet = new MinHeap();
        openSet.Enqueue(start, Heuristic(start, heuristicTarget), 0);
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [start] = 0f };
        int insertionOrder = 1;
        Vector2Int[] directionBuffer = new Vector2Int[GridManager.Directions8.Length];

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();
            if (closedSet.Contains(current))
                continue;

            if (current != start && ContainsGrid(goals, current))
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);
            if (maxVisitedNodes > 0 && closedSet.Count >= maxVisitedNodes)
                return null;

            Vector2Int[] orderedDirections = GetOrderedDirections(current, heuristicTarget, directionBuffer);
            for (int i = 0; i < orderedDirections.Length; i++)
            {
                Vector2Int neighbor = current + orderedDirections[i];

                if (closedSet.Contains(neighbor))
                    continue;

                float tentativeG = GetOrInfinity(gScore, current) + 1f;
                if (maxPathCost >= 0 && tentativeG > maxPathCost)
                    continue;

                if (tentativeG >= GetOrInfinity(gScore, neighbor))
                    continue;

                bool isTerminalGoal = ContainsGrid(goals, neighbor);
                if (isAllowedSearchCell != null && !isAllowedSearchCell(neighbor))
                    continue;
                Vector2Int pathGoalForRules = isTerminalGoal ? neighbor : heuristicTarget;
                if (gridManager != null && !CanEnterPathCell(neighbor, pathGoalForRules, selfTransform, ignoreVisibilityRules, enemyEncounterPathMode, allowItemCells, mainEventInteractionPathMode))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                openSet.Enqueue(neighbor, tentativeG + Heuristic(neighbor, heuristicTarget), insertionOrder++);
            }
        }

        return null;
    }

    private static bool ContainsGrid(IReadOnlyList<Vector2Int> grids, Vector2Int grid)
    {
        if (grids == null)
            return false;

        for (int i = 0; i < grids.Count; i++)
        {
            if (grids[i] == grid)
                return true;
        }

        return false;
    }

    public List<Vector2Int> FindPathUntil(
        Vector2Int start,
        Vector2Int heuristicTarget,
        System.Func<Vector2Int, bool> isTerminalCell,
        System.Func<Vector2Int, bool> isAllowedSearchCell = null,
        Transform selfTransform = null,
        bool ignoreVisibilityRules = false,
        EnemyEncounterPathMode enemyEncounterPathMode = EnemyEncounterPathMode.Ignore,
        bool allowItemCells = false,
        MainEventInteractionPathMode mainEventInteractionPathMode = MainEventInteractionPathMode.Ignore,
        int maxVisitedNodes = 0,
        int maxPathCost = -1)
    {
        if (isTerminalCell == null)
            return null;

        if (isTerminalCell(start))
            return new List<Vector2Int> { start };

        var openSet = new MinHeap();
        openSet.Enqueue(start, Heuristic(start, heuristicTarget), 0);
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [start] = 0f };
        int insertionOrder = 1;
        Vector2Int[] directionBuffer = new Vector2Int[GridManager.Directions8.Length];

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();
            if (closedSet.Contains(current))
                continue;

            if (current != start && isTerminalCell(current))
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);
            if (maxVisitedNodes > 0 && closedSet.Count >= maxVisitedNodes)
                return null;

            Vector2Int[] orderedDirections = GetOrderedDirections(current, heuristicTarget, directionBuffer);
            for (int i = 0; i < orderedDirections.Length; i++)
            {
                Vector2Int neighbor = current + orderedDirections[i];

                if (closedSet.Contains(neighbor))
                    continue;

                float tentativeG = GetOrInfinity(gScore, current) + 1f;
                if (maxPathCost >= 0 && tentativeG > maxPathCost)
                    continue;

                if (tentativeG >= GetOrInfinity(gScore, neighbor))
                    continue;

                if (isAllowedSearchCell != null && !isAllowedSearchCell(neighbor))
                    continue;

                bool isTerminal = isTerminalCell(neighbor);
                Vector2Int pathGoalForRules = isTerminal ? neighbor : heuristicTarget;
                if (gridManager != null && !CanEnterPathCell(neighbor, pathGoalForRules, selfTransform, ignoreVisibilityRules, enemyEncounterPathMode, allowItemCells, mainEventInteractionPathMode))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                openSet.Enqueue(neighbor, tentativeG + Heuristic(neighbor, heuristicTarget), insertionOrder++);
            }
        }

        return null;
    }

    private bool CanEnterPathCell(
        Vector2Int grid,
        Vector2Int goal,
        Transform selfTransform,
        bool ignoreVisibilityRules,
        EnemyEncounterPathMode enemyEncounterPathMode,
        bool allowItemCells,
        MainEventInteractionPathMode mainEventInteractionPathMode)
    {
        if (gridManager == null)
            return true;

        if (grid != goal && GateTeleportController.IsOpenGateTeleportCell(grid))
            return false;

        if (enemyEncounterPathMode != EnemyEncounterPathMode.Ignore)
        {
            EnemyEncounterZoneState zoneState = gridManager.GetGridEnemyEncounterZoneState(grid, out _);
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

        if (mainEventInteractionPathMode == MainEventInteractionPathMode.BlockInteractionCells
            && grid != goal
            && gridManager.TryGetMainEventAtInteractionCell(grid, out _))
        {
            return false;
        }

        return gridManager.CanEnterCell(grid, goal, selfTransform, ignoreVisibilityRules, allowItemCells);
    }

    private static Vector2Int[] GetOrderedDirections(Vector2Int current, Vector2Int goal, Vector2Int[] ordered)
    {
        // Reset tie ordering without allocating an array for every expanded cell.
        System.Array.Copy(GridManager.Directions8, ordered, ordered.Length);
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

    private sealed class MinHeap
    {
        private readonly List<Entry> entries = new List<Entry>();

        public int Count => entries.Count;

        public void Enqueue(Vector2Int node, float priority, int order)
        {
            entries.Add(new Entry(node, priority, order));
            SiftUp(entries.Count - 1);
        }

        public Vector2Int Dequeue()
        {
            Entry root = entries[0];
            int lastIndex = entries.Count - 1;
            entries[0] = entries[lastIndex];
            entries.RemoveAt(lastIndex);

            if (entries.Count > 0)
                SiftDown(0);

            return root.Node;
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (Compare(entries[parent], entries[index]) <= 0)
                    break;

                Swap(parent, index);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                int left = index * 2 + 1;
                int right = left + 1;
                int smallest = index;

                if (left < entries.Count && Compare(entries[left], entries[smallest]) < 0)
                    smallest = left;

                if (right < entries.Count && Compare(entries[right], entries[smallest]) < 0)
                    smallest = right;

                if (smallest == index)
                    break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            Entry temp = entries[a];
            entries[a] = entries[b];
            entries[b] = temp;
        }

        private static int Compare(Entry a, Entry b)
        {
            int priorityCompare = a.Priority.CompareTo(b.Priority);
            return priorityCompare != 0 ? priorityCompare : a.Order.CompareTo(b.Order);
        }

        private readonly struct Entry
        {
            public Entry(Vector2Int node, float priority, int order)
            {
                Node = node;
                Priority = priority;
                Order = order;
            }

            public Vector2Int Node { get; }
            public float Priority { get; }
            public int Order { get; }
        }
    }
}

