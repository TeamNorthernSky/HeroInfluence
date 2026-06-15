using System.Collections.Generic;
using UnityEngine;

public class EnemySpawnController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private CastleRegistry castleRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private LevelPrefabRegistry prefabRegistry;
    [SerializeField] private Transform enemyRoot;

    [Header("Spawn Rules")]
    [SerializeField] private EnemyGridMover enemyPrefab;
    [SerializeField, Min(1)] private int runtimeEnemyGroupIndex = 30002;
    [SerializeField, Min(1)] private int spawnInterval = 3;
    [SerializeField, Min(1)] private int maxActiveEnemies = 3;
    [SerializeField] private bool spawnOneEnemyOnStart;
    [SerializeField] private bool skipInitialSpawnWhenSceneHasMobileEnemy = true;
    [SerializeField] private string runtimeSpawnSourceKey;
    [SerializeField, Min(1)] private int nextRuntimeEnemySequence = 1;

    private readonly List<ProductionBaseCandidate> productionBaseCandidates = new List<ProductionBaseCandidate>();
    private bool hasSpawnedInitialEnemy;

    private readonly struct ProductionBaseCandidate
    {
        public ProductionBaseCandidate(Vector2Int baseGrid, IReadOnlyList<Vector2Int> spawnCells)
        {
            BaseGrid = baseGrid;
            SpawnCells = spawnCells;
        }

        public Vector2Int BaseGrid { get; }
        public IReadOnlyList<Vector2Int> SpawnCells { get; }
    }

    private void OnEnable()
    {
        ResolveReferences();
        RestoreRuntimeEnemies();

        if (turnManager != null)
            turnManager.DayAdvanced += HandleDayAdvanced;

        if (spawnOneEnemyOnStart && !hasSpawnedInitialEnemy)
        {
            enemyRegistry?.RefreshSceneEnemies();
            if (!skipInitialSpawnWhenSceneHasMobileEnemy || GetActiveEnemyCount() == 0)
                TrySpawnOneEnemy();

            hasSpawnedInitialEnemy = true;
        }
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.DayAdvanced -= HandleDayAdvanced;
    }

    [ContextMenu("Try Spawn One Enemy")]
    public void TrySpawnOneEnemy()
    {
        if (enemyPrefab == null || gridManager == null)
            return;

        if (GetActiveEnemyCount() >= maxActiveEnemies)
            return;

        if (!TryGetPlayerMainCastle(out CastleUnit playerMainCastle))
            return;

        Vector2Int playerCastleGrid = playerMainCastle.GetCurrentGrid();
        CollectProductionBaseCandidates();
        SortCandidatesByDistanceToPlayerCastle(playerCastleGrid);

        for (int i = 0; i < productionBaseCandidates.Count; i++)
        {
            ProductionBaseCandidate candidate = productionBaseCandidates[i];
            if (TrySpawnFromCandidate(candidate, playerCastleGrid))
                return;
        }
    }

    private void HandleDayAdvanced(int currentDay)
    {
        if (spawnInterval <= 0 || currentDay % spawnInterval != 0)
            return;

        TrySpawnOneEnemy();
    }

    private void CollectProductionBaseCandidates()
    {
        productionBaseCandidates.Clear();

        if (villainUnionBaseRegistry != null)
        {
            IReadOnlyList<VillainUnionBase> villainUnionBases = villainUnionBaseRegistry.VillainUnionBases;
            for (int i = 0; i < villainUnionBases.Count; i++)
            {
                VillainUnionBase villainUnionBase = villainUnionBases[i];
                if (villainUnionBase == null)
                    continue;

                productionBaseCandidates.Add(new ProductionBaseCandidate(
                    villainUnionBase.GetCurrentGrid(),
                    GetBottomSpawnCells(villainUnionBase.transform, villainUnionBase.GetCurrentGrid())));
            }
        }

        if (outpostRegistry == null)
            return;

        IReadOnlyList<Outpost> outposts = outpostRegistry.Outposts;
        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost outpost = outposts[i];
            if (outpost == null || !outpost.IsEnemyClaimed)
                continue;

            Vector2Int outpostGrid = outpost.GetAnchorGrid(gridManager);
            productionBaseCandidates.Add(new ProductionBaseCandidate(
                outpostGrid,
                GetBottomSpawnCells(outpost.transform, outpostGrid)));
        }
    }

    private void SortCandidatesByDistanceToPlayerCastle(Vector2Int playerCastleGrid)
    {
        productionBaseCandidates.Sort((a, b) =>
            GridManager.GridDistance(a.BaseGrid, playerCastleGrid).CompareTo(
                GridManager.GridDistance(b.BaseGrid, playerCastleGrid)));
    }

    private bool TrySpawnFromCandidate(ProductionBaseCandidate candidate, Vector2Int playerCastleGrid)
    {
        if (candidate.SpawnCells == null || candidate.SpawnCells.Count == 0)
            return false;

        int primaryIndex = 0;
        int secondaryIndex = candidate.SpawnCells.Count > 1 ? 1 : -1;

        if (candidate.SpawnCells.Count > 1)
        {
            int firstDistance = GridManager.GridDistance(candidate.SpawnCells[0], playerCastleGrid);
            int secondDistance = GridManager.GridDistance(candidate.SpawnCells[1], playerCastleGrid);
            if (secondDistance < firstDistance)
            {
                primaryIndex = 1;
                secondaryIndex = 0;
            }
        }

        if (TrySpawnAtGrid(candidate.SpawnCells[primaryIndex]))
            return true;

        return secondaryIndex >= 0 && TrySpawnAtGrid(candidate.SpawnCells[secondaryIndex]);
    }

    private bool TrySpawnAtGrid(Vector2Int spawnGrid)
    {
        if (!gridManager.CanOccupyCell(spawnGrid, null, true))
            return false;

        EnemyGridMover spawnedEnemy = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity, enemyRoot);
        string placementKey = CreateRuntimeEnemyPlacementKey();
        EnemyIdentity enemyIdentity = spawnedEnemy.GetComponent<EnemyIdentity>();
        if (enemyIdentity != null)
        {
            enemyIdentity.SetPlacementSource(EnemyPlacementSource.Runtime);
            enemyIdentity.SetPlacementKey(placementKey);
        }

        spawnedEnemy.InitializePlacementIdentity(placementKey);
        spawnedEnemy.SnapToGridPosition(spawnGrid);

        EnemyUnitBootstrap enemyBootstrap = spawnedEnemy.GetComponent<EnemyUnitBootstrap>();
        if (!TryInitializeRuntimeEnemyGroup(enemyBootstrap, spawnedEnemy, spawnGrid, placementKey, runtimeEnemyGroupIndex))
        {
            Destroy(spawnedEnemy.gameObject);
            return false;
        }

        return true;
    }

    private void RestoreRuntimeEnemies()
    {
        if (enemyPrefab == null)
            return;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;
        if (progressRepository == null || enemyGroupRepository == null)
            return;

        SyncRuntimeEnemySequence(progressRepository);

        IReadOnlyList<EnemyWorldState> enemyStates = progressRepository.EnemyWorldStates;
        for (int i = 0; i < enemyStates.Count; i++)
        {
            EnemyWorldState state = enemyStates[i];
            if (!ShouldRestoreRuntimeEnemy(state, enemyGroupRepository))
                continue;

            if (HasMatchingEnemyInScene(state.PlacementKey, state.EnemyId))
                continue;

            RestoreRuntimeEnemy(state);
        }

        enemyRegistry?.RefreshSceneEnemies();
    }

    private static bool ShouldRestoreRuntimeEnemy(EnemyWorldState state, EnemyGroupPersistentRepository enemyGroupRepository)
    {
        if (state == null ||
            state.Defeated ||
            string.IsNullOrWhiteSpace(state.PlacementKey) ||
            string.IsNullOrWhiteSpace(state.EnemyId))
            return false;

        if (!IsRuntimeEnemyState(state))
            return false;

        return enemyGroupRepository != null && enemyGroupRepository.ContainsEnemy(state.EnemyId);
    }

    private static bool IsRuntimeEnemyState(EnemyWorldState state)
    {
        if (state.PlacementSource == EnemyPlacementSource.Runtime)
            return true;

        return !string.IsNullOrWhiteSpace(state.PlacementKey) &&
            state.PlacementKey.StartsWith("runtime_enemy_", System.StringComparison.Ordinal);
    }

    private static bool HasMatchingEnemyInScene(string placementKey, string enemyId)
    {
        EnemyGridMover[] enemies = FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null)
                continue;

            EnemyIdentity identity = enemy.GetComponent<EnemyIdentity>();
            if (identity == null)
                continue;

            bool placementMatches = !string.IsNullOrWhiteSpace(placementKey) &&
                string.Equals(identity.PlacementKey, placementKey, System.StringComparison.Ordinal);
            bool enemyIdMatches = !string.IsNullOrWhiteSpace(enemyId) &&
                string.Equals(identity.EnemyId, enemyId, System.StringComparison.Ordinal);

            if (placementMatches || enemyIdMatches)
                return true;
        }

        return false;
    }

    private void RestoreRuntimeEnemy(EnemyWorldState state)
    {
        EnemyGridMover restoredEnemy = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity, enemyRoot);
        EnemyIdentity enemyIdentity = restoredEnemy.GetComponent<EnemyIdentity>();
        if (enemyIdentity != null)
        {
            enemyIdentity.SetPlacementSource(EnemyPlacementSource.Runtime);
            enemyIdentity.SetPlacementKey(state.PlacementKey);
            enemyIdentity.SetEnemyId(state.EnemyId);
        }

        restoredEnemy.InitializePlacementIdentity(state.PlacementKey);
        restoredEnemy.InitializePersistentIdentity(state.EnemyId);
        restoredEnemy.SnapToGridPosition(state.Grid);

        EnemyUnitBootstrap enemyBootstrap = restoredEnemy.GetComponent<EnemyUnitBootstrap>();
        int groupIndex = ResolveRuntimeEnemyGroupIndex(state);
        if (!TryInitializeRuntimeEnemyGroup(enemyBootstrap, restoredEnemy, state.Grid, state.PlacementKey, groupIndex))
        {
            Destroy(restoredEnemy.gameObject);
        }
    }

    private bool TryInitializeRuntimeEnemyGroup(
        EnemyUnitBootstrap enemyBootstrap,
        EnemyGridMover enemy,
        Vector2Int grid,
        string placementKey,
        int enemyGroupIndex)
    {
        if (enemyBootstrap == null || enemy == null)
            return false;

        if (prefabRegistry == null)
        {
            Debug.LogWarning("EnemySpawnController could not find a LevelPrefabRegistry.", this);
            return false;
        }

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
        {
            Debug.LogWarning("EnemySpawnController could not find a DHCsvTemplateCatalog in the scene.", this);
            return false;
        }

        if (!templateCatalog.TryGetEnemyGroup(enemyGroupIndex, out EnemyGroupData groupData))
        {
            Debug.LogWarning($"EnemySpawnController could not find an enemy group CSV index '{enemyGroupIndex}'.", this);
            return false;
        }

        return enemyBootstrap.InitializeEnemyGroupFromCsv(
            groupData,
            prefabRegistry,
            grid,
            EnemyBehaviorType.Mobile,
            placementKey,
            EnemyPlacementSource.Runtime,
            enemyGroupIndex.ToString());
    }

    private int ResolveRuntimeEnemyGroupIndex(EnemyWorldState state)
    {
        if (state != null &&
            !string.IsNullOrWhiteSpace(state.PrefabKey) &&
            int.TryParse(state.PrefabKey, out int savedGroupIndex) &&
            savedGroupIndex > 0)
            return savedGroupIndex;

        return Mathf.Max(1, runtimeEnemyGroupIndex);
    }

    private void SyncRuntimeEnemySequence(MapProgressRepository progressRepository)
    {
        if (progressRepository == null)
            return;

        string sourceKey = MapProgressKey.NormalizeSegment(ResolveRuntimeSpawnSourceKey());
        string keyPrefix = $"runtime_enemy_{sourceKey}_";
        int highestSequence = 0;

        IReadOnlyList<EnemyWorldState> enemyStates = progressRepository.EnemyWorldStates;
        for (int i = 0; i < enemyStates.Count; i++)
        {
            EnemyWorldState state = enemyStates[i];
            if (state == null || string.IsNullOrWhiteSpace(state.PlacementKey))
                continue;

            if (!state.PlacementKey.StartsWith(keyPrefix, System.StringComparison.Ordinal))
                continue;

            string sequenceText = state.PlacementKey.Substring(keyPrefix.Length);
            if (int.TryParse(sequenceText, out int sequence) && sequence > highestSequence)
                highestSequence = sequence;
        }

        if (nextRuntimeEnemySequence <= highestSequence)
            nextRuntimeEnemySequence = highestSequence + 1;
    }

    private string CreateRuntimeEnemyPlacementKey()
    {
        int sequence = Mathf.Max(1, nextRuntimeEnemySequence);
        nextRuntimeEnemySequence = sequence + 1;
        return MapProgressKey.ForRuntimeEnemy(ResolveRuntimeSpawnSourceKey(), sequence);
    }

    private string ResolveRuntimeSpawnSourceKey()
    {
        return !string.IsNullOrWhiteSpace(runtimeSpawnSourceKey)
            ? runtimeSpawnSourceKey
            : $"{gameObject.scene.name}_{name}";
    }

    private List<Vector2Int> GetBottomSpawnCells(Transform originTransform, Vector2Int baseGrid)
    {
        MultiGridOccupant occupant = originTransform != null ? originTransform.GetComponent<MultiGridOccupant>() : null;
        if (occupant != null)
        {
            Vector2Int anchorGrid = occupant.AnchorGrid;
            Vector2Int size = occupant.Size;
            List<Vector2Int> bottomCells = new List<Vector2Int>(Mathf.Max(1, size.x));
            for (int x = 0; x < size.x; x++)
                bottomCells.Add(new Vector2Int(anchorGrid.x + x, anchorGrid.y - 1));

            return bottomCells;
        }

        return new List<Vector2Int>
        {
            new Vector2Int(baseGrid.x, baseGrid.y - 1),
            new Vector2Int(baseGrid.x + 1, baseGrid.y - 1)
        };
    }

    private int GetActiveEnemyCount()
    {
        if (enemyRegistry == null)
            return 0;

        int activeEnemyCount = 0;
        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && !enemies[i].IsStayEnemy)
                activeEnemyCount++;
        }

        return activeEnemyCount;
    }

    private bool TryGetPlayerMainCastle(out CastleUnit playerMainCastle)
    {
        playerMainCastle = null;

        if (castleRegistry == null)
            return false;

        IReadOnlyList<CastleUnit> castles = castleRegistry.Castles;
        for (int i = 0; i < castles.Count; i++)
        {
            CastleUnit castle = castles[i];
            if (castle == null)
                continue;

            playerMainCastle = castle;
            return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (castleRegistry == null)
            castleRegistry = FindFirstObjectByType<CastleRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (prefabRegistry == null)
            prefabRegistry = FindFirstObjectByType<LevelPrefabRegistry>();
    }
}
