using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyGridMover))]
[RequireComponent(typeof(EnemyIdentity))]
[RequireComponent(typeof(EnemyComposition))]
public class EnemyUnitBootstrap : MonoBehaviour
{
    [Header("Bootstrap")]
    [SerializeField] private bool populateOnStart = true;
    [SerializeField] private List<EnemyUnitState> unitStates = new List<EnemyUnitState>();
    [SerializeField] private bool onlyWhenUninitialized = true;

    private EnemyGridMover enemyUnit;
    private EnemyIdentity enemyIdentity;
    private EnemyComposition enemyComposition;
    private bool hasInitialized;

    private void Awake()
    {
        enemyUnit = GetComponent<EnemyGridMover>();
        enemyIdentity = GetComponent<EnemyIdentity>();
        enemyComposition = GetComponent<EnemyComposition>();
    }

    private void Start()
    {
        if (!Application.isPlaying || !populateOnStart)
            return;

        InitializeEnemyUnits();
    }

    [ContextMenu("Initialize Enemy Units")]
    public void InitializeEnemyUnits()
    {
        if (hasInitialized)
            return;

        enemyUnit ??= GetComponent<EnemyGridMover>();
        enemyIdentity ??= GetComponent<EnemyIdentity>();
        enemyComposition ??= GetComponent<EnemyComposition>();
        PersistentEnemyRepository enemyRepository = PersistentEnemyRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;

        if (enemyUnit == null || enemyIdentity == null || enemyComposition == null || enemyRepository == null || enemyGroupRepository == null)
            return;

        MapProgressRepository mapProgressRepository = MapProgressRepository.Instance;
        Vector2Int initialGrid = enemyUnit.GetCurrentGrid();
        string placementKey = EnsurePlacementKey(initialGrid);
        if (TryHandleDefeatedEnemy(mapProgressRepository, placementKey))
            return;

        if (TryRestoreExistingEnemy(mapProgressRepository, enemyGroupRepository, placementKey, initialGrid))
            return;

        if (!HasConfiguredUnitStates())
            CollectUnitStatesFromChildren();

        if (!HasConfiguredUnitStates())
            return;

        if (onlyWhenUninitialized && !string.IsNullOrWhiteSpace(enemyIdentity.EnemyId) && !AreAllSlotsEmpty())
        {
            BindEnemyProgress(mapProgressRepository, placementKey, enemyIdentity.EnemyId);
            hasInitialized = true;
            return;
        }

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
        {
            Debug.LogWarning("EnemyUnitBootstrap could not find a DHCsvTemplateCatalog in the scene.", this);
            return;
        }

        List<int> unitIndices = new List<int>(unitStates.Count);
        enemyComposition.EnsureSlotCount(unitStates.Count);
        for (int i = 0; i < unitStates.Count; i++)
        {
            EnemyUnitState unitState = unitStates[i];
            if (unitState == null)
                continue;

            if (string.IsNullOrWhiteSpace(unitState.UnitTemplateKey))
            {
                Debug.LogWarning($"Enemy unit state on '{unitState.name}' is missing a unitTemplateKey.", unitState);
                continue;
            }

            if (!templateCatalog.TryGetEnemyTemplate(unitState.UnitTemplateKey, out EnemyData template))
            {
                Debug.LogWarning($"Enemy unit state on '{unitState.name}' could not resolve CSV template '{unitState.UnitTemplateKey}'.", unitState);
                continue;
            }

            unitState.InitializeFromTemplate(template);
            int unitIndex = enemyRepository.CreateUnit(
                unitState.UnitTemplateKey,
                unitState.Level,
                unitState.BaseStats,
                unitState.IngameStats,
                unitState.CurrentHp);
            unitIndices.Add(unitIndex);
            unitState.AssignUnitIndex(unitIndex);
            enemyComposition.SetUnitIndexAt(i, unitIndex);
        }

        if (unitIndices.Count == 0)
            return;

        string enemyId = enemyGroupRepository.CreateEnemy(unitIndices);
        enemyIdentity.SetEnemyId(enemyId);
        enemyUnit.InitializePersistentIdentity(enemyId);
        BindEnemyProgress(mapProgressRepository, placementKey, enemyId);
        hasInitialized = true;
    }

