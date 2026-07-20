using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "LevelData",
    menuName = "DH Work/Level Editor/Level Data")]
public class LevelData : ScriptableObject
{
    private static readonly IReadOnlyList<EnemyPlacementData> EmptyEnemyPlacements = Array.Empty<EnemyPlacementData>();
    private static readonly IReadOnlyList<DecorativeBuildingPlacementData> EmptyDecorativeBuildingPlacements = Array.Empty<DecorativeBuildingPlacementData>();
    private static readonly IReadOnlyList<GatePlacementData> EmptyGatePlacements = Array.Empty<GatePlacementData>();
    private static readonly IReadOnlyList<EnemySpawnPointPlacementData> EmptyEnemySpawnPointPlacements = Array.Empty<EnemySpawnPointPlacementData>();

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
    [SerializeField] private List<EnemyPlacementData> enemyPlacements = new List<EnemyPlacementData>();
    [SerializeField] private List<DecorativeBuildingPlacementData> decorativeBuildingPlacements = new List<DecorativeBuildingPlacementData>();
    [SerializeField] private List<GatePlacementData> gatePlacements = new List<GatePlacementData>();
    [SerializeField] private List<EnemySpawnPointPlacementData> enemySpawnPointPlacements = new List<EnemySpawnPointPlacementData>();
    [SerializeField] private UniqueBuildingPlacementData heroUnionPlacement;
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
    public IReadOnlyList<EnemyPlacementData> EnemyPlacements => enemyPlacements != null ? enemyPlacements : EmptyEnemyPlacements;
    public IReadOnlyList<DecorativeBuildingPlacementData> DecorativeBuildingPlacements =>
        decorativeBuildingPlacements != null ? decorativeBuildingPlacements : EmptyDecorativeBuildingPlacements;
    public IReadOnlyList<GatePlacementData> GatePlacements => gatePlacements != null ? gatePlacements : EmptyGatePlacements;
    public IReadOnlyList<EnemySpawnPointPlacementData> EnemySpawnPointPlacements =>
        enemySpawnPointPlacements != null ? enemySpawnPointPlacements : EmptyEnemySpawnPointPlacements;
    public UniqueBuildingPlacementData HeroUnionPlacement => heroUnionPlacement;
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

    public bool HasEnemyPlacementAt(Vector2Int grid)
    {
        return TryGetEnemyPlacementAt(grid, out _);
    }

    public bool HasDecorativeBuildingAt(Vector2Int grid)
    {
        return TryGetDecorativeBuildingAt(grid, out _);
    }

