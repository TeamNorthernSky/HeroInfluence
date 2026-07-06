using System.Collections.Generic;
using UnityEngine;

public class HeroUnionUnit : MonoBehaviour
{
    [SerializeField] private string heroUnionId = "heroUnion_001";
    [SerializeField] private GridManager gridManager;
    private HeroUnionRegistry heroUnionRegistry;

    public string HeroUnionId => heroUnionId;

    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private void OnEnable()
    {
        ResolveReferences();
        heroUnionRegistry?.Register(this);
    }

    private void OnDisable()
    {
        heroUnionRegistry?.Unregister(this);
    }

    public Vector2Int GetCurrentGrid()
    {
        return gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
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

    public bool IsInteractionCell(Vector2Int grid)
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

        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();
    }
}
