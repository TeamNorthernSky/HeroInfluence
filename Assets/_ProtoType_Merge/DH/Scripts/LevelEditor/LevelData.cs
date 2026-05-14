using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "LevelData",
    menuName = "DH Work/Level Editor/Level Data")]
public class LevelData : ScriptableObject
{
    private static readonly IReadOnlyList<Vector2Int> EmptyStayEnemyCells = Array.Empty<Vector2Int>();

    [Header("Meta")]
    [SerializeField] private string levelId = "level_001";
    [SerializeField] private string displayName = "New Level";

    [Header("Grid")]
    [SerializeField] private Vector2Int gridSize = new Vector2Int(20, 20);

    [Header("Placement")]
    [SerializeField] private List<TilePlacementData> groundTilePlacements = new List<TilePlacementData>();
    [SerializeField] private List<Vector2Int> obstacleCells = new List<Vector2Int>();
    [SerializeField] private List<ItemPlacementData> itemPlacements = new List<ItemPlacementData>();
    [FormerlySerializedAs("minePlacements")]
    [SerializeField] private List<OutpostPlacementData> outpostPlacements = new List<OutpostPlacementData>();
    [SerializeField] private List<EventPlacementData> eventPlacements = new List<EventPlacementData>();
    [SerializeField] private List<Vector2Int> stayEnemyCells = new List<Vector2Int>();
    [SerializeField] private UniqueBuildingPlacementData castlePlacement;
    [SerializeField] private UniqueBuildingPlacementData villainUnionPlacement;

    public string LevelId => levelId;
    public string DisplayName => displayName;
    public Vector2Int GridSize => gridSize;
    public Vector2Int GridMin => Vector2Int.zero;
    public Vector2Int GridMax => new Vector2Int(gridSize.x - 1, gridSize.y - 1);
    public IReadOnlyList<TilePlacementData> GroundTilePlacements => groundTilePlacements;
    public IReadOnlyList<Vector2Int> ObstacleCells => obstacleCells;
    public IReadOnlyList<ItemPlacementData> ItemPlacements => itemPlacements;
    public IReadOnlyList<OutpostPlacementData> OutpostPlacements => outpostPlacements;
    public IReadOnlyList<EventPlacementData> EventPlacements => eventPlacements;
    public IReadOnlyList<Vector2Int> StayEnemyCells => stayEnemyCells != null ? stayEnemyCells : EmptyStayEnemyCells;
    public UniqueBuildingPlacementData CastlePlacement => castlePlacement;
    public UniqueBuildingPlacementData VillainUnionPlacement => villainUnionPlacement;

    public bool IsInsideGrid(Vector2Int grid)
    {
        Vector2Int min = GridMin;
        Vector2Int max = GridMax;

        return grid.x >= min.x
            && grid.y >= min.y
            && grid.x <= max.x
            && grid.y <= max.y;
    }

    public bool HasObstacleAt(Vector2Int grid)
    {
        return obstacleCells.Contains(grid);
    }

    public bool HasGroundTileAt(Vector2Int grid)
    {
        return TryGetGroundTileAt(grid, out _);
    }

    public bool HasItemAt(Vector2Int grid)
    {
        for (int i = 0; i < itemPlacements.Count; i++)
        {
            if (itemPlacements[i].GridPosition == grid)
                return true;
        }

        return false;
    }

    public bool HasOutpostAt(Vector2Int grid)
    {
        for (int i = 0; i < outpostPlacements.Count; i++)
        {
            if (outpostPlacements[i].GridPosition == grid)
                return true;
        }

        return false;
    }

    public bool HasEventAt(Vector2Int grid)
    {
        for (int i = 0; i < eventPlacements.Count; i++)
        {
            if (eventPlacements[i].GridPosition == grid)
                return true;
        }

        return false;
    }

    public bool HasStayEnemyAt(Vector2Int grid)
    {
        return stayEnemyCells != null && stayEnemyCells.Contains(grid);
    }

    public bool HasCastleAt(Vector2Int grid)
    {
        return castlePlacement.IsAt(grid);
    }

