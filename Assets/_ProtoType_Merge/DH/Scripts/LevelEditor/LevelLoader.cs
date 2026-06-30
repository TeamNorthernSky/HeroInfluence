using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class LevelLoader : MonoBehaviour
{
    private const string ObstacleRootName = "ObstacleRoot";
    private const string ItemRootName = "ItemRoot";
    private const string OutpostRootName = "OutpostRoot";
    private const string EventRootName = "EventRoot";
    private const string EnemyRootName = "EnemyRoot";
    private const string CastleRootName = "CastleRoot";
    private const string VillainUnionRootName = "VillainUnionRoot";

    [Header("Data")]
    [SerializeField] private LevelData levelData;

    [Header("Runtime References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private LevelPrefabRegistry prefabRegistry;
    [FormerlySerializedAs("tilemapGenerator")]
    [SerializeField] private LevelTileMeshGenerator tileMeshGenerator;

    [Header("Spawn Roots")]
    [SerializeField] private Transform obstacleRoot;
    [SerializeField] private Transform itemRoot;
    [FormerlySerializedAs("mineRoot")]
    [SerializeField] private Transform outpostRoot;
    [SerializeField] private Transform eventRoot;
    [FormerlySerializedAs("stayEnemyRoot")]
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private Transform castleRoot;
    [SerializeField] private Transform villainUnionRoot;

    [Header("Load Options")]
    [SerializeField] private bool loadOnStart;
    [SerializeField] private bool clearExistingBeforeLoad = true;
    [SerializeField] private bool applyInEditMode = true;
    [SerializeField] private bool autoReloadOnValidate = true;

    public LevelData LevelData => levelData;
    public GridManager GridManager => gridManager;
    public LevelPrefabRegistry PrefabRegistry => prefabRegistry;

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
            LoadLevel();
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

    [ContextMenu("Load Level")]
    public void LoadLevel()
    {
        if (levelData == null || gridManager == null)
            return;

        if (clearExistingBeforeLoad)
            ClearSpawnedObjects();

        GenerateTileMeshes();
        SpawnObstacles();
        SpawnItems();
        SpawnOutposts();
        SpawnEvents();
        SpawnEnemyPlacements();
        SpawnUniqueBuildings();
    }

    private void TryLoadInEditMode()
    {
        if (!applyInEditMode)
            return;

        if (levelData == null || gridManager == null)
            return;

        LoadLevel();
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

    private void SpawnObstacles()
    {
        GameObject obstaclePrefab = prefabRegistry != null ? prefabRegistry.ObstaclePrefab : null;
        if (obstaclePrefab == null)
            return;

        var obstacleCells = levelData.ObstacleCells;
        Transform parent = GetObstacleRoot(true);
        for (int i = 0; i < obstacleCells.Count; i++)
        {
            SpawnGameObject(obstaclePrefab, obstacleCells[i], parent);
        }
    }

    private void SpawnItems()
    {
        if (prefabRegistry == null)
            return;

        var itemPlacements = levelData.ItemPlacements;
        Transform parent = GetItemRoot(true);
        for (int i = 0; i < itemPlacements.Count; i++)
        {
            ItemPlacementData placement = itemPlacements[i];
            if (Application.isPlaying && IsItemCollected(placement.GridPosition))
                continue;

            if (!prefabRegistry.TryGetItemPrefab(placement.ResourceType, out ItemObject itemPrefab))
            {
                Debug.LogWarning(
                    $"LevelLoader could not find an item prefab for resource type '{placement.ResourceType}'.",
                    this);
                continue;
            }

            ItemObject item = SpawnComponent(itemPrefab, placement.GridPosition, parent);
            if (item == null)
                continue;

            item.ApplyInitialAmount(placement.Amount);
        }
    }

    private static bool IsItemCollected(Vector2Int grid)
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.IsItemCollected(MapProgressKey.ForItem(grid));
    }

    private void SpawnOutposts()
    {
        if (prefabRegistry == null)
            return;

        var outpostPlacements = levelData.OutpostPlacements;
        Transform parent = GetOutpostRoot(true);
        for (int i = 0; i < outpostPlacements.Count; i++)
        {
            OutpostPlacementData placement = outpostPlacements[i];
            if (!prefabRegistry.TryGetOutpostPrefab(placement.OutpostType, out Outpost outpostPrefab))
            {
                Debug.LogWarning(
                    $"LevelLoader could not find an outpost prefab for outpost type '{placement.OutpostType}'.",
                    this);
                continue;
            }

            Outpost outpost = SpawnComponent(outpostPrefab, placement.GridPosition, parent);
            if (outpost == null)
                continue;

            OutpostState initialState = GetOutpostInitialState(placement.GridPosition, placement.InitialState);
            outpost.ApplyInitialData(
                placement.OutpostType,
                placement.ResourcePerTurn,
                initialState);
            ApplyOutpostProgressData(outpost, placement.GridPosition);
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

    private void SpawnEvents()
    {
        if (prefabRegistry == null)
            return;

        Transform resolvedEventRoot = GetEventRoot(true);
        var eventPlacements = levelData.EventPlacements;
        for (int i = 0; i < eventPlacements.Count; i++)
        {
            EventPlacementData placement = eventPlacements[i];
            if (Application.isPlaying && IsEventCompleted(placement.GridPosition, placement.EventType))
                continue;

            if (!prefabRegistry.TryGetEventPrefab(placement.EventType, out MapEventObject eventPrefab))
            {
                Debug.LogWarning(
                    $"LevelLoader could not find an event prefab for event key '{placement.EventKey}'.",
                    this);
                continue;
            }

            MapEventObject mapEvent = SpawnComponent(eventPrefab, placement.GridPosition, resolvedEventRoot);
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

    private void SpawnEnemyPlacements()
    {
        var enemyPlacements = levelData.EnemyPlacements;
        if (enemyPlacements.Count == 0)
            return;

        if (prefabRegistry == null || !prefabRegistry.TryGetEnemyGroupPrefab(out EnemyGridMover enemyGroupPrefab))
        {
            Debug.LogWarning("LevelLoader could not find an enemy group prefab.", this);
            return;
        }

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (Application.isPlaying && templateCatalog == null)
        {
            Debug.LogWarning("LevelLoader could not find a DHCsvTemplateCatalog in the scene.", this);
            return;
        }

        Transform parent = GetEnemyRoot(true);
        for (int i = 0; i < enemyPlacements.Count; i++)
        {
            EnemyPlacementData placement = enemyPlacements[i];
            string placementKey = MapProgressKey.ForSceneEnemy(placement.GridPosition);

            if (Application.isPlaying && IsEnemyDefeated(placementKey))
                continue;

            EnemyGroupData groupData = null;
            if (Application.isPlaying &&
                !templateCatalog.TryGetEnemyGroup(placement.EnemyGroupIndex, out groupData))
            {
                Debug.LogWarning(
                    $"LevelLoader could not find an enemy group CSV index '{placement.EnemyGroupIndex}'.",
                    this);
                continue;
            }

            EnemyGridMover enemy = SpawnComponent(enemyGroupPrefab, placement.GridPosition, parent);
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
                    placement.GridPosition,
                    placement.BehaviorType,
                    placementKey))
            {
                Debug.LogWarning(
                    $"LevelLoader failed to spawn enemy group '{placement.EnemyGroupIndex}' at {placement.GridPosition}.",
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

    private void SpawnUniqueBuildings()
    {
        if (prefabRegistry == null)
            return;

        SpawnCastle();
        SpawnVillainUnionBase();
    }

    private void SpawnCastle()
    {
        UniqueBuildingPlacementData placement = levelData.CastlePlacement;
        if (!placement.HasPlacement)
            return;

        if (!prefabRegistry.TryGetCastlePrefab(out CastleUnit castlePrefab))
        {
            Debug.LogWarning("LevelLoader could not find a castle prefab.", this);
            return;
        }

        Transform parent = GetCastleRoot(true);
        SpawnComponent(castlePrefab, placement.GridPosition, parent);
    }

    private void SpawnVillainUnionBase()
    {
        UniqueBuildingPlacementData placement = levelData.VillainUnionPlacement;
        if (!placement.HasPlacement)
            return;

        if (!prefabRegistry.TryGetVillainUnionBasePrefab(out VillainUnionBase villainUnionBasePrefab))
        {
            Debug.LogWarning("LevelLoader could not find a villain union base prefab.", this);
            return;
        }

        Transform parent = GetVillainUnionRoot(true);
        SpawnComponent(villainUnionBasePrefab, placement.GridPosition, parent);
    }

    private void ClearSpawnedObjects()
    {
        if (tileMeshGenerator != null)
            tileMeshGenerator.ClearTileMeshes();

        ClearChildren(GetObstacleRoot(false));
        ClearChildren(GetItemRoot(false));
        ClearChildren(GetOutpostRoot(false));
        ClearChildren(GetEventRoot(false));
        ClearChildren(GetCastleRoot(false));
        ClearChildren(GetVillainUnionRoot(false));
        ClearLevelSpawnedEnemies();
        ClearDirectChildrenWithComponent<CastleUnit>();
        ClearDirectChildrenWithComponent<VillainUnionBase>();
    }

    private void GenerateTileMeshes()
    {
        if (tileMeshGenerator == null)
            return;

        tileMeshGenerator.Generate(levelData);
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

    private Transform GetCastleRoot(bool createIfMissing) =>
        GetSpawnRoot(ref castleRoot, CastleRootName, createIfMissing);

    private Transform GetVillainUnionRoot(bool createIfMissing) =>
        GetSpawnRoot(ref villainUnionRoot, VillainUnionRootName, createIfMissing);

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

    private bool IsPrefabFootprintInside(GameObject prefab, Vector2Int grid)
    {
        Vector2Int size = GetPrefabFootprintSize(prefab);
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (!levelData.IsInsideGrid(new Vector2Int(grid.x + x, grid.y + y)))
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

[DisallowMultipleComponent]
public class LevelSpawnedEnemyMarker : MonoBehaviour
{
}
