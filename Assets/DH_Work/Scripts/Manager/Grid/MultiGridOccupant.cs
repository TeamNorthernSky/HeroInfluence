using System.Collections.Generic;
using UnityEngine;

public class MultiGridOccupant : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    private MultiGridOccupantRegistry occupantRegistry;

    [Header("Grid Shape")]
    [SerializeField] private Vector2Int anchorGrid = Vector2Int.zero;
    [SerializeField] private Vector2Int size = Vector2Int.one;
    [SerializeField] private bool syncAnchorFromTransformOnAwake;

    [Header("Debug")]
    [SerializeField] private bool drawOccupiedCellsGizmo = true;
    [SerializeField] private bool drawAdjacentCellsGizmo;

    private readonly List<Vector2Int> occupiedCells = new List<Vector2Int>();
    private readonly List<Vector2Int> adjacentOuterCells = new List<Vector2Int>();

    public Vector2Int AnchorGrid => anchorGrid;
    public Vector2Int Size => new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));

    private void Awake()
    {
        ResolveReferences();

        if (syncAnchorFromTransformOnAwake && gridManager != null)
            anchorGrid = InferAnchorGridFromTransform();

        RebuildCachedCells();
    }

    private void OnEnable()
    {
        ResolveReferences();
        occupantRegistry?.Register(this);
    }

    private void OnDisable()
    {
        occupantRegistry?.Unregister(this);
    }

    private void OnValidate()
    {
        ResolveReferences();
        size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));

        if (syncAnchorFromTransformOnAwake && gridManager != null)
            anchorGrid = InferAnchorGridFromTransform();

        RebuildCachedCells();
    }

    public void SetAnchorGrid(Vector2Int nextAnchorGrid)
    {
        anchorGrid = nextAnchorGrid;
        RebuildCachedCells();
    }

    public void SetSize(Vector2Int nextSize)
    {
        size = new Vector2Int(Mathf.Max(1, nextSize.x), Mathf.Max(1, nextSize.y));
        RebuildCachedCells();
    }

    public IReadOnlyList<Vector2Int> GetOccupiedCells()
    {
        return occupiedCells;
    }

    public IReadOnlyList<Vector2Int> GetAdjacentOuterCells()
    {
        return adjacentOuterCells;
    }

    public bool OccupiesCell(Vector2Int grid)
    {
        for (int i = 0; i < occupiedCells.Count; i++)
        {
            if (occupiedCells[i] == grid)
                return true;
        }

        return false;
    }

    public bool IsAdjacentOuterCell(Vector2Int grid)
    {
        for (int i = 0; i < adjacentOuterCells.Count; i++)
        {
            if (adjacentOuterCells[i] == grid)
                return true;
        }

        return false;
    }

    public Vector3 GetWorldCenter()
    {
        if (gridManager == null)
            return transform.position;

        Vector2Int clampedSize = Size;
        Vector2Int maxGrid = new Vector2Int(
            anchorGrid.x + clampedSize.x - 1,
            anchorGrid.y + clampedSize.y - 1);

        Vector3 minWorld = gridManager.GridToWorldCenter(anchorGrid);
        Vector3 maxWorld = gridManager.GridToWorldCenter(maxGrid);

        Vector3 center = (minWorld + maxWorld) * 0.5f;
        center.y = transform.position.y;
        return center;
    }

    private void RebuildCachedCells()
    {
        occupiedCells.Clear();
        adjacentOuterCells.Clear();

        Vector2Int clampedSize = Size;
        HashSet<Vector2Int> adjacentSet = new HashSet<Vector2Int>();

        for (int y = 0; y < clampedSize.y; y++)
        {
            for (int x = 0; x < clampedSize.x; x++)
            {
                Vector2Int occupied = new Vector2Int(anchorGrid.x + x, anchorGrid.y + y);
                occupiedCells.Add(occupied);
            }
        }

        for (int i = 0; i < occupiedCells.Count; i++)
        {
            Vector2Int occupied = occupiedCells[i];
            for (int dirIndex = 0; dirIndex < GridManager.Directions8.Length; dirIndex++)
            {
                Vector2Int candidate = occupied + GridManager.Directions8[dirIndex];
                if (OccupiesCellInternal(candidate))
                    continue;

                adjacentSet.Add(candidate);
            }
        }

        foreach (Vector2Int cell in adjacentSet)
            adjacentOuterCells.Add(cell);
    }

    private bool OccupiesCellInternal(Vector2Int grid)
    {
        Vector2Int clampedSize = Size;
        return grid.x >= anchorGrid.x
            && grid.y >= anchorGrid.y
            && grid.x < anchorGrid.x + clampedSize.x
            && grid.y < anchorGrid.y + clampedSize.y;
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (occupantRegistry == null)
            occupantRegistry = FindFirstObjectByType<MultiGridOccupantRegistry>();
    }

    private Vector2Int InferAnchorGridFromTransform()
    {
        if (gridManager == null)
            return anchorGrid;

        Vector2Int clampedSize = Size;
        Vector3 worldOffset = new Vector3(
            (clampedSize.x - 1) * gridManager.CellSize * 0.5f,
            0f,
            (clampedSize.y - 1) * gridManager.CellSize * 0.5f);

        return gridManager.WorldToGrid(transform.position - worldOffset);
    }

    private void OnDrawGizmosSelected()
    {
        if (gridManager == null)
            ResolveReferences();

        if (gridManager == null)
            return;

        if (drawOccupiedCellsGizmo)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.75f);
            DrawCellSet(occupiedCells);
        }

        if (drawAdjacentCellsGizmo)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.75f);
            DrawCellSet(adjacentOuterCells);
        }
    }

    private void DrawCellSet(List<Vector2Int> cells)
    {
        float cellSize = gridManager.CellSize;
        float y = gridManager.GetLandSurfaceY();

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 center = gridManager.GridToWorldCenter(cells[i]);
            center.y = y;

            Vector3 size3D = new Vector3(cellSize * 0.9f, 0.02f, cellSize * 0.9f);
            Gizmos.DrawWireCube(center, size3D);
        }
    }
}
