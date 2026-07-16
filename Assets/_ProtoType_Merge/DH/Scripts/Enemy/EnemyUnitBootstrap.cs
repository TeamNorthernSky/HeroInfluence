using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyGridMover))]
[RequireComponent(typeof(EnemyIdentity))]
[RequireComponent(typeof(EnemyComposition))]
public class EnemyUnitBootstrap : MonoBehaviour
{
    private const int MinCombatSlot = 1;
    private const int MaxCombatSlot = 6;

    private static readonly Vector3[] ExplorationUnitLocalPositions =
    {
        new Vector3(-0.3f, 0f, 0f),
        new Vector3(0f, 0f, 0.3f),
        new Vector3(0f, 0f, -0.3f),
        new Vector3(0.3f, 0f, 0f),
        Vector3.zero
    };

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

    public bool InitializeEnemyGroupFromCsv(
        DHEnemyGroupTemplate groupData,
        LevelPrefabRegistry prefabRegistry,
        Vector2Int initialGrid,
        EnemyBehaviorType behaviorType,
        string placementKey)
    {
        return InitializeEnemyGroupFromCsv(
            groupData,
            prefabRegistry,
            initialGrid,
            behaviorType,
            placementKey,
            EnemyPlacementSource.Scene,
            groupData != null ? groupData.GroupIndex.ToString() : EnemyWorldState.DefaultPrefabKey,
            1,
            string.Empty);
    }

    public bool InitializeEnemyGroupFromCsv(
        DHEnemyGroupTemplate groupData,
        LevelPrefabRegistry prefabRegistry,
        Vector2Int initialGrid,
        EnemyBehaviorType behaviorType,
        string placementKey,
        EnemyPlacementSource placementSource,
        string prefabKey,
        int enemyLevel = 1,
        string zoneId = "")
    {
        if (hasInitialized)
            return true;

        enemyUnit ??= GetComponent<EnemyGridMover>();
        enemyIdentity ??= GetComponent<EnemyIdentity>();
        enemyComposition ??= GetComponent<EnemyComposition>();

        PersistentEnemyRepository enemyRepository = PersistentEnemyRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;
        MapProgressRepository mapProgressRepository = MapProgressRepository.Instance;

        if (groupData == null || prefabRegistry == null || enemyUnit == null || enemyIdentity == null ||
            enemyComposition == null || enemyRepository == null || enemyGroupRepository == null)
            return false;

        enemyUnit.SetBehaviorType(behaviorType);
        enemyIdentity.SetPlacementSource(placementSource);

        placementKey = string.IsNullOrWhiteSpace(placementKey)
            ? MapProgressKey.ForSceneEnemy(initialGrid)
            : placementKey;

        enemyIdentity.SetPlacementKey(placementKey);
        enemyUnit.InitializePlacementIdentity(placementKey);

        if (TryHandleDefeatedEnemy(mapProgressRepository, placementKey))
            return true;

        if (TryRestoreExistingCsvEnemy(
                mapProgressRepository,
                enemyGroupRepository,
                enemyRepository,
                prefabRegistry,
                placementKey,
                initialGrid,
                zoneId))
            return true;

        if (!TryBuildCsvGroupMembers(groupData, out List<CsvEnemyGroupMember> members))
            return false;

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
        {
            Debug.LogWarning("EnemyUnitBootstrap could not find a DHCsvTemplateCatalog in the scene.", this);
            return false;
        }

        if (!ValidateCsvMembers(groupData.GroupIndex, members, templateCatalog, prefabRegistry))
            return false;

        ClearUnitStateChildren();

        List<int> unitIndices = new List<int>(members.Count);
        List<int> unitSlots = new List<int>(members.Count);
        enemyComposition.EnsureSlotCount(members.Count);

        for (int i = 0; i < members.Count; i++)
        {
            CsvEnemyGroupMember member = members[i];
            string templateKey = member.EnemyUnitIndex.ToString();
            templateCatalog.TryGetEnemyTemplate(templateKey, out EnemyData template);
            prefabRegistry.TryGetEnemyUnitPrefab(member.EnemyUnitIndex, out EnemyUnitState unitPrefab);

            EnemyUnitState unitState = Instantiate(unitPrefab, transform);
            unitState.transform.localPosition = GetExplorationUnitLocalPosition(i);
            unitState.transform.localRotation = Quaternion.identity;
            unitState.SetUnitTemplateKey(templateKey);
            unitState.SetLevel(enemyLevel);
            unitState.InitializeFromTemplate(template);

            int unitIndex = enemyRepository.CreateUnit(
                templateKey,
                unitState.Level,
                unitState.BaseStats,
                unitState.IngameStats,
                unitState.CurrentHp);

            unitState.AssignUnitIndex(unitIndex);
            unitStates.Add(unitState);
            unitIndices.Add(unitIndex);
            unitSlots.Add(member.CombatSlot);
            enemyComposition.SetUnitIndexAt(i, unitIndex);
        }

        if (unitIndices.Count == 0)
            return false;

        string enemyId = enemyGroupRepository.CreateEnemy(unitIndices, unitSlots);
        enemyIdentity.SetEnemyId(enemyId);
        enemyUnit.InitializePersistentIdentity(enemyId);
        enemyUnit.SnapToGridPosition(initialGrid);
        mapProgressRepository?.BindEnemy(
            placementKey,
            enemyId,
            initialGrid,
            placementSource,
            string.IsNullOrWhiteSpace(prefabKey) ? groupData.GroupIndex.ToString() : prefabKey,
            zoneId);

        RefreshFogVisibilityBinding();
        hasInitialized = true;
        return true;
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

    private string EnsurePlacementKey(Vector2Int initialGrid,
        string zoneId = "")
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
        Vector2Int initialGrid,
        string zoneId = "")
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