    public bool HasHeroUnionAt(Vector2Int grid)
    {
        return heroUnionPlacement.IsAt(grid);
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

    public bool TryGetEnemyPlacementAt(Vector2Int grid, out EnemyPlacementData enemyPlacement)
    {
        if (enemyPlacements != null)
        {
            for (int i = 0; i < enemyPlacements.Count; i++)
            {
                if (enemyPlacements[i].GridPosition != grid)
                    continue;

                enemyPlacement = enemyPlacements[i];
                return true;
            }
        }

        enemyPlacement = default;
        return false;
    }

    public bool TryGetDecorativeBuildingAt(Vector2Int grid, out DecorativeBuildingPlacementData decorativeBuildingPlacement)
    {
        if (decorativeBuildingPlacements == null)
        {
            decorativeBuildingPlacement = default;
            return false;
        }

        for (int i = 0; i < decorativeBuildingPlacements.Count; i++)
        {
            if (decorativeBuildingPlacements[i].GridPosition != grid)
                continue;

            decorativeBuildingPlacement = decorativeBuildingPlacements[i];
            return true;
        }

        decorativeBuildingPlacement = default;
        return false;
    }

    public bool TryGetGateBlockerAt(Vector2Int grid, out GatePlacementData gatePlacement)
    {
        if (gatePlacements != null)
        {
            for (int i = 0; i < gatePlacements.Count; i++)
            {
                GatePlacementData placement = gatePlacements[i];
                if (!placement.ContainsBlockerCell(grid))
                    continue;

                gatePlacement = placement;
                return true;
            }
        }

        gatePlacement = default;
        return false;
    }

    public bool TryGetEnemySpawnPointAt(Vector2Int grid, out EnemySpawnPointPlacementData spawnPointPlacement)
    {
        if (enemySpawnPointPlacements != null)
        {
            for (int i = 0; i < enemySpawnPointPlacements.Count; i++)
            {
                if (enemySpawnPointPlacements[i].GridPosition != grid)
                    continue;

                spawnPointPlacement = enemySpawnPointPlacements[i];
                return true;
            }
        }

        spawnPointPlacement = default;
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

    public void SetEvent(Vector2Int grid, MapEventType eventType, int requireAmount, int effectAmount)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveAllPlacementsAt(grid);
        eventPlacements.Add(new EventPlacementData(grid, eventType, requireAmount, effectAmount));
    }

    public void SetEnemyPlacement(Vector2Int grid, string enemyGroupKey, EnemyBehaviorType behaviorType)
    {
        if (!IsInsideGrid(grid) || string.IsNullOrWhiteSpace(enemyGroupKey))
            return;

        EnsureEnemyPlacements();
        RemoveAllPlacementsAt(grid);
        enemyPlacements.Add(new EnemyPlacementData(grid, enemyGroupKey, behaviorType));
    }

    public void SetDecorativeBuilding(Vector2Int grid, string prefabKey)
    {
        if (!IsInsideGrid(grid) || string.IsNullOrWhiteSpace(prefabKey))
            return;

        EnsureDecorativeBuildingPlacements();
        decorativeBuildingPlacements.RemoveAll(x => x.GridPosition == grid);
        decorativeBuildingPlacements.Add(new DecorativeBuildingPlacementData(grid, prefabKey));
    }

    public void AddGateBlockerCell(
        string gateId,
        string firstZoneId,
        string secondZoneId,
        Vector2Int grid,
        int openDurationTurns)
    {
        if (!IsInsideGrid(grid))
            return;

        EnsureGatePlacements();
        string normalizedGateId = GatePlacementData.NormalizeId(gateId);
        if (string.IsNullOrWhiteSpace(normalizedGateId))
            normalizedGateId = $"gate_{grid.x}_{grid.y}";

        for (int i = 0; i < gatePlacements.Count; i++)
        {
            GatePlacementData placement = gatePlacements[i];
            if (placement.GateId != normalizedGateId)
                continue;

            placement.AddBlockerCell(grid);
            placement.SetConnectedZones(firstZoneId, secondZoneId);
            placement.SetOpenDurationTurns(openDurationTurns);
            gatePlacements[i] = placement.Normalized();
            return;
        }

        GatePlacementData nextPlacement = new GatePlacementData(
            normalizedGateId,
            firstZoneId,
            secondZoneId,
            openDurationTurns);
        nextPlacement.AddBlockerCell(grid);
        gatePlacements.Add(nextPlacement.Normalized());
    }

    public void SetEnemySpawnPoint(Vector2Int grid, string zoneId)
    {
        if (!IsInsideGrid(grid))
            return;

        EnsureEnemySpawnPointPlacements();
        enemySpawnPointPlacements.RemoveAll(x => x.GridPosition == grid);
        enemySpawnPointPlacements.Add(new EnemySpawnPointPlacementData(grid, zoneId));
    }

    public void RemoveEnemyPlacementAt(Vector2Int grid)
    {
        enemyPlacements?.RemoveAll(x => x.GridPosition == grid);
    }

    public void RemoveDecorativeBuildingAt(Vector2Int grid)
    {
        decorativeBuildingPlacements?.RemoveAll(x => x.GridPosition == grid);
    }

    public void RemoveGateBlockerAt(Vector2Int grid)
    {
        if (gatePlacements == null)
            return;

        for (int i = gatePlacements.Count - 1; i >= 0; i--)
        {
            GatePlacementData placement = gatePlacements[i];
            if (!placement.RemoveBlockerCell(grid))
                continue;

            if (placement.BlockerCells.Count == 0)
                gatePlacements.RemoveAt(i);
            else
                gatePlacements[i] = placement.Normalized();
        }
    }

    public void RemoveEnemySpawnPointAt(Vector2Int grid)
    {
        enemySpawnPointPlacements?.RemoveAll(x => x.GridPosition == grid);
    }

    public void EraseNonGroundTileAt(Vector2Int grid)
    {
        obstacleCells.Remove(grid);
        itemPlacements.RemoveAll(x => x.GridPosition == grid);
        outpostPlacements.RemoveAll(x => x.GridPosition == grid);
        eventPlacements.RemoveAll(x => x.GridPosition == grid);
        enemyPlacements?.RemoveAll(x => x.GridPosition == grid);
        decorativeBuildingPlacements?.RemoveAll(x => x.GridPosition == grid);
        RemoveGateBlockerAt(grid);
        enemySpawnPointPlacements?.RemoveAll(x => x.GridPosition == grid);
        ClearUniquePlacementsAt(grid);
    }

    public void SetHeroUnion(Vector2Int grid)
    {
        SetHeroUnion(grid, string.Empty);
    }

    public void SetHeroUnion(Vector2Int grid, string prefabKey)
    {
        if (!IsInsideGrid(grid))
            return;

        RemoveAllPlacementsAt(grid);
        heroUnionPlacement = new UniqueBuildingPlacementData(grid, prefabKey);
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
        enemyPlacements?.RemoveAll(x => x.GridPosition == grid);
        decorativeBuildingPlacements?.RemoveAll(x => x.GridPosition == grid);
        RemoveGateBlockerAt(grid);
        enemySpawnPointPlacements?.RemoveAll(x => x.GridPosition == grid);
        ClearUniquePlacementsAt(grid);
    }

    private void RemoveNonObstaclePlacementsAt(Vector2Int grid)
    {
        itemPlacements.RemoveAll(x => x.GridPosition == grid);
        outpostPlacements.RemoveAll(x => x.GridPosition == grid);
        eventPlacements.RemoveAll(x => x.GridPosition == grid);
        enemyPlacements?.RemoveAll(x => x.GridPosition == grid);
        decorativeBuildingPlacements?.RemoveAll(x => x.GridPosition == grid);
        RemoveGateBlockerAt(grid);
        enemySpawnPointPlacements?.RemoveAll(x => x.GridPosition == grid);
        ClearUniquePlacementsAt(grid);
    }

    private void RemoveAllPlacementsAt(Vector2Int grid)
    {
        obstacleCells.Remove(grid);
        itemPlacements.RemoveAll(x => x.GridPosition == grid);
        outpostPlacements.RemoveAll(x => x.GridPosition == grid);
        eventPlacements.RemoveAll(x => x.GridPosition == grid);
        enemyPlacements?.RemoveAll(x => x.GridPosition == grid);
        decorativeBuildingPlacements?.RemoveAll(x => x.GridPosition == grid);
        RemoveGateBlockerAt(grid);
        enemySpawnPointPlacements?.RemoveAll(x => x.GridPosition == grid);
        ClearUniquePlacementsAt(grid);
    }

    private void ClearUniquePlacementsAt(Vector2Int grid)
    {
        if (heroUnionPlacement.IsAt(grid))
            heroUnionPlacement = default;

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

        EnsureEnemyPlacements();
        enemyPlacements.RemoveAll(x => string.IsNullOrWhiteSpace(x.EnemyGroupKey));
        for (int i = 0; i < enemyPlacements.Count; i++)
            enemyPlacements[i] = enemyPlacements[i].Normalized();

        for (int i = 0; i < eventPlacements.Count; i++)
            eventPlacements[i] = eventPlacements[i].Normalized();

        EnsureDecorativeBuildingPlacements();
        for (int i = 0; i < decorativeBuildingPlacements.Count; i++)
            decorativeBuildingPlacements[i] = decorativeBuildingPlacements[i].Normalized();

        EnsureGatePlacements();
        for (int i = gatePlacements.Count - 1; i >= 0; i--)
        {
            GatePlacementData normalized = gatePlacements[i].Normalized();
            if (string.IsNullOrWhiteSpace(normalized.GateId) || normalized.BlockerCells.Count == 0)
                gatePlacements.RemoveAt(i);
            else
                gatePlacements[i] = normalized;
        }

        EnsureEnemySpawnPointPlacements();
        for (int i = 0; i < enemySpawnPointPlacements.Count; i++)
            enemySpawnPointPlacements[i] = enemySpawnPointPlacements[i].Normalized();
    }

    private static Vector2Int NormalizeGridSize(Vector2Int value)
    {
        return new Vector2Int(
            Mathf.Max(1, value.x),
            Mathf.Max(1, value.y));
    }

    private void EnsureEnemyPlacements()
    {
        enemyPlacements ??= new List<EnemyPlacementData>();
    }

    private void EnsureDecorativeBuildingPlacements()
    {
        decorativeBuildingPlacements ??= new List<DecorativeBuildingPlacementData>();
    }

    private void EnsureGatePlacements()
    {
        gatePlacements ??= new List<GatePlacementData>();
    }

    private void EnsureEnemySpawnPointPlacements()
    {
        enemySpawnPointPlacements ??= new List<EnemySpawnPointPlacementData>();
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
    [SerializeField] private MapEventType eventType;
    [SerializeField] private int requireAmount;
    [SerializeField] private int effectAmount;

    public EventPlacementData(Vector2Int gridPosition, MapEventType eventType, int requireAmount, int effectAmount)
    {
        this.gridPosition = gridPosition;
        this.eventType = eventType;
        this.requireAmount = Mathf.Max(1, requireAmount);
        this.effectAmount = Mathf.Max(0, effectAmount);
    }

    public Vector2Int GridPosition => gridPosition;
    public MapEventType EventType => eventType;
    public string EventKey => MapEventTypeUtility.ToEventKey(eventType);
    public int RequireAmount => requireAmount > 0
        ? requireAmount
        : MapEventTypeUtility.GetDefaultCostAmount(eventType);
    public int EffectAmount => effectAmount > 0 || eventType == MapEventType.Heal
        ? Mathf.Max(0, effectAmount)
        : MapEventTypeUtility.GetDefaultEffectAmount(eventType);

    public EventPlacementData Normalized()
    {
        return new EventPlacementData(
            gridPosition,
            eventType,
            RequireAmount,
            EffectAmount);
    }
}

[Serializable]
public struct EnemyPlacementData
{
    [SerializeField] private Vector2Int gridPosition;
    [FormerlySerializedAs("enemyGroupIndex")]
    [SerializeField] private string enemyGroupKey;
    [SerializeField] private EnemyBehaviorType behaviorType;

    public EnemyPlacementData(Vector2Int gridPosition, string enemyGroupKey, EnemyBehaviorType behaviorType)
    {
        this.gridPosition = gridPosition;
        this.enemyGroupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        this.behaviorType = behaviorType;
    }

    public Vector2Int GridPosition => gridPosition;
    public string EnemyGroupKey => string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
    public EnemyBehaviorType BehaviorType => behaviorType;

    public EnemyPlacementData Normalized()
    {
        return new EnemyPlacementData(
            gridPosition,
            EnemyGroupKey,
            behaviorType);
    }
}

[Serializable]
public struct DecorativeBuildingPlacementData
{
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private string prefabKey;

    public DecorativeBuildingPlacementData(Vector2Int gridPosition, string prefabKey)
    {
        this.gridPosition = gridPosition;
        this.prefabKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
    }

    public Vector2Int GridPosition => gridPosition;
    public string PrefabKey => string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();

    public DecorativeBuildingPlacementData Normalized()
    {
        return new DecorativeBuildingPlacementData(gridPosition, PrefabKey);
    }
}

[Serializable]
public struct GatePlacementData
{
    [SerializeField] private string gateId;
    [SerializeField] private string firstZoneId;
    [SerializeField] private string secondZoneId;
    [SerializeField, Min(1)] private int openDurationTurns;
    [SerializeField] private List<Vector2Int> blockerCells;

    public GatePlacementData(string gateId, string firstZoneId, string secondZoneId, int openDurationTurns)
    {
        this.gateId = NormalizeId(gateId);
        this.firstZoneId = NormalizeId(firstZoneId);
        this.secondZoneId = NormalizeId(secondZoneId);
        this.openDurationTurns = Mathf.Max(1, openDurationTurns);
        blockerCells = new List<Vector2Int>();
    }

    public string GateId => NormalizeId(gateId);
    public string FirstZoneId => NormalizeId(firstZoneId);
    public string SecondZoneId => NormalizeId(secondZoneId);
    public int OpenDurationTurns => Mathf.Max(1, openDurationTurns);
    public IReadOnlyList<Vector2Int> BlockerCells => blockerCells != null ? blockerCells : Array.Empty<Vector2Int>();

    public void SetConnectedZones(string firstZoneId, string secondZoneId)
    {
        this.firstZoneId = NormalizeId(firstZoneId);
        this.secondZoneId = NormalizeId(secondZoneId);
    }

    public void SetOpenDurationTurns(int nextOpenDurationTurns)
    {
        openDurationTurns = Mathf.Max(1, nextOpenDurationTurns);
    }

    public void AddBlockerCell(Vector2Int grid)
    {
        blockerCells ??= new List<Vector2Int>();
        if (!blockerCells.Contains(grid))
            blockerCells.Add(grid);
    }

    public bool RemoveBlockerCell(Vector2Int grid)
    {
        return blockerCells != null && blockerCells.Remove(grid);
    }

    public bool ContainsBlockerCell(Vector2Int grid)
    {
        return blockerCells != null && blockerCells.Contains(grid);
    }

    public bool ContainsZone(string zoneId)
    {
        string normalizedZoneId = NormalizeId(zoneId);
        return !string.IsNullOrWhiteSpace(normalizedZoneId) &&
            (FirstZoneId == normalizedZoneId || SecondZoneId == normalizedZoneId);
    }

    public GatePlacementData Normalized()
    {
        GatePlacementData normalized = new GatePlacementData(
            GateId,
            FirstZoneId,
            SecondZoneId,
            OpenDurationTurns);

        if (blockerCells != null)
        {
            for (int i = 0; i < blockerCells.Count; i++)
                normalized.AddBlockerCell(blockerCells[i]);
        }

        return normalized;
    }

    public static string NormalizeId(string value)
    {
        return MapProgressKey.NormalizeSegment(value);
    }
}

[Serializable]
public struct EnemySpawnPointPlacementData
{
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private string zoneId;

    public EnemySpawnPointPlacementData(Vector2Int gridPosition, string zoneId)
    {
        this.gridPosition = gridPosition;
        this.zoneId = MapProgressKey.NormalizeSegment(zoneId);
    }

    public Vector2Int GridPosition => gridPosition;
    public string ZoneId => MapProgressKey.NormalizeSegment(zoneId);

    public EnemySpawnPointPlacementData Normalized()
    {
        return new EnemySpawnPointPlacementData(gridPosition, ZoneId);
    }
}

[Serializable]
public struct UniqueBuildingPlacementData
{
    [SerializeField] private bool hasPlacement;
    [SerializeField] private Vector2Int gridPosition;
    [SerializeField] private string prefabKey;

    public UniqueBuildingPlacementData(Vector2Int gridPosition)
        : this(gridPosition, string.Empty)
    {
    }

    public UniqueBuildingPlacementData(Vector2Int gridPosition, string prefabKey)
    {
        hasPlacement = true;
        this.gridPosition = gridPosition;
        this.prefabKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
    }

    public bool HasPlacement => hasPlacement;
    public Vector2Int GridPosition => gridPosition;
    public string PrefabKey => string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();

    public bool IsAt(Vector2Int grid)
    {
        return hasPlacement && gridPosition == grid;
    }
}
