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
    [SerializeField] private EnemyGridMover enemyPrefab;
    [FormerlySerializedAs("runtimeEnemyGroupIndex")]
    [SerializeField] private string runtimeEnemyGroupKey = "FEP002";
    [SerializeField, Min(1)] private int nextRuntimeEnemySequence = 1;

    private void OnEnable()
    {
        ResolveReferences();
        RestoreRuntimeEnemies();
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

        if (enemyPrefab == null || gridManager == null)
            return false;

        string sourceKey = $"gate_threat_{MapProgressKey.NormalizeSegment(zoneId)}";
        placementKey = CreateRuntimeEnemyPlacementKey(sourceKey);
        if (TrySpawnAtGrid(spawnGrid, placementKey, ResolveSpawnEnemyGroupKey(enemyGroupKey), MapProgressKey.NormalizeSegment(zoneId), out spawnedEnemy))
            return true;

        placementKey = string.Empty;
        spawnedEnemy = null;
        return false;
    }

    private bool TrySpawnAtGrid(Vector2Int spawnGrid, string placementKey, string enemyGroupKey, string zoneId, out EnemyGridMover spawnedEnemy)
    {
        spawnedEnemy = null;
        if (!TryResolveSpawnGrid(spawnGrid, out Vector2Int resolvedSpawnGrid))
            return false;

        spawnedEnemy = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity, enemyRoot);
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
        int enemyLevel = ResolveZoneEnemyLevelFromPlacementKey(placementKey);
        if (!TryInitializeRuntimeEnemyGroup(enemyBootstrap, spawnedEnemy, resolvedSpawnGrid, placementKey, enemyGroupKey, enemyLevel, zoneId))
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
        string groupKey = ResolveRuntimeEnemyGroupKey(state);
        EnemyIdentity enemyIdentity = restoredEnemy.GetComponent<EnemyIdentity>();
        if (enemyIdentity != null)
        {
            enemyIdentity.SetPlacementSource(EnemyPlacementSource.Runtime);
            enemyIdentity.SetPlacementKey(state.PlacementKey);
            enemyIdentity.SetEnemyId(state.EnemyId);
            enemyIdentity.SetEnemyGroupKey(groupKey);
        }

        restoredEnemy.InitializePlacementIdentity(state.PlacementKey);
        restoredEnemy.InitializePersistentIdentity(state.EnemyId);
        restoredEnemy.SnapToGridPosition(state.Grid);

        EnemyUnitBootstrap enemyBootstrap = restoredEnemy.GetComponent<EnemyUnitBootstrap>();
        if (!TryInitializeRuntimeEnemyGroup(enemyBootstrap, restoredEnemy, state.Grid, state.PlacementKey, groupKey))
        {
            Destroy(restoredEnemy.gameObject);
            return;
        }

        ApplyEventEncounterBinding(
            restoredEnemy,
            state.PlacementKey,
            state.EncounterChatZoneId,
            state.EncounterChatId,
            state.EventBattleKey);
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

        if (!templateCatalog.TryGetEnemyGroupTemplate(enemyGroupKey, out DHEnemyGroupTemplate groupData))
        {
            Debug.LogWarning($"EnemySpawnController could not find an enemy group CSV index '{enemyGroupKey}'.", this);
            return false;
        }

        return enemyBootstrap.InitializeEnemyGroupFromCsv(
            groupData,
            prefabRegistry,
            grid,
            EnemyBehaviorType.Mobile,
            placementKey,
            EnemyPlacementSource.Runtime,
            enemyGroupKey,
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

    private string ResolveSpawnEnemyGroupKey(string requestedEnemyGroupKey)
    {
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
