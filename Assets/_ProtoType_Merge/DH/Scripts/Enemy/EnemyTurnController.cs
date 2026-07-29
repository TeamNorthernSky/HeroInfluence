using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemyTurnController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [SerializeField] private AStarPathfinder pathfinder;
    [SerializeField] private CombatEncounterManager combatEncounterManager;
    [SerializeField] private CombatPromptService combatPromptService;
    [SerializeField] private GridManager gridManager;
    [FormerlySerializedAs("mineRegistry")]
    [SerializeField] private OutpostRegistry outpostRegistry;

    private readonly List<TargetCandidate> targetCandidates = new List<TargetCandidate>();
    private EnemyTurnSessionRepository turnSessionRepository;
    private bool interruptedByCombat;

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
        interruptedByCombat = false;

        if (DHGameEndState.IsEnding)
            yield break;

        ResolveReferences();
        turnSessionRepository = EnemyTurnSessionRepository.Instance;

        if (enemyRegistry == null || partyRegistry == null || pathfinder == null)
            yield break;

        if (turnSessionRepository == null)
        {
            Debug.LogWarning("EnemyTurnController could not find an EnemyTurnSessionRepository.", this);
            yield break;
        }

        if (turnSessionRepository != null)
        {
            if (!turnSessionRepository.IsActive)
                turnSessionRepository.BeginSession();
            else if (turnSessionRepository.ShouldResumeAfterCombat)
                turnSessionRepository.MarkResumedFromCombat();
        }

        if (turnSessionRepository != null &&
            turnSessionRepository.HasCurrentEnemy &&
            turnSessionRepository.RemainingMovePoints > 0)
        {
            EnemyGridMover currentEnemy = FindSessionCurrentEnemy();
            if (currentEnemy != null && !currentEnemy.IsStatic && !IsEnemyDefeated(currentEnemy))
            {
                yield return ProcessEnemy(currentEnemy, turnSessionRepository.RemainingMovePoints);
                if (interruptedByCombat)
                    yield break;
            }

            turnSessionRepository.CompleteCurrentEnemy();
        }

        while (!DHGameEndState.IsEnding)
        {
            EnemyGridMover enemy = GetNextUnprocessedEnemy();
            if (enemy == null)
                break;

            turnSessionRepository?.SetCurrentEnemy(
                ResolveEnemyPlacementKey(enemy),
                enemy.EnemyId,
                enemy.MovePointsPerTurn);

            yield return ProcessEnemy(enemy, enemy.MovePointsPerTurn);
            if (interruptedByCombat)
                yield break;

            turnSessionRepository?.CompleteCurrentEnemy();
        }

        turnSessionRepository?.ClearSession();
    }

    private IEnumerator ProcessEnemy(EnemyGridMover enemy, int movePoints)
    {
        if (enemy == null || enemy.IsStatic || movePoints < 0)
            yield break;

        ValidateCurrentTarget(enemy);

        if (!enemy.HasTarget())
            AcquireTarget(enemy);

        if (!enemy.HasTarget())
            yield break;

        PartyGridMover adjacentParty = FindAdjacentParty(enemy.GetCurrentGrid());
        if (adjacentParty != null)
        {
            bool combatHandled = false;
            yield return TryBeginCombat(adjacentParty, enemy, movePoints, handled => combatHandled = handled);
            if (combatHandled)
                yield break;
        }

        Vector2Int targetGrid = GetTargetGrid(enemy.CurrentTargetType, enemy.CurrentTarget);
        List<Vector2Int> fullPath = FindApproachPath(
            enemy,
            enemy.CurrentTargetType,
            enemy.CurrentTarget,
            targetGrid);
        List<Vector2Int> movePath = TrimPathToMovePoints(fullPath, movePoints);
        int totalMoveSteps = GetPathMoveCost(movePath);
        int usedSteps = 0;
        bool interruptedDuringMove = false;
        PartyGridMover pendingCombatParty = null;
        EnemyGridMover pendingCombatEnemy = null;
        int pendingCombatRemainingMovePoints = 0;

        if (movePath != null && movePath.Count > 1)
        {
            yield return enemy.MoveAlongPath(
                movePath,
                (movingEnemy, arrivedGrid, stepsUsed) =>
                {
                    usedSteps = stepsUsed;

                    if (stepsUsed >= totalMoveSteps)
                        return true;

                    int remainingMovePointsDuringMove = Mathf.Max(0, movePoints - stepsUsed);
                    turnSessionRepository?.SetCurrentEnemy(
                        ResolveEnemyPlacementKey(movingEnemy),
                        movingEnemy.EnemyId,
                        remainingMovePointsDuringMove);

                    PartyGridMover partyOnRoute = FindAdjacentParty(arrivedGrid);
                    if (partyOnRoute == null)
                        return true;

                    interruptedDuringMove = true;
                    pendingCombatParty = partyOnRoute;
                    pendingCombatEnemy = movingEnemy;
                    pendingCombatRemainingMovePoints = remainingMovePointsDuringMove;
                    return false;
                });
        }

        if (interruptedDuringMove)
        {
            bool combatHandled = false;
            yield return TryBeginCombat(
                pendingCombatParty,
                pendingCombatEnemy,
                pendingCombatRemainingMovePoints,
                handled => combatHandled = handled);

            if (combatHandled)
                yield break;
        }

        if (interruptedByCombat)
            yield break;

        int remainingMovePoints = Mathf.Max(0, movePoints - usedSteps);
        turnSessionRepository?.SetCurrentEnemy(
            ResolveEnemyPlacementKey(enemy),
            enemy.EnemyId,
            remainingMovePoints);

        adjacentParty = FindAdjacentParty(enemy.GetCurrentGrid());
        if (adjacentParty != null)
        {
            bool combatHandled = false;
            yield return TryBeginCombat(adjacentParty, enemy, remainingMovePoints, handled => combatHandled = handled);
            if (combatHandled)
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

        if (enemy.CurrentTargetType == EnemyTargetType.HeroUnion &&
            enemy.CurrentTarget is HeroUnionUnit heroUnion &&
            !heroUnion.IsClaimedByHero)
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

        if (heroUnionRegistry != null)
        {
            IReadOnlyList<HeroUnionUnit> heroUnions = heroUnionRegistry.HeroUnions;
            for (int i = 0; i < heroUnions.Count; i++)
            {
                HeroUnionUnit heroUnion = heroUnions[i];
                if (heroUnion == null || !heroUnion.IsClaimedByHero)
                    continue;

                targetCandidates.Add(new TargetCandidate(
                    EnemyTargetType.HeroUnion,
                    heroUnion,
                    heroUnion.GetCurrentGrid()));
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
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null)
            return null;

        if (DefeatedPartyReturnController.IsPartyWaiting(party))
            return null;

        return IsAdjacent(enemyGrid, party.GetCurrentGrid()) ? party : null;
    }

    private IEnumerator TryBeginCombat(
        PartyGridMover party,
        EnemyGridMover enemy,
        int remainingMovePoints,
        System.Action<bool> onComplete)
    {
        if (DHGameEndState.IsEnding)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        if (combatEncounterManager == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        if (party == null || enemy == null || !IsAdjacent(party.GetCurrentGrid(), enemy.GetCurrentGrid()))
        {
            onComplete?.Invoke(false);
            yield break;
        }

        bool eventEncounterClosed = false;
        bool eventEncounterStartedCombat = false;
        if (EnemyEventEncounterService.TryOpenEncounterChat(
                party,
                enemy,
                startedCombat =>
                {
                    eventEncounterStartedCombat = startedCombat;
                    eventEncounterClosed = true;
                }))
        {
            yield return new WaitUntil(() => eventEncounterClosed);

            if (eventEncounterStartedCombat)
                InterruptForCombat(enemy, remainingMovePoints);

            onComplete?.Invoke(true);
            yield break;
        }

        if (combatPromptService != null && !combatPromptService.IsOpen)
        {
            bool promptClosed = false;
            bool startedCombat = false;
            bool promptOpened = combatPromptService.TryOpenEnemyCombatPrompt(
                party,
                enemy,
                combatEncounterManager,
                keepInputLocked =>
                {
                    startedCombat = keepInputLocked;
                    promptClosed = true;
                });

            if (promptOpened)
            {
                yield return new WaitUntil(() =>
                    promptClosed ||
                    combatPromptService == null ||
                    !combatPromptService.IsOpen);

                if (startedCombat)
                    InterruptForCombat(enemy, remainingMovePoints);

                onComplete?.Invoke(true);
                yield break;
            }
        }

        bool combatStarted = combatEncounterManager.BeginCombat(party, enemy);
        if (!combatStarted)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        InterruptForCombat(enemy, remainingMovePoints);
        onComplete?.Invoke(true);
    }

    private void InterruptForCombat(EnemyGridMover enemy, int remainingMovePoints)
    {
        interruptedByCombat = true;
        turnSessionRepository?.InterruptForCombat(
            ResolveEnemyPlacementKey(enemy),
            enemy != null ? enemy.EnemyId : string.Empty,
            remainingMovePoints);
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
        if (targetType == EnemyTargetType.HeroUnion && target is HeroUnionUnit heroUnion && gridManager != null)
            return new List<Vector2Int>(heroUnion.GetInteractionCells());

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
            case EnemyTargetType.HeroUnion:
                if (target is HeroUnionUnit heroUnion)
                    return heroUnion.GetCurrentGrid();
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
        if (targetType == EnemyTargetType.HeroUnion && target is HeroUnionUnit heroUnion && gridManager != null)
            return gridManager.IsAdjacentToHeroUnion(enemyGrid, heroUnion);

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

    private EnemyGridMover GetNextUnprocessedEnemy()
    {
        if (enemyRegistry == null)
            return null;

        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null || enemy.IsStatic || IsEnemyDefeated(enemy))
                continue;

            string placementKey = ResolveEnemyPlacementKey(enemy);
            if (turnSessionRepository != null && turnSessionRepository.IsProcessed(placementKey))
                continue;

            return enemy;
        }

        return null;
    }

    private EnemyGridMover FindSessionCurrentEnemy()
    {
        if (turnSessionRepository == null || enemyRegistry == null)
            return null;

        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null)
                continue;

            string placementKey = ResolveEnemyPlacementKey(enemy);
            if (!string.IsNullOrWhiteSpace(turnSessionRepository.CurrentEnemyPlacementKey) &&
                string.Equals(placementKey, turnSessionRepository.CurrentEnemyPlacementKey, System.StringComparison.Ordinal))
                return enemy;

            if (!string.IsNullOrWhiteSpace(turnSessionRepository.CurrentEnemyId) &&
                string.Equals(enemy.EnemyId, turnSessionRepository.CurrentEnemyId, System.StringComparison.Ordinal))
                return enemy;
        }

        return null;
    }

    private static bool IsEnemyDefeated(EnemyGridMover enemy)
    {
        string placementKey = ResolveEnemyPlacementKey(enemy);
        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        return progressRepository != null &&
            !string.IsNullOrWhiteSpace(placementKey) &&
            progressRepository.IsEnemyDefeated(placementKey);
    }

    private static string ResolveEnemyPlacementKey(EnemyGridMover enemy)
    {
        EnemyIdentity identity = enemy != null ? enemy.GetComponent<EnemyIdentity>() : null;
        if (identity != null && !string.IsNullOrWhiteSpace(identity.PlacementKey))
            return identity.PlacementKey;

        return enemy != null ? MapProgressKey.ForSceneEnemy(enemy.GetCurrentGrid()) : string.Empty;
    }

    private static int GetPathMoveCost(List<Vector2Int> path)
    {
        return path == null ? 0 : Mathf.Max(0, path.Count - 1);
    }

    private bool HandleAdjacentOutpostInteraction(EnemyGridMover enemy)
    {
        if (DHGameEndState.IsEnding)
            return false;

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

        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();

        if (pathfinder == null)
            pathfinder = FindFirstObjectByType<AStarPathfinder>();

        if (combatEncounterManager == null)
            combatEncounterManager = FindFirstObjectByType<CombatEncounterManager>();

        if (combatPromptService == null)
            combatPromptService = FindFirstObjectByType<CombatPromptService>();

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();
    }
}

