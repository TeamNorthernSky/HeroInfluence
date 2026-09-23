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

        if (enemyUnit == null || enemyIdentity == null || enemyComposition == null)
            return;

        MapProgressRepository mapProgressRepository = MapProgressRepository.Instance;
        Vector2Int initialGrid = enemyUnit.GetCurrentGrid();
        string placementKey = EnsurePlacementKey(initialGrid);
        if (TryHandleDefeatedEnemy(mapProgressRepository, placementKey))
            return;

        if (TryRestoreExistingEnemy(mapProgressRepository, placementKey, initialGrid))
            return;

        if (!HasConfiguredUnitStates())
            CollectUnitStatesFromChildren();

        if (!HasConfiguredUnitStates())
            return;

        if (onlyWhenUninitialized && !string.IsNullOrWhiteSpace(enemyIdentity.EnemyId) && !AreAllSlotsEmpty())
        {
            string resolvedEnemyId = ResolveEnemyId(mapProgressRepository, placementKey);
            enemyIdentity.SetEnemyId(resolvedEnemyId);
            enemyUnit.InitializePersistentIdentity(resolvedEnemyId);
            BindEnemyProgress(mapProgressRepository, placementKey, resolvedEnemyId);
            hasInitialized = true;
            return;
        }

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null)
        {
            Debug.LogWarning("EnemyUnitBootstrap could not find a DHCsvTemplateCatalog in the scene.", this);
            return;
        }

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

            if (!templateCatalog.TryGetEnemyUnitTemplate(unitState.UnitTemplateKey, out DHEnemyUnitTemplate template))
            {
                Debug.LogWarning($"Enemy unit state on '{unitState.name}' could not resolve CSV template '{unitState.UnitTemplateKey}'.", unitState);
                continue;
            }

            unitState.InitializeFromTemplate(template);
            enemyComposition.SetUnitIndexAt(i, -1);
        }

        string enemyId = ResolveEnemyId(mapProgressRepository, placementKey);
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
            groupData != null ? groupData.GroupKey : EnemyWorldState.DefaultPrefabKey,
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

        MapProgressRepository mapProgressRepository = MapProgressRepository.Instance;

        if (groupData == null || prefabRegistry == null || enemyUnit == null || enemyIdentity == null ||
            enemyComposition == null)
            return false;

        string resolvedGroupKey = string.IsNullOrWhiteSpace(prefabKey) ? groupData.GroupKey : prefabKey.Trim();
        enemyUnit.SetBehaviorType(behaviorType);
        enemyIdentity.SetPlacementSource(placementSource);
        enemyIdentity.SetEnemyGroupKey(resolvedGroupKey);

        placementKey = string.IsNullOrWhiteSpace(placementKey)
            ? MapProgressKey.ForSceneEnemy(initialGrid)
            : placementKey;

        enemyIdentity.SetPlacementKey(placementKey);
        enemyUnit.InitializePlacementIdentity(placementKey);

        if (TryHandleDefeatedEnemy(mapProgressRepository, placementKey))
            return true;

        if (TryRestoreExistingCsvEnemy(
                mapProgressRepository,
                prefabRegistry,
                groupData,
                placementKey,
                initialGrid,
                enemyLevel,
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

        if (!ValidateCsvMembers(groupData.GroupKey, members, templateCatalog, prefabRegistry))
            return false;

        ClearUnitStateChildren();

        enemyComposition.EnsureSlotCount(members.Count);

        for (int i = 0; i < members.Count; i++)
        {
            CsvEnemyGroupMember member = members[i];
            string templateKey = member.EnemyUnitIndex.ToString();
            templateCatalog.TryGetEnemyUnitTemplate(templateKey, out DHEnemyUnitTemplate template);
            prefabRegistry.TryGetEnemyUnitPrefab(member.EnemyUnitIndex, out EnemyUnitState unitPrefab);

            EnemyUnitState unitState = Instantiate(unitPrefab, transform);
            unitState.transform.localPosition = GetExplorationUnitLocalPosition(i);
            unitState.transform.localRotation = Quaternion.identity;
            unitState.SetUnitTemplateKey(templateKey);
            unitState.SetLevel(enemyLevel);
            unitState.InitializeFromTemplate(template);

            unitStates.Add(unitState);
            enemyComposition.SetUnitIndexAt(i, -1);
        }

        if (unitStates.Count == 0)
            return false;

        string enemyId = ResolveEnemyId(mapProgressRepository, placementKey);
        enemyIdentity.SetEnemyId(enemyId);
        enemyUnit.InitializePersistentIdentity(enemyId);
        enemyUnit.SnapToGridPosition(initialGrid);
        mapProgressRepository?.BindEnemy(
            placementKey,
            enemyId,
            initialGrid,
            placementSource,
            resolvedGroupKey,
            zoneId,
            behaviorType);

        RefreshFogVisibilityBinding();
        hasInitialized = true;
        return true;
    }

    public bool InitializeEnemyGroupFromEventBattle(
        DHEventBattleGroupTemplate groupData,
        EventScriptCatalog eventCatalog,
        LevelPrefabRegistry prefabRegistry,
        Vector2Int initialGrid,
        EnemyBehaviorType behaviorType,
        string placementKey,
        EnemyPlacementSource placementSource,
        string prefabKey,
        int zoneId,
        int enemyLevel = 1,
        string zoneKey = "")
    {
        if (hasInitialized)
            return true;

        enemyUnit ??= GetComponent<EnemyGridMover>();
        enemyIdentity ??= GetComponent<EnemyIdentity>();
        enemyComposition ??= GetComponent<EnemyComposition>();

        MapProgressRepository mapProgressRepository = MapProgressRepository.Instance;

        if (groupData == null || eventCatalog == null || prefabRegistry == null || enemyUnit == null ||
            enemyIdentity == null || enemyComposition == null || zoneId <= 0)
            return false;

        string resolvedGroupKey = string.IsNullOrWhiteSpace(prefabKey) ? groupData.BattleKey : prefabKey.Trim();
        enemyUnit.SetBehaviorType(behaviorType);
        enemyIdentity.SetPlacementSource(placementSource);
        enemyIdentity.SetEnemyGroupKey(resolvedGroupKey);

        placementKey = string.IsNullOrWhiteSpace(placementKey)
            ? MapProgressKey.ForSceneEnemy(initialGrid)
            : placementKey;

        enemyIdentity.SetPlacementKey(placementKey);
        enemyUnit.InitializePlacementIdentity(placementKey);

        if (TryHandleDefeatedEnemy(mapProgressRepository, placementKey))
            return true;

        if (TryRestoreExistingEventBattleEnemy(
                mapProgressRepository,
                prefabRegistry,
                eventCatalog,
                groupData,
                placementKey,
                initialGrid,
                zoneId,
                enemyLevel,
                zoneKey))
            return true;

        int resolvedLevel = ResolveEventBattleEnemyLevel(groupData, enemyLevel);
        if (!RebuildEventBattleUnitStateChildren(groupData, eventCatalog, prefabRegistry, zoneId, resolvedLevel))
            return false;

        string enemyId = ResolveEnemyId(mapProgressRepository, placementKey);
        enemyIdentity.SetEnemyId(enemyId);
        enemyUnit.InitializePersistentIdentity(enemyId);
        enemyUnit.SnapToGridPosition(initialGrid);
        mapProgressRepository?.BindEnemy(
            placementKey,
            enemyId,
            initialGrid,
            placementSource,
            resolvedGroupKey,
            zoneKey,
            behaviorType);

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

    private string ResolveEnemyId(MapProgressRepository mapProgressRepository, string placementKey)
    {
        if (mapProgressRepository != null &&
            mapProgressRepository.TryGetEnemyState(placementKey, out EnemyWorldState worldState) &&
            worldState != null &&
            !string.IsNullOrWhiteSpace(worldState.EnemyId))
        {
            return worldState.EnemyId;
        }

        return string.IsNullOrWhiteSpace(placementKey) ? gameObject.name : placementKey;
    }

    private bool TryRestoreExistingEnemy(
        MapProgressRepository mapProgressRepository,
        string placementKey,
        Vector2Int initialGrid,
        string zoneId = "")
    {
        if (mapProgressRepository == null ||
            string.IsNullOrWhiteSpace(placementKey))
            return false;

        if (!mapProgressRepository.TryGetEnemyState(placementKey, out EnemyWorldState worldState))
            return false;

        if (string.IsNullOrWhiteSpace(worldState.EnemyId))
            return false;

        enemyIdentity.SetEnemyId(worldState.EnemyId);
        enemyIdentity.SetEnemyGroupKey(worldState.PrefabKey);
        enemyUnit.InitializePersistentIdentity(worldState.EnemyId);

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
        LevelPrefabRegistry prefabRegistry,
        DHEnemyGroupTemplate fallbackGroupData,
        string placementKey,
        Vector2Int initialGrid,
        int enemyLevel,
        string zoneId = "")
    {
        if (mapProgressRepository == null ||
            prefabRegistry == null ||
            string.IsNullOrWhiteSpace(placementKey))
            return false;

        if (!mapProgressRepository.TryGetEnemyState(placementKey, out EnemyWorldState worldState))
            return false;

        if (worldState == null || worldState.Defeated || string.IsNullOrWhiteSpace(worldState.EnemyId))
            return false;

        enemyIdentity.SetEnemyId(worldState.EnemyId);
        enemyIdentity.SetEnemyGroupKey(worldState.PrefabKey);
        enemyUnit.InitializePersistentIdentity(worldState.EnemyId);

        DHEnemyGroupTemplate groupData = fallbackGroupData;
        string groupKey = !string.IsNullOrWhiteSpace(worldState.PrefabKey) &&
            !string.Equals(worldState.PrefabKey, EnemyWorldState.DefaultPrefabKey, System.StringComparison.Ordinal)
                ? worldState.PrefabKey
                : fallbackGroupData != null ? fallbackGroupData.GroupKey : string.Empty;
        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (!string.IsNullOrWhiteSpace(groupKey) &&
            templateCatalog != null &&
            templateCatalog.TryGetEnemyGroupTemplate(groupKey, out DHEnemyGroupTemplate restoredGroupData))
        {
            groupData = restoredGroupData;
        }

        int restoredLevel = ResolveEnemyLevelForRestore(mapProgressRepository, worldState, enemyLevel);
        if (!RebuildCsvUnitStateChildren(groupData, prefabRegistry, restoredLevel))
            return false;

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

    private bool TryRestoreExistingEventBattleEnemy(
        MapProgressRepository mapProgressRepository,
        LevelPrefabRegistry prefabRegistry,
        EventScriptCatalog eventCatalog,
        DHEventBattleGroupTemplate fallbackGroupData,
        string placementKey,
        Vector2Int initialGrid,
        int zoneId,
        int enemyLevel,
        string zoneKey = "")
    {
        if (mapProgressRepository == null ||
            prefabRegistry == null ||
            eventCatalog == null ||
            string.IsNullOrWhiteSpace(placementKey))
            return false;

        if (!mapProgressRepository.TryGetEnemyState(placementKey, out EnemyWorldState worldState))
            return false;

        if (worldState == null || worldState.Defeated || string.IsNullOrWhiteSpace(worldState.EnemyId))
            return false;

        enemyIdentity.SetEnemyId(worldState.EnemyId);
        enemyIdentity.SetEnemyGroupKey(worldState.PrefabKey);
        enemyUnit.InitializePersistentIdentity(worldState.EnemyId);

        DHEventBattleGroupTemplate groupData = fallbackGroupData;
        string groupKey = !string.IsNullOrWhiteSpace(worldState.PrefabKey) &&
            !string.Equals(worldState.PrefabKey, EnemyWorldState.DefaultPrefabKey, System.StringComparison.Ordinal)
                ? worldState.PrefabKey
                : fallbackGroupData != null ? fallbackGroupData.BattleKey : string.Empty;
        if (!string.IsNullOrWhiteSpace(groupKey) &&
            eventCatalog.TryGetBattleEnemyGroupTemplate(zoneId, groupKey, out DHEventBattleGroupTemplate restoredGroupData))
        {
            groupData = restoredGroupData;
        }

        int restoredLevel = ResolveEnemyLevelForRestore(mapProgressRepository, worldState, enemyLevel);
        restoredLevel = ResolveEventBattleEnemyLevel(groupData, restoredLevel);
        if (!RebuildEventBattleUnitStateChildren(groupData, eventCatalog, prefabRegistry, zoneId, restoredLevel))
            return false;

        if (worldState.Grid != initialGrid)
            enemyUnit.SnapToGridPosition(worldState.Grid);
        else
            mapProgressRepository.SetEnemyGrid(placementKey, initialGrid);

        if (!string.IsNullOrWhiteSpace(zoneKey))
            mapProgressRepository.SetEnemyZone(placementKey, zoneKey);

        RefreshFogVisibilityBinding();
        hasInitialized = true;
        return true;
    }

    private bool RebuildCsvUnitStateChildren(
        DHEnemyGroupTemplate groupData,
        LevelPrefabRegistry prefabRegistry,
        int enemyLevel)
    {
        ClearUnitStateChildren();

        if (!TryBuildCsvGroupMembers(groupData, out List<CsvEnemyGroupMember> members))
            return false;

        DHCsvTemplateCatalog templateCatalog = DHCsvTemplateCatalog.Instance;
        if (templateCatalog == null || !ValidateCsvMembers(groupData.GroupKey, members, templateCatalog, prefabRegistry))
            return false;

        enemyComposition.EnsureSlotCount(members.Count);
        for (int i = 0; i < members.Count; i++)
        {
            CsvEnemyGroupMember member = members[i];
            string templateKey = member.EnemyUnitIndex.ToString();
            templateCatalog.TryGetEnemyUnitTemplate(templateKey, out DHEnemyUnitTemplate template);
            prefabRegistry.TryGetEnemyUnitPrefab(member.EnemyUnitIndex, out EnemyUnitState unitPrefab);

            EnemyUnitState unitState = Instantiate(unitPrefab, transform);
            unitState.transform.localPosition = GetExplorationUnitLocalPosition(i);
            unitState.transform.localRotation = Quaternion.identity;
            unitState.SetUnitTemplateKey(templateKey);
            unitState.SetLevel(enemyLevel);
            unitState.InitializeFromTemplate(template);
            unitStates.Add(unitState);
            enemyComposition.SetUnitIndexAt(i, -1);
        }

        return unitStates.Count > 0;
    }

    private bool RebuildEventBattleUnitStateChildren(
        DHEventBattleGroupTemplate groupData,
        EventScriptCatalog eventCatalog,
        LevelPrefabRegistry prefabRegistry,
        int zoneId,
        int enemyLevel)
    {
        ClearUnitStateChildren();

        if (!TryBuildEventBattleGroupMembers(groupData, out List<EventBattleEnemyGroupMember> members))
            return false;

        if (!ValidateEventBattleMembers(groupData.BattleKey, members, eventCatalog, prefabRegistry, zoneId))
            return false;

        enemyComposition.EnsureSlotCount(members.Count);
        for (int i = 0; i < members.Count; i++)
        {
            EventBattleEnemyGroupMember member = members[i];
            eventCatalog.TryGetBattleEnemyUnitTemplate(zoneId, member.UnitKey, out DHEventBattleUnitTemplate template);
            prefabRegistry.TryGetEnemyUnitPrefab(member.NumericUnitKey, out EnemyUnitState unitPrefab);

            EnemyUnitState unitState = Instantiate(unitPrefab, transform);
            unitState.transform.localPosition = GetExplorationUnitLocalPosition(i);
            unitState.transform.localRotation = Quaternion.identity;
            unitState.SetUnitTemplateKey(member.UnitKey);
            unitState.SetLevel(enemyLevel);
            unitState.InitializeFromTemplate(template);
            unitStates.Add(unitState);
            enemyComposition.SetUnitIndexAt(i, -1);
        }

        return unitStates.Count > 0;
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

    private static bool TryBuildEventBattleGroupMembers(
        DHEventBattleGroupTemplate groupData,
        out List<EventBattleEnemyGroupMember> members)
    {
        members = new List<EventBattleEnemyGroupMember>(6);

        if (groupData == null || groupData.Members == null)
            return false;

        for (int i = 0; i < groupData.Members.Count; i++)
        {
            DHEventBattleGroupMember member = groupData.Members[i];
            if (!TryAddEventBattleMember(members, member.UnitKey, member.CombatSlot))
                return false;
        }

        return members.Count > 0;
    }

    private static bool TryAddEventBattleMember(
        List<EventBattleEnemyGroupMember> members,
        string unitKey,
        int combatSlot)
    {
        if (string.IsNullOrWhiteSpace(unitKey))
            return true;

        if (combatSlot < MinCombatSlot || combatSlot > MaxCombatSlot)
            return false;

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].CombatSlot == combatSlot)
                return false;
        }

        if (!TryExtractNumericUnitKey(unitKey, out int numericUnitKey))
            return false;

        members.Add(new EventBattleEnemyGroupMember(unitKey.Trim(), numericUnitKey, combatSlot));
        return true;
    }

    private bool ValidateCsvMembers(
        string enemyGroupKey,
        IReadOnlyList<CsvEnemyGroupMember> members,
        DHCsvTemplateCatalog templateCatalog,
        LevelPrefabRegistry prefabRegistry)
    {
        if (members == null || members.Count == 0)
        {
            Debug.LogWarning($"Enemy group '{enemyGroupKey}' has no valid enemy units.", this);
            return false;
        }

        for (int i = 0; i < members.Count; i++)
        {
            int enemyUnitIndex = members[i].EnemyUnitIndex;
            string templateKey = enemyUnitIndex.ToString();

            if (!templateCatalog.TryGetEnemyUnitTemplate(templateKey, out _))
            {
                Debug.LogWarning(
                    $"Enemy group '{enemyGroupKey}' references missing enemy unit CSV index '{enemyUnitIndex}'.",
                    this);
                return false;
            }

            if (!prefabRegistry.TryGetEnemyUnitPrefab(enemyUnitIndex, out _))
            {
                Debug.LogWarning(
                    $"Enemy group '{enemyGroupKey}' references missing enemy unit prefab index '{enemyUnitIndex}'.",
                    this);
                return false;
            }
        }

        return true;
    }

    private bool ValidateEventBattleMembers(
        string enemyGroupKey,
        IReadOnlyList<EventBattleEnemyGroupMember> members,
        EventScriptCatalog eventCatalog,
        LevelPrefabRegistry prefabRegistry,
        int zoneId)
    {
        if (members == null || members.Count == 0)
        {
            Debug.LogWarning($"Event battle enemy group '{enemyGroupKey}' has no valid enemy units.", this);
            return false;
        }

        for (int i = 0; i < members.Count; i++)
        {
            EventBattleEnemyGroupMember member = members[i];

            if (!eventCatalog.TryGetBattleEnemyUnitTemplate(zoneId, member.UnitKey, out _))
            {
                Debug.LogWarning(
                    $"Event battle enemy group '{enemyGroupKey}' references missing enemy unit '{member.UnitKey}' in zone {zoneId}.",
                    this);
                return false;
            }

            if (!prefabRegistry.TryGetEnemyUnitPrefab(member.NumericUnitKey, out _))
            {
                Debug.LogWarning(
                    $"Event battle enemy group '{enemyGroupKey}' references missing enemy unit prefab index '{member.NumericUnitKey}'.",
                    this);
                return false;
            }
        }

        return true;
    }

    private static int ResolveEventBattleEnemyLevel(DHEventBattleGroupTemplate groupData, int fallbackLevel)
    {
        int level = Mathf.Max(1, fallbackLevel);
        if (groupData == null)
            return level;

        if (groupData.MinLevel > 0 && level < groupData.MinLevel)
            level = groupData.MinLevel;

        if (groupData.MaxLevel > 0 && level > groupData.MaxLevel)
            level = groupData.MaxLevel;

        return Mathf.Max(1, level);
    }

    private static bool TryExtractNumericUnitKey(string unitKey, out int numericUnitKey)
    {
        numericUnitKey = 0;
        if (string.IsNullOrWhiteSpace(unitKey))
            return false;

        string trimmed = unitKey.Trim();
        if (int.TryParse(trimmed, out numericUnitKey) && numericUnitKey > 0)
            return true;

        var digits = new System.Text.StringBuilder();
        for (int i = 0; i < trimmed.Length; i++)
        {
            if (char.IsDigit(trimmed[i]))
                digits.Append(trimmed[i]);
        }

        return digits.Length > 0 &&
            int.TryParse(digits.ToString(), out numericUnitKey) &&
            numericUnitKey > 0;
    }

    private static int ResolveEnemyLevelForRestore(
        MapProgressRepository mapProgressRepository,
        EnemyWorldState worldState,
        int fallbackLevel)
    {
        if (mapProgressRepository != null &&
            worldState != null &&
            mapProgressRepository.TryGetZoneEnemyLevel(worldState.ZoneId, out int zoneLevel))
        {
            return Mathf.Max(1, zoneLevel);
        }

        return Mathf.Max(1, fallbackLevel);
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

    private readonly struct EventBattleEnemyGroupMember
    {
        public EventBattleEnemyGroupMember(string unitKey, int numericUnitKey, int combatSlot)
        {
            UnitKey = string.IsNullOrWhiteSpace(unitKey) ? string.Empty : unitKey.Trim();
            NumericUnitKey = Mathf.Max(0, numericUnitKey);
            CombatSlot = combatSlot;
        }

        public string UnitKey { get; }
        public int NumericUnitKey { get; }
        public int CombatSlot { get; }
    }
}