    public bool HasVillainUnionAt(Vector2Int grid)
    {
        return villainUnionPlacement.IsAt(grid);
    }

    public bool TryGetGroundTileAt(Vector2Int grid, out TilePlacementData tilePlacement)
    {
        for (int i = 0; i < groundTilePlacements.Count; i++)
        {
            if (groundTilePlacements[i].GridPosition != grid)
                continue;

            tilePlacement = groundTilePlacements[i];
            return true;
        }

        tilePlacement = default;
        return false;
    }

    public void SetGroundTile(Vector2Int grid, string tileKey)
    {
        if (!IsInsideGrid(grid))
            return;

        SetTilePlacement(groundTilePlacements, grid, tileKey);
    }

    public void EraseGroundTileAt(Vector2Int grid)
    {
        groundTilePlacements.RemoveAll(x => x.GridPosition == grid);
    }

    public void SetObstacle(Vector2Int grid)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveNonObstaclePlacementsAt(grid);
        if (!obstacleCells.Contains(grid))
            obstacleCells.Add(grid);
    }

    public void SetItem(Vector2Int grid, ResourceType resourceType, int amount)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveAllPlacementsAt(grid);
        itemPlacements.Add(new ItemPlacementData(grid, resourceType, amount));
    }

    public void SetOutpost(Vector2Int grid, OutpostType outpostType, int resourcePerTurn, OutpostState initialState)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveAllPlacementsAt(grid);
        outpostPlacements.Add(new OutpostPlacementData(grid, outpostType, resourcePerTurn, initialState));
    }

    public void SetEvent(Vector2Int grid, string eventKey)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveAllPlacementsAt(grid);
        eventPlacements.Add(new EventPlacementData(grid, eventKey));
    }

    public void SetStayEnemy(Vector2Int grid)
    {
        if (!IsInsideGrid(grid))
            return;

        EnsureStayEnemyCells();
        RemoveAllPlacementsAt(grid);
        if (!stayEnemyCells.Contains(grid))
            stayEnemyCells.Add(grid);
    }

    public void SetCastle(Vector2Int grid)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveAllPlacementsAt(grid);
        castlePlacement = new UniqueBuildingPlacementData(grid);
    }

    public void SetVillainUnion(Vector2Int grid)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveAllPlacementsAt(grid);
        villainUnionPlacement = new UniqueBuildingPlacementData(grid);
    }

    public void EraseAt(Vector2Int grid)
    {
        groundTilePlacements.RemoveAll(x => x.GridPosition == grid);
        obstacleCells.Remove(grid);
        itemPlacements.RemoveAll(x => x.GridPosition == grid);
        outpostPlacements.RemoveAll(x => x.GridPosition == grid);
        eventPlacements.RemoveAll(x => x.GridPosition == grid);
        stayEnemyCells?.Remove(grid);
        ClearUniquePlacementsAt(grid);
    }

    private void RemoveNonObstaclePlacementsAt(Vector2Int grid)
    {
        itemPlacements.RemoveAll(x => x.GridPosition == grid);
        outpostPlacements.RemoveAll(x => x.GridPosition == grid);
        eventPlacements.RemoveAll(x => x.GridPosition == grid);
        stayEnemyCells?.Remove(grid);
        ClearUniquePlacementsAt(grid);
    }

    private void RemoveAllPlacementsAt(Vector2Int grid)
    {
        obstacleCells.Remove(grid);
        itemPlacements.RemoveAll(x => x.GridPosition == grid);
        outpostPlacements.RemoveAll(x => x.GridPosition == grid);
        eventPlacements.RemoveAll(x => x.GridPosition == grid);
        stayEnemyCells?.Remove(grid);
        ClearUniquePlacementsAt(grid);
    }

    private void ClearUniquePlacementsAt(Vector2Int grid)
    {
        if (castlePlacement.IsAt(grid))
            castlePlacement = default;

        if (villainUnionPlacement.IsAt(grid))
            villainUnionPlacement = default;
    }

    private static void SetTilePlacement(List<TilePlacementData> placements, Vector2Int grid, string tileKey)
    {
        placements.RemoveAll(x => x.GridPosition == grid);

        if (string.IsNullOrWhiteSpace(tileKey))
            return;

        placements.Add(new TilePlacementData(grid, tileKey));
    }

    private void OnValidate()
    {
        gridSize = NormalizeGridSize(gridSize);

        for (int i = 0; i < outpostPlacements.Count; i++)
            outpostPlacements[i] = outpostPlacements[i].Normalized();

        EnsureStayEnemyCells();
        stayEnemyCells.RemoveAll(x => !IsInsideGrid(x));

        if (castlePlacement.HasPlacement && !IsInsideGrid(castlePlacement.GridPosition))
            castlePlacement = default;

        if (villainUnionPlacement.HasPlacement && !IsInsideGrid(villainUnionPlacement.GridPosition))
            villainUnionPlacement = default;
    }

    private static Vector2Int NormalizeGridSize(Vector2Int value)
    {
        return new Vector2Int(
            Mathf.Max(1, value.x),
            Mathf.Max(1, value.y));
    }

    private void EnsureStayEnemyCells()
    {
        stayEnemyCells ??= new List<Vector2Int>();
    }
}

