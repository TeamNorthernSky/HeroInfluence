using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class LevelLoader : MonoBehaviour
{
    private const string EventRootName = "EventRoot";

    [Header("Data")]
    [SerializeField] private LevelData levelData;

    [Header("Runtime References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private LevelPrefabRegistry prefabRegistry;
    [SerializeField] private LevelTilemapGenerator tilemapGenerator;

    [Header("Spawn Roots")]
    [SerializeField] private Transform obstacleRoot;
    [SerializeField] private Transform itemRoot;
    [FormerlySerializedAs("mineRoot")]
    [SerializeField] private Transform outpostRoot;
    [SerializeField] private Transform eventRoot;
    [SerializeField] private Transform stayEnemyRoot;

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

        GenerateTilemaps();
        SpawnObstacles();
        SpawnItems();
        SpawnOutposts();
        SpawnEvents();
        SpawnStayEnemies();
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
        for (int i = 0; i < obstacleCells.Count; i++)
        {
            SpawnGameObject(obstaclePrefab, obstacleCells[i], obstacleRoot);
        }
    }

    private void SpawnItems()
    {
        if (prefabRegistry == null)
            return;

        var itemPlacements = levelData.ItemPlacements;
        for (int i = 0; i < itemPlacements.Count; i++)
        {
            ItemPlacementData placement = itemPlacements[i];
            if (!prefabRegistry.TryGetItemPrefab(placement.ResourceType, out ItemObject itemPrefab))
            {
                Debug.LogWarning(
                    $"LevelLoader could not find an item prefab for resource type '{placement.ResourceType}'.",
                    this);
                continue;
            }

            ItemObject item = SpawnComponent(itemPrefab, placement.GridPosition, itemRoot);
            if (item == null)
                continue;

            item.ApplyInitialAmount(placement.Amount);
        }
    }

    private void SpawnOutposts()
    {
        if (prefabRegistry == null)
            return;

        var outpostPlacements = levelData.OutpostPlacements;
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

            Outpost outpost = SpawnComponent(outpostPrefab, placement.GridPosition, outpostRoot);
            if (outpost == null)
                continue;

            outpost.ApplyInitialData(
                placement.OutpostType,
                placement.ResourcePerTurn,
                placement.InitialState);
        }
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
            if (!prefabRegistry.TryGetEventPrefab(placement.EventKey, out MapEventObject eventPrefab))
            {
                Debug.LogWarning(
                    $"LevelLoader could not find an event prefab for event key '{placement.EventKey}'.",
                    this);
                continue;
            }

            MapEventObject mapEvent = SpawnComponent(eventPrefab, placement.GridPosition, resolvedEventRoot);
            if (mapEvent == null)
                continue;

            mapEvent.ApplyInitialData(placement.EventKey);
        }
    }

    private void SpawnStayEnemies()
    {
        var stayEnemyCells = levelData.StayEnemyCells;
        if (stayEnemyCells.Count == 0)
            return;

        if (prefabRegistry == null || !prefabRegistry.TryGetStayEnemyPrefab(out EnemyGridMover stayEnemyPrefab))
        {
            Debug.LogWarning("LevelLoader could not find a stay enemy prefab.", this);
            return;
        }

        Transform parent = stayEnemyRoot != null ? stayEnemyRoot : transform;
        for (int i = 0; i < stayEnemyCells.Count; i++)
        {
            EnemyGridMover stayEnemy = SpawnComponent(stayEnemyPrefab, stayEnemyCells[i], parent);
            if (stayEnemy == null)
                continue;

            stayEnemy.SetBehaviorType(EnemyBehaviorType.StayEnemy);

            if (Application.isPlaying)
            {
                EnemyUnitBootstrap enemyBootstrap = stayEnemy.GetComponent<EnemyUnitBootstrap>();
                enemyBootstrap?.InitializeEnemyUnits();
            }
        }
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

        SpawnComponent(castlePrefab, placement.GridPosition, transform);
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

        SpawnComponent(villainUnionBasePrefab, placement.GridPosition, transform);
    }

    private void ClearSpawnedObjects()
    {
        if (tilemapGenerator != null)
            tilemapGenerator.ClearTilemaps();

        ClearChildren(obstacleRoot);
        ClearChildren(itemRoot);
        ClearChildren(outpostRoot);
        ClearChildren(GetEventRoot(false));
        ClearStayEnemies();
        ClearDirectChildrenWithComponent<CastleUnit>();
        ClearDirectChildrenWithComponent<VillainUnionBase>();
    }

    private void GenerateTilemaps()
    {
        if (tilemapGenerator == null)
            return;

        tilemapGenerator.Generate(levelData);
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

    private void ClearStayEnemies()
    {
        if (stayEnemyRoot != null && stayEnemyRoot != transform)
            ClearStayEnemyChildren(stayEnemyRoot);

        ClearStayEnemyChildren(transform);
    }

    private void ClearStayEnemyChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            EnemyGridMover enemy = child.GetComponent<EnemyGridMover>();
            if (enemy == null || !enemy.IsStayEnemy)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
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

    private Transform GetEventRoot(bool createIfMissing)
    {
        if (eventRoot != null)
            return eventRoot;

        Transform existingRoot = transform.Find(EventRootName);
        if (existingRoot != null)
        {
            eventRoot = existingRoot;
            return eventRoot;
        }

        if (!createIfMissing)
            return null;

        GameObject createdRoot = new GameObject(EventRootName);
        createdRoot.transform.SetParent(transform);
        createdRoot.transform.localPosition = Vector3.zero;
        createdRoot.transform.localRotation = Quaternion.identity;
        createdRoot.transform.localScale = Vector3.one;
        eventRoot = createdRoot.transform;
        return eventRoot;
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
