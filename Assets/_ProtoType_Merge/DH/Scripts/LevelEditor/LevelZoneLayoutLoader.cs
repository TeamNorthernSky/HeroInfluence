using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class LevelZoneLayoutLoader : MonoBehaviour
{
    private const string ObstacleRootName = "ObstacleRoot";
    private const string ItemRootName = "ItemRoot";
    private const string OutpostRootName = "OutpostRoot";
    private const string EventRootName = "EventRoot";
    private const string EnemyRootName = "EnemyRoot";
    private const string HeroUnionRootName = "HeroUnionRoot";
    private const string VillainUnionRootName = "VillainUnionRoot";
    private const string DecorativeBuildingRootName = "DecorativeBuildingRoot";

    [Header("Data")]
    [SerializeField] private LevelZoneLayoutData layoutData;

    [Header("Runtime References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private LevelPrefabRegistry prefabRegistry;
    [FormerlySerializedAs("tilemapGenerator")]
    [SerializeField] private LevelTileMeshGenerator tileMeshGenerator;

    [Header("Spawn Roots")]
    [SerializeField] private Transform obstacleRoot;
    [SerializeField] private Transform itemRoot;
    [SerializeField] private Transform outpostRoot;
    [SerializeField] private Transform eventRoot;
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private Transform heroUnionRoot;
    [SerializeField] private Transform villainUnionRoot;
    [SerializeField] private Transform decorativeBuildingRoot;

    [Header("Load Options")]
    [SerializeField] private bool loadOnStart;
    [SerializeField] private bool clearExistingBeforeLoad = true;
    [SerializeField] private bool applyInEditMode = true;
    [SerializeField] private bool autoReloadOnValidate = true;
    [SerializeField] private bool stopOnValidationErrors = true;
    [SerializeField] private bool randomizeInEditMode;
    [SerializeField] private int randomSeed;

    [Header("Zone Options")]
    [FormerlySerializedAs("generateTilemaps")]
    [SerializeField] private bool generateTileMeshes = true;
    [SerializeField] private bool spawnUniqueBuildingsFromZones;

    private readonly List<string> validationErrors = new List<string>();
    private readonly List<LoadedLevelZoneData> loadedZones = new List<LoadedLevelZoneData>();

    public LevelZoneLayoutData LayoutData => layoutData;
    public GridManager GridManager => gridManager;
    public LevelPrefabRegistry PrefabRegistry => prefabRegistry;
    public IReadOnlyList<LoadedLevelZoneData> LoadedZones => loadedZones;
    public static event System.Action<LevelZoneLayoutLoader> RuntimeLayoutLoaded;

#if UNITY_EDITOR
    private bool queuedEditorReload;
#endif

    private void Awake()
    {
        if (!Application.isPlaying || !clearExistingBeforeLoad)
            return;

        ClearSpawnedObjects();
    }

    private void Start()
    {
        if (loadOnStart)
            LoadLayout();
    }

    private void OnValidate()
    {
        if (!autoReloadOnValidate || Application.isPlaying)
            return;

        QueueEditorReload();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        QueueEditorReload();
    }

    [ContextMenu("Load Zone Layout")]
    public void LoadLayout()
    {
        if (layoutData == null || gridManager == null)
            return;

        if (!ValidateLayout())
            return;

        if (clearExistingBeforeLoad)
            ClearSpawnedObjects();

        loadedZones.Clear();
        IReadOnlyList<LevelZoneSlot> zones = layoutData.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            LevelZoneSlot zone = zones[i];
            if (zone == null)
                continue;

            if (!TrySelectCandidate(zone, i, out LevelData levelData, out int selectedCandidateIndex))
                continue;

            loadedZones.Add(new LoadedLevelZoneData(
                zone.ZoneId,
                zone.Anchor,
                zone.Size,
                selectedCandidateIndex,
                levelData));
            LoadZoneLevelData(zone, levelData);
        }

        if (Application.isPlaying)
            RuntimeLayoutLoaded?.Invoke(this);
    }

    private void TryLoadInEditMode()
    {
        if (!applyInEditMode)
            return;

        if (layoutData == null || gridManager == null)
            return;

        LoadLayout();
    }

    private void QueueEditorReload()
    {
#if UNITY_EDITOR
        if (queuedEditorReload)
            return;

        queuedEditorReload = true;
        EditorApplication.delayCall += HandleEditorReload;
#endif
    }

#if UNITY_EDITOR
    private void HandleEditorReload()
    {
        EditorApplication.delayCall -= HandleEditorReload;
        queuedEditorReload = false;

        if (this == null || !isActiveAndEnabled || Application.isPlaying)
            return;

        TryLoadInEditMode();
    }
#endif

    private bool ValidateLayout()
    {
        validationErrors.Clear();
        bool valid = layoutData.ValidateLayout(validationErrors);
        if (valid)
            return true;

        for (int i = 0; i < validationErrors.Count; i++)
            Debug.LogWarning(validationErrors[i], this);

        return !stopOnValidationErrors;
    }

    private bool TrySelectCandidate(
        LevelZoneSlot zone,
        int zoneIndex,
        out LevelData selectedLevelData,
        out int selectedCandidateIndex)
    {
        selectedLevelData = null;
        selectedCandidateIndex = -1;

        IReadOnlyList<LevelData> candidates = zone.Candidates;
        if (candidates == null || candidates.Count == 0)
            return false;

        MapProgressRepository progressRepository = Application.isPlaying ? MapProgressRepository.Instance : null;
        if (progressRepository != null &&
            progressRepository.TryGetLevelZoneSelection(layoutData.LayoutId, zone.ZoneId, out int savedCandidateIndex) &&
            zone.TryGetCandidate(savedCandidateIndex, out LevelData savedCandidate))
        {
            selectedLevelData = savedCandidate;
            selectedCandidateIndex = savedCandidateIndex;
            return true;
        }

        List<int> validCandidateIndices = null;
        for (int i = 0; i < candidates.Count; i++)
        {
            LevelData candidate = candidates[i];
            if (candidate == null)
                continue;

            validCandidateIndices ??= new List<int>();
            validCandidateIndices.Add(i);
        }

        if (validCandidateIndices == null || validCandidateIndices.Count == 0)
        {
            if (!zone.Optional)
                Debug.LogWarning($"LevelZoneLayoutLoader could not find candidates for zone '{zone.ZoneId}'.", this);

            return false;
        }

        if (zone.ZoneType == LevelZoneType.Fixed || validCandidateIndices.Count == 1)
        {
            selectedCandidateIndex = validCandidateIndices[0];
            selectedLevelData = candidates[selectedCandidateIndex];
            progressRepository?.SetLevelZoneSelection(layoutData.LayoutId, zone.ZoneId, selectedCandidateIndex);
            return true;
        }

        int selectedIndex = GetRandomIndex(zoneIndex, validCandidateIndices.Count);
        selectedCandidateIndex = validCandidateIndices[selectedIndex];
        selectedLevelData = candidates[selectedCandidateIndex];
        progressRepository?.SetLevelZoneSelection(layoutData.LayoutId, zone.ZoneId, selectedCandidateIndex);
        return true;
    }

    private int GetRandomIndex(int zoneIndex, int count)
    {
        if (count <= 1)
            return 0;

        if (Application.isPlaying || randomizeInEditMode)
            return Random.Range(0, count);

        unchecked
        {
            int value = randomSeed;
            value = (value * 397) ^ zoneIndex;
            value = Mathf.Abs(value);
            return value % count;
        }
    }

    private void LoadZoneLevelData(LevelZoneSlot zone, LevelData levelData)
    {
        Vector2Int offset = zone.Anchor;

        if (generateTileMeshes && tileMeshGenerator != null)
            tileMeshGenerator.Generate(levelData, offset, false);

        SpawnObstacles(levelData, offset);
        SpawnItems(levelData, offset);
        SpawnOutposts(levelData, offset);
        SpawnEvents(levelData, offset);
        SpawnEnemyPlacements(levelData, offset);
        SpawnDecorativeBuildings(levelData, offset);

        if (spawnUniqueBuildingsFromZones)
            SpawnUniqueBuildings(levelData, offset);
    }

    private void SpawnObstacles(LevelData levelData, Vector2Int offset)
    {
        GameObject obstaclePrefab = prefabRegistry != null ? prefabRegistry.ObstaclePrefab : null;
        if (obstaclePrefab == null)
            return;

        var obstacleCells = levelData.ObstacleCells;
        Transform parent = GetObstacleRoot(true);
        for (int i = 0; i < obstacleCells.Count; i++)
            SpawnGameObject(obstaclePrefab, obstacleCells[i] + offset, parent);
    }

    private void SpawnItems(LevelData levelData, Vector2Int offset)
    {
        if (prefabRegistry == null)
            return;

        var itemPlacements = levelData.ItemPlacements;
        Transform parent = GetItemRoot(true);
        for (int i = 0; i < itemPlacements.Count; i++)
        {
            ItemPlacementData placement = itemPlacements[i];
            Vector2Int grid = placement.GridPosition + offset;
            if (Application.isPlaying && IsItemCollected(grid))
                continue;

            if (!prefabRegistry.TryGetItemPrefab(placement.ResourceType, out ItemObject itemPrefab))
            {
                Debug.LogWarning(
                    $"LevelZoneLayoutLoader could not find an item prefab for resource type '{placement.ResourceType}'.",
                    this);
                continue;
            }

            ItemObject item = SpawnComponent(itemPrefab, grid, parent);
            if (item != null)
                item.ApplyInitialAmount(placement.Amount);
        }
    }

    private static bool IsItemCollected(Vector2Int grid)
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.IsItemCollected(MapProgressKey.ForItem(grid));
    }

    private void SpawnOutposts(LevelData levelData, Vector2Int offset)
    {
        if (prefabRegistry == null)
            return;

        var outpostPlacements = levelData.OutpostPlacements;
        Transform parent = GetOutpostRoot(true);
        for (int i = 0; i < outpostPlacements.Count; i++)
        {
            OutpostPlacementData placement = outpostPlacements[i];
            Vector2Int grid = placement.GridPosition + offset;
            if (!prefabRegistry.TryGetOutpostPrefab(placement.OutpostType, out Outpost outpostPrefab))
            {
                Debug.LogWarning(
                    $"LevelZoneLayoutLoader could not find an outpost prefab for outpost type '{placement.OutpostType}'.",
                    this);
                continue;
            }

            Outpost outpost = SpawnComponent(outpostPrefab, grid, parent);
            if (outpost == null)
                continue;

            OutpostState initialState = GetOutpostInitialState(grid, placement.InitialState);
            outpost.ApplyInitialData(
                placement.OutpostType,
                placement.ResourcePerTurn,
                initialState);
            ApplyOutpostProgressData(outpost, grid);
        }
    }

    private static OutpostState GetOutpostInitialState(Vector2Int grid, OutpostState fallbackState)
    {
        if (!Application.isPlaying)
            return fallbackState;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository != null && repository.TryGetOutpostState(MapProgressKey.ForOutpost(grid), out OutpostState state))
            return state;

        return fallbackState;
    }

    private static void ApplyOutpostProgressData(Outpost outpost, Vector2Int grid)
    {
        if (!Application.isPlaying || outpost == null)
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        if (!repository.TryGetOutpostProgress(MapProgressKey.ForOutpost(grid), out OutpostProgressState progressState) ||
            progressState == null)
            return;

        outpost.ApplyProgressData(
            progressState.State,
            progressState.EnemyDefenderGroupIndex,
            progressState.DefenderEnemyId);
    }

    private void SpawnEvents(LevelData levelData, Vector2Int offset)
    {
        if (prefabRegistry == null)
            return;

        Transform resolvedEventRoot = GetEventRoot(true);
        var eventPlacements = levelData.EventPlacements;
        for (int i = 0; i < eventPlacements.Count; i++)
        {
            EventPlacementData placement = eventPlacements[i];
            Vector2Int grid = placement.GridPosition + offset;
            if (Application.isPlaying && IsEventCompleted(grid, placement.EventType))
                continue;

            if (!prefabRegistry.TryGetEventPrefab(placement.EventType, out MapEventObject eventPrefab))
            {
                Debug.LogWarning(
                    $"LevelZoneLayoutLoader could not find an event prefab for event key '{placement.EventKey}'.",
                    this);
                continue;
            }

            MapEventObject mapEvent = SpawnComponent(eventPrefab, grid, resolvedEventRoot);
            if (mapEvent == null)
                continue;

            mapEvent.ApplyInitialData(
                placement.EventType,
                placement.RequireAmount,
                placement.EffectAmount);
        }
    }

    private static bool IsEventCompleted(Vector2Int grid, MapEventType eventType)
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.IsEventCompleted(MapProgressKey.ForEvent(grid, MapEventTypeUtility.ToEventKey(eventType)));
    }

    private void SpawnEnemyPlacements(LevelData levelData, Vector2Int offset)
    {
        var enemyPlacements = levelData.EnemyPlacements;
        if (enemyPlacements.Count == 0)
            return;

        if (prefabRegistry == null || !prefabRegistry.TryGetEnemyGroupPrefab(out EnemyGridMover enemyGroupPrefab))
        {
            Debug.LogWarning("LevelZoneLayoutLoader could not find an enemy group prefab.", this);
            return;
        }

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (Application.isPlaying && templateCatalog == null)
        {
            Debug.LogWarning("LevelZoneLayoutLoader could not find a DHCsvTemplateCatalog in the scene.", this);
            return;
        }

        Transform parent = GetEnemyRoot(true);
        for (int i = 0; i < enemyPlacements.Count; i++)
        {
            EnemyPlacementData placement = enemyPlacements[i];
            Vector2Int grid = placement.GridPosition + offset;
            string placementKey = MapProgressKey.ForSceneEnemy(grid);

            if (Application.isPlaying && IsEnemyDefeated(placementKey))
                continue;

            EnemyGroupData groupData = null;
            if (Application.isPlaying &&
                !templateCatalog.TryGetEnemyGroup(placement.EnemyGroupIndex, out groupData))
            {
                Debug.LogWarning(
                    $"LevelZoneLayoutLoader could not find an enemy group CSV index '{placement.EnemyGroupIndex}'.",
                    this);
                continue;
            }

            EnemyGridMover enemy = SpawnComponent(enemyGroupPrefab, grid, parent);
            if (enemy == null)
                continue;

            enemy.gameObject.AddComponent<LevelSpawnedEnemyMarker>();
            enemy.SetBehaviorType(placement.BehaviorType);

            if (!Application.isPlaying)
                continue;

            EnemyUnitBootstrap enemyBootstrap = enemy.GetComponent<EnemyUnitBootstrap>();
            if (enemyBootstrap == null ||
                !enemyBootstrap.InitializeEnemyGroupFromCsv(
                    groupData,
                    prefabRegistry,
                    grid,
                    placement.BehaviorType,
                    placementKey))
            {
                Debug.LogWarning(
                    $"LevelZoneLayoutLoader failed to spawn enemy group '{placement.EnemyGroupIndex}' at {grid}.",
                    this);
                Destroy(enemy.gameObject);
            }
        }
    }

    private static bool IsEnemyDefeated(string placementKey)
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.IsEnemyDefeated(placementKey);
    }

    private void SpawnUniqueBuildings(LevelData levelData, Vector2Int offset)
    {
        if (prefabRegistry == null)
            return;

        SpawnHeroUnion(levelData, offset);
        SpawnVillainUnionBase(levelData, offset);
    }

    private void SpawnHeroUnion(LevelData levelData, Vector2Int offset)
    {
        UniqueBuildingPlacementData placement = levelData.HeroUnionPlacement;
        if (!placement.HasPlacement)
            return;

        if (!prefabRegistry.TryGetHeroUnionPrefab(out HeroUnionUnit heroUnionPrefab))
        {
            Debug.LogWarning("LevelZoneLayoutLoader could not find a heroUnion prefab.", this);
            return;
        }

        Transform parent = GetHeroUnionRoot(true);
        SpawnComponent(heroUnionPrefab, placement.GridPosition + offset, parent);
    }

    private void SpawnVillainUnionBase(LevelData levelData, Vector2Int offset)
    {
        UniqueBuildingPlacementData placement = levelData.VillainUnionPlacement;
        if (!placement.HasPlacement)
            return;

        if (!prefabRegistry.TryGetVillainUnionBasePrefab(out VillainUnionBase villainUnionBasePrefab))
        {
            Debug.LogWarning("LevelZoneLayoutLoader could not find a villain union base prefab.", this);
            return;
        }

        Transform parent = GetVillainUnionRoot(true);
        SpawnComponent(villainUnionBasePrefab, placement.GridPosition + offset, parent);
    }

    private void SpawnDecorativeBuildings(LevelData levelData, Vector2Int offset)
    {
        if (prefabRegistry == null)
            return;

        var placements = levelData.DecorativeBuildingPlacements;
        Transform parent = GetDecorativeBuildingRoot(true);
        for (int i = 0; i < placements.Count; i++)
        {
            DecorativeBuildingPlacementData placement = placements[i];
            if (!prefabRegistry.TryGetDecorativeBuildingPrefab(placement.PrefabKey, out GameObject prefab))
            {
                Debug.LogWarning(
                    $"LevelZoneLayoutLoader could not find a decorative building prefab for key '{placement.PrefabKey}'.",
                    this);
                continue;
            }

            SpawnDecorativeGameObject(prefab, placement.GridPosition + offset, parent);
        }
    }

    private void ClearSpawnedObjects()
    {
        if (tileMeshGenerator != null)
            tileMeshGenerator.ClearTileMeshes();

        ClearChildren(GetObstacleRoot(false));
        ClearChildren(GetItemRoot(false));
        ClearChildren(GetOutpostRoot(false));
        ClearChildren(GetEventRoot(false));
        ClearChildren(GetHeroUnionRoot(false));
        ClearChildren(GetVillainUnionRoot(false));
        ClearChildren(GetDecorativeBuildingRoot(false));
        ClearLevelSpawnedEnemies();
        ClearDirectChildrenWithComponent<HeroUnionUnit>();
        ClearDirectChildrenWithComponent<VillainUnionBase>();
    }

    private void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void ClearLevelSpawnedEnemies()
    {
        Transform enemyRootTransform = GetEnemyRoot(false);
        if (enemyRootTransform != null && enemyRootTransform != transform)
            ClearLevelSpawnedEnemyChildren(enemyRootTransform);

        ClearLevelSpawnedEnemyChildren(transform);
    }

    private void ClearLevelSpawnedEnemyChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child.GetComponent<EnemyGridMover>() == null)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        LevelSpawnedEnemyMarker[] markers = root.GetComponentsInChildren<LevelSpawnedEnemyMarker>(true);
        for (int i = markers.Length - 1; i >= 0; i--)
        {
            LevelSpawnedEnemyMarker marker = markers[i];
            if (marker == null)
                continue;

            if (Application.isPlaying)
                Destroy(marker.gameObject);
            else
                DestroyImmediate(marker.gameObject);
        }
    }

    private void ClearDirectChildrenWithComponent<T>() where T : Component
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<T>() == null)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private Transform GetObstacleRoot(bool createIfMissing) =>
        GetSpawnRoot(ref obstacleRoot, ObstacleRootName, createIfMissing);

    private Transform GetItemRoot(bool createIfMissing) =>
        GetSpawnRoot(ref itemRoot, ItemRootName, createIfMissing);

    private Transform GetOutpostRoot(bool createIfMissing) =>
        GetSpawnRoot(ref outpostRoot, OutpostRootName, createIfMissing);

    private Transform GetEventRoot(bool createIfMissing) =>
        GetSpawnRoot(ref eventRoot, EventRootName, createIfMissing);

    private Transform GetEnemyRoot(bool createIfMissing) =>
        GetSpawnRoot(ref enemyRoot, EnemyRootName, createIfMissing);

    private Transform GetHeroUnionRoot(bool createIfMissing) =>
        GetSpawnRoot(ref heroUnionRoot, HeroUnionRootName, createIfMissing);

    private Transform GetVillainUnionRoot(bool createIfMissing) =>
        GetSpawnRoot(ref villainUnionRoot, VillainUnionRootName, createIfMissing);

    private Transform GetDecorativeBuildingRoot(bool createIfMissing) =>
        GetSpawnRoot(ref decorativeBuildingRoot, DecorativeBuildingRootName, createIfMissing);

    private Transform GetSpawnRoot(ref Transform root, string rootName, bool createIfMissing)
    {
        if (root != null)
            return root;

        Transform existingRoot = transform.Find(rootName);
        if (existingRoot != null)
        {
            root = existingRoot;
            return root;
        }

        if (!createIfMissing)
            return null;

        GameObject createdRoot = new GameObject(rootName);
        createdRoot.transform.SetParent(transform);
        createdRoot.transform.localPosition = Vector3.zero;
        createdRoot.transform.localRotation = Quaternion.identity;
        createdRoot.transform.localScale = Vector3.one;
        root = createdRoot.transform;
        return root;
    }

    private GameObject SpawnGameObject(GameObject prefab, Vector2Int grid, Transform parent)
    {
        if (prefab == null || !IsPrefabFootprintInside(prefab, grid))
            return null;

        Vector3 worldPosition = GetWorldPosition(prefab, grid);
        GameObject instance = Instantiate(prefab, worldPosition, Quaternion.identity, parent);
        ApplyMultiGridAnchor(instance, grid);
        return instance;
    }

    private T SpawnComponent<T>(T prefab, Vector2Int grid, Transform parent) where T : Component
    {
        if (prefab == null || !IsPrefabFootprintInside(prefab.gameObject, grid))
            return null;

        Vector3 worldPosition = GetWorldPosition(prefab.gameObject, grid);
        T instance = Instantiate(prefab, worldPosition, Quaternion.identity, parent);
        ApplyMultiGridAnchor(instance.gameObject, grid);
        return instance;
    }

    private GameObject SpawnDecorativeGameObject(GameObject prefab, Vector2Int grid, Transform parent)
    {
        if (prefab == null || !layoutData.IsInsideGrid(grid))
            return null;

        DecorativeBuildingPlacement placement = prefab.GetComponent<DecorativeBuildingPlacement>();
        if (placement == null)
        {
            Debug.LogWarning(
                $"Decorative building prefab '{prefab.name}' needs a DecorativeBuildingPlacement component.",
                this);
            return null;
        }

        Vector3 anchorWorldPosition = gridManager.GridToWorldCenter(grid);
        anchorWorldPosition.y = gridManager.GetLandSurfaceY();
        Vector3 worldPosition = placement.GetRootPositionForAnchor(anchorWorldPosition);
        return Instantiate(prefab, worldPosition, prefab.transform.rotation, parent);
    }

    private bool IsPrefabFootprintInside(GameObject prefab, Vector2Int grid)
    {
        Vector2Int size = GetPrefabFootprintSize(prefab);
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                Vector2Int cell = new Vector2Int(grid.x + x, grid.y + y);
                if (!layoutData.IsInsideGrid(cell))
                    return false;
            }
        }

        return true;
    }

    private Vector3 GetWorldPosition(GameObject prefab, Vector2Int grid)
    {
        Vector2Int size = GetPrefabFootprintSize(prefab);
        Vector2Int maxGrid = new Vector2Int(grid.x + size.x - 1, grid.y + size.y - 1);
        Vector3 minWorldPosition = gridManager.GridToWorldCenter(grid);
        Vector3 maxWorldPosition = gridManager.GridToWorldCenter(maxGrid);
        Vector3 worldPosition = (minWorldPosition + maxWorldPosition) * 0.5f;
        worldPosition.y = gridManager.GetLandSurfaceY();
        return worldPosition;
    }

    private static Vector2Int GetPrefabFootprintSize(GameObject prefab)
    {
        if (prefab == null)
            return Vector2Int.one;

        MultiGridOccupant occupant = prefab.GetComponent<MultiGridOccupant>();
        return occupant != null ? occupant.Size : Vector2Int.one;
    }

    private static void ApplyMultiGridAnchor(GameObject instance, Vector2Int grid)
    {
        if (instance == null)
            return;

        MultiGridOccupant occupant = instance.GetComponent<MultiGridOccupant>();
        if (occupant != null)
            occupant.SetAnchorGrid(grid);
    }
}

public readonly struct LoadedLevelZoneData
{
    public LoadedLevelZoneData(
        string zoneId,
        Vector2Int anchor,
        Vector2Int size,
        int selectedCandidateIndex,
        LevelData levelData)
    {
        ZoneId = zoneId;
        Anchor = anchor;
        Size = size;
        SelectedCandidateIndex = selectedCandidateIndex;
        LevelData = levelData;
    }

    public string ZoneId { get; }
    public Vector2Int Anchor { get; }
    public Vector2Int Size { get; }
    public int SelectedCandidateIndex { get; }
    public LevelData LevelData { get; }
}
