using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveRangeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private Transform lineRoot;

    [Header("Line Renderer")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField, Min(0.005f)] private float lineWidth = 0.05f;
    [SerializeField] private float lineYOffset = 0.035f;

    [Header("Search Guard")]
    [SerializeField, Min(16)] private int maxSearchCells = 1000;

    private readonly HashSet<Vector2Int> reachableCells = new HashSet<Vector2Int>();
    private readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> costs = new Dictionary<Vector2Int, int>();
    private readonly List<LineRenderer> linePool = new List<LineRenderer>();
    private readonly List<GridLineSegment> gridLineSegments = new List<GridLineSegment>();
    private readonly HashSet<GridLineKey> gridLineKeys = new HashSet<GridLineKey>();

    private PartyGridMover currentMover;
    private Material runtimeDefaultLineMaterial;
    private Coroutine pendingDelayedRefreshCoroutine;
    private bool isMoverMoving;

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
        {
            yield return null;
            isMoverMoving = false;
            RefreshForCurrentParty();
        }

        pendingDelayedRefreshCoroutine = null;
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
        BindMover(mover);

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
        return !IsMoveRangeActive || reachableCells.Contains(grid);
    }

    public void Hide()
    {
        for (int i = 0; i < linePool.Count; i++)
        {
            if (linePool[i] != null)
                linePool[i].gameObject.SetActive(false);
        }
    }

    public void ClearRange()
    {
        reachableCells.Clear();
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
        isMoverMoving = true;
        RefreshForMover(currentMover);
    }

    private void HandleMoverMoveCompleted()
    {
        isMoverMoving = false;
        RefreshForMover(currentMover);
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

                if (!CanEnterMoveRangeCell(next, mover))
                    continue;

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

        if (IsOtherPartyCell(grid, mover))
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

    private bool IsOtherPartyCell(Vector2Int grid, PartyGridMover mover)
    {
        PartyGridMover playerParty = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (playerParty == null || !playerParty.gameObject.activeInHierarchy)
            return false;

        if (mover != null && playerParty == mover)
            return false;

        return playerParty.GetCurrentGrid() == grid;
    }

    private void RenderRange()
    {
        BuildGridLineSegments();

        for (int i = 0; i < gridLineSegments.Count; i++)
        {
            LineRenderer line = GetLine(i);
            GridLineSegment segment = gridLineSegments[i];
            line.gameObject.SetActive(true);
            line.positionCount = 2;
            line.SetPosition(0, GridLinePointToWorld(segment.From, segment.OwnerGrid));
            line.SetPosition(1, GridLinePointToWorld(segment.To, segment.OwnerGrid));
        }

        for (int i = gridLineSegments.Count; i < linePool.Count; i++)
        {
            if (linePool[i] != null)
                linePool[i].gameObject.SetActive(false);
        }
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

    private LineRenderer GetLine(int index)
    {
        while (linePool.Count <= index)
        {
            GameObject lineObject = new GameObject("MoveRangeGridLine");
            lineObject.transform.SetParent(lineRoot, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 0;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.sharedMaterial = lineMaterial != null
                ? lineMaterial
                : GetDefaultLineMaterial();
            linePool.Add(line);
        }

        LineRenderer result = linePool[index];
        result.startWidth = lineWidth;
        result.endWidth = lineWidth;
        result.startColor = lineColor;
        result.endColor = lineColor;
        if (lineMaterial != null)
            result.sharedMaterial = lineMaterial;
        return result;
    }

    private Vector3 GridLinePointToWorld(Vector2Int gridLinePoint, Vector2Int ownerGrid)
    {
        float cellSize = gridManager != null ? gridManager.CellSize : 1f;
        Vector3 ownerCenter = gridManager.GridToWorldCenter(ownerGrid);
        Vector3 world = new Vector3(
            (gridLinePoint.x - ownerGrid.x - 0.5f) * cellSize,
            0f,
            (gridLinePoint.y - ownerGrid.y - 0.5f) * cellSize);

        world += ownerCenter;
        world.y = gridManager.GetCellSurfaceY(ownerGrid) + lineYOffset;
        return world;
    }

    private Material GetDefaultLineMaterial()
    {
        if (runtimeDefaultLineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            runtimeDefaultLineMaterial = shader != null ? new Material(shader) : null;
        }

        return runtimeDefaultLineMaterial;
    }
}
