using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DHEventBattleRuntimeManager : MonoBehaviour
{
    private const string RootName = "[DH_EventBattleRuntime]";
    private const string BattleResultKey = "Flag_BattleResult";
    private const string HostageInjuredCountKey = "HostageInjuredCount";

    public static DHEventBattleRuntimeManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool logRequests;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static DHEventBattleRuntimeManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject(RootName);
        DontDestroyOnLoad(root);
        return root.AddComponent<DHEventBattleRuntimeManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        DHEventEffectRuntimeManager.EnsureInstance().EventBattleRequested += HandleEventBattleRequested;
    }

    private void OnDisable()
    {
        DHEventEffectRuntimeManager effectManager = DHEventEffectRuntimeManager.Instance;
        if (effectManager != null)
            effectManager.EventBattleRequested -= HandleEventBattleRequested;
    }

    private void HandleEventBattleRequested(DHEventBattleEffectRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.BattleKey))
        {
            Debug.LogWarning("[DHEventBattleRuntime] Event battle request is empty.", this);
            return;
        }

        int zoneId = request.ZoneId > 0 ? request.ZoneId : ResolveZoneIdFromActiveParty();
        if (zoneId <= 0)
        {
            Debug.LogWarning($"[DHEventBattleRuntime] ZoneId could not be resolved for battle '{request.BattleKey}'.", this);
            return;
        }

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogWarning($"[DHEventBattleRuntime] EventScriptCatalog is missing for battle '{request.BattleKey}'.", this);
            return;
        }

        if (!catalog.TryGetBattleEnemyGroup(zoneId, request.BattleKey, out EnemyGroupData group) || group == null)
        {
            Debug.LogWarning($"[DHEventBattleRuntime] Battle group was not found. Zone: {zoneId}, BattleKey: {request.BattleKey}", this);
            return;
        }

        PartyGridMover party = ResolveActiveParty();
        if (party == null)
        {
            Debug.LogWarning($"[DHEventBattleRuntime] Active party was not found for battle '{request.BattleKey}'.", this);
            return;
        }

        int enemyLevel = ResolveEnemyLevel(zoneId, group, party);
        List<CombatEventBattleUnitData> units = BuildEventBattleUnits(catalog, zoneId, group, enemyLevel);
        if (units.Count == 0)
        {
            Debug.LogWarning($"[DHEventBattleRuntime] Battle group has no valid enemy units. Zone: {zoneId}, BattleKey: {request.BattleKey}", this);
            return;
        }

        var eventBattle = new CombatEventBattleData(
            zoneId,
            request.BattleKey.Trim(),
            request.ResumeChatId,
            enemyLevel,
            units);
        eventBattle.SetNumericResult(HostageInjuredCountKey, 0f);

        CombatEncounterManager encounterManager = FindFirstObjectByType<CombatEncounterManager>();
        if (encounterManager == null)
        {
            Debug.LogWarning($"[DHEventBattleRuntime] CombatEncounterManager is missing for battle '{request.BattleKey}'.", this);
            return;
        }

        if (logRequests)
        {
            Debug.Log(
                $"[DHEventBattleRuntime] Start event battle. Zone={zoneId}, BattleKey={request.BattleKey}, Level={enemyLevel}, Units={units.Count}, ResumeChat={request.ResumeChatId}",
                this);
        }

        if (!encounterManager.BeginEventBattleCombat(party, eventBattle))
        {
            Debug.LogWarning($"[DHEventBattleRuntime] Failed to begin event battle '{request.BattleKey}'.", this);
        }
    }

    public static void HandleCompletedEventBattle(CombatContext context)
    {
        if (context == null || context.EventBattle == null)
            return;

        CombatEventBattleData eventBattle = context.EventBattle;
        DHEventStateRepository stateRepository = DHEventStateRepository.EnsureInstance();
        stateRepository.SetNumericValue(BattleResultKey, context.Result == CombatResult.Victory ? 1f : 0f);
        ApplyNumericResults(stateRepository, eventBattle);

        if (eventBattle.ResumeChatId <= 0)
            return;

        ChatManager chatManager = ChatManager.Instance;
        if (chatManager == null)
        {
            Debug.LogWarning(
                $"[DHEventBattleRuntime] ChatManager is missing. Event battle chat resume skipped. Zone={eventBattle.ZoneId}, Chat={eventBattle.ResumeChatId}");
            return;
        }

        chatManager.ResumeEventBattleChat(eventBattle.ZoneId, eventBattle.ResumeChatId);
    }

    private static void ApplyNumericResults(DHEventStateRepository stateRepository, CombatEventBattleData eventBattle)
    {
        if (stateRepository == null || eventBattle == null || eventBattle.NumericResults == null)
            return;

        for (int i = 0; i < eventBattle.NumericResults.Count; i++)
        {
            DHEventNumericState result = eventBattle.NumericResults[i];
            if (result == null || string.IsNullOrWhiteSpace(result.key))
                continue;

            stateRepository.SetNumericValue(result.key, result.value);
        }
    }

    private static List<CombatEventBattleUnitData> BuildEventBattleUnits(
        EventScriptCatalog catalog,
        int zoneId,
        EnemyGroupData group,
        int enemyLevel)
    {
        var units = new List<CombatEventBattleUnitData>();
        TryAddUnit(catalog, zoneId, units, group.Enemy1, group.Enemy1Slot, enemyLevel);
        TryAddUnit(catalog, zoneId, units, group.Enemy2, group.Enemy2Slot, enemyLevel);
        TryAddUnit(catalog, zoneId, units, group.Enemy3, group.Enemy3Slot, enemyLevel);
        TryAddUnit(catalog, zoneId, units, group.Enemy4, group.Enemy4Slot, enemyLevel);
        TryAddUnit(catalog, zoneId, units, group.Enemy5, group.Enemy5Slot, enemyLevel);
        TryAddUnit(catalog, zoneId, units, group.Enemy6, group.Enemy6Slot, enemyLevel);
        return units;
    }

    private static void TryAddUnit(
        EventScriptCatalog catalog,
        int zoneId,
        List<CombatEventBattleUnitData> units,
        string unitKey,
        int slot,
        int enemyLevel)
    {
        if (catalog == null || units == null || string.IsNullOrWhiteSpace(unitKey))
            return;

        string normalizedUnitKey = unitKey.Trim();
        if (!catalog.TryGetBattleEnemyUnit(zoneId, normalizedUnitKey, out EnemyUnit1SectorData source) || source == null)
        {
            Debug.LogWarning($"[DHEventBattleRuntime] Battle enemy unit was not found. Zone: {zoneId}, UnitKey: {normalizedUnitKey}");
            return;
        }

        int resolvedSlot = slot > 0 ? slot : units.Count + 1;
        CombatEventBattleUnitData unit = new CombatEventBattleUnitData(normalizedUnitKey, resolvedSlot, enemyLevel, source);
        ApplyLevelGrowth(unit, source, enemyLevel);
        units.Add(unit);
    }

    private static void ApplyLevelGrowth(CombatEventBattleUnitData unit, EnemyUnit1SectorData source, int enemyLevel)
    {
        if (unit == null || source == null)
            return;

        int growthCount = Mathf.Max(0, enemyLevel - 1);
        unit.MaxHp = Mathf.Max(1, source.UnitMaxHP + source.LevelGrowthMaxHP * growthCount);
        unit.Atk = Mathf.Max(0, source.UnitATK + source.LevelGrowthAtk * growthCount);
        unit.Def = Mathf.Max(0, source.UnitDEF + source.LevelGrowthDef * growthCount);
        unit.ExperiencePoint = Mathf.Max(0, source.ExperiencePoint + source.LevelGrowthExperiencePoint * growthCount);
    }

    private static int ResolveEnemyLevel(int zoneId, EnemyGroupData group, PartyGridMover party)
    {
        string zoneKey = zoneId.ToString();
        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        int level = progressRepository != null && progressRepository.TryGetZoneEnemyLevel(zoneKey, out int storedLevel)
            ? storedLevel
            : progressRepository != null
                ? progressRepository.EnsureZoneEnemyLevel(zoneKey, CalculatePartyAverageLevel(party))
                : CalculatePartyAverageLevel(party);

        if (group != null)
        {
            if (group.MinLevel > 0)
                level = Mathf.Max(level, group.MinLevel);
            if (group.MaxLevel > 0)
                level = Mathf.Min(level, group.MaxLevel);
        }

        return Mathf.Max(1, level);
    }

    private static int CalculatePartyAverageLevel(PartyGridMover party)
    {
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        if (unitRepository == null)
            return 1;

        IReadOnlyList<int> unitIndices = ResolvePartyUnitIndices(partyRepository, party);
        if (unitIndices == null || unitIndices.Count == 0)
            return 1;

        int totalLevel = 0;
        int unitCount = 0;
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (unitIndex <= 0 || !unitRepository.TryGetUnit(unitIndex, out UnitPersistentData unitData) || unitData == null)
                continue;

            totalLevel += Mathf.Max(1, unitData.Level);
            unitCount++;
        }

        return unitCount > 0 ? Mathf.Max(1, totalLevel / unitCount) : 1;
    }

    private static IReadOnlyList<int> ResolvePartyUnitIndices(PartyPersistentRepository repository, PartyGridMover party)
    {
        if (repository != null && party != null)
        {
            PartyIdentity identity = party.GetComponent<PartyIdentity>();
            if (identity != null && repository.TryGetParty(identity.PartyId, out PartyPersistentData partyData) && partyData != null)
                return partyData.UnitIndices;
        }

        PartyComposition composition = party != null ? party.GetComponent<PartyComposition>() : null;
        return composition != null ? composition.UnitIndices : Array.Empty<int>();
    }

    private static PartyGridMover ResolveActiveParty()
    {
        PartyRegistry registry = FindFirstObjectByType<PartyRegistry>();
        if (registry != null && registry.PlayerParty != null)
            return registry.PlayerParty;

        return FindFirstObjectByType<PartyGridMover>();
    }

    private static int ResolveZoneIdFromActiveParty()
    {
        PartyGridMover party = ResolveActiveParty();
        if (party == null)
            return 0;

        Vector2Int grid = party.GetCurrentGrid();
        LevelZoneLayoutLoader layoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
        if (layoutLoader == null)
            return 0;

        IReadOnlyList<LoadedLevelZoneData> zones = layoutLoader.LoadedZones;
        for (int i = 0; i < zones.Count; i++)
        {
            LoadedLevelZoneData zone = zones[i];
            Vector2Int min = zone.Anchor;
            Vector2Int max = zone.Anchor + zone.Size - Vector2Int.one;
            if (grid.x < min.x || grid.x > max.x || grid.y < min.y || grid.y > max.y)
                continue;

            return int.TryParse(zone.ZoneId, out int parsedZoneId) ? parsedZoneId : 0;
        }

        return 0;
    }
}
