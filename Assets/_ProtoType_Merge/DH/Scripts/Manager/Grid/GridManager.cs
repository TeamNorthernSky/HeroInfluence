using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public enum EnemyEncounterZoneState
{
    None,
    EnemyOccupied,
    SingleEnemyZone,
    OverlappedEnemyZone
}

public class GridManager : MonoBehaviour
{
    private static readonly Vector2Int[] directions8 =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1)
    };

    [Header("Grid 기준 오브젝트")]
    [SerializeField] private Transform landTransform;

    [Header("Grid Settings")]
    [FormerlySerializedAs("hexRadius")]
    [SerializeField] private float cellSize = 1f;

    [Header("Obstacle Settings")]
    [Tooltip("이 레이어에 있는 콜라이더는 장애물로 간주합니다.")]
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private bool useLegacyObstacleColliderFallback;
    [SerializeField] private LayerMask eventLayerMask;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private bool useLegacyEnemyColliderFallback;
    [SerializeField] private LayerMask heroUnionLayerMask;
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private ItemRegistry itemRegistry;
    [SerializeField] private TutorialItemRegistry tutorialItemRegistry;
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private TutorialEnemyRegistry tutorialEnemyRegistry;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [FormerlySerializedAs("mineRegistry")]
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;
    [SerializeField] private MultiGridOccupantRegistry multiGridOccupantRegistry;
    [SerializeField] private MapEventRegistry mapEventRegistry;
    [SerializeField] private WorldEventRegistry worldEventRegistry;
    [SerializeField] private MainEventRegistry mainEventRegistry;
    [SerializeField] private SubEventRegistry subEventRegistry;
    [SerializeField] private bool restrictMovementToVisibleCells = true;
    [Tooltip("셀 워커블 검사 시, 셀 크기 대비 체크 박스 비율(너무 크면 오탐, 너무 작으면 통과).")]
    [SerializeField, Range(0.1f, 1f)] private float obstacleCheckFill = 0.9f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private bool drawGizmosOnlyWhenSelected = false;
    [SerializeField] private bool drawDebugCenterSpheres = true;
    [SerializeField] private bool drawDebugCellOutlines = true;
    [SerializeField] private int debugQMin = -15;
    [SerializeField] private int debugQMax = 15;
    [SerializeField] private int debugRMin = -15;
    [SerializeField] private int debugRMax = 15;
    [SerializeField, Range(0.01f, 0.5f)] private float debugCenterSphereRadius = 0.08f;

    // 전제 조건: Land 월드 중심 = (0, 0, 0)
    // 필요 시 인스펙터에서 원점 오프셋 확장 가능
    private Vector3 gridOrigin = Vector3.zero;
    private readonly HashSet<Vector2Int> levelObstacleCells = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> gateBlockerCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, float> cellSurfaceYOffsetByGrid = new Dictionary<Vector2Int, float>();

    public float CellSize => cellSize;
    public Transform LandTransform => landTransform;
    public Transform GroundRaycastTransform => landTransform;
    public static Vector2Int[] Directions8 => directions8;

    private static bool IsTutorialSceneActive()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() && activeScene.name.StartsWith("Tutorial", System.StringComparison.Ordinal);
    }

    private void Awake()
    {
        if (cellSize <= 0f)
            cellSize = 1f;

        if (fogGridManager == null)
            fogGridManager = FindFirstObjectByType<FogGridManager>();

        if (itemRegistry == null)
            itemRegistry = FindFirstObjectByType<ItemRegistry>();

        if (IsTutorialSceneActive() && tutorialItemRegistry == null)
            tutorialItemRegistry = FindFirstObjectByType<TutorialItemRegistry>();

        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (IsTutorialSceneActive() && tutorialEnemyRegistry == null)
            tutorialEnemyRegistry = FindFirstObjectByType<TutorialEnemyRegistry>();

        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (multiGridOccupantRegistry == null)
            multiGridOccupantRegistry = FindFirstObjectByType<MultiGridOccupantRegistry>();

        if (mapEventRegistry == null)
            mapEventRegistry = FindFirstObjectByType<MapEventRegistry>();

        if (worldEventRegistry == null)
            worldEventRegistry = FindFirstObjectByType<WorldEventRegistry>();

        if (mainEventRegistry == null)
            mainEventRegistry = FindFirstObjectByType<MainEventRegistry>();

        if (subEventRegistry == null)
            subEventRegistry = FindFirstObjectByType<SubEventRegistry>();
    }

    private void OnValidate()
    {
        if (cellSize <= 0f)
            cellSize = 1f;

        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();

        if (itemRegistry == null)
            itemRegistry = FindFirstObjectByType<ItemRegistry>();

        if (IsTutorialSceneActive() && tutorialItemRegistry == null)
            tutorialItemRegistry = FindFirstObjectByType<TutorialItemRegistry>();

        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (IsTutorialSceneActive() && tutorialEnemyRegistry == null)
            tutorialEnemyRegistry = FindFirstObjectByType<TutorialEnemyRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (multiGridOccupantRegistry == null)
            multiGridOccupantRegistry = FindFirstObjectByType<MultiGridOccupantRegistry>();

        if (mapEventRegistry == null)
            mapEventRegistry = FindFirstObjectByType<MapEventRegistry>();

        if (worldEventRegistry == null)
            worldEventRegistry = FindFirstObjectByType<WorldEventRegistry>();

        if (mainEventRegistry == null)
            mainEventRegistry = FindFirstObjectByType<MainEventRegistry>();

        if (subEventRegistry == null)
            subEventRegistry = FindFirstObjectByType<SubEventRegistry>();
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        float localX = worldPosition.x - gridOrigin.x;
        float localZ = worldPosition.z - gridOrigin.z;

        int x = Mathf.RoundToInt(localX / cellSize);
        int y = Mathf.RoundToInt(localZ / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorldCenter(Vector2Int grid)
    {
        float x = gridOrigin.x + cellSize * grid.x;
        float z = gridOrigin.z + cellSize * grid.y;

        return new Vector3(x, 0f, z);
    }

    public void ClearCellSurfaceOffsets()
    {
        cellSurfaceYOffsetByGrid.Clear();
    }

    public void RegisterCellSurfaceOffset(Vector2Int grid, float yOffset)
    {
        cellSurfaceYOffsetByGrid[grid] = yOffset;
    }

    public bool HasCellSurfaceOffset(Vector2Int grid)
    {
        return cellSurfaceYOffsetByGrid.ContainsKey(grid);
    }

    public float GetCellSurfaceY(Vector2Int grid)
    {
        return GetLandSurfaceY() + GetCellSurfaceYOffset(grid);
    }

    public float GetFootprintSurfaceY(Vector2Int anchorGrid, Vector2Int size)
    {
        Vector2Int clampedSize = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        float surfaceY = GetCellSurfaceY(anchorGrid);

        for (int y = 0; y < clampedSize.y; y++)
        {
            for (int x = 0; x < clampedSize.x; x++)
            {
                Vector2Int grid = new Vector2Int(anchorGrid.x + x, anchorGrid.y + y);
                surfaceY = Mathf.Max(surfaceY, GetCellSurfaceY(grid));
            }
        }

        return surfaceY;
    }

    private float GetCellSurfaceYOffset(Vector2Int grid)
    {
        return cellSurfaceYOffsetByGrid.TryGetValue(grid, out float yOffset) ? yOffset : 0f;
    }

    public static int GridDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    public bool HasObstacle(Vector2Int grid)
    {
        if (levelObstacleCells.Contains(grid) || gateBlockerCells.Contains(grid))
            return true;

        return useLegacyObstacleColliderFallback && HasBlockingCollider(grid, obstacleLayerMask);
    }

    public void ClearLevelObstacleCells()
    {
        levelObstacleCells.Clear();
    }

    public void RegisterLevelObstacleCell(Vector2Int grid)
    {
        levelObstacleCells.Add(grid);
    }

    public void RegisterLevelObstacleCells(IEnumerable<Vector2Int> grids)
    {
        if (grids == null)
            return;

        foreach (Vector2Int grid in grids)
            levelObstacleCells.Add(grid);
    }

    public void ClearGateBlockerCells()
    {
        gateBlockerCells.Clear();
    }

    public void RegisterGateBlockerCell(Vector2Int grid)
    {
        gateBlockerCells.Add(grid);
    }

    public void UnregisterGateBlockerCell(Vector2Int grid)
    {
        gateBlockerCells.Remove(grid);
    }

    public void RegisterGateBlockerCells(IEnumerable<Vector2Int> grids)
    {
        if (grids == null)
            return;

        foreach (Vector2Int grid in grids)
            gateBlockerCells.Add(grid);
    }

    public void UnregisterGateBlockerCells(IEnumerable<Vector2Int> grids)
    {
        if (grids == null)
            return;

        foreach (Vector2Int grid in grids)
            gateBlockerCells.Remove(grid);
    }

    public bool HasOtherPlayer(Vector2Int grid, Transform selfTransform)
    {
        PartyRegistry partyRegistry = FindFirstObjectByType<PartyRegistry>();
        PartyGridMover playerParty = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (playerParty == null)
            return false;

        if (selfTransform != null &&
            (playerParty.transform == selfTransform || playerParty.transform.IsChildOf(selfTransform)))
        {
            return false;
        }

        if (!playerParty.gameObject.activeInHierarchy)
            return false;

        return playerParty.GetCurrentGrid() == grid;
    }

    public bool HasItem(Vector2Int grid)
    {
        return TryGetGridItemObjectAtGrid(grid, out _);
    }

    public bool TryGetItemObjectAtGrid(Vector2Int grid, out ItemObject itemObject)
    {
        itemObject = null;

        IReadOnlyList<ItemObject> items = itemRegistry != null
            ? itemRegistry.Items
            : FindObjectsByType<ItemObject>(FindObjectsSortMode.None);

        for (int i = 0; i < items.Count; i++)
        {
            ItemObject candidate = items[i];
            if (candidate == null || !candidate.OccupiesGrid(grid, this))
                continue;

            itemObject = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetGridItemObjectAtGrid(Vector2Int grid, out IGridItemObject itemObject)
    {
        if (TryGetItemObjectAtGrid(grid, out ItemObject runtimeItem))
        {
            itemObject = runtimeItem;
            return true;
        }

        if (TryGetTutorialItemObjectAtGrid(grid, out TutorialItemObject tutorialItem))
        {
            itemObject = tutorialItem;
            return true;
        }

        itemObject = null;
        return false;
    }

    public bool TryGetTutorialItemObjectAtGrid(Vector2Int grid, out TutorialItemObject itemObject)
    {
        itemObject = null;
        if (!IsTutorialSceneActive())
            return false;

        IReadOnlyList<TutorialItemObject> items = tutorialItemRegistry != null
            ? tutorialItemRegistry.Items
            : FindObjectsByType<TutorialItemObject>(FindObjectsSortMode.None);

        for (int i = 0; i < items.Count; i++)
        {
            TutorialItemObject candidate = items[i];
            if (candidate == null || !candidate.OccupiesGrid(grid, this))
                continue;

            itemObject = candidate;
            return true;
        }

        return false;
    }

    public bool HasOutpost(Vector2Int grid)
    {
        return TryGetOutpostObjectAtGrid(grid, out _);
    }

    public bool HasEvent(Vector2Int grid)
    {
        return TryGetEventObjectAtGrid(grid, out _);
    }

    public bool HasWorldEvent(Vector2Int grid)
    {
        return TryGetWorldEventObjectAtGrid(grid, out _);
    }

    public bool HasMainEvent(Vector2Int grid)
    {
        return TryGetMainEventObjectAtGrid(grid, out _);
    }

    public bool HasSubEvent(Vector2Int grid)
    {
        return TryGetSubEventObjectAtGrid(grid, out _);
    }

    public bool HasEnemy(Vector2Int grid, Transform selfTransform = null)
    {
        return TryGetGridEnemyObjectAtGrid(grid, out _, selfTransform);
    }

    public bool HasMultiGridOccupant(Vector2Int grid, Transform selfTransform = null)
    {
        return TryGetMultiGridOccupantAtGrid(grid, out _, selfTransform);
    }

    public bool HasTutorialBuilding(Vector2Int grid, Transform selfTransform = null)
    {
        return TryGetTutorialBuildingObjectAtGrid(grid, out _, selfTransform);
    }

    public bool HasHeroUnion(Vector2Int grid, Transform selfTransform = null)
    {
        return TryGetHeroUnionByMultiGrid(grid, out _, selfTransform);
    }

    public bool TryGetTutorialBuildingObjectAtGrid(Vector2Int grid, out TutorialBuildingObject building, Transform ignoredTransform = null)
    {
        building = null;
        if (!IsTutorialSceneActive())
            return false;

        TutorialBuildingObject[] buildings = FindObjectsByType<TutorialBuildingObject>(FindObjectsSortMode.None);
        for (int i = 0; i < buildings.Length; i++)
        {
            TutorialBuildingObject candidate = buildings[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            Transform candidateTransform = candidate.transform;
            if (ignoredTransform != null
                && (candidateTransform == ignoredTransform || candidateTransform.IsChildOf(ignoredTransform)))
            {
                continue;
            }

            if (!candidate.OccupiesGrid(grid))
                continue;

            building = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetTutorialOutpostInteractionOwner(Vector2Int grid, out TutorialOutpostObject outpost)
    {
        outpost = null;
        if (!IsTutorialSceneActive())
            return false;

        TutorialOutpostObject[] outposts = FindObjectsByType<TutorialOutpostObject>(FindObjectsSortMode.None);
        for (int i = 0; i < outposts.Length; i++)
        {
            TutorialOutpostObject candidate = outposts[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            if (!candidate.IsInteractionCell(grid))
                continue;

            outpost = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetOutpostObjectAtGrid(Vector2Int grid, out Outpost outpost)
    {
        outpost = null;

        IReadOnlyList<Outpost> outposts = outpostRegistry != null
            ? outpostRegistry.Outposts
            : FindObjectsByType<Outpost>(FindObjectsSortMode.None);

        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost candidate = outposts[i];
            if (candidate == null || !candidate.OccupiesGrid(grid, this))
                continue;

            outpost = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetEventObjectAtGrid(Vector2Int grid, out MapEventObject mapEvent)
    {
        mapEvent = null;

        IReadOnlyList<MapEventObject> mapEvents = mapEventRegistry != null
            ? mapEventRegistry.MapEvents
            : FindObjectsByType<MapEventObject>(FindObjectsSortMode.None);

        for (int i = 0; i < mapEvents.Count; i++)
        {
            MapEventObject candidate = mapEvents[i];
            if (candidate == null || !candidate.OccupiesGrid(grid, this))
                continue;

            mapEvent = candidate;
            return true;
        }

        Vector3 center = GridToWorldCenter(grid);
        center.y = GetLandSurfaceY() + 0.5f;

        Vector3 halfExtents = new Vector3(cellSize * 0.5f * obstacleCheckFill, 0.5f, cellSize * 0.5f * obstacleCheckFill);
        Collider[] cols = Physics.OverlapBox(center, halfExtents, Quaternion.identity, eventLayerMask);

        for (int i = 0; i < cols.Length; i++)
        {
            Collider col = cols[i];
            if (col == null)
                continue;

            mapEvent = col.GetComponentInParent<MapEventObject>();
            if (mapEvent != null)
                return true;
        }

        return false;
    }

    public bool TryGetWorldEventObjectAtGrid(Vector2Int grid, out WorldEventObject worldEvent)
    {
        worldEvent = null;

        IReadOnlyList<WorldEventObject> worldEvents = worldEventRegistry != null
            ? worldEventRegistry.WorldEvents
            : FindObjectsByType<WorldEventObject>(FindObjectsSortMode.None);

        for (int i = 0; i < worldEvents.Count; i++)
        {
            WorldEventObject candidate = worldEvents[i];
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.OccupiesGrid(grid, this))
                continue;

            worldEvent = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetMainEventObjectAtGrid(Vector2Int grid, out MainEventObject mainEvent)
    {
        mainEvent = null;

        IReadOnlyList<MainEventObject> mainEvents = mainEventRegistry != null
            ? mainEventRegistry.MainEvents
            : FindObjectsByType<MainEventObject>(FindObjectsSortMode.None);

        for (int i = 0; i < mainEvents.Count; i++)
        {
            MainEventObject candidate = mainEvents[i];
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.OccupiesGrid(grid, this))
                continue;

            mainEvent = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetMainEventAtInteractionCell(Vector2Int grid, out MainEventObject mainEvent)
    {
        mainEvent = null;

        if (mainEventRegistry != null)
            return mainEventRegistry.TryGetEventAtInteractionCell(grid, this, out mainEvent);

        IReadOnlyList<MainEventObject> mainEvents = FindObjectsByType<MainEventObject>(FindObjectsSortMode.None);

        for (int i = 0; i < mainEvents.Count; i++)
        {
            MainEventObject candidate = mainEvents[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            if (!candidate.IsInteractionCell(grid, this))
                continue;

            mainEvent = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetSubEventObjectAtGrid(Vector2Int grid, out SubEventObject subEvent)
    {
        subEvent = null;

        IReadOnlyList<SubEventObject> subEvents = subEventRegistry != null
            ? subEventRegistry.SubEvents
            : FindObjectsByType<SubEventObject>(FindObjectsSortMode.None);

        for (int i = 0; i < subEvents.Count; i++)
        {
            SubEventObject candidate = subEvents[i];
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.OccupiesGrid(grid, this))
                continue;

            subEvent = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetEnemyObjectAtGrid(Vector2Int grid, out EnemyGridMover enemy, Transform ignoredTransform = null)
    {
        enemy = null;

        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry != null
            ? enemyRegistry.Enemies
            : FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover candidate = enemies[i];
            if (candidate == null || candidate.GetCurrentGrid() != grid)
                continue;

            Transform candidateTransform = candidate.transform;
            if (ignoredTransform != null
                && (candidateTransform == ignoredTransform || candidateTransform.IsChildOf(ignoredTransform)))
            {
                continue;
            }

            enemy = candidate;
            return true;
        }

        if (!useLegacyEnemyColliderFallback)
            return false;

        Vector3 center = GridToWorldCenter(grid);
        center.y = GetLandSurfaceY() + 0.5f;

        Vector3 halfExtents = new Vector3(cellSize * 0.5f * obstacleCheckFill, 0.5f, cellSize * 0.5f * obstacleCheckFill);
        Collider[] cols = Physics.OverlapBox(center, halfExtents, Quaternion.identity, enemyLayerMask);

        for (int i = 0; i < cols.Length; i++)
        {
            Collider col = cols[i];
            if (col == null)
                continue;

            if (ignoredTransform != null && (col.transform == ignoredTransform || col.transform.IsChildOf(ignoredTransform)))
                continue;

            enemy = col.GetComponentInParent<EnemyGridMover>();
            if (enemy != null)
                return true;
        }

        return false;
    }

    public bool TryGetGridEnemyObjectAtGrid(Vector2Int grid, out IGridEnemyObject enemy, Transform ignoredTransform = null)
    {
        if (TryGetEnemyObjectAtGrid(grid, out EnemyGridMover runtimeEnemy, ignoredTransform))
        {
            enemy = runtimeEnemy;
            return true;
        }

        if (TryGetTutorialEnemyObjectAtGrid(grid, out TutorialEnemyObject tutorialEnemy, ignoredTransform))
        {
            enemy = tutorialEnemy;
            return true;
        }

        enemy = null;
        return false;
    }

    public bool TryGetTutorialEnemyObjectAtGrid(Vector2Int grid, out TutorialEnemyObject enemy, Transform ignoredTransform = null)
    {
        enemy = null;
        if (!IsTutorialSceneActive())
            return false;

        IReadOnlyList<TutorialEnemyObject> enemies = tutorialEnemyRegistry != null
            ? tutorialEnemyRegistry.Enemies
            : FindObjectsByType<TutorialEnemyObject>(FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Count; i++)
        {
            TutorialEnemyObject candidate = enemies[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.GetCurrentGrid() != grid)
                continue;

            Transform candidateTransform = candidate.transform;
            if (ignoredTransform != null
                && (candidateTransform == ignoredTransform || candidateTransform.IsChildOf(ignoredTransform)))
            {
                continue;
            }

            enemy = candidate;
            return true;
        }

        return false;
    }

    public EnemyEncounterZoneState GetEnemyEncounterZoneState(Vector2Int grid, out EnemyGridMover owner)
    {
        owner = null;

        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry != null
            ? enemyRegistry.Enemies
            : FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null)
                continue;

            if (enemy.GetCurrentGrid() != grid)
                continue;

            owner = enemy;
            return EnemyEncounterZoneState.EnemyOccupied;
        }

        int ownerCount = 0;
        EnemyGridMover singleOwner = null;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null)
                continue;

            if (GridDistance(grid, enemy.GetCurrentGrid()) > 1)
                continue;

            ownerCount++;
            if (ownerCount == 1)
            {
                singleOwner = enemy;
                continue;
            }

            owner = null;
            return EnemyEncounterZoneState.OverlappedEnemyZone;
        }

        if (ownerCount == 1)
        {
            owner = singleOwner;
            return EnemyEncounterZoneState.SingleEnemyZone;
        }

        return EnemyEncounterZoneState.None;
    }

    public EnemyEncounterZoneState GetGridEnemyEncounterZoneState(Vector2Int grid, out IGridEnemyObject owner)
    {
        owner = null;

        IReadOnlyList<EnemyGridMover> runtimeEnemies = enemyRegistry != null
            ? enemyRegistry.Enemies
            : FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);
        for (int i = 0; i < runtimeEnemies.Count; i++)
        {
            EnemyGridMover enemy = runtimeEnemies[i];
            if (enemy == null)
                continue;

            if (enemy.GetCurrentGrid() != grid)
                continue;

            owner = enemy;
            return EnemyEncounterZoneState.EnemyOccupied;
        }

        IReadOnlyList<TutorialEnemyObject> tutorialEnemies = null;
        if (IsTutorialSceneActive())
        {
            tutorialEnemies = tutorialEnemyRegistry != null
                ? tutorialEnemyRegistry.Enemies
                : FindObjectsByType<TutorialEnemyObject>(FindObjectsSortMode.None);
            for (int i = 0; i < tutorialEnemies.Count; i++)
            {
                TutorialEnemyObject enemy = tutorialEnemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled)
                    continue;

                if (enemy.GetCurrentGrid() != grid)
                    continue;

                owner = enemy;
                return EnemyEncounterZoneState.EnemyOccupied;
            }
        }

        int ownerCount = 0;
        IGridEnemyObject singleOwner = null;
        CountRuntimeEnemyInteractionOwners(grid, runtimeEnemies, ref ownerCount, ref singleOwner);
        if (ownerCount > 1)
        {
            owner = null;
            return EnemyEncounterZoneState.OverlappedEnemyZone;
        }

        if (tutorialEnemies != null)
        {
            CountTutorialEnemyInteractionOwners(grid, tutorialEnemies, ref ownerCount, ref singleOwner);
            if (ownerCount > 1)
            {
                owner = null;
                return EnemyEncounterZoneState.OverlappedEnemyZone;
            }
        }

        if (ownerCount == 1)
        {
            owner = singleOwner;
            return EnemyEncounterZoneState.SingleEnemyZone;
        }

        return EnemyEncounterZoneState.None;
    }

    private static void CountRuntimeEnemyInteractionOwners(
        Vector2Int grid,
        IReadOnlyList<EnemyGridMover> enemies,
        ref int ownerCount,
        ref IGridEnemyObject singleOwner)
    {
        if (enemies == null)
            return;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null || !enemy.IsInteractionCell(grid))
                continue;

            ownerCount++;
            if (ownerCount == 1)
                singleOwner = enemy;

            if (ownerCount > 1)
                return;
        }
    }

    private static void CountTutorialEnemyInteractionOwners(
        Vector2Int grid,
        IReadOnlyList<TutorialEnemyObject> enemies,
        ref int ownerCount,
        ref IGridEnemyObject singleOwner)
    {
        if (enemies == null)
            return;

        for (int i = 0; i < enemies.Count; i++)
        {
            TutorialEnemyObject enemy = enemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsInteractionCell(grid))
                continue;

            ownerCount++;
            if (ownerCount == 1)
                singleOwner = enemy;

            if (ownerCount > 1)
                return;
        }
    }

    public bool TryGetEnemyEncounterZoneOwner(Vector2Int grid, out EnemyGridMover enemy)
    {
        return GetEnemyEncounterZoneState(grid, out enemy) == EnemyEncounterZoneState.SingleEnemyZone;
    }

    public bool TryGetTutorialEnemyEncounterZoneOwner(Vector2Int grid, out TutorialEnemyObject enemy)
    {
        enemy = null;
        if (!IsTutorialSceneActive())
            return false;

        if (GetGridEnemyEncounterZoneState(grid, out IGridEnemyObject owner) != EnemyEncounterZoneState.SingleEnemyZone)
            return false;

        enemy = owner as TutorialEnemyObject;
        return enemy != null;
    }

    public bool TryGetHeroUnionObjectAtGrid(Vector2Int grid, out HeroUnionUnit heroUnion)
    {
        return TryGetHeroUnionByMultiGrid(grid, out heroUnion);
    }

    public bool HasItemOrOutpost(Vector2Int grid)
    {
        return HasItem(grid) || HasOutpost(grid);
    }

    public bool HasItemOutpostOrEvent(Vector2Int grid)
    {
        return HasItem(grid) || HasOutpost(grid) || HasEvent(grid) || HasWorldEvent(grid) || HasMainEvent(grid) || HasSubEvent(grid);
    }

    public bool HasVillainUnionBase(Vector2Int grid)
    {
        return TryGetVillainUnionBaseAtGrid(grid, out _);
    }

    public bool HasInteractionTarget(Vector2Int grid)
    {
        return HasItem(grid) || HasOutpost(grid) || HasEvent(grid) || HasWorldEvent(grid) || HasMainEvent(grid) || HasSubEvent(grid) || HasEnemy(grid) || HasHeroUnion(grid) || HasVillainUnionBase(grid);
    }

    public bool IsVisibleCell(Vector2Int grid)
    {
        if (!restrictMovementToVisibleCells || fogGridManager == null)
            return true;

        return fogGridManager.IsVisible(grid);
    }

    public bool TryGetAdjacentItemGrid(Vector2Int grid, out Vector2Int itemGrid)
    {
        for (int i = 0; i < directions8.Length; i++)
        {
            Vector2Int candidate = grid + directions8[i];
            if (HasItem(candidate))
            {
                itemGrid = candidate;
                return true;
            }
        }

        itemGrid = grid;
        return false;
    }

    public bool TryGetAdjacentOutpostGrid(Vector2Int grid, out Vector2Int outpostGrid)
    {
        IReadOnlyList<Outpost> outposts = outpostRegistry != null
            ? outpostRegistry.Outposts
            : FindObjectsByType<Outpost>(FindObjectsSortMode.None);

        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost outpost = outposts[i];
            if (outpost == null)
                continue;

            IReadOnlyList<Vector2Int> interactionCells = outpost.GetAdjacentInteractionCells(this);
            for (int cellIndex = 0; cellIndex < interactionCells.Count; cellIndex++)
            {
                if (interactionCells[cellIndex] != grid)
                    continue;

                outpostGrid = outpost.GetAnchorGrid(this);
                return true;
            }
        }

        outpostGrid = grid;
        return false;
    }

    public bool TryGetAdjacentEventGrid(Vector2Int grid, out Vector2Int eventGrid)
    {
        for (int i = 0; i < directions8.Length; i++)
        {
            Vector2Int candidate = grid + directions8[i];
            if (TryGetEventObjectAtGrid(candidate, out _))
            {
                eventGrid = candidate;
                return true;
            }
        }

        eventGrid = grid;
        return false;
    }

    public bool TryGetAdjacentWorldEventGrid(Vector2Int grid, out Vector2Int eventGrid)
    {
        for (int i = 0; i < directions8.Length; i++)
        {
            Vector2Int candidate = grid + directions8[i];
            if (TryGetWorldEventObjectAtGrid(candidate, out _))
            {
                eventGrid = candidate;
                return true;
            }
        }

        eventGrid = grid;
        return false;
    }

    public bool TryGetAdjacentEnemyGrid(Vector2Int grid, out Vector2Int enemyGrid)
    {
        for (int i = 0; i < directions8.Length; i++)
        {
            Vector2Int candidate = grid + directions8[i];
            if (HasEnemy(candidate))
            {
                enemyGrid = candidate;
                return true;
            }
        }

        enemyGrid = grid;
        return false;
    }

    public bool TryGetAdjacentHeroUnionObject(Vector2Int grid, out HeroUnionUnit heroUnion)
    {
        heroUnion = null;

        HeroUnionUnit[] heroUnions = FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < heroUnions.Length; i++)
        {
            HeroUnionUnit candidate = heroUnions[i];
            if (candidate == null)
                continue;

            if (!IsAdjacentToHeroUnion(grid, candidate))
                continue;

            heroUnion = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetVillainUnionBaseAtGrid(Vector2Int grid, out VillainUnionBase villainUnionBase)
    {
        villainUnionBase = null;

        IReadOnlyList<VillainUnionBase> bases = villainUnionBaseRegistry != null
            ? villainUnionBaseRegistry.VillainUnionBases
            : FindObjectsByType<VillainUnionBase>(FindObjectsSortMode.None);

        for (int i = 0; i < bases.Count; i++)
        {
            VillainUnionBase candidate = bases[i];
            if (candidate == null)
                continue;

            MultiGridOccupant occupant = candidate.GetComponent<MultiGridOccupant>();
            if (occupant != null)
            {
                if (!occupant.OccupiesCell(grid))
                    continue;
            }
            else if (candidate.GetCurrentGrid() != grid)
            {
                continue;
            }

            villainUnionBase = candidate;
            return true;
        }

        return false;
    }

    public bool IsAdjacentToHeroUnion(Vector2Int grid, HeroUnionUnit heroUnion)
    {
        if (heroUnion == null)
            return false;

        return heroUnion.IsInteractionCell(grid);
    }

    public bool CanEnterCell(Vector2Int grid, Vector2Int destination, Transform selfTransform = null, bool ignoreFogVisibility = false, bool allowItemCells = false)
    {
        if (HasObstacle(grid))
            return false;

        if (HasOtherPlayer(grid, selfTransform))
            return false;

        if (!ZoneEntryGuidanceController.IsCellAllowed(grid))
            return false;

        if (!ignoreFogVisibility && !IsVisibleCell(grid))
            return false;

        if (HasTutorialBuilding(grid, selfTransform))
            return false;

        if (grid == destination)
            return true;

        if (HasEnemy(grid, selfTransform))
            return false;

        if (HasMultiGridOccupant(grid, selfTransform))
            return false;

        if (HasHeroUnion(grid, selfTransform))
            return false;

        if (HasOutpost(grid) || HasEvent(grid) || HasWorldEvent(grid) || HasMainEvent(grid) || HasSubEvent(grid))
            return false;

        if (HasItem(grid))
            return allowItemCells || ZoneEntryGuidanceController.IsActive;

        return true;
    }

    public bool CanOccupyCell(Vector2Int grid, Transform selfTransform = null, bool ignoreFogVisibility = false)
    {
        if (HasObstacle(grid))
            return false;

        if (HasOtherPlayer(grid, selfTransform))
            return false;

        if (!ZoneEntryGuidanceController.IsCellAllowed(grid))
            return false;

        if (!ignoreFogVisibility && !IsVisibleCell(grid))
            return false;

        if (HasTutorialBuilding(grid, selfTransform))
            return false;

        if (HasEnemy(grid, selfTransform))
            return false;

        if (HasMultiGridOccupant(grid, selfTransform))
            return false;

        if (HasHeroUnion(grid, selfTransform))
            return false;

        return !HasItemOutpostOrEvent(grid);
    }

    private bool HasBlockingCollider(Vector2Int grid, LayerMask layerMask)
    {
        return HasBlockingCollider(grid, layerMask, null);
    }

    private bool HasBlockingCollider(Vector2Int grid, LayerMask layerMask, Transform ignoredTransform)
    {
        Vector3 center = GridToWorldCenter(grid);
        center.y = GetLandSurfaceY() + 0.5f;

        Vector3 halfExtents = new Vector3(cellSize * 0.5f * obstacleCheckFill, 0.5f, cellSize * 0.5f * obstacleCheckFill);
        Collider[] cols = Physics.OverlapBox(center, halfExtents, Quaternion.identity, layerMask);

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null)
                continue;

            if (landTransform != null && (c.transform == landTransform || c.transform.IsChildOf(landTransform)))
                continue;

            if (ignoredTransform != null && (c.transform == ignoredTransform || c.transform.IsChildOf(ignoredTransform)))
                continue;

            return true;
        }

        return false;
    }

    private bool TryGetMultiGridOccupantAtGrid(Vector2Int grid, out MultiGridOccupant occupant, Transform ignoredTransform = null)
    {
        occupant = null;

        IReadOnlyList<MultiGridOccupant> occupants = multiGridOccupantRegistry != null
            ? multiGridOccupantRegistry.Occupants
            : FindObjectsByType<MultiGridOccupant>(FindObjectsSortMode.None);

        for (int i = 0; i < occupants.Count; i++)
        {
            MultiGridOccupant candidate = occupants[i];
            if (candidate == null)
                continue;

            Transform candidateTransform = candidate.transform;
            if (ignoredTransform != null
                && (candidateTransform == ignoredTransform || candidateTransform.IsChildOf(ignoredTransform)))
            {
                continue;
            }

            if (!candidate.OccupiesCell(grid))
                continue;

            occupant = candidate;
            return true;
        }

        return false;
    }

    private bool TryGetHeroUnionByMultiGrid(Vector2Int grid, out HeroUnionUnit heroUnion, Transform ignoredTransform = null)
    {
        heroUnion = null;

        IReadOnlyList<HeroUnionUnit> heroUnions = heroUnionRegistry != null
            ? heroUnionRegistry.HeroUnions
            : FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);

        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit candidate = heroUnions[i];
            if (candidate == null)
                continue;

            Transform candidateTransform = candidate.transform;
            if (ignoredTransform != null
                && (candidateTransform == ignoredTransform || candidateTransform.IsChildOf(ignoredTransform)))
            {
                continue;
            }

            MultiGridOccupant occupant = candidate.GetComponent<MultiGridOccupant>();
            if (occupant != null)
            {
                if (!occupant.OccupiesCell(grid))
                    continue;

                heroUnion = candidate;
                return true;
            }

            if (candidate.GetCurrentGrid() != grid)
                continue;

            heroUnion = candidate;
            return true;
        }

        return false;
    }

    private static bool IsAdjacentToSingleCell(Vector2Int fromGrid, Vector2Int targetGrid)
    {
        int dx = Mathf.Abs(fromGrid.x - targetGrid.x);
        int dy = Mathf.Abs(fromGrid.y - targetGrid.y);
        return dx <= 1 && dy <= 1 && (dx != 0 || dy != 0);
    }

    // Land의 y 높이에 맞춰 marker/player를 올려놓기 위한 헬퍼
    public float GetLandSurfaceY()
    {
        if (landTransform == null)
            return 0.01f;

        // BoxCollider가 있으면 bounds 상단 사용
        if (landTransform.TryGetComponent<Collider>(out var col))
            return (col.bounds.max.y+0.01f);

        return landTransform.position.y + 0.01f;
    }

    private void OnDrawGizmos()
    {
        if (drawGizmosOnlyWhenSelected)
            return;

        DrawDebugGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmosOnlyWhenSelected)
            return;

        DrawDebugGizmos();
    }

    private void DrawDebugGizmos()
    {
        if (cellSize <= 0f)
            return;

        if (!drawDebugGizmos)
            return;

        if (!drawDebugCenterSpheres && !drawDebugCellOutlines)
            return;

        float y = GetLandSurfaceY();

        for (int q = debugQMin; q <= debugQMax; q++)
        {
            for (int r = debugRMin; r <= debugRMax; r++)
            {
                Vector3 center = GridToWorldCenter(new Vector2Int(q, r));
                center.y = y;

                if (drawDebugCenterSpheres)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(center, debugCenterSphereRadius);
                }

                if (drawDebugCellOutlines)
                    DrawSquareOutlineGizmo(center, q == debugQMin, r == debugRMin);
            }
        }
    }

    private void DrawSquareOutlineGizmo(Vector3 center, bool drawLeftEdge, bool drawBottomEdge)
    {
        Gizmos.color = Color.gray;
        float half = cellSize * 0.5f;
        Vector3 a = new Vector3(center.x - half, center.y, center.z - half);
        Vector3 b = new Vector3(center.x + half, center.y, center.z - half);
        Vector3 c = new Vector3(center.x + half, center.y, center.z + half);
        Vector3 d = new Vector3(center.x - half, center.y, center.z + half);

        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);

        if (drawBottomEdge)
            Gizmos.DrawLine(a, b);

        if (drawLeftEdge)
            Gizmos.DrawLine(d, a);
    }

}