    private bool TryRestoreExistingCsvEnemy(
        MapProgressRepository mapProgressRepository,
        EnemyGroupPersistentRepository enemyGroupRepository,
        PersistentEnemyRepository enemyRepository,
        LevelPrefabRegistry prefabRegistry,
        string placementKey,
        Vector2Int initialGrid,
        string zoneId = "")
    {
        if (mapProgressRepository == null ||
            enemyGroupRepository == null ||
            enemyRepository == null ||
            prefabRegistry == null ||
            string.IsNullOrWhiteSpace(placementKey))
            return false;

        if (!mapProgressRepository.TryGetEnemyState(placementKey, out EnemyWorldState worldState))
            return false;

        if (worldState == null || worldState.Defeated || string.IsNullOrWhiteSpace(worldState.EnemyId))
            return false;

        if (!enemyGroupRepository.TryGetEnemy(worldState.EnemyId, out EnemyPersistentData persistentData) ||
            persistentData == null)
            return false;

        enemyIdentity.SetEnemyId(worldState.EnemyId);
        enemyUnit.InitializePersistentIdentity(worldState.EnemyId);
        ApplyPersistentUnitIndices(persistentData.UnitIndices);
        RebuildCsvUnitStateChildren(persistentData.UnitIndices, enemyRepository, prefabRegistry);

        if (worldState.Grid != initialGrid)
            enemyUnit.SnapToGridPosition(worldState.Grid);
        else
            mapProgressRepository.SetEnemyGrid(placementKey, initialGrid);

        if (!string.IsNullOrWhiteSpace(zoneId))
            mapProgressRepository.SetEnemyZone(placementKey, zoneId);

        RefreshFogVisibilityBinding();
        hasInitialized = true;
        return true;
    }

    private void RebuildCsvUnitStateChildren(
        IReadOnlyList<int> persistentUnitIndices,
        PersistentEnemyRepository enemyRepository,
        LevelPrefabRegistry prefabRegistry)
    {
        ClearUnitStateChildren();

        if (persistentUnitIndices == null)
            return;

        for (int i = 0; i < persistentUnitIndices.Count; i++)
        {
            int unitIndex = persistentUnitIndices[i];
            if (!enemyRepository.TryGetUnit(unitIndex, out EnemyUnitPersistentData persistentUnit) ||
                persistentUnit == null)
            {
                Debug.LogWarning($"EnemyUnitBootstrap could not restore missing enemy unit index '{unitIndex}'.", this);
                continue;
            }

            if (!int.TryParse(persistentUnit.UnitTemplateKey, out int enemyUnitIndex) ||
                !prefabRegistry.TryGetEnemyUnitPrefab(enemyUnitIndex, out EnemyUnitState unitPrefab))
            {
                Debug.LogWarning(
                    $"EnemyUnitBootstrap could not find an enemy unit prefab for restored template '{persistentUnit.UnitTemplateKey}'.",
                    this);
                continue;
            }

            EnemyUnitState unitState = Instantiate(unitPrefab, transform);
            unitState.transform.localPosition = GetExplorationUnitLocalPosition(i);
            unitState.transform.localRotation = Quaternion.identity;
            unitState.ApplyPersistentData(persistentUnit);
            unitStates.Add(unitState);
        }
    }

