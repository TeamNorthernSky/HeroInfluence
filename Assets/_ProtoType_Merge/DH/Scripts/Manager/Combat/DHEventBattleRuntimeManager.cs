using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DHEventBattleRuntimeManager : MonoBehaviour
{
    private const string RootName = "[DH_EventBattleRuntime]";
    private const string BattleResultKey = "Flag_BattleResult";
    private const string HostageInjuredCountKey = "HostageInjuredCount";
    private const string BattleEndRelayText = "전투 종료";

    public static DHEventBattleRuntimeManager Instance { get; private set; }
    private static string pendingMainEventSourceKey = string.Empty;

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

        if (!catalog.TryGetBattleEnemyGroupTemplate(zoneId, request.BattleKey, out DHEventBattleGroupTemplate group) || group == null)
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
        if (DHEnemyEventEncounterRuntimeManager.Instance != null &&
            DHEnemyEventEncounterRuntimeManager.Instance.TryConsumePendingBattleSource(
                request.BattleKey,
                out string sourcePlacementKey))
        {
            eventBattle.SourceEnemyPlacementKey = sourcePlacementKey;
        }

        eventBattle.SourceMainEventKey = ResolveSourceMainEventKey(request.SourceMainEventKey);
        eventBattle.Scenario = BuildBattleScenario(zoneId, request.BattleKey, units);
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
        DHEnemyEventEncounterRuntimeManager.EnsureInstance().RegisterCompletedEventBattle(context);

        if (context.Result == CombatResult.Victory)
            CompleteSourceMainEvent(eventBattle.SourceMainEventKey);
        ClearPendingMainEventSource(eventBattle.SourceMainEventKey);

        if (eventBattle.ResumeChatId <= 0)
            return;

        if (!TryResolveResumeChatForModal(eventBattle.ZoneId, eventBattle.ResumeChatId, out int modalChatId))
        {
            Debug.LogWarning(
                $"[DHEventBattleRuntime] Event battle resume chat could not be resolved. Zone={eventBattle.ZoneId}, Chat={eventBattle.ResumeChatId}");
            return;
        }

        ChatModalController.Show(eventBattle.ZoneId, modalChatId);
    }

    public static void AssignCurrentEventBattleSourceMainEvent(string eventKey)
    {
        string normalizedKey = DHEventStateRepository.NormalizeKey(eventKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        CombatContext context = CombatContext.Instance;
        if (context == null || context.EventBattle == null)
            return;

        context.EventBattle.SourceMainEventKey = normalizedKey;
    }

    public static void SetPendingMainEventSource(string eventKey)
    {
        pendingMainEventSourceKey = DHEventStateRepository.NormalizeKey(eventKey);
    }

    public static void ClearPendingMainEventSource(string eventKey)
    {
        string normalizedKey = DHEventStateRepository.NormalizeKey(eventKey);
        if (string.Equals(pendingMainEventSourceKey, normalizedKey, StringComparison.Ordinal))
            pendingMainEventSourceKey = string.Empty;
    }

    private static string ResolveSourceMainEventKey(string requestSourceKey)
    {
        string normalizedRequestKey = DHEventStateRepository.NormalizeKey(requestSourceKey);
        return !string.IsNullOrWhiteSpace(normalizedRequestKey)
            ? normalizedRequestKey
            : pendingMainEventSourceKey;
    }

    private static bool TryResolveResumeChatForModal(int zoneId, int resumeChatId, out int modalChatId)
    {
        modalChatId = 0;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null)
            return false;

        if (!catalog.TryGetChatTemplate(zoneId, resumeChatId, out DHEventChatTemplate chat) || chat == null)
            return false;

        modalChatId = resumeChatId;
        if (!IsBattleEndRelayChat(chat))
            return true;

        if (!TryResolveAutoBranch(catalog, zoneId, chat, out DHEventBranchTemplate autoBranch) || autoBranch == null)
            return true;

        DHChatBranchRuleEvaluator.ExecuteTriggerEffect(autoBranch);
        modalChatId = autoBranch.TargetTalkId;
        return modalChatId > 0;
    }

    private static bool TryResolveAutoBranch(
        EventScriptCatalog catalog,
        int zoneId,
        DHEventChatTemplate chat,
        out DHEventBranchTemplate autoBranch)
    {
        autoBranch = null;
        if (catalog == null || chat == null || chat.BranchGroupId == 0)
            return false;

        if (!catalog.TryGetBranchOptionTemplates(zoneId, chat.BranchGroupId, out IReadOnlyList<DHEventBranchTemplate> options) ||
            options == null)
        {
            return false;
        }

        for (int i = 0; i < options.Count; i++)
        {
            DHEventBranchTemplate option = options[i];
            if (option == null || !string.IsNullOrWhiteSpace(option.SelectionText))
                continue;

            if (!DHChatBranchRuleEvaluator.IsBranchAvailable(option))
                continue;

            autoBranch = option;
            return true;
        }

        return false;
    }

    private static bool IsBattleEndRelayChat(DHEventChatTemplate chat)
    {
        if (chat == null || chat.BranchGroupId == 0)
            return false;

        return string.Equals(chat.MessageText, BattleEndRelayText, StringComparison.Ordinal);
    }

    private static void CompleteSourceMainEvent(string eventKey)
    {
        string normalizedKey = DHEventStateRepository.NormalizeKey(eventKey);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return;

        MapProgressRepository.Instance?.MarkMainEventCompleted(normalizedKey);

        MainEventObject[] mainEvents = FindObjectsByType<MainEventObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < mainEvents.Length; i++)
        {
            MainEventObject mainEvent = mainEvents[i];
            if (mainEvent == null)
                continue;

            if (!string.Equals(
                    DHEventStateRepository.NormalizeKey(mainEvent.EventKey),
                    normalizedKey,
                    StringComparison.Ordinal))
            {
                continue;
            }

            mainEvent.CompleteAfterEventBattleVictory();
        }
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
        DHEventBattleGroupTemplate group,
        int enemyLevel)
    {
        var units = new List<CombatEventBattleUnitData>();
        if (group == null || group.Members == null)
            return units;

        for (int i = 0; i < group.Members.Count; i++)
        {
            DHEventBattleGroupMember member = group.Members[i];
            TryAddUnit(catalog, zoneId, units, member.UnitKey, member.CombatSlot, enemyLevel);
        }

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
        if (!catalog.TryGetBattleEnemyUnitTemplate(zoneId, normalizedUnitKey, out DHEventBattleUnitTemplate source) || source == null)
        {
            Debug.LogWarning($"[DHEventBattleRuntime] Battle enemy unit was not found. Zone: {zoneId}, UnitKey: {normalizedUnitKey}");
            return;
        }

        int resolvedSlot = slot > 0 ? slot : units.Count + 1;
        CombatEventBattleUnitData unit = new CombatEventBattleUnitData(normalizedUnitKey, resolvedSlot, enemyLevel, source);
        ApplyLevelGrowth(unit, source, enemyLevel);
        PopulateEventBattleSkills(unit, source);
        units.Add(unit);
    }

    private static BattleScenarioConfig BuildBattleScenario(
        int zoneId,
        string battleKey,
        IReadOnlyList<CombatEventBattleUnitData> units)
    {
        BattleScenarioCatalog scenarioCatalog = BattleScenarioCatalog.LoadDefault();
        if (scenarioCatalog == null ||
            !scenarioCatalog.TryGetHostageScenario(zoneId, battleKey, out HostageScenarioDefinition definition) ||
            definition == null)
        {
            return null;
        }

        var hostageConfig = new HostageScenarioConfig
        {
            FullHealthAggroGain = Mathf.Max(0f, definition.FullHealthAggroGain),
            DamagedAggroGain = Mathf.Max(0f, definition.DamagedAggroGain),
            ThreatDamage = Mathf.Max(0f, definition.ThreatDamage),
            ThreatAggroReduction = Mathf.Max(0f, definition.ThreatAggroReduction),
            ThreatChancePerTotalAggro = Mathf.Max(0f, definition.ThreatChancePerTotalAggro),
            MaximumThreatChance = Mathf.Clamp01(definition.MaximumThreatChance),
            ThreatWindupSeconds = Mathf.Max(0f, definition.ThreatWindupSeconds),
            ThreatRecoverySeconds = Mathf.Max(0f, definition.ThreatRecoverySeconds)
        };

        if (definition.ThreatEnemyUnitKeys != null)
            hostageConfig.ThreatEnemyUnitKeys.AddRange(definition.ThreatEnemyUnitKeys);

        float initialHpRatio = Mathf.Clamp01(definition.InitialHpRatio);
        if (definition.Hostages != null)
        {
            for (int i = 0; i < definition.Hostages.Count; i++)
            {
                HostageDefinitionEntry entry = definition.Hostages[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.SourceUnitKey))
                    continue;

                CombatEventBattleUnitData sourceUnit = FindEventBattleUnit(units, entry.SourceUnitKey);
                if (sourceUnit == null)
                {
                    Debug.LogWarning(
                        $"[DHEventBattleRuntime] Hostage unit was not found in battle group. Battle={battleKey}, Unit={entry.SourceUnitKey}");
                    continue;
                }

                float maxHp = Mathf.Max(1f, sourceUnit.MaxHp);
                hostageConfig.Hostages.Add(new HostageSpawnConfig
                {
                    HostageId = string.IsNullOrWhiteSpace(entry.HostageId) ? entry.SourceUnitKey.Trim() : entry.HostageId.Trim(),
                    SourceUnitKey = sourceUnit.UnitKey,
                    Slot = sourceUnit.Slot,
                    MaxHp = maxHp,
                    InitialHp = Mathf.Max(1f, maxHp * initialHpRatio),
                    InitialAggro = 0f,
                    PrefabResourcePath = entry.PrefabResourcePath
                });
            }
        }

        if (hostageConfig.Hostages.Count == 0)
        {
            Debug.LogWarning($"[DHEventBattleRuntime] Hostage scenario has no valid hostages. Battle={battleKey}");
            return null;
        }

        return new BattleScenarioConfig
        {
            ScenarioType = BattleScenarioType.HostageRescue,
            HostageRescue = hostageConfig
        };
    }

    private static CombatEventBattleUnitData FindEventBattleUnit(
        IReadOnlyList<CombatEventBattleUnitData> units,
        string unitKey)
    {
        if (units == null || string.IsNullOrWhiteSpace(unitKey))
            return null;

        string normalized = BattleScenarioUnitKey.Normalize(unitKey);
        for (int i = 0; i < units.Count; i++)
        {
            CombatEventBattleUnitData candidate = units[i];
            if (candidate != null &&
                string.Equals(BattleScenarioUnitKey.Normalize(candidate.UnitKey), normalized, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void PopulateEventBattleSkills(CombatEventBattleUnitData unit, DHEventBattleUnitTemplate source)
    {
        if (unit == null || source == null)
            return;

        unit.Skills.Clear();
        int enemyIndex = ExtractNumericId(unit.UnitKey);
        if (enemyIndex <= 0)
            return;

        for (int i = 0; i < source.Skills.Count; i++)
        {
            DHEventBattleSkillTemplate skill = source.Skills[i];
            TryAddEventBattleSkill(
                unit.Skills, enemyIndex, skill.Slot, source.EnemyName,
                skill.SkillName, skill.Description,
                skill.Effect, skill.Range, skill.RangeLine,
                skill.Target, skill.Boundary,
                skill.MultiTargetType, skill.MultiTargetCount,
                skill.Value, skill.SubValue);
        }
    }

    private static void TryAddEventBattleSkill(
        List<SkillData> destination,
        int enemyIndex,
        int slot,
        string enemyName,
        string skillName,
        string description,
        int effect,
        int range,
        int rangeLine,
        int target,
        IReadOnlyList<int> boundary,
        int multiTargetType,
        int multiTargetCount,
        float value,
        float subValue)
    {
        if (destination == null || string.IsNullOrWhiteSpace(skillName))
            return;

        var skill = new SkillData
        {
            skillIndex = (enemyIndex * 10) + slot,
            skillClass = enemyName,
            acquireLevel = 1,
            skillName = skillName,
            description = description,
            ipCost = 0,
            classSkillEffect = effect,
            classSkillRange = range,
            EnemySkill1Range = slot == 1 ? range : -1,
            EnemySkill2Range = slot == 2 ? range : -1,
            classSkillRangeLine = rangeLine,
            classSkillTarget = target,
            multiTargetType = multiTargetType,
            multiTargetCount = multiTargetCount,
            skillValue = value,
            skillSubValue = subValue,
            AnimationTrigger = "Attack",
            TargetAnimationTrigger = "Hit",
            HitDelay = 0.25f,
            TotalDelay = 0.5f
        };

        if (boundary != null)
        {
            for (int i = 0; i < boundary.Count; i++)
                skill.boundary.Add(boundary[i]);
        }

        destination.Add(skill);
    }

    private static int ExtractNumericId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        string normalized = BattleScenarioUnitKey.Normalize(value);
        return int.TryParse(normalized, out int parsed) ? parsed : 0;
    }

    private static void ApplyLevelGrowth(CombatEventBattleUnitData unit, DHEventBattleUnitTemplate source, int enemyLevel)
    {
        if (unit == null || source == null)
            return;

        int growthCount = Mathf.Max(0, enemyLevel - 1);
        unit.MaxHp = Mathf.Max(1, source.BaseMaxHp + source.LevelGrowthMaxHp * growthCount);
        unit.Atk = Mathf.Max(0, source.BaseAtk + source.LevelGrowthAtk * growthCount);
        unit.Def = Mathf.Max(0, source.BaseDef + source.LevelGrowthDef * growthCount);
        unit.ExperiencePoint = Mathf.Max(0, source.ExperiencePoint + source.LevelGrowthExperiencePoint * growthCount);
    }

    private static int ResolveEnemyLevel(int zoneId, DHEventBattleGroupTemplate group, PartyGridMover party)
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