public class EnemyTurnSessionRepository : MonoBehaviour
{
    public static EnemyTurnSessionRepository Instance { get; private set; }

    [SerializeField] private bool isActive;
    [SerializeField] private bool shouldResumeAfterCombat;
    [SerializeField] private string currentEnemyPlacementKey;
    [SerializeField] private string currentEnemyId;
    [SerializeField] private int remainingMovePoints;
    [SerializeField] private List<string> processedEnemyPlacementKeys = new List<string>();

    private readonly HashSet<string> processedEnemyLookup = new HashSet<string>();

    public bool IsActive => isActive;
    public bool ShouldResumeAfterCombat => isActive && shouldResumeAfterCombat;
    public bool HasCurrentEnemy => !string.IsNullOrWhiteSpace(currentEnemyPlacementKey) ||
        !string.IsNullOrWhiteSpace(currentEnemyId);
    public string CurrentEnemyPlacementKey => currentEnemyPlacementKey;
    public string CurrentEnemyId => currentEnemyId;
    public int RemainingMovePoints => Mathf.Max(0, remainingMovePoints);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildLookup();
    }

    private void OnValidate()
    {
        remainingMovePoints = Mathf.Max(0, remainingMovePoints);
        RebuildLookup();
    }

    public void BeginSession()
    {
        isActive = true;
        shouldResumeAfterCombat = false;
        currentEnemyPlacementKey = string.Empty;
        currentEnemyId = string.Empty;
        remainingMovePoints = 0;
        processedEnemyPlacementKeys.Clear();
        processedEnemyLookup.Clear();
    }

    public void ClearSession()
    {
        isActive = false;
        shouldResumeAfterCombat = false;
        currentEnemyPlacementKey = string.Empty;
        currentEnemyId = string.Empty;
        remainingMovePoints = 0;
        processedEnemyPlacementKeys.Clear();
        processedEnemyLookup.Clear();
    }

    public void MarkResumedFromCombat()
    {
        shouldResumeAfterCombat = false;
    }

    public void SetCurrentEnemy(string placementKey, string enemyId, int movePoints)
    {
        if (!isActive)
            isActive = true;

        currentEnemyPlacementKey = NormalizeKey(placementKey);
        currentEnemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
        remainingMovePoints = Mathf.Max(0, movePoints);
    }

    public void InterruptForCombat(string placementKey, string enemyId, int movePoints)
    {
        SetCurrentEnemy(placementKey, enemyId, movePoints);
        shouldResumeAfterCombat = true;
    }

    public void CompleteCurrentEnemy()
    {
        MarkProcessed(currentEnemyPlacementKey);
        currentEnemyPlacementKey = string.Empty;
        currentEnemyId = string.Empty;
        remainingMovePoints = 0;
    }

    public bool IsProcessed(string placementKey)
    {
        string normalizedKey = NormalizeKey(placementKey);
        return !string.IsNullOrWhiteSpace(normalizedKey) && processedEnemyLookup.Contains(normalizedKey);
    }

    private void MarkProcessed(string placementKey)
    {
        string normalizedKey = NormalizeKey(placementKey);
        if (string.IsNullOrWhiteSpace(normalizedKey) || !processedEnemyLookup.Add(normalizedKey))
            return;

        processedEnemyPlacementKeys.Add(normalizedKey);
    }

    private void RebuildLookup()
    {
        processedEnemyLookup.Clear();

        if (processedEnemyPlacementKeys == null)
            processedEnemyPlacementKeys = new List<string>();

        for (int i = processedEnemyPlacementKeys.Count - 1; i >= 0; i--)
        {
            string normalizedKey = NormalizeKey(processedEnemyPlacementKeys[i]);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                processedEnemyPlacementKeys.RemoveAt(i);
                continue;
            }

            processedEnemyPlacementKeys[i] = normalizedKey;
        }

        for (int i = 0; i < processedEnemyPlacementKeys.Count; i++)
            processedEnemyLookup.Add(processedEnemyPlacementKeys[i]);

        currentEnemyPlacementKey = NormalizeKey(currentEnemyPlacementKey);
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key)
            ? string.Empty
            : MapProgressKey.NormalizeSegment(key);
    }
}

public static class EnemyTurnSessionRepositoryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRepository()
    {
        if (EnemyTurnSessionRepository.Instance != null)
            return;

        var go = new GameObject("[EnemyTurnSessionRepository]");
        go.AddComponent<EnemyTurnSessionRepository>();
    }
}