    [ContextMenu("Collect Unit States From Children")]
    public void CollectUnitStatesFromChildren()
    {
        unitStates.Clear();
        unitStates.AddRange(GetComponentsInChildren<EnemyUnitState>(true));
    }

    private bool HasConfiguredUnitStates()
    {
        for (int i = 0; i < unitStates.Count; i++)
        {
            if (unitStates[i] != null)
                return true;
        }

        return false;
    }

    private bool AreAllSlotsEmpty()
    {
        if (enemyComposition == null)
            return true;

        int[] unitIndices = enemyComposition.UnitIndices;
        for (int i = 0; i < unitIndices.Length; i++)
        {
            if (unitIndices[i] > 0)
                return false;
        }

        return true;
    }

    private string EnsurePlacementKey(Vector2Int initialGrid)
    {
        if (!string.IsNullOrWhiteSpace(enemyIdentity.PlacementKey))
            return enemyIdentity.PlacementKey;

        if (enemyIdentity.PlacementSource == EnemyPlacementSource.Runtime)
        {
            Debug.LogWarning(
                "Runtime enemy placementKey is empty. Runtime-spawned enemies need a spawner-assigned placementKey; using a scene-style fallback for now.",
                this);
        }

        string placementKey = MapProgressKey.ForSceneEnemy(initialGrid);

        enemyIdentity.SetPlacementKey(placementKey);
        enemyUnit.InitializePlacementIdentity(placementKey);
        return placementKey;
    }

    private void BindEnemyProgress(MapProgressRepository mapProgressRepository, string placementKey, string enemyId)
    {
        mapProgressRepository?.BindEnemy(
            placementKey,
            enemyId,
            enemyUnit.GetCurrentGrid(),
            enemyIdentity.PlacementSource,
            EnemyWorldState.DefaultPrefabKey);
    }

    private bool TryRestoreExistingEnemy(
        MapProgressRepository mapProgressRepository,
        EnemyGroupPersistentRepository enemyGroupRepository,
        string placementKey,
        Vector2Int initialGrid)
    {
        if (mapProgressRepository == null ||
            enemyGroupRepository == null ||
            string.IsNullOrWhiteSpace(placementKey))
            return false;

        if (!mapProgressRepository.TryGetEnemyState(placementKey, out EnemyWorldState worldState))
            return false;

        if (string.IsNullOrWhiteSpace(worldState.EnemyId))
            return false;

        if (!enemyGroupRepository.TryGetEnemy(worldState.EnemyId, out EnemyPersistentData persistentData) ||
            persistentData == null)
            return false;

        enemyIdentity.SetEnemyId(worldState.EnemyId);
        enemyUnit.InitializePersistentIdentity(worldState.EnemyId);
        ApplyPersistentUnitIndices(persistentData.UnitIndices);

        if (worldState.Grid != initialGrid)
            enemyUnit.SnapToGridPosition(worldState.Grid);
        else
            mapProgressRepository.SetEnemyGrid(placementKey, initialGrid);

        hasInitialized = true;
        return true;
    }

    private bool TryHandleDefeatedEnemy(MapProgressRepository mapProgressRepository, string placementKey)
    {
        if (mapProgressRepository == null ||
            string.IsNullOrWhiteSpace(placementKey) ||
            !mapProgressRepository.TryGetEnemyState(placementKey, out EnemyWorldState worldState) ||
            worldState == null ||
            !worldState.Defeated)
            return false;

        hasInitialized = true;
        Destroy(gameObject);
        return true;
    }

    private void ApplyPersistentUnitIndices(IReadOnlyList<int> unitIndices)
    {
        if (unitIndices == null)
            return;

        enemyComposition.EnsureSlotCount(unitIndices.Count);
        for (int i = 0; i < unitIndices.Count; i++)
            enemyComposition.SetUnitIndexAt(i, unitIndices[i]);
    }
}
