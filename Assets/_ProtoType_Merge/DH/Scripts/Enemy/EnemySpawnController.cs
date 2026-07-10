using System.Collections.Generic;
using UnityEngine;

public class EnemySpawnController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
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
    [SerializeField] private bool enableBaseEnemyProduction;
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

        if (!TryGetPlayerMainHeroUnion(out HeroUnionUnit playerMainHeroUnion))
            return;

        Vector2Int playerHeroUnionGrid = playerMainHeroUnion.GetCurrentGrid();
        CollectProductionBaseCandidates();
        SortCandidatesByDistanceToPlayerHeroUnion(playerHeroUnionGrid);

        for (int i = 0; i < productionBaseCandidates.Count; i++)
        {
            ProductionBaseCandidate candidate = productionBaseCandidates[i];
            if (TrySpawnFromCandidate(candidate, playerHeroUnionGrid))
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

        if (!enableBaseEnemyProduction)
            return;

        if (villainUnionBaseRegistry != null)
        {
            IReadOnlyList<VillainUnionBase> villainUnionBases = villainUnionBaseRegistry.VillainUnionBases;
            for (int i = 0; i < villainUnionBases.Count; i++)
            {
                VillainUnionBase villainUnionBase = villainUnionBases[i];
                if (villainUnionBase == null || !IsProductionZoneActive(villainUnionBase.ZoneId))
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
            if (outpost == null || !outpost.IsEnemyClaimed || !IsProductionZoneActive(outpost.ZoneId))
                continue;

            Vector2Int outpostGrid = outpost.GetAnchorGrid(gridManager);
            productionBaseCandidates.Add(new ProductionBaseCandidate(
                outpostGrid,
                GetBottomSpawnCells(outpost.transform, outpostGrid)));
        }
    }

    private bool IsProductionZoneActive(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return true;

        return heroUnionRegistry != null && heroUnionRegistry.TryGetClaimedByZoneId(normalizedZoneId, out _);
    }
    private void SortCandidatesByDistanceToPlayerHeroUnion(Vector2Int playerHeroUnionGrid)
    {
        productionBaseCandidates.Sort((a, b) =>
            GridManager.GridDistance(a.BaseGrid, playerHeroUnionGrid).CompareTo(
                GridManager.GridDistance(b.BaseGrid, playerHeroUnionGrid)));
    }

    private bool TrySpawnFromCandidate(ProductionBaseCandidate candidate, Vector2Int playerHeroUnionGrid)
    {
        if (candidate.SpawnCells == null || candidate.SpawnCells.Count == 0)
            return false;

        int primaryIndex = 0;
        int secondaryIndex = candidate.SpawnCells.Count > 1 ? 1 : -1;

        if (candidate.SpawnCells.Count > 1)
        {
            int firstDistance = GridManager.GridDistance(candidate.SpawnCells[0], playerHeroUnionGrid);
            int secondDistance = GridManager.GridDistance(candidate.SpawnCells[1], playerHeroUnionGrid);
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

    public bool TrySpawnGateThreatEnemy(string zoneId, Vector2Int spawnGrid, out string placementKey)
    {
        placementKey = string.Empty;

        if (enemyPrefab == null || gridManager == null)
            return false;

        string sourceKey = $"gate_threat_{MapProgressKey.NormalizeSegment(zoneId)}";
        placementKey = CreateRuntimeEnemyPlacementKey(sourceKey);
        if (TrySpawnAtGrid(spawnGrid, placementKey))
            return true;

        placementKey = string.Empty;
        return false;
    }

    private bool TrySpawnAtGrid(Vector2Int spawnGrid)
    {
        return TrySpawnAtGrid(spawnGrid, CreateRuntimeEnemyPlacementKey());
    }

    private bool TrySpawnAtGrid(Vector2Int spawnGrid, string placementKey)
    {
        if (!gridManager.CanOccupyCell(spawnGrid, null, true))
            return false;

        EnemyGridMover spawnedEnemy = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity, enemyRoot);
        EnemyIdentity enemyIdentity = spawnedEnemy.GetComponent<EnemyIdentity>();
        if (enemyIdentity != null)
        {
            enemyIdentity.SetPlacementSource(EnemyPlacementSource.Runtime);
            enemyIdentity.SetPlacementKey(placementKey);
        }

        spawnedEnemy.InitializePlacementIdentity(placementKey);
        spawnedEnemy.SnapToGridPosition(spawnGrid);

        EnemyUnitBootstrap enemyBootstrap = spawnedEnemy.GetComponent<EnemyUnitBootstrap>();
        int enemyLevel = ResolveZoneEnemyLevelFromPlacementKey(placementKey);
        if (!TryInitializeRuntimeEnemyGroup(enemyBootstrap, spawnedEnemy, spawnGrid, placementKey, runtimeEnemyGroupIndex, enemyLevel, ExtractZoneIdFromRuntimePlacementKey(placementKey)))
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
        int enemyGroupIndex,
        int enemyLevel = 1,
        string zoneId = "")
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
            enemyGroupIndex.ToString(),
            enemyLevel,
            zoneId);
    }

    private static int ResolveZoneEnemyLevelFromPlacementKey(string placementKey)
    {
        string zoneId = ExtractZoneIdFromRuntimePlacementKey(placementKey);
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.TryGetZoneEnemyLevel(zoneId, out int level)
            ? Mathf.Max(1, level)
            : 1;
    }

    private static string ExtractZoneIdFromRuntimePlacementKey(string placementKey)
    {
        string normalized = MapProgressKey.NormalizeSegment(placementKey);
        const string prefix = "runtime_enemy_gate_threat_";
        if (string.IsNullOrWhiteSpace(normalized) || !normalized.StartsWith(prefix, System.StringComparison.Ordinal))
            return string.Empty;

        string remainder = normalized.Substring(prefix.Length);
        int lastUnderscore = remainder.LastIndexOf('_');
        return lastUnderscore > 0 ? remainder.Substring(0, lastUnderscore) : remainder;
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

        int highestSequence = 0;

        IReadOnlyList<EnemyWorldState> enemyStates = progressRepository.EnemyWorldStates;
        for (int i = 0; i < enemyStates.Count; i++)
        {
            EnemyWorldState state = enemyStates[i];
            if (state == null || string.IsNullOrWhiteSpace(state.PlacementKey))
                continue;

            string placementKey = MapProgressKey.NormalizeSegment(state.PlacementKey);
            const string keyPrefix = "runtime_enemy_";
            if (!placementKey.StartsWith(keyPrefix, System.StringComparison.Ordinal))
                continue;

            int lastUnderscoreIndex = placementKey.LastIndexOf('_');
            if (lastUnderscoreIndex < keyPrefix.Length || lastUnderscoreIndex >= placementKey.Length - 1)
                continue;

            string sequenceText = placementKey.Substring(lastUnderscoreIndex + 1);
            if (int.TryParse(sequenceText, out int sequence) && sequence > highestSequence)
                highestSequence = sequence;
        }

        if (nextRuntimeEnemySequence <= highestSequence)
            nextRuntimeEnemySequence = highestSequence + 1;
    }

    private string CreateRuntimeEnemyPlacementKey()
    {
        return CreateRuntimeEnemyPlacementKey(ResolveRuntimeSpawnSourceKey());
    }

    private string CreateRuntimeEnemyPlacementKey(string sourceKey)
    {
        int sequence = Mathf.Max(1, nextRuntimeEnemySequence);
        nextRuntimeEnemySequence = sequence + 1;
        return MapProgressKey.ForRuntimeEnemy(sourceKey, sequence);
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
            if (enemies[i] != null && !enemies[i].IsStatic)
                activeEnemyCount++;
        }

        return activeEnemyCount;
    }

    private bool TryGetPlayerMainHeroUnion(out HeroUnionUnit playerMainHeroUnion)
    {
        playerMainHeroUnion = null;

        if (heroUnionRegistry == null)
            return false;

        return heroUnionRegistry.TryGetFirstClaimed(out playerMainHeroUnion);
    }

    private void ResolveReferences()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();

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
