using System.Collections.Generic;
using UnityEngine;

public class VillainUnionBase : MonoBehaviour
{
    [SerializeField] private string baseId = "villain_union_base_001";
    [SerializeField] private GridManager gridManager;
    private VillainUnionBaseRegistry villainUnionBaseRegistry;

    public string BaseId => baseId;

    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private void OnEnable()
    {
        ResolveReferences();
        villainUnionBaseRegistry?.Register(this);
    }

    private void OnDisable()
    {
        villainUnionBaseRegistry?.Unregister(this);
    }

    public Vector2Int GetCurrentGrid()
    {
        return gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    public Vector2Int GetAnchorGrid()
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.AnchorGrid;

        return GetCurrentGrid();
    }

    public IReadOnlyList<Vector2Int> GetAdjacentOuterCells()
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.GetAdjacentOuterCells();

        Vector2Int origin = GetCurrentGrid();
        List<Vector2Int> adjacentCells = new List<Vector2Int>(GridManager.Directions8.Length);
        for (int i = 0; i < GridManager.Directions8.Length; i++)
            adjacentCells.Add(origin + GridManager.Directions8[i]);

        return adjacentCells;
    }

    public IReadOnlyList<Vector2Int> GetInteractionCells()
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
        {
            if (occupant.IsTwoByTwo())
                return occupant.GetBottomOuterCells();

            return occupant.GetAdjacentOuterCells();
        }

        Vector2Int origin = GetCurrentGrid();
        List<Vector2Int> adjacentCells = new List<Vector2Int>(GridManager.Directions8.Length);
        for (int i = 0; i < GridManager.Directions8.Length; i++)
            adjacentCells.Add(origin + GridManager.Directions8[i]);

        return adjacentCells;
    }

    public bool IsAdjacentCell(Vector2Int grid)
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.IsTwoByTwo()
                ? occupant.IsBottomOuterCell(grid)
                : occupant.IsAdjacentOuterCell(grid);

        Vector2Int origin = GetCurrentGrid();
        int dx = Mathf.Abs(grid.x - origin.x);
        int dy = Mathf.Abs(grid.y - origin.y);
        return dx <= 1 && dy <= 1 && (dx != 0 || dy != 0);
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();
    }
}
