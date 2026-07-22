using System.Collections.Generic;
using UnityEngine;

public enum GateFootprintType
{
    Horizontal2,
    Vertical2,
    CornerLeft3,
    CornerRight3,
    Custom
}

[DisallowMultipleComponent]
public sealed class GateFootprint : MonoBehaviour
{
    private static readonly Vector2Int[] Horizontal2Offsets =
    {
        new Vector2Int(0, 0),
        new Vector2Int(1, 0)
    };

    private static readonly Vector2Int[] Vertical2Offsets =
    {
        new Vector2Int(0, 0),
        new Vector2Int(0, 1)
    };

    private static readonly Vector2Int[] CornerLeft3Offsets =
    {
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1)
    };

    private static readonly Vector2Int[] CornerRight3Offsets =
    {
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(1, 1)
    };

    [Header("Footprint")]
    [SerializeField] private GateFootprintType footprintType = GateFootprintType.Horizontal2;
    [SerializeField] private Vector2Int anchorGrid = Vector2Int.zero;
    [SerializeField] private Vector2Int anchorOffsetFromObjectGrid;
    [SerializeField] private bool syncAnchorFromTransformOnAwake = true;
    [SerializeField] private bool syncAnchorFromTransformOnValidate;
    [SerializeField] private List<Vector2Int> customOccupiedOffsets = new List<Vector2Int> { Vector2Int.zero };
    [SerializeField] private bool useOccupiedCellsAsTeleportCells = true;
    [SerializeField] private List<Vector2Int> customTeleportOffsets = new List<Vector2Int> { Vector2Int.zero };

    [Header("Debug Preview")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawOnlyWhenSelected;
    [SerializeField] private bool drawObjectToAnchorOffset = true;
    [SerializeField, Min(0.05f)] private float fallbackCellSize = 1f;
    [SerializeField, Range(0.1f, 1f)] private float cellFill = 0.85f;
    [SerializeField] private float gizmoYOffset = 0.04f;
    [SerializeField] private Color occupiedCellColor = new Color(0.35f, 0.75f, 1f, 0.24f);
    [SerializeField] private Color occupiedOutlineColor = new Color(0.1f, 0.95f, 1f, 0.95f);
    [SerializeField] private Color teleportCellColor = new Color(1f, 0.85f, 0.15f, 0.35f);
    [SerializeField] private Color objectOffsetColor = new Color(1f, 0.35f, 0.15f, 1f);

    public GateFootprintType FootprintType => footprintType;
    public Vector2Int AnchorGrid => anchorGrid;
    public Vector2Int AnchorOffsetFromObjectGrid => anchorOffsetFromObjectGrid;
    public bool UseOccupiedCellsAsTeleportCells => useOccupiedCellsAsTeleportCells;

    private void Awake()
    {
        if (syncAnchorFromTransformOnAwake)
            SyncAnchorFromTransform();
    }

    private void OnValidate()
    {
        fallbackCellSize = Mathf.Max(0.05f, fallbackCellSize);
        cellFill = Mathf.Clamp01(cellFill);

        EnsureNonEmpty(customOccupiedOffsets);
        EnsureNonEmpty(customTeleportOffsets);

        if (syncAnchorFromTransformOnValidate)
            SyncAnchorFromTransform();
    }

    [ContextMenu("Sync Anchor From Transform")]
    public void SyncAnchorFromTransform()
    {
        anchorGrid = InferAnchorGridFromTransform();
    }

    public Vector2Int GetObjectGrid(GridManager targetGridManager = null)
    {
        GridManager resolvedGridManager = targetGridManager != null ? targetGridManager : ResolveGridManager();
        if (resolvedGridManager != null)
            return resolvedGridManager.WorldToGrid(transform.position);

        return new Vector2Int(
            Mathf.RoundToInt(transform.position.x / fallbackCellSize),
            Mathf.RoundToInt(transform.position.z / fallbackCellSize));
    }

    public Vector2Int GetAnchorGrid(GridManager targetGridManager = null)
    {
        return anchorGrid + anchorOffsetFromObjectGrid;
    }

    public void CollectOccupiedCells(Vector2Int anchorGrid, List<Vector2Int> results)
    {
        CollectCells(anchorGrid, GetOccupiedOffsets(), results);
    }

    public void CollectTeleportCells(Vector2Int anchorGrid, List<Vector2Int> results)
    {
        CollectCells(anchorGrid, GetTeleportOffsets(), results);
    }

    public List<Vector2Int> GetOccupiedCells(Vector2Int anchorGrid)
    {
        var results = new List<Vector2Int>();
        CollectOccupiedCells(anchorGrid, results);
        return results;
    }

    public List<Vector2Int> GetTeleportCells(Vector2Int anchorGrid)
    {
        var results = new List<Vector2Int>();
        CollectTeleportCells(anchorGrid, results);
        return results;
    }

    public Vector3 GetRootPositionForAnchor(GridManager targetGridManager, Vector2Int anchorGrid)
    {
        float cellSize = ResolveCellSize(targetGridManager);
        Vector2 centerOffset = CalculateBoundsCenterOffset(GetOccupiedOffsets());
        Vector3 anchorWorldPosition = targetGridManager != null
            ? targetGridManager.GridToWorldCenter(anchorGrid)
            : new Vector3(anchorGrid.x * cellSize, 0f, anchorGrid.y * cellSize);
        Vector3 rootPosition = anchorWorldPosition + new Vector3(centerOffset.x * cellSize, 0f, centerOffset.y * cellSize);

        if (targetGridManager != null)
            rootPosition.y = targetGridManager.GetLandSurfaceY();

        return rootPosition;
    }

    public IReadOnlyList<Vector2Int> GetOccupiedOffsets()
    {
        switch (footprintType)
        {
            case GateFootprintType.Vertical2:
                return Vertical2Offsets;
            case GateFootprintType.CornerLeft3:
                return CornerLeft3Offsets;
            case GateFootprintType.CornerRight3:
                return CornerRight3Offsets;
            case GateFootprintType.Custom:
                return customOccupiedOffsets;
            default:
                return Horizontal2Offsets;
        }
    }

    public IReadOnlyList<Vector2Int> GetTeleportOffsets()
    {
        return useOccupiedCellsAsTeleportCells ? GetOccupiedOffsets() : customTeleportOffsets;
    }

    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected)
            return;

        DrawFootprintGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawFootprintGizmos();
    }

    private void DrawFootprintGizmos()
    {
        if (!drawGizmos)
            return;

        GridManager resolvedGridManager = ResolveGridManager();
        Vector2Int objectGrid = GetObjectGrid(resolvedGridManager);
        Vector2Int resolvedAnchorGrid = GetAnchorGrid(resolvedGridManager);
        float cellSize = ResolveCellSize(resolvedGridManager);
        float y = ResolveGizmoY(resolvedGridManager);

        DrawCells(resolvedAnchorGrid, GetOccupiedOffsets(), cellSize, y, occupiedCellColor, occupiedOutlineColor);

        if (!useOccupiedCellsAsTeleportCells)
            DrawCells(resolvedAnchorGrid, GetTeleportOffsets(), cellSize * 0.58f, y + 0.02f, teleportCellColor, teleportCellColor);

        if (drawObjectToAnchorOffset)
            DrawObjectAnchorOffset(objectGrid, resolvedAnchorGrid, cellSize, y);
    }

    private void DrawCells(
        Vector2Int anchorGrid,
        IReadOnlyList<Vector2Int> offsets,
        float cellSize,
        float y,
        Color fillColor,
        Color outlineColor)
    {
        if (offsets == null)
            return;

        for (int i = 0; i < offsets.Count; i++)
        {
            Vector3 center = GridToWorldCenter(anchorGrid + offsets[i]);
            center.y = y;

            Gizmos.color = fillColor;
            Gizmos.DrawCube(center, new Vector3(cellSize * cellFill, 0.02f, cellSize * cellFill));

            Gizmos.color = outlineColor;
            Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.04f, cellSize));
        }
    }

    private void DrawObjectAnchorOffset(Vector2Int objectGrid, Vector2Int anchorGrid, float cellSize, float y)
    {
        Vector3 objectPosition = transform.position;
        objectPosition.y = y + 0.12f;

        Vector3 objectGridCenter = GridToWorldCenter(objectGrid);
        objectGridCenter.y = y + 0.12f;

        Vector3 anchorCenter = GridToWorldCenter(anchorGrid);
        anchorCenter.y = y + 0.12f;

        Gizmos.color = objectOffsetColor;
        Gizmos.DrawLine(objectPosition, objectGridCenter);
        Gizmos.DrawLine(objectGridCenter, anchorCenter);
        Gizmos.DrawSphere(objectPosition, cellSize * 0.08f);
        Gizmos.DrawWireCube(objectGridCenter, new Vector3(cellSize * 0.35f, 0.04f, cellSize * 0.35f));
        Gizmos.DrawWireSphere(anchorCenter, cellSize * 0.12f);

#if UNITY_EDITOR
        float distance = Vector3.Distance(
            new Vector3(objectPosition.x, 0f, objectPosition.z),
            new Vector3(anchorCenter.x, 0f, anchorCenter.z));
        UnityEditor.Handles.color = objectOffsetColor;
        UnityEditor.Handles.Label(
            (objectPosition + anchorCenter) * 0.5f + Vector3.up * 0.18f,
            $"object-anchor {distance:0.###} | anchor offset {anchorOffsetFromObjectGrid}");
#endif
    }

    private Vector3 GridToWorldCenter(Vector2Int grid)
    {
        GridManager resolvedGridManager = ResolveGridManager();
        if (resolvedGridManager != null)
            return resolvedGridManager.GridToWorldCenter(grid);

        return new Vector3(grid.x * fallbackCellSize, 0f, grid.y * fallbackCellSize);
    }

    private float ResolveCellSize(GridManager resolvedGridManager)
    {
        return resolvedGridManager != null ? resolvedGridManager.CellSize : fallbackCellSize;
    }

    private float ResolveGizmoY(GridManager resolvedGridManager)
    {
        return (resolvedGridManager != null ? resolvedGridManager.GetLandSurfaceY() : transform.position.y) + gizmoYOffset;
    }

    private GridManager ResolveGridManager()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        return gridManager;
    }

    private Vector2Int InferAnchorGridFromTransform()
    {
        GridManager resolvedGridManager = ResolveGridManager();
        float cellSize = ResolveCellSize(resolvedGridManager);
        Vector2 centerOffset = CalculateBoundsCenterOffset(GetOccupiedOffsets());
        Vector3 worldOffset = new Vector3(centerOffset.x * cellSize, 0f, centerOffset.y * cellSize);

        if (resolvedGridManager != null)
            return resolvedGridManager.WorldToGrid(transform.position - worldOffset);

        Vector3 adjustedPosition = transform.position - worldOffset;
        return new Vector2Int(
            Mathf.RoundToInt(adjustedPosition.x / cellSize),
            Mathf.RoundToInt(adjustedPosition.z / cellSize));
    }

    private static Vector2 CalculateBoundsCenterOffset(IReadOnlyList<Vector2Int> offsets)
    {
        if (offsets == null || offsets.Count == 0)
            return Vector2.zero;

        int minX = offsets[0].x;
        int maxX = offsets[0].x;
        int minY = offsets[0].y;
        int maxY = offsets[0].y;

        for (int i = 1; i < offsets.Count; i++)
        {
            Vector2Int offset = offsets[i];
            minX = Mathf.Min(minX, offset.x);
            maxX = Mathf.Max(maxX, offset.x);
            minY = Mathf.Min(minY, offset.y);
            maxY = Mathf.Max(maxY, offset.y);
        }

        return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }

    private static void CollectCells(Vector2Int anchorGrid, IReadOnlyList<Vector2Int> offsets, List<Vector2Int> results)
    {
        if (offsets == null || results == null)
            return;

        for (int i = 0; i < offsets.Count; i++)
        {
            Vector2Int cell = anchorGrid + offsets[i];
            if (!results.Contains(cell))
                results.Add(cell);
        }
    }

    private static void EnsureNonEmpty(List<Vector2Int> offsets)
    {
        if (offsets == null || offsets.Count > 0)
            return;

        offsets.Add(Vector2Int.zero);
    }
}