    private void ClearUnitStateChildren()
    {
        unitStates.Clear();

        EnemyUnitState[] existingStates = GetComponentsInChildren<EnemyUnitState>(true);
        for (int i = existingStates.Length - 1; i >= 0; i--)
        {
            EnemyUnitState unitState = existingStates[i];
            if (unitState == null || unitState.transform == transform)
                continue;

            if (Application.isPlaying)
                Destroy(unitState.gameObject);
            else
                DestroyImmediate(unitState.gameObject);
        }
    }

    private static Vector3 GetExplorationUnitLocalPosition(int index)
    {
        if (index < 0 || index >= ExplorationUnitLocalPositions.Length)
            return Vector3.zero;

        return ExplorationUnitLocalPositions[index];
    }

    private static bool TryBuildCsvGroupMembers(DHEnemyGroupTemplate groupData, out List<CsvEnemyGroupMember> members)
    {
        members = new List<CsvEnemyGroupMember>(5);

        if (groupData == null || groupData.Members == null)
            return false;

        for (int i = 0; i < groupData.Members.Count; i++)
        {
            DHEnemyGroupMember member = groupData.Members[i];
            if (!TryAddCsvMember(members, member.EnemyUnitIndex, member.CombatSlot))
                return false;
        }

        return members.Count > 0;
    }

    private static bool TryAddCsvMember(List<CsvEnemyGroupMember> members, int enemyUnitIndex, int combatSlot)
    {
        if (enemyUnitIndex <= 0)
            return true;

        if (combatSlot < MinCombatSlot || combatSlot > MaxCombatSlot)
            return false;

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].CombatSlot == combatSlot)
                return false;
        }

        members.Add(new CsvEnemyGroupMember(enemyUnitIndex, combatSlot));
        return true;
    }

    private bool ValidateCsvMembers(
        int enemyGroupIndex,
        IReadOnlyList<CsvEnemyGroupMember> members,
        DHCsvTemplateCatalog templateCatalog,
        LevelPrefabRegistry prefabRegistry)
    {
        if (members == null || members.Count == 0)
        {
            Debug.LogWarning($"Enemy group '{enemyGroupIndex}' has no valid enemy units.", this);
            return false;
        }

        for (int i = 0; i < members.Count; i++)
        {
            int enemyUnitIndex = members[i].EnemyUnitIndex;
            string templateKey = enemyUnitIndex.ToString();

            if (!templateCatalog.TryGetEnemyTemplate(templateKey, out _))
            {
                Debug.LogWarning(
                    $"Enemy group '{enemyGroupIndex}' references missing enemy unit CSV index '{enemyUnitIndex}'.",
                    this);
                return false;
            }

            if (!prefabRegistry.TryGetEnemyUnitPrefab(enemyUnitIndex, out _))
            {
                Debug.LogWarning(
                    $"Enemy group '{enemyGroupIndex}' references missing enemy unit prefab index '{enemyUnitIndex}'.",
                    this);
                return false;
            }
        }

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

    private void RefreshFogVisibilityBinding()
    {
        if (enemyUnit == null)
            return;

        EnemyFogVisibilitySystem fogVisibilitySystem = FindFirstObjectByType<EnemyFogVisibilitySystem>();
        fogVisibilitySystem?.RefreshEnemyBinding(enemyUnit);
    }

    private readonly struct CsvEnemyGroupMember
    {
        public CsvEnemyGroupMember(int enemyUnitIndex, int combatSlot)
        {
            EnemyUnitIndex = enemyUnitIndex;
            CombatSlot = combatSlot;
        }

        public int EnemyUnitIndex { get; }
        public int CombatSlot { get; }
    }
}
