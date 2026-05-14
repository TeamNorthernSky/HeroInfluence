using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemyTurnController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private CastleRegistry castleRegistry;
    [SerializeField] private AStarPathfinder pathfinder;
    [SerializeField] private CombatEncounterManager combatEncounterManager;
    [SerializeField] private GridManager gridManager;
    [FormerlySerializedAs("mineRegistry")]
    [SerializeField] private OutpostRegistry outpostRegistry;

    private readonly List<TargetCandidate> targetCandidates = new List<TargetCandidate>();

    private readonly struct TargetCandidate
    {
        public TargetCandidate(EnemyTargetType targetType, Component target, Vector2Int grid)
        {
            TargetType = targetType;
            Target = target;
            Grid = grid;
        }

        public EnemyTargetType TargetType { get; }
        public Component Target { get; }
        public Vector2Int Grid { get; }
    }

    public IEnumerator ExecuteEnemyTurn()
    {
        ResolveReferences();

        if (enemyRegistry == null || partyRegistry == null || pathfinder == null)
            yield break;

        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null)
                continue;

            if (enemy.IsStayEnemy)
                continue;

            if (HandleAdjacentOutpostInteraction(enemy))
                continue;

            ValidateCurrentTarget(enemy);

            if (!enemy.HasTarget())
                AcquireTarget(enemy);

            if (!enemy.HasTarget())
                continue;

            PartyGridMover adjacentParty = FindAdjacentParty(enemy.GetCurrentGrid());
            if (adjacentParty != null && TryBeginCombat(adjacentParty, enemy))
                yield break;

            Vector2Int targetGrid = GetTargetGrid(enemy.CurrentTargetType, enemy.CurrentTarget);
            List<Vector2Int> fullPath = FindApproachPath(
                enemy,
                enemy.CurrentTargetType,
                enemy.CurrentTarget,
                targetGrid);
            List<Vector2Int> movePath = TrimPathToMovePoints(fullPath, enemy.MovePointsPerTurn);

            if (movePath != null && movePath.Count > 1)
                yield return enemy.MoveAlongPath(movePath);

            if (HandleAdjacentOutpostInteraction(enemy))
                continue;

            adjacentParty = FindAdjacentParty(enemy.GetCurrentGrid());
            if (adjacentParty != null && TryBeginCombat(adjacentParty, enemy))
                yield break;
        }
    }

    private void ValidateCurrentTarget(EnemyGridMover enemy)
    {
        if (enemy == null || !enemy.HasTarget())
            return;

        if (enemy.CurrentTarget == null)
        {
            enemy.ClearTarget();
            return;
        }

        Vector2Int enemyGrid = enemy.GetCurrentGrid();
        Vector2Int targetGrid = GetTargetGrid(enemy.CurrentTargetType, enemy.CurrentTarget);

        if (IsTargetReached(enemyGrid, enemy.CurrentTargetType, enemy.CurrentTarget, targetGrid))
            enemy.ClearTarget();
    }

    private void AcquireTarget(EnemyGridMover enemy)
    {
        targetCandidates.Clear();

        Vector2Int enemyGrid = enemy.GetCurrentGrid();
        CollectTargetCandidates();

        TargetCandidate? selectedCandidate = GetClosestCandidate(enemyGrid);
        if (selectedCandidate.HasValue)
        {
            enemy.SetTarget(selectedCandidate.Value.TargetType, selectedCandidate.Value.Target);
            return;
        }

        enemy.ClearTarget();
    }

    private void CollectTargetCandidates()
    {
        if (gridManager == null)
            return;

        if (outpostRegistry != null)
        {
            IReadOnlyList<Outpost> outposts = outpostRegistry.Outposts;
            for (int i = 0; i < outposts.Count; i++)
            {
                Outpost outpost = outposts[i];
                if (outpost == null || outpost.IsEnemyClaimed)
                    continue;

                targetCandidates.Add(new TargetCandidate(
                    EnemyTargetType.Outpost,
                    outpost,
                    outpost.GetAnchorGrid(gridManager)));
            }
        }

        if (castleRegistry != null)
        {
            IReadOnlyList<CastleUnit> castles = castleRegistry.Castles;
            for (int i = 0; i < castles.Count; i++)
            {
                CastleUnit castle = castles[i];
                if (castle == null)
                    continue;

                targetCandidates.Add(new TargetCandidate(
                    EnemyTargetType.Castle,
                    castle,
                    castle.GetCurrentGrid()));
            }
        }
    }

    private TargetCandidate? GetClosestCandidate(Vector2Int enemyGrid)
    {
        TargetCandidate? closestCandidate = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < targetCandidates.Count; i++)
        {
            TargetCandidate candidate = targetCandidates[i];
            int distance = GridManager.GridDistance(enemyGrid, candidate.Grid);

            if (closestCandidate == null || distance < closestDistance)
            {
                closestCandidate = candidate;
                closestDistance = distance;
            }
        }

        return closestCandidate;
    }

    private PartyGridMover FindAdjacentParty(Vector2Int enemyGrid)
    {
        PartyGridMover[] parties = partyRegistry.PartyMovers;
        for (int i = 0; i < parties.Length; i++)
        {
            PartyGridMover party = parties[i];
            if (party == null)
                continue;

            if (IsAdjacent(enemyGrid, party.GetCurrentGrid()))
                return party;
        }

        return null;
    }

    private bool TryBeginCombat(PartyGridMover party, EnemyGridMover enemy)
    {
        if (combatEncounterManager == null)
            return false;

        if (!IsAdjacent(party.GetCurrentGrid(), enemy.GetCurrentGrid()))
            return false;

        return combatEncounterManager.BeginCombat(party, enemy);
    }

    private List<Vector2Int> FindApproachPath(
        EnemyGridMover enemy,
        EnemyTargetType targetType,
        Component target,
        Vector2Int targetGrid)
    {
        Vector2Int enemyGrid = enemy.GetCurrentGrid();
        List<Vector2Int> bestPath = null;
        Vector2Int bestApproachGrid = enemyGrid;

        List<Vector2Int> approachCandidates = GetApproachCandidates(targetType, target, targetGrid);
        for (int i = 0; i < approachCandidates.Count; i++)
        {
            Vector2Int candidateApproachGrid = approachCandidates[i];
            if (gridManager != null && !gridManager.CanOccupyCell(candidateApproachGrid, enemy.transform, true))
                continue;

            List<Vector2Int> candidatePath = pathfinder.FindPath(
                enemyGrid,
                candidateApproachGrid,
                enemy.transform,
                true);
            if (candidatePath == null || candidatePath.Count <= 1)
                continue;

            if (bestPath == null || candidatePath.Count < bestPath.Count)
            {
                bestPath = candidatePath;
                bestApproachGrid = candidateApproachGrid;
                continue;
            }

            if (bestPath != null
                && candidatePath.Count == bestPath.Count
                && IsBetterAlignedApproach(enemyGrid, targetGrid, candidateApproachGrid, bestApproachGrid))
            {
                bestPath = candidatePath;
                bestApproachGrid = candidateApproachGrid;
            }
        }

        return bestPath;
    }

    private List<Vector2Int> GetApproachCandidates(EnemyTargetType targetType, Component target, Vector2Int targetGrid)
    {
        if (targetType == EnemyTargetType.Castle && target is CastleUnit castle && gridManager != null)
            return new List<Vector2Int>(castle.GetInteractionCells());

        if (targetType == EnemyTargetType.Outpost && target is Outpost outpost)
            return new List<Vector2Int>(outpost.GetAdjacentInteractionCells(gridManager));

        List<Vector2Int> approachCandidates = new List<Vector2Int>(GridManager.Directions8.Length);
        for (int i = 0; i < GridManager.Directions8.Length; i++)
            approachCandidates.Add(targetGrid + GridManager.Directions8[i]);

        return approachCandidates;
    }

    private Vector2Int GetTargetGrid(EnemyTargetType targetType, Component target)
    {
        switch (targetType)
        {
            case EnemyTargetType.Outpost:
                if (target is Outpost outpost)
                    return gridManager != null ? outpost.GetAnchorGrid(gridManager) : Vector2Int.zero;
                break;
            case EnemyTargetType.Castle:
                if (target is CastleUnit castle)
                    return castle.GetCurrentGrid();
                break;
        }

        return target != null && gridManager != null
            ? gridManager.WorldToGrid(target.transform.position)
            : Vector2Int.zero;
    }

    private static bool IsAdjacent(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1;
    }

    private bool IsTargetReached(
        Vector2Int enemyGrid,
        EnemyTargetType targetType,
        Component target,
        Vector2Int targetGrid)
    {
        if (targetType == EnemyTargetType.Castle && target is CastleUnit castle && gridManager != null)
            return gridManager.IsAdjacentToCastle(enemyGrid, castle);

        if (targetType == EnemyTargetType.Outpost && target is Outpost outpost)
        {
            IReadOnlyList<Vector2Int> interactionCells = outpost.GetAdjacentInteractionCells(gridManager);
            for (int i = 0; i < interactionCells.Count; i++)
            {
                if (interactionCells[i] == enemyGrid)
                    return true;
            }
        }

        return IsAdjacent(enemyGrid, targetGrid);
    }

    private static List<Vector2Int> TrimPathToMovePoints(List<Vector2Int> path, int movePoints)
    {
        if (path == null || path.Count == 0)
            return path;

        int clampedSteps = Mathf.Clamp(movePoints, 0, Mathf.Max(0, path.Count - 1));
        int allowedNodeCount = clampedSteps + 1;
        if (allowedNodeCount >= path.Count)
            return path;

        return path.GetRange(0, allowedNodeCount);
    }

    private bool HandleAdjacentOutpostInteraction(EnemyGridMover enemy)
    {
        if (enemy == null || gridManager == null)
            return false;

        if (!gridManager.TryGetAdjacentOutpostGrid(enemy.GetCurrentGrid(), out Vector2Int outpostGrid))
            return false;

        if (!gridManager.TryGetOutpostObjectAtGrid(outpostGrid, out Outpost outpost))
            return false;

        if (outpost.IsEnemyClaimed)
            return false;

        if (enemy.CurrentTarget == outpost)
            enemy.ClearTarget();

        outpost.EnemyClaim();
        return true;
    }

    private static bool IsBetterAlignedApproach(
        Vector2Int enemyGrid,
        Vector2Int targetGrid,
        Vector2Int candidateApproachGrid,
        Vector2Int currentBestApproachGrid)
    {
        int candidateAlignment = GetApproachAlignmentScore(enemyGrid, targetGrid, candidateApproachGrid);
        int currentAlignment = GetApproachAlignmentScore(enemyGrid, targetGrid, currentBestApproachGrid);
        if (candidateAlignment != currentAlignment)
            return candidateAlignment > currentAlignment;

        int candidateDistance = GridManager.GridDistance(enemyGrid, candidateApproachGrid);
        int currentDistance = GridManager.GridDistance(enemyGrid, currentBestApproachGrid);
        if (candidateDistance != currentDistance)
            return candidateDistance < currentDistance;

        int candidateManhattan =
            Mathf.Abs(targetGrid.x - candidateApproachGrid.x) + Mathf.Abs(targetGrid.y - candidateApproachGrid.y);
        int currentManhattan =
            Mathf.Abs(targetGrid.x - currentBestApproachGrid.x) + Mathf.Abs(targetGrid.y - currentBestApproachGrid.y);
        return candidateManhattan < currentManhattan;
    }

    private static int GetApproachAlignmentScore(Vector2Int enemyGrid, Vector2Int targetGrid, Vector2Int approachGrid)
    {
        Vector2Int toTarget = targetGrid - enemyGrid;
        Vector2Int toApproach = approachGrid - enemyGrid;

        int dot = toTarget.x * toApproach.x + toTarget.y * toApproach.y;
        int axisMatch = 0;

        if (toTarget.x != 0 && toApproach.x != 0 && Mathf.Sign(toTarget.x) == Mathf.Sign(toApproach.x))
            axisMatch++;

        if (toTarget.y != 0 && toApproach.y != 0 && Mathf.Sign(toTarget.y) == Mathf.Sign(toApproach.y))
            axisMatch++;

        return dot * 10 + axisMatch;
    }

    private void ResolveReferences()
    {
        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (castleRegistry == null)
            castleRegistry = FindFirstObjectByType<CastleRegistry>();

        if (pathfinder == null)
            pathfinder = FindFirstObjectByType<AStarPathfinder>();

        if (combatEncounterManager == null)
            combatEncounterManager = FindFirstObjectByType<CombatEncounterManager>();

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();
    }
}
