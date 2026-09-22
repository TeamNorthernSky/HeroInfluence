using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PartyIdentity))]
[RequireComponent(typeof(PartyComposition))]
public class PartyGridMover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;

    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float arriveThreshold = 0.01f;
    [SerializeField] private int maxMovePoints = 10;
    [SerializeField] private bool usePersistentState = true;

    private readonly Queue<Vector2Int> pathQueue = new Queue<Vector2Int>();
    private bool isMoving;
    private bool stopRequested;
    private Vector2Int currentGrid;
    private float fixedY;
    private PartyMovePointController movePointController;
    private PartyIdentity cachedIdentity;
    private bool restoredPersistentPosition;
    private bool canRefreshHeroUnionStartAfterLevelLoad;
    private const string ScenePartyPrefabKey = "scene";
    private const string DefaultStartHeroUnionZoneId = "zone_001";

    public Vector2Int? TargetInteractionGrid { get; private set; }

    public event Action<List<Vector2Int>> PathUpdated;
    public event Action<Vector2Int> GridEntered;
    public event Action MoveCompleted;
    public event Action<bool> MovementStateChanged;

    public void SetPersistentStateEnabled(bool enabled)
    {
        usePersistentState = enabled;
    }

    private void Awake()
    {
        fixedY = transform.position.y;
        cachedIdentity = GetComponent<PartyIdentity>();
        currentGrid = gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
        movePointController = new PartyMovePointController(maxMovePoints);
    }

    private void OnEnable()
    {
        LevelLoader.RuntimeLevelLoaded -= HandleRuntimeLevelLoaded;
        LevelLoader.RuntimeLevelLoaded += HandleRuntimeLevelLoaded;
        LevelZoneLayoutLoader.RuntimeLayoutLoaded -= HandleRuntimeLayoutLoaded;
        LevelZoneLayoutLoader.RuntimeLayoutLoaded += HandleRuntimeLayoutLoaded;
    }

    private void OnDisable()
    {
        LevelLoader.RuntimeLevelLoaded -= HandleRuntimeLevelLoaded;
        LevelZoneLayoutLoader.RuntimeLayoutLoaded -= HandleRuntimeLayoutLoaded;
    }

    // [JC 추가 260511] 위치 영속화: PartyPersistentData.LastGrid가 있으면 그 위치로 복원
    // Start로 둔 이유: PartyPersistentRepository 초기화 이후 저장 위치를 적용하기 위함
    // [JC 수정 260512] 머지 사이클: PartyPersistentRepository로 책임 이관됨
    // [JC 수정 260512] LastGrid 없을 때 currentGrid 재계산 + GridEntered 발화 추가.
    //   원인: Awake 시점에 transform.position 또는 GridManager 내부 상태가 부정확해 currentGrid가 (0,0)으로 박힘.
    //   결과: 본부 방문 인디케이터가 새 게임 첫 진입 시 활성화 안 되는 버그 발생. 이 보정으로 첫 진입부터 정확.
    // [JC 수정 260514 R-1] SnapToGridPosition을 notifyMoveCompleted=false로 호출 — Start 시점 자동 전투 트리거 방지.
    private void Start()
    {
        var identity = cachedIdentity != null ? cachedIdentity : GetComponent<PartyIdentity>();
        if (identity == null) return;
        cachedIdentity = identity;

        if (!ShouldUsePersistentState(identity))
        {
            currentGrid = gridManager != null ? gridManager.WorldToGrid(transform.position) : currentGrid;
            GridEntered?.Invoke(currentGrid);
            return;
        }

        var repo = PartyPersistentRepository.Instance;
        PartyPersistentData partyData = null;
        repo?.TryGetParty(identity.PartyId, out partyData);

        if (partyData != null && partyData.HasRemainingMovePoints)
            movePointController?.SetRemaining(partyData.RemainingMovePoints);
        else if (partyData != null)
            partyData.SetRemainingMovePoints(RemainingMovePoints);

        string placementKey = ResolvePlacementKey(identity);
        MapProgressRepository progressRepository = MapProgressRepository.Instance;

        bool restoredPosition = false;
        if (progressRepository != null &&
            progressRepository.TryGetPartyState(placementKey, out PartyWorldState worldState) &&
            worldState != null &&
            !worldState.Removed)
        {
            SnapToGridPosition(worldState.Grid, notifyMoveCompleted: false);
            restoredPosition = true;
            restoredPersistentPosition = true;
            canRefreshHeroUnionStartAfterLevelLoad = false;
        }
        else if (partyData != null && partyData.HasLastGrid)
        {
            SnapToGridPosition(partyData.LastGrid, notifyMoveCompleted: false);
            restoredPosition = true;
            restoredPersistentPosition = true;
            canRefreshHeroUnionStartAfterLevelLoad = false;
        }
        else if (gridManager != null)
        {
            canRefreshHeroUnionStartAfterLevelLoad = true;
            if (TryResolveHeroUnionStartGrid(out Vector2Int startGrid))
            {
                SnapToGridPosition(startGrid, notifyMoveCompleted: false);
                restoredPosition = true;
            }
            else
            {
                StartCoroutine(SnapToHeroUnionStartOrCurrentNextFrame(identity));
            }
        }

        if (restoredPosition && partyData == null)
            StartCoroutine(PersistCurrentGridWhenPartyDataReady(identity));
    }

    private void HandleRuntimeLevelLoaded(LevelLoader _)
    {
        RefreshHeroUnionStartAfterLevelLoad();
        RefreshCurrentGridSurfaceHeight();
    }

    private void HandleRuntimeLayoutLoaded(LevelZoneLayoutLoader _)
    {
        RefreshHeroUnionStartAfterLevelLoad();
        RefreshCurrentGridSurfaceHeight();
    }

    private void RefreshHeroUnionStartAfterLevelLoad()
    {
        if (!Application.isPlaying ||
            restoredPersistentPosition ||
            !canRefreshHeroUnionStartAfterLevelLoad ||
            !ShouldUsePersistentState(cachedIdentity) ||
            gridManager == null ||
            cachedIdentity == null)
        {
            return;
        }

        if (!TryResolveHeroUnionStartGrid(out Vector2Int startGrid))
            return;

        SnapToGridPosition(startGrid, notifyMoveCompleted: false);
        canRefreshHeroUnionStartAfterLevelLoad = false;
        StartCoroutine(PersistCurrentGridWhenPartyDataReady(cachedIdentity));
    }

    private void RefreshCurrentGridSurfaceHeight()
    {
        if (!Application.isPlaying || isMoving || gridManager == null)
            return;

        transform.position = GetWorldPositionForGrid(currentGrid);
    }

    private void Update()
    {
        if (!isMoving || pathQueue.Count == 0 || gridManager == null)
            return;

        Vector2Int nextGrid = pathQueue.Peek();
        Vector3 target = GetWorldPositionForGrid(nextGrid);

        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude <= arriveThreshold * arriveThreshold)
        {
            transform.position = target;
            pathQueue.Dequeue();
            currentGrid = nextGrid;
            if (!DHExplorationCheatState.UnlimitedMovePoints)
                movePointController?.SpendStep();
            bool reachedPathEnd = pathQueue.Count == 0;

            GridEntered?.Invoke(currentGrid);
            // [JC 추가 260511] 한 칸 이동마다 위치 영속화 (이동 도중 정지 케이스 포함 안전 저장)
            PersistLastGrid();
            NotifyPathUpdated();

            if (reachedPathEnd && pathQueue.Count == 0)
            {
                stopRequested = false;
                SetMoving(false);
                TargetInteractionGrid = null;
                MoveCompleted?.Invoke();
            }
        }
    }

    public Vector2Int GetCurrentGrid()
    {
        return currentGrid;
    }

    public bool IsMoving => isMoving;
    public int RemainingMovePoints => DHExplorationCheatState.UnlimitedMovePoints
        ? maxMovePoints
        : movePointController != null ? movePointController.RemainingMovePoints : 0;
    public int MaxMovePoints => maxMovePoints;

    public bool CanSpendMovePoints(int amount)
    {
        return HasAnyValidPartyUnit() &&
            (DHExplorationCheatState.UnlimitedMovePoints ||
             movePointController != null && movePointController.CanSpend(amount));
    }

    public void ResetMovePointsToMax()
    {
        movePointController?.ResetToMax();
        PersistLastGrid();
    }

    // [JC 추가 260511] Snap도 영속화 (외부 위치 강제 변경 케이스 안전 처리)
    // [JC 추가 260514 R-1] notifyMoveCompleted 인자 — Start 영속 복원 시 MoveCompleted 발화 회피용 (false)
    public void SetRemainingMovePoints(int amount)
    {
        movePointController?.SetRemaining(amount);
        PersistLastGrid();
    }

    public void SnapToGridPosition(Vector2Int grid, bool notifyMoveCompleted = true)
    {
        pathQueue.Clear();
        SetMoving(false);
        stopRequested = false;
        TargetInteractionGrid = null;
        currentGrid = grid;

        if (gridManager == null)
            return;

        transform.position = GetWorldPositionForGrid(grid);
        GridEntered?.Invoke(currentGrid);
        PersistLastGrid();
        NotifyPathUpdated();
        if (notifyMoveCompleted)
            MoveCompleted?.Invoke();
    }

    // [JC 추가 260511] 이동 중 클릭 정지. 마커는 유지하되 path는 재계산되도록 외부에서 처리.
    // 셀 사이에서 정지 시 가장 가까운 셀로 스냅. 1셀 진행으로 판정되면 이동력 1 차감 (무료 이동 방지).
    // [JC 추가 260514 R-2] TargetInteractionGrid 리셋 — StopMovement 후 PathUpdated 발화 시
    //   HandlePathUpdated가 우연히 자동 상호작용 트리거하는 위험 차단. Orora SnapToGridPosition/MoveByGridPath 패턴과 일치.
    public void StopMovement()
    {
        if (!isMoving && pathQueue.Count == 0) return;
        if (stopRequested) return;

        if (gridManager == null)
        {
            pathQueue.Clear();
            SetMoving(false);
            TargetInteractionGrid = null;
            NotifyPathUpdated();
            return;
        }

        if (isMoving && pathQueue.Count > 0)
        {
            // Stop after reaching the next queued cell to avoid snapping backward mid-step.
            Vector2Int stopGrid = pathQueue.Peek();
            pathQueue.Clear();
            pathQueue.Enqueue(stopGrid);
            TargetInteractionGrid = null;
            stopRequested = true;
            NotifyPathUpdated();
            return;
        }

        pathQueue.Clear();
        SetMoving(false);
        TargetInteractionGrid = null;

        transform.position = GetWorldPositionForGrid(currentGrid);
        PersistLastGrid();
        NotifyPathUpdated();

    }

    // [JC 추가 260511] 현재 위치를 PartyPersistentData.LastGrid로 저장
    // [JC 수정 260512] 머지 사이클: PartyPersistentRepository로 책임 이관됨
    private void PersistLastGrid()
    {
        if (!ShouldUsePersistentState())
            return;

        var identity = GetComponent<PartyIdentity>();
        if (identity == null) return;

        var repo = PartyPersistentRepository.Instance;
        if (repo == null) return;

        if (!repo.TryGetParty(identity.PartyId, out var partyData) || partyData == null) return;
        partyData.SetLastGrid(currentGrid);
        partyData.SetRemainingMovePoints(RemainingMovePoints);
        PersistPartyWorldState(identity, currentGrid);
    }

    private string ResolvePlacementKey(PartyIdentity identity)
    {
        if (identity == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(identity.PlacementKey))
            return identity.PlacementKey;

        string placementKey = MapProgressKey.ForSceneParty(identity.PartyId);
        identity.SetPlacementKey(placementKey);
        return placementKey;
    }

    private void PersistPartyWorldState(PartyIdentity identity, Vector2Int grid)
    {
        if (!ShouldUsePersistentState(identity))
            return;

        if (identity == null)
            return;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        if (progressRepository == null)
            return;

        string placementKey = ResolvePlacementKey(identity);
        progressRepository.BindParty(
            placementKey,
            identity.PartyId,
            grid,
            identity.PlacementSource,
            ScenePartyPrefabKey);
    }

    private IEnumerator SnapToHeroUnionStartOrCurrentNextFrame(PartyIdentity identity)
    {
        if (!ShouldUsePersistentState(identity))
            yield break;

        yield return null;

        if (identity == null || gridManager == null)
            yield break;

        if (TryResolveHeroUnionStartGrid(out Vector2Int startGrid))
        {
            SnapToGridPosition(startGrid, notifyMoveCompleted: false);
            StartCoroutine(PersistCurrentGridWhenPartyDataReady(identity));
            yield break;
        }

        currentGrid = gridManager.WorldToGrid(transform.position);
        GridEntered?.Invoke(currentGrid);
        PersistPartyWorldState(identity, currentGrid);
        StartCoroutine(PersistCurrentGridWhenPartyDataReady(identity));
    }

    private IEnumerator PersistCurrentGridWhenPartyDataReady(PartyIdentity identity)
    {
        if (!ShouldUsePersistentState(identity))
            yield break;

        const int maxAttempts = 5;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            yield return null;

            if (identity == null)
                yield break;

            PartyPersistentRepository repo = PartyPersistentRepository.Instance;
            if (repo == null || !repo.TryGetParty(identity.PartyId, out PartyPersistentData partyData) || partyData == null)
                continue;

            partyData.SetLastGrid(currentGrid);
            partyData.SetRemainingMovePoints(RemainingMovePoints);
            PersistPartyWorldState(identity, currentGrid);
            yield break;
        }
    }

    private bool TryResolveHeroUnionStartGrid(out Vector2Int startGrid)
    {
        startGrid = Vector2Int.zero;

        HeroUnionUnit heroUnion = ResolveStartHeroUnion();
        if (heroUnion == null)
            return false;

        IReadOnlyList<Vector2Int> interactionCells = heroUnion.GetInteractionCells();
        if (interactionCells == null || interactionCells.Count == 0)
            return false;

        MultiGridOccupant occupant = heroUnion.GetComponent<MultiGridOccupant>();
        Vector2Int heroUnionGrid = occupant != null
            ? new Vector2Int(occupant.AnchorGrid.x + (occupant.Size.x - 1) / 2, occupant.AnchorGrid.y - 1)
            : heroUnion.GetCurrentGrid();

        startGrid = interactionCells[0];
        int bestDistance = GetSquaredGridDistance(startGrid, heroUnionGrid);
        for (int i = 1; i < interactionCells.Count; i++)
        {
            Vector2Int candidate = interactionCells[i];
            int distance = GetSquaredGridDistance(candidate, heroUnionGrid);
            if (distance >= bestDistance)
                continue;

            startGrid = candidate;
            bestDistance = distance;
        }

        return true;
    }

    private bool ShouldUsePersistentState()
    {
        PartyIdentity identity = cachedIdentity != null ? cachedIdentity : GetComponent<PartyIdentity>();
        return ShouldUsePersistentState(identity);
    }

    private bool ShouldUsePersistentState(PartyIdentity identity)
    {
        if (!usePersistentState)
            return false;

        if (identity == null)
            return true;

        return !IsTutorialIdentity(identity);
    }

    private static bool IsTutorialIdentity(PartyIdentity identity)
    {
        return IsTutorialKey(identity.PartyId) || IsTutorialKey(identity.PlacementKey);
    }

    private static bool IsTutorialKey(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.StartsWith("tutorial_", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetSquaredGridDistance(Vector2Int left, Vector2Int right)
    {
        int dx = left.x - right.x;
        int dy = left.y - right.y;
        return dx * dx + dy * dy;
    }

    private static HeroUnionUnit ResolveStartHeroUnion()
    {
        HeroUnionRegistry registry = FindFirstObjectByType<HeroUnionRegistry>();
        if (registry != null)
        {
            if (registry.TryGetClaimedByZoneId(DefaultStartHeroUnionZoneId, out HeroUnionUnit claimedStartHeroUnion))
                return claimedStartHeroUnion;

            if (registry.TryGetByZoneId(DefaultStartHeroUnionZoneId, out HeroUnionUnit startHeroUnion))
                return startHeroUnion;

            if (registry.TryGetFirstClaimed(out HeroUnionUnit claimedHeroUnion))
                return claimedHeroUnion;

            IReadOnlyList<HeroUnionUnit> heroUnions = registry.HeroUnions;
            for (int i = 0; i < heroUnions.Count; i++)
            {
                if (heroUnions[i] != null)
                    return heroUnions[i];
            }
        }

        HeroUnionUnit[] sceneHeroUnions = FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);
        if (sceneHeroUnions == null || sceneHeroUnions.Length == 0)
            return null;

        HeroUnionUnit firstClaimed = null;
        for (int i = 0; i < sceneHeroUnions.Length; i++)
        {
            HeroUnionUnit heroUnion = sceneHeroUnions[i];
            if (heroUnion == null)
                continue;

            if (string.Equals(heroUnion.ZoneId, DefaultStartHeroUnionZoneId, StringComparison.Ordinal))
                return heroUnion;

            if (firstClaimed == null && heroUnion.IsClaimedByHero)
                firstClaimed = heroUnion;
        }

        return firstClaimed != null ? firstClaimed : sceneHeroUnions[0];
    }

    public List<Vector2Int> GetRemainingPath()
    {
        var remainingPath = new List<Vector2Int>();
        remainingPath.AddRange(pathQueue);
        return remainingPath;
    }

    public void MoveByGridPath(List<Vector2Int> fullPath, Vector2Int? interactionTarget = null)
    {
        TargetInteractionGrid = interactionTarget;
        pathQueue.Clear();
        SetMoving(false);
        stopRequested = false;

        if (!HasAnyValidPartyUnit())
        {
            TargetInteractionGrid = null;
            NotifyPathUpdated();
            return;
        }

        if (fullPath != null && fullPath.Count > 0)
            currentGrid = fullPath[0];

        if (fullPath == null || fullPath.Count <= 1)
        {
            NotifyPathUpdated();

            if (interactionTarget.HasValue && !TargetInteractionGrid.HasValue)
                return;

            TargetInteractionGrid = null;
            MoveCompleted?.Invoke();
            return;
        }

        // path[0]는 현재 위치이므로 제외하고 enqueue
        int moveCost = GetPathMoveCost(fullPath);
        if (!CanSpendMovePoints(moveCost))
        {
            NotifyPathUpdated();
            return;
        }

        for (int i = 1; i < fullPath.Count; i++)
            pathQueue.Enqueue(fullPath[i]);

        SetMoving(pathQueue.Count > 0);
        NotifyPathUpdated();
    }

    private void SetMoving(bool moving)
    {
        if (isMoving == moving)
            return;

        isMoving = moving;
        MovementStateChanged?.Invoke(isMoving);
    }

    private void NotifyPathUpdated()
    {
        var remainingPath = new List<Vector2Int> { GetCurrentGrid() };
        remainingPath.AddRange(pathQueue);
        PathUpdated?.Invoke(remainingPath);
    }

    private static int GetPathMoveCost(List<Vector2Int> path)
    {
        return path == null ? 0 : Mathf.Max(0, path.Count - 1);
    }

    private Vector3 GetWorldPositionForGrid(Vector2Int grid)
    {
        Vector3 worldPosition = gridManager.GridToWorldCenter(grid);
        float heightOffset = fixedY - gridManager.GetLandSurfaceY();
        worldPosition.y = gridManager.GetCellSurfaceY(grid) + heightOffset;
        return worldPosition;
    }

    private bool HasAnyValidPartyUnit()
    {
        TutorialPartyComposition tutorialComposition = GetComponent<TutorialPartyComposition>();
        if (tutorialComposition != null)
            return tutorialComposition.HasAnyJoinedUnit;

        PartyComposition composition = GetComponent<PartyComposition>();
        if (composition == null)
            return false;

        int[] unitIndices = composition.UnitIndices;
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;

        for (int i = 0; i < unitIndices.Length; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0)
                continue;

            if (repository == null || repository.ContainsUnit(unitIndex))
                return true;
        }

        return false;
    }
}

