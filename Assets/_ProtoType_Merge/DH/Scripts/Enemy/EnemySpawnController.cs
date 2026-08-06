using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemySpawnController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private LevelPrefabRegistry prefabRegistry;
    [SerializeField] private Transform enemyRoot;

    [Header("Spawn Rules")]
    [FormerlySerializedAs("runtimeEnemyGroupIndex")]
    [SerializeField] private string runtimeEnemyGroupKey = "FEP002";
    [SerializeField, Min(1)] private int nextRuntimeEnemySequence = 1;

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeRuntimeLoaders();
    }

    private void OnDisable()
    {
        UnsubscribeRuntimeLoaders();
    }

    public bool TrySpawnGateThreatEnemy(string zoneId, Vector2Int spawnGrid, out string placementKey)
    {
        return TrySpawnGateThreatEnemy(zoneId, spawnGrid, string.Empty, out placementKey);
    }

    public bool TrySpawnGateThreatEnemy(string zoneId, Vector2Int spawnGrid, string enemyGroupKey, out string placementKey)
    {
        return TrySpawnGateThreatEnemy(zoneId, spawnGrid, enemyGroupKey, out placementKey, out _);
    }

    public bool TrySpawnGateThreatEnemy(
        string zoneId,
        Vector2Int spawnGrid,
        string enemyGroupKey,
        out string placementKey,
        out EnemyGridMover spawnedEnemy)
    {
        placementKey = string.Empty;
        spawnedEnemy = null;

        if (ResolveEnemyPrefab(EnemyBehaviorType.Mobile) == null || gridManager == null)
            return false;

        // Gate threats are runtime mobile enemies. Their encounter chat is attached by GateThreatController.
        string sourceKey = $"gate_threat_{MapProgressKey.NormalizeSegment(zoneId)}";
        placementKey = CreateRuntimeEnemyPlacementKey(sourceKey);
        if (TrySpawnAtGrid(spawnGrid, placementKey, ResolveSpawnEnemyGroupKey(enemyGroupKey), MapProgressKey.NormalizeSegment(zoneId), out spawnedEnemy))
            return true;

        placementKey = string.Empty;
        spawnedEnemy = null;
        return false;
    }

    public bool TrySpawnMainEventReplacementEnemy(
        string eventKey,
        string enemyGroupKey,
        Vector2Int spawnGrid,
        out string placementKey,
        out EnemyGridMover spawnedEnemy)
    {
        placementKey = string.Empty;
        spawnedEnemy = null;

        string normalizedEventKey = MapProgressKey.NormalizeSegment(eventKey);
        string normalizedGroupKey = string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEventKey) || string.IsNullOrWhiteSpace(normalizedGroupKey))
            return false;

        // Replacement enemies are static runtime enemies spawned from a completed/disabled main event.
        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null || !templateCatalog.TryGetEnemyGroupTemplate(normalizedGroupKey, out _))
            return false;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        placementKey = MapProgressKey.ForRuntimeEnemy($"main_event_replacement_{normalizedEventKey}", 1);
        if (progressRepository != null && progressRepository.TryGetEnemyState(placementKey, out EnemyWorldState existingState))
        {
            EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;
            if (ShouldRestoreRuntimeEnemy(existingState, enemyGroupRepository) &&
                !HasMatchingEnemyInScene(existingState.PlacementKey, existingState.EnemyId) &&
                TryRestoreRuntimeEnemy(existingState, out spawnedEnemy))
            {
                return true;
            }

            placementKey = string.Empty;
            return false;
        }

        string zoneId = ResolveZoneId(spawnGrid);
        int enemyLevel = ResolveZoneEnemyLevel(zoneId);
        if (TrySpawnAtGrid(spawnGrid, placementKey, normalizedGroupKey, zoneId, enemyLevel, EnemyBehaviorType.Static, out spawnedEnemy))
            return true;

        placementKey = string.Empty;
        spawnedEnemy = null;
        return false;
    }

    private bool TrySpawnAtGrid(Vector2Int spawnGrid, string placementKey, string enemyGroupKey, string zoneId, out EnemyGridMover spawnedEnemy)
    {
        return TrySpawnAtGrid(
            spawnGrid,
            placementKey,
            enemyGroupKey,
            zoneId,
            ResolveZoneEnemyLevelFromPlacementKey(placementKey),
            EnemyBehaviorType.Mobile,
            out spawnedEnemy);
    }

    private bool TrySpawnAtGrid(
        Vector2Int spawnGrid,
        string placementKey,
        string enemyGroupKey,
        string zoneId,
        int enemyLevel,
        EnemyBehaviorType behaviorType,
        out EnemyGridMover spawnedEnemy)
    {
        spawnedEnemy = null;
        if (!TryResolveSpawnGrid(spawnGrid, out Vector2Int resolvedSpawnGrid))
            return false;

        EnemyGridMover spawnPrefab = ResolveEnemyPrefab(behaviorType);
        if (spawnPrefab == null)
            return false;

        spawnedEnemy = Instantiate(spawnPrefab, Vector3.zero, Quaternion.identity, enemyRoot);
        EnemyIdentity enemyIdentity = spawnedEnemy.GetComponent<EnemyIdentity>();
        if (enemyIdentity != null)
        {
            enemyIdentity.SetPlacementSource(EnemyPlacementSource.Runtime);
            enemyIdentity.SetPlacementKey(placementKey);
            enemyIdentity.SetEnemyGroupKey(enemyGroupKey);
        }

        spawnedEnemy.InitializePlacementIdentity(placementKey);
        spawnedEnemy.SnapToGridPosition(resolvedSpawnGrid);

        EnemyUnitBootstrap enemyBootstrap = spawnedEnemy.GetComponent<EnemyUnitBootstrap>();
        if (!TryInitializeRuntimeEnemyGroup(enemyBootstrap, spawnedEnemy, resolvedSpawnGrid, placementKey, enemyGroupKey, enemyLevel, zoneId, behaviorType))
        {
            Destroy(spawnedEnemy.gameObject);
            spawnedEnemy = null;
            return false;
        }

        return true;
    }

    private bool TryResolveSpawnGrid(Vector2Int preferredGrid, out Vector2Int resolvedGrid)
    {
        resolvedGrid = preferredGrid;
        if (gridManager == null)
            return false;

        if (gridManager.CanOccupyCell(preferredGrid, null, true))
            return true;

        for (int i = 0; i < GridManager.Directions8.Length; i++)
        {
            Vector2Int candidate = preferredGrid + GridManager.Directions8[i];
            if (!gridManager.CanOccupyCell(candidate, null, true))
                continue;

            resolvedGrid = candidate;
            return true;
        }

        return false;
    }

    private void RestoreRuntimeEnemies()
    {
        if (!HasAnyRuntimeEnemyPrefab())
            return;

        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;
        if (progressRepository == null || enemyGroupRepository == null)
            return;

        SyncRuntimeEnemySequence(progressRepository);

        // Runtime enemies are recreated from MapProgress enemy states after level/layout loading.
        IReadOnlyList<EnemyWorldState> enemyStates = progressRepository.EnemyWorldStates;
        for (int i = 0; i < enemyStates.Count; i++)
        {
            EnemyWorldState state = enemyStates[i];
            if (!ShouldRestoreRuntimeEnemy(state, enemyGroupRepository))
                continue;

            if (HasMatchingEnemyInScene(state.PlacementKey, state.EnemyId))
                continue;

            TryRestoreRuntimeEnemy(state, out _);
        }

        enemyRegistry?.RefreshSceneEnemies();
    }

    private void SubscribeRuntimeLoaders()
    {
        LevelLoader.RuntimeLevelLoaded -= HandleRuntimeLevelLoaded;
        LevelLoader.RuntimeLevelLoaded += HandleRuntimeLevelLoaded;
        LevelZoneLayoutLoader.RuntimeLayoutLoaded -= HandleRuntimeLayoutLoaded;
        LevelZoneLayoutLoader.RuntimeLayoutLoaded += HandleRuntimeLayoutLoaded;
    }

    private void UnsubscribeRuntimeLoaders()
    {
        LevelLoader.RuntimeLevelLoaded -= HandleRuntimeLevelLoaded;
        LevelZoneLayoutLoader.RuntimeLayoutLoaded -= HandleRuntimeLayoutLoaded;
    }

    private void HandleRuntimeLevelLoaded(LevelLoader loader)
    {
        RestoreRuntimeEnemiesAfterLevelLoaded();
    }

    private void HandleRuntimeLayoutLoaded(LevelZoneLayoutLoader loader)
    {
        RestoreRuntimeEnemiesAfterLevelLoaded();
    }

    private void RestoreRuntimeEnemiesAfterLevelLoaded()
    {
        ResolveReferences();
        RestoreRuntimeEnemies();
    }

    private bool ShouldRestoreRuntimeEnemy(EnemyWorldState state, EnemyGroupPersistentRepository enemyGroupRepository)
    {
        if (state == null ||
            state.Defeated ||
            string.IsNullOrWhiteSpace(state.PlacementKey) ||
            string.IsNullOrWhiteSpace(state.EnemyId))
            return false;

        if (!IsRuntimeEnemyState(state))
            return false;

        if (enemyGroupRepository != null && enemyGroupRepository.ContainsEnemy(state.EnemyId))
            return true;

        // Some runtime enemies are template-only and do not have a persistent enemy group entry.
        return CanRecreateRuntimeEnemyFromTemplate(state);
    }

    private bool CanRecreateRuntimeEnemyFromTemplate(EnemyWorldState state)
    {
        string groupKey = ResolveRuntimeEnemyGroupKey(state);
        if (string.IsNullOrWhiteSpace(groupKey))
            return false;

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        return templateCatalog != null && templateCatalog.TryGetEnemyGroupTemplate(groupKey, out _);
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

    private bool TryRestoreRuntimeEnemy(EnemyWorldState state, out EnemyGridMover restoredEnemy)
    {
        restoredEnemy = null;
        if (state == null)
            return false;

        EnemyBehaviorType behaviorType = state.BehaviorType;
        EnemyGridMover instance = Instantiate(ResolveEnemyPrefab(behaviorType), Vector3.zero, Quaternion.identity, enemyRoot);
        string groupKey = ResolveRuntimeEnemyGroupKey(state);
        EnemyIdentity enemyIdentity = instance.GetComponent<EnemyIdentity>();
        if (enemyIdentity != null)
        {
            enemyIdentity.SetPlacementSource(EnemyPlacementSource.Runtime);
            enemyIdentity.SetPlacementKey(state.PlacementKey);
            enemyIdentity.SetEnemyId(state.EnemyId);
            enemyIdentity.SetEnemyGroupKey(groupKey);
        }

        instance.InitializePlacementIdentity(state.PlacementKey);
        instance.InitializePersistentIdentity(state.EnemyId);
        instance.SnapToGridPosition(state.Grid);

        EnemyUnitBootstrap enemyBootstrap = instance.GetComponent<EnemyUnitBootstrap>();
        if (!TryInitializeRuntimeEnemyGroup(enemyBootstrap, instance, state.Grid, state.PlacementKey, groupKey, 1, state.ZoneId, behaviorType))
        {
            Destroy(instance.gameObject);
            return false;
        }

        ApplyEventEncounterBinding(
            instance,
            state.PlacementKey,
            state.EncounterChatZoneId,
            state.EncounterChatId,
            state.EventBattleKey);

        restoredEnemy = instance;
        return true;
    }

    public static void ApplyEventEncounterBinding(
        EnemyGridMover enemy,
        string placementKey,
        int encounterChatZoneId,
        int encounterChatId,
        string eventBattleKey)
    {
        if (enemy == null || encounterChatZoneId <= 0 || encounterChatId <= 0)
            return;

        EnemyEventEncounterBinding binding = enemy.GetComponent<EnemyEventEncounterBinding>();
        if (binding == null)
            binding = enemy.gameObject.AddComponent<EnemyEventEncounterBinding>();

        binding.Initialize(encounterChatZoneId, encounterChatId, eventBattleKey, placementKey);
    }

    private bool TryInitializeRuntimeEnemyGroup(
        EnemyUnitBootstrap enemyBootstrap,
        EnemyGridMover enemy,
        Vector2Int grid,
        string placementKey,
        string enemyGroupKey,
        int enemyLevel = 1,
        string zoneId = "",
        EnemyBehaviorType behaviorType = EnemyBehaviorType.Mobile)
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

        if (!templateCatalog.TryGetEnemyGroupTemplate(enemyGroupKey, out DHEnemyGroupTemplate groupData))
        {
            Debug.LogWarning($"EnemySpawnController could not find an enemy group CSV index '{enemyGroupKey}'.", this);
            return false;
        }

        return enemyBootstrap.InitializeEnemyGroupFromCsv(
            groupData,
            prefabRegistry,
            grid,
            behaviorType,
            placementKey,
            EnemyPlacementSource.Runtime,
            enemyGroupKey,
            enemyLevel,
            zoneId);
    }

    private static int ResolveZoneEnemyLevelFromPlacementKey(string placementKey)
    {
        string zoneId = ExtractZoneIdFromRuntimePlacementKey(placementKey);
        return ResolveZoneEnemyLevel(zoneId);
    }

    private static int ResolveZoneEnemyLevel(string zoneId)
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.TryGetZoneEnemyLevel(zoneId, out int level)
            ? Mathf.Max(1, level)
            : 1;
    }

    private static string ResolveZoneId(Vector2Int grid)
    {
        LevelZoneLayoutLoader layoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
        if (layoutLoader == null || layoutLoader.LoadedZones == null)
            return string.Empty;

        IReadOnlyList<LoadedLevelZoneData> zones = layoutLoader.LoadedZones;
        for (int i = 0; i < zones.Count; i++)
        {
            LoadedLevelZoneData zone = zones[i];
            RectInt bounds = new RectInt(zone.Anchor, zone.Size);
            if (!bounds.Contains(grid))
                continue;

            return MapProgressKey.NormalizeSegment(zone.ZoneId);
        }

        return string.Empty;
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

    private string ResolveSpawnEnemyGroupKey(string requestedEnemyGroupKey)
    {
        // Spawn-point chat data is independent; this fallback only decides which enemy group appears.
        string fallbackKey = string.IsNullOrWhiteSpace(runtimeEnemyGroupKey) ? "FEP002" : runtimeEnemyGroupKey.Trim();
        string normalizedRequest = string.IsNullOrWhiteSpace(requestedEnemyGroupKey) ? string.Empty : requestedEnemyGroupKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRequest))
            return fallbackKey;

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
            return fallbackKey;

        return templateCatalog.TryGetEnemyGroupTemplate(normalizedRequest, out _)
            ? normalizedRequest
            : fallbackKey;
    }

    private string ResolveRuntimeEnemyGroupKey(EnemyWorldState state)
    {
        if (state != null &&
            !string.IsNullOrWhiteSpace(state.PrefabKey) &&
            !string.Equals(state.PrefabKey, EnemyWorldState.DefaultPrefabKey, System.StringComparison.Ordinal))
            return state.PrefabKey.Trim();

        return string.IsNullOrWhiteSpace(runtimeEnemyGroupKey) ? "FEP002" : runtimeEnemyGroupKey.Trim();
    }

    private EnemyGridMover ResolveEnemyPrefab(EnemyBehaviorType behaviorType)
    {
        if (prefabRegistry != null &&
            prefabRegistry.TryGetRuntimeEnemyGroupPrefab(behaviorType, out EnemyGridMover catalogPrefab))
            return catalogPrefab;

        return null;
    }

    private bool HasAnyRuntimeEnemyPrefab()
    {
        return ResolveEnemyPrefab(EnemyBehaviorType.Mobile) != null ||
            ResolveEnemyPrefab(EnemyBehaviorType.Static) != null;
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

    private string CreateRuntimeEnemyPlacementKey(string sourceKey)
    {
        int sequence = Mathf.Max(1, nextRuntimeEnemySequence);
        nextRuntimeEnemySequence = sequence + 1;
        return MapProgressKey.ForRuntimeEnemy(sourceKey, sequence);
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (prefabRegistry == null)
            prefabRegistry = FindFirstObjectByType<LevelPrefabRegistry>();
    }
}
