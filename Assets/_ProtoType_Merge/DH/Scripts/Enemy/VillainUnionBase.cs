using System.Collections.Generic;
using UnityEngine;

public class VillainUnionBase : MonoBehaviour
{
    [SerializeField] private string baseId = "villain_union_base_001";
    [SerializeField] private string zoneId;
    [SerializeField, Min(1)] private int resolvedEnemyLevel = 1;

    [Header("Defender Combat Chat")]
    [SerializeField, Min(0)] private int defenderCombatChatId;

    [SerializeField] private GridManager gridManager;
    private VillainUnionBaseRegistry villainUnionBaseRegistry;

    public string BaseId => baseId;
    public string ZoneId => NormalizeZoneId(zoneId);
    public int ResolvedEnemyLevel => Mathf.Max(1, resolvedEnemyLevel);
    public int DefenderCombatChatId => Mathf.Max(0, defenderCombatChatId);

    private void OnValidate()
    {
        defenderCombatChatId = Mathf.Max(0, defenderCombatChatId);
        RefreshResolvedEnemyLevel();
    }
    private void Awake()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();
    }

    private void OnEnable()
    {
        ResolveReferences();
        villainUnionBaseRegistry?.Register(this);
        RefreshResolvedEnemyLevel();
    }

    private void OnDisable()
    {
        villainUnionBaseRegistry?.Unregister(this);
    }

    public void ApplyZoneIdFromLoader(string loaderZoneId)
    {
        if (!string.IsNullOrWhiteSpace(loaderZoneId))
            zoneId = NormalizeZoneId(loaderZoneId);
        RefreshResolvedEnemyLevel();
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
            return occupant.GetBottomOuterCells();

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
            return occupant.IsBottomOuterCell(grid);

        Vector2Int origin = GetCurrentGrid();
        int dx = Mathf.Abs(grid.x - origin.x);
        int dy = Mathf.Abs(grid.y - origin.y);
        return dx <= 1 && dy <= 1 && (dx != 0 || dy != 0);
    }

    public void RefreshResolvedEnemyLevel()
    {
        resolvedEnemyLevel = ResolveZoneEnemyLevel(ZoneId);
    }

    public string GetProgressKey(GridManager targetGridManager)
    {
        return MapProgressKey.ForVillainUnion(GetAnchorGrid());
    }

    private static int ResolveZoneEnemyLevel(string targetZoneId)
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.TryGetZoneEnemyLevel(targetZoneId, out int level)
            ? Mathf.Max(1, level)
            : 1;
    }

    private static string NormalizeZoneId(string value)
    {
        return MapProgressKey.NormalizeSegment(value);
    }


    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();
    }


}
