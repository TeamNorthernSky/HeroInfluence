using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveRangeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private Transform lineRoot;

    [Header("Movement Grid")]
    [SerializeField, Tooltip("이동 가능 그리드의 색상입니다. 투명도는 아래 항목에서 별도로 조절합니다.")]
    private Color gridColor = new Color(0.15f, 1f, 0.35f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("그리드의 불투명도입니다. 0은 투명, 1은 불투명입니다.")]
    private float gridOpacity = 0.4f;
    [SerializeField, Min(0f), Tooltip("주변 조명과 무관한 선의 자체 발광 강도입니다. 1보다 큰 HDR 값은 카메라의 Bloom이 켜져 있을 때 빛 번짐에도 반영됩니다.")]
    private float gridEmissionIntensity = 3f;
    [SerializeField, Min(0.001f), Tooltip("그리드 선의 월드 단위 두께입니다. 셀 크기의 1/4 이하로 제한합니다.")]
    private float gridWidth = 0.025f;
    [SerializeField, Range(0f, 0.5f), Tooltip("선 두께 대비 가장자리의 부드러운 감쇠 폭입니다. 0은 기본 픽셀 안티앨리어싱만 적용하며, 높일수록 선 경계가 부드러워집니다.")]
    private float gridEdgeSoftness = 0.15f;
    [SerializeField, Tooltip("바닥과 겹쳐 깜빡이지 않도록 그리드를 지면에서 띄우는 높이입니다.")]
    private float lineYOffset = 0.035f;

    [Header("Light Curtain")]
    [SerializeField, Min(0f), Tooltip("선을 따라 올라오는 빛 장막의 월드 단위 높이입니다. 0이면 장막을 숨깁니다.")]
    private float curtainHeight = 0.18f;
    [SerializeField, Range(0f, 1f), Tooltip("빛 장막의 불투명도입니다. 선의 투명도와 별도로 적용합니다.")]
    private float curtainOpacity = 0.16f;
    [SerializeField, Min(0f), Tooltip("빛 장막의 자체 발광 강도입니다.")]
    private float curtainIntensity = 1.5f;
    [SerializeField, Range(0.5f, 6f), Tooltip("위로 올라갈수록 장막이 흐려지는 정도입니다. 높을수록 윗부분이 더 빨리 사라집니다.")]
    private float curtainSoftness = 2f;

    [Header("Search Guard")]
    [SerializeField, Min(16)] private int maxSearchCells = 1000;

    private readonly HashSet<Vector2Int> reachableCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> interactableTargetCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();
    private readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> costs = new Dictionary<Vector2Int, int>();
    private JcMoveGridMesh gridMesh;
    private float renderedWidth, renderedYOffset;
    private bool rangeVisible;
    private readonly List<GridLineSegment> gridLineSegments = new List<GridLineSegment>();
    private readonly HashSet<GridLineKey> gridLineKeys = new HashSet<GridLineKey>();

    private PartyGridMover currentMover;
    private Coroutine pendingDelayedRefreshCoroutine;
    private bool isMoverMoving;
    private bool movementRefreshRequested;

    public bool IsMoveRangeActive => true;

    private readonly struct GridLineSegment
    {
        public GridLineSegment(Vector2Int from, Vector2Int to, Vector2Int ownerGrid)
        {
            From = from;
            To = to;
            OwnerGrid = ownerGrid;
        }

        public Vector2Int From { get; }
        public Vector2Int To { get; }
        public Vector2Int OwnerGrid { get; }
    }

    private readonly struct GridLineKey : System.IEquatable<GridLineKey>
    {
        public GridLineKey(Vector2Int a, Vector2Int b)
        {
            if (a.x < b.x || (a.x == b.x && a.y <= b.y))
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public Vector2Int A { get; }
        public Vector2Int B { get; }

        public bool Equals(GridLineKey other)
        {
            return A == other.A && B == other.B;
        }

        public override bool Equals(object obj)
        {
            return obj is GridLineKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (A.GetHashCode() * 397) ^ B.GetHashCode();
            }
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        RefreshForCurrentParty();
        RequestRefreshAfterSceneSetup();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        pendingDelayedRefreshCoroutine = null;
        movementRefreshRequested = false;
        BindMover(null);
        Hide();
    }

    public void RequestRefreshAfterSceneSetup(int frameCount = 8)
    {
        if (!isActiveAndEnabled)
            return;

        if (pendingDelayedRefreshCoroutine != null)
            StopCoroutine(pendingDelayedRefreshCoroutine);

        pendingDelayedRefreshCoroutine = StartCoroutine(RefreshAfterSceneSetup(Mathf.Max(1, frameCount)));
    }

    private IEnumerator RefreshAfterSceneSetup(int frameCount)
    {
        for (int i = 0; i < frameCount; i++)
            yield return null;

        pendingDelayedRefreshCoroutine = null;
        RefreshForCurrentParty();
    }

    private void LateUpdate()
    {
        if (movementRefreshRequested)
            RefreshForMover(currentMover);
        if (rangeVisible && (renderedWidth != EffectiveWidth || renderedYOffset != lineYOffset))
            RenderRange();
        UpdateGridAppearance();
    }

    private float EffectiveWidth => Mathf.Clamp(gridWidth, 0.001f,
        Mathf.Max(0.001f, (gridManager != null ? gridManager.CellSize : 1f) * 0.25f));

    private void OnDestroy()
    {
        gridMesh?.Dispose();
        gridMesh = null;
    }

    private void UpdateGridAppearance()
    {
        if (gridMesh == null) return;
        gridMesh.SetAppearance(gridColor, gridOpacity, gridEmissionIntensity, gridEdgeSoftness);
        gridMesh.SetCurtain(curtainHeight, curtainOpacity, curtainIntensity, curtainSoftness);
    }

    public void RefreshForCurrentParty()
    {
        ResolveReferences();

        PartyGridMover mover = ResolvePlayerParty();
        RefreshForMover(mover);
    }

    private PartyGridMover ResolvePlayerParty()
    {
        PartyGridMover mover = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (mover != null && mover.gameObject.activeInHierarchy)
            return mover;

        PartyGridMover fallback = null;
        int fallbackCount = 0;
        PartyGridMover[] movers = FindObjectsByType<PartyGridMover>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < movers.Length; i++)
        {
            PartyGridMover candidate = movers[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            fallback = candidate;
            fallbackCount++;

            PartyIdentity identity = candidate.GetComponent<PartyIdentity>();
            if (identity == null)
                continue;

            if (partyRegistry != null && partyRegistry.TryGetPartyById(identity.PartyId, out PartyGridMover registered) && registered == candidate)
                return candidate;

            if (string.Equals(identity.PartyId, "player", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identity.PartyId, "party", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identity.PartyId, "main", System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        if (fallbackCount == 1)
            return fallback;

        return mover;
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (lineRoot == null)
            lineRoot = transform;
    }

    public void RefreshForMover(PartyGridMover mover)
    {
        movementRefreshRequested = false;
        BindMover(mover);
        isMoverMoving = mover != null && mover.IsMoving;

        if (!IsMoveRangeActive || mover == null || gridManager == null)
        {
            ClearRange();
            Hide();
            return;
        }

        RebuildRange(mover);
        if (isMoverMoving)
            Hide();
        else
            RenderRange();
    }

    public bool AllowsCell(Vector2Int grid)
    {
        // Input may query the range before this frame's LateUpdate.
        if (movementRefreshRequested)
            RefreshForMover(currentMover);
        return !IsMoveRangeActive || reachableCells.Contains(grid);
    }

    public void Hide()
    {
        rangeVisible = false;
        gridMesh?.Hide();
    }

    public void ClearRange()
    {
        reachableCells.Clear();
        interactableTargetCells.Clear();
        blockedCells.Clear();
        costs.Clear();
        frontier.Clear();
        gridLineSegments.Clear();
        gridLineKeys.Clear();
    }

    private void BindMover(PartyGridMover mover)
    {
        if (currentMover == mover)
            return;

        if (currentMover != null)
        {
            currentMover.GridEntered -= HandleMoverGridEntered;
            currentMover.MoveCompleted -= HandleMoverMoveCompleted;
        }

        currentMover = mover;
        isMoverMoving = false;

        if (currentMover != null)
        {
            currentMover.GridEntered += HandleMoverGridEntered;
            currentMover.MoveCompleted += HandleMoverMoveCompleted;
        }
    }

    private void HandleMoverGridEntered(Vector2Int _)
    {
        movementRefreshRequested = true;
        if (currentMover != null && currentMover.IsMoving)
            Hide();
    }

    private void HandleMoverMoveCompleted()
    {
        // The final GridEntered and MoveCompleted arrive in the same frame.
        movementRefreshRequested = true;
    }

    private void RebuildRange(PartyGridMover mover)
    {
        ClearRange();

        Vector2Int start = mover.GetCurrentGrid();
        int maxCost = Mathf.Max(0, mover.RemainingMovePoints);
        reachableCells.Add(start);
        costs[start] = 0;
        frontier.Enqueue(start);

        while (frontier.Count > 0 && reachableCells.Count < maxSearchCells)
        {
            Vector2Int current = frontier.Dequeue();
            int currentCost = costs[current];
            if (currentCost >= maxCost)
                continue;

            for (int i = 0; i < GridManager.Directions8.Length; i++)
            {
                Vector2Int next = current + GridManager.Directions8[i];
                int nextCost = currentCost + 1;

                if (costs.TryGetValue(next, out int knownCost) && knownCost <= nextCost)
                    continue;

                if (interactableTargetCells.Contains(next) || blockedCells.Contains(next))
                    continue;

                if (IsInteractableTargetCell(next, mover))
                {
                    interactableTargetCells.Add(next);
                    continue;
                }

                if (!CanEnterMoveRangeCell(next, mover))
                {
                    blockedCells.Add(next);
                    continue;
                }

                costs[next] = nextCost;
                if (reachableCells.Add(next))
                    frontier.Enqueue(next);
            }
        }
    }

    private bool CanEnterMoveRangeCell(Vector2Int grid, PartyGridMover mover)
    {
        if (gridManager == null)
            return false;

        if (!gridManager.HasLevelGroundCell(grid))
            return false;

        if (gridManager.HasRegisteredObstacleOrGateBlocker(grid))
            return false;

        if (!ZoneEntryGuidanceController.IsCellAllowed(grid))
            return false;

        if (gridManager.HasOutpost(grid))
            return false;

        if (gridManager.HasHeroUnion(grid, mover != null ? mover.transform : null))
            return false;

        if (gridManager.HasVillainUnionBase(grid))
            return false;

        if (gridManager.HasTutorialBuilding(grid, mover != null ? mover.transform : null))
            return false;

        return true;
    }

    private bool IsInteractableTargetCell(Vector2Int grid, PartyGridMover mover)
    {
        if (gridManager == null)
            return false;

        if (!gridManager.HasLevelGroundCell(grid))
            return false;

        if (!ZoneEntryGuidanceController.IsCellAllowed(grid))
            return false;

        return gridManager.HasMoveRangeBlockingInteractionOccupant(grid, mover != null ? mover.transform : null);
    }

    private void RenderRange()
    {
        BuildGridLineSegments();
        if (gridMesh == null) gridMesh = new JcMoveGridMesh(lineRoot != null ? lineRoot : transform);
        renderedWidth = EffectiveWidth;
        renderedYOffset = lineYOffset;
        gridMesh.Begin(renderedWidth);
        foreach (GridLineSegment segment in gridLineSegments)
            gridMesh.AddSegment(GridLinePointToWorld(segment.From, segment.OwnerGrid),
                GridLinePointToWorld(segment.To, segment.OwnerGrid));
        UpdateGridAppearance();
        gridMesh.End();
        rangeVisible = gridLineSegments.Count > 0;
    }

    private void BuildGridLineSegments()
    {
        gridLineSegments.Clear();
        gridLineKeys.Clear();
        foreach (Vector2Int grid in reachableCells)
        {
            AddGridLineSegment(grid, new Vector2Int(0, 1), new Vector2Int(1, 1));
            AddGridLineSegment(grid, new Vector2Int(1, 1), new Vector2Int(1, 0));
            AddGridLineSegment(grid, new Vector2Int(1, 0), new Vector2Int(0, 0));
            AddGridLineSegment(grid, new Vector2Int(0, 0), new Vector2Int(0, 1));
        }

        foreach (Vector2Int grid in interactableTargetCells)
        {
            AddGridLineSegment(grid, new Vector2Int(0, 1), new Vector2Int(1, 1));
            AddGridLineSegment(grid, new Vector2Int(1, 1), new Vector2Int(1, 0));
            AddGridLineSegment(grid, new Vector2Int(1, 0), new Vector2Int(0, 0));
            AddGridLineSegment(grid, new Vector2Int(0, 0), new Vector2Int(0, 1));
        }
    }

    private void AddGridLineSegment(Vector2Int grid, Vector2Int fromOffset, Vector2Int toOffset)
    {
        Vector2Int from = grid + fromOffset;
        Vector2Int to = grid + toOffset;
        GridLineKey key = new GridLineKey(from, to);
        if (!gridLineKeys.Add(key))
            return;

        gridLineSegments.Add(new GridLineSegment(from, to, grid));
    }

    private Vector3 GridLinePointToWorld(Vector2Int gridLinePoint, Vector2Int ownerGrid)
    {
        float cellSize = gridManager != null ? gridManager.CellSize : 1f;
        // 같은 격자 꼭짓점은 소유 셀과 무관하게 같은 계산을 사용해 교차점 중복을 방지합니다.
        Vector3 world = gridManager.GridToWorldCenter(gridLinePoint)
            - new Vector3(cellSize * 0.5f, 0f, cellSize * 0.5f);
        world.y = gridManager.GetCellSurfaceY(ownerGrid) + lineYOffset;
        return world;
    }

}
