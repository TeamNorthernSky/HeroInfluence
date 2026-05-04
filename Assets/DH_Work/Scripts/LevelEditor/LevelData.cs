using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "LevelData",
    menuName = "DH Work/Level Editor/Level Data")]
public class LevelData : ScriptableObject
{
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
    [SerializeField] private List<PartySpawnData> partySpawns = new List<PartySpawnData>();

    public string LevelId => levelId;
    public string DisplayName => displayName;
    public Vector2Int GridSize => gridSize;
    public Vector2Int GridMin => Vector2Int.zero;
    public Vector2Int GridMax => new Vector2Int(gridSize.x - 1, gridSize.y - 1);
    public IReadOnlyList<TilePlacementData> GroundTilePlacements => groundTilePlacements;
    public IReadOnlyList<Vector2Int> ObstacleCells => obstacleCells;
    public IReadOnlyList<ItemPlacementData> ItemPlacements => itemPlacements;
    public IReadOnlyList<OutpostPlacementData> OutpostPlacements => outpostPlacements;
    public IReadOnlyList<PartySpawnData> PartySpawns => partySpawns;

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

    public bool HasPartySpawnAt(Vector2Int grid)
    {
        for (int i = 0; i < partySpawns.Count; i++)
        {
            if (partySpawns[i].GridPosition == grid)
                return true;
        }

        return false;
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

    public void SetOutpost(Vector2Int grid, ResourceType resourceType, int resourcePerTurn, OutpostState initialState)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveAllPlacementsAt(grid);
        outpostPlacements.Add(new OutpostPlacementData(grid, resourceType, resourcePerTurn, initialState));
    }

    public void SetPartySpawn(Vector2Int grid, string partyId)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveAllPlacementsAt(grid);

        if (!string.IsNullOrWhiteSpace(partyId))
            partySpawns.RemoveAll(x => string.Equals(x.PartyId, partyId, StringComparison.Ordinal));

        partySpawns.Add(new PartySpawnData(partyId, grid));
    }

    public void EraseAt(Vector2Int grid)
    {
        groundTilePlacements.RemoveAll(x => x.GridPosition == grid);
        obstacleCells.Remove(grid);
        itemPlacements.RemoveAll(x => x.GridPosition == grid);
        outpostPlacements.RemoveAll(x => x.GridPosition == grid);
        partySpawns.RemoveAll(x => x.GridPosition == grid);
    }

    private void RemoveNonObstaclePlacementsAt(Vector2Int grid)
    {
        itemPlacements.RemoveAll(x => x.GridPosition == grid);
        outpostPlacements.RemoveAll(x => x.GridPosition == grid);
        partySpawns.RemoveAll(x => x.GridPosition == grid);
    }

    private void RemoveAllPlacementsAt(Vector2Int grid)
    {
        obstacleCells.Remove(grid);
        itemPlacements.RemoveAll(x => x.GridPosition == grid);
        outpostPlacements.RemoveAll(x => x.GridPosition == grid);
        partySpawns.RemoveAll(x => x.GridPosition == grid);
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
    }

    private static Vector2Int NormalizeGridSize(Vector2Int value)
    {
        return new Vector2Int(
            Mathf.Max(1, value.x),
            Mathf.Max(1, value.y));
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
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int resourcePerTurn;
    [SerializeField] private OutpostState initialState;

    public OutpostPlacementData(Vector2Int gridPosition, ResourceType resourceType, int resourcePerTurn, OutpostState initialState)
    {
        this.gridPosition = gridPosition;
        this.resourceType = resourceType;
        this.resourcePerTurn = resourcePerTurn;
        this.initialState = initialState;
    }

    public Vector2Int GridPosition => gridPosition;
    public ResourceType ResourceType => resourceType;
    public int ResourcePerTurn => resourcePerTurn;
    public OutpostState InitialState => initialState;
}

[Serializable]
public struct PartySpawnData
{
    [SerializeField] private string partyId;
    [SerializeField] private Vector2Int gridPosition;

    public PartySpawnData(string partyId, Vector2Int gridPosition)
    {
        this.partyId = partyId;
        this.gridPosition = gridPosition;
    }

    public string PartyId => partyId;
    public Vector2Int GridPosition => gridPosition;
}