[Serializable]
public struct TilePlacementData
{
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private string tileKey;

    public TilePlacementData(Vector2Int gridPosition, string tileKey)
    {
        this.gridPosition = gridPosition;
        this.tileKey = tileKey;
    }

    public Vector2Int GridPosition => gridPosition;
    public string TileKey => tileKey;
}

[Serializable]
public struct ItemPlacementData
{
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int amount;

    public ItemPlacementData(Vector2Int gridPosition, ResourceType resourceType, int amount)
    {
        this.gridPosition = gridPosition;
        this.resourceType = resourceType;
        this.amount = amount;
    }

    public Vector2Int GridPosition => gridPosition;
    public ResourceType ResourceType => resourceType;
    public int Amount => amount;
}

[Serializable]
public struct OutpostPlacementData
{
    [SerializeField] private Vector2Int gridPosition;
    [FormerlySerializedAs("resourceType")]
    [SerializeField] private OutpostType outpostType;
    [SerializeField] private int resourcePerTurn;
    [SerializeField] private OutpostState initialState;

    public OutpostPlacementData(Vector2Int gridPosition, OutpostType outpostType, int resourcePerTurn, OutpostState initialState)
    {
        this.gridPosition = gridPosition;
        this.outpostType = OutpostTypeUtility.Normalize(outpostType);
        this.resourcePerTurn = resourcePerTurn;
        this.initialState = initialState;
    }

    public Vector2Int GridPosition => gridPosition;
    public OutpostType OutpostType => OutpostTypeUtility.Normalize(outpostType);
    public int ResourcePerTurn => resourcePerTurn;
    public OutpostState InitialState => initialState;

    public OutpostPlacementData Normalized()
    {
        return new OutpostPlacementData(
            gridPosition,
            OutpostType,
            Mathf.Max(1, resourcePerTurn),
            initialState);
    }
}

[Serializable]
public struct EventPlacementData
{
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private string eventKey;

    public EventPlacementData(Vector2Int gridPosition, string eventKey)
    {
        this.gridPosition = gridPosition;
        this.eventKey = eventKey;
    }

    public Vector2Int GridPosition => gridPosition;
    public string EventKey => eventKey;
}

[Serializable]
public struct UniqueBuildingPlacementData
{
    [SerializeField] private bool hasPlacement;
    [SerializeField] private Vector2Int gridPosition;

    public UniqueBuildingPlacementData(Vector2Int gridPosition)
    {
        hasPlacement = true;
        this.gridPosition = gridPosition;
    }

    public bool HasPlacement => hasPlacement;
    public Vector2Int GridPosition => gridPosition;

    public bool IsAt(Vector2Int grid)
    {
        return hasPlacement && gridPosition == grid;
    }
}
