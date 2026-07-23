using System.Collections.Generic;
using UnityEngine;

public class GateThreatController : MonoBehaviour
{
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private EnemySpawnController enemySpawnController;
    [SerializeField] private LevelZoneLayoutLoader layoutLoader;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private CombatPromptService combatPromptService;
    [SerializeField] private CombatEncounterManager combatEncounterManager;

    private readonly List<GateRuntimeController> gates = new List<GateRuntimeController>();
    private readonly List<EnemySpawnPoint> spawnPoints = new List<EnemySpawnPoint>();
    private string currentPartyZoneId;

    public static GateThreatController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        ResolveReferences();
        SubscribeTurnManager();
        Outpost.OutpostClaimed += HandleOutpostClaimed;
        HeroUnionUnit.HeroUnionStateChanged += HandleHeroUnionStateChanged;
    }

    private void OnDisable()
    {
        Outpost.OutpostClaimed -= HandleOutpostClaimed;
        HeroUnionUnit.HeroUnionStateChanged -= HandleHeroUnionStateChanged;

        if (turnManager != null)
            turnManager.DayAdvanced -= HandleDayAdvanced;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ResolveReferences();

        if (DHGameEndState.IsEnding)
            return;

        string zoneId = ResolvePartyZoneId();
        EnsureZoneEnemyLevel(zoneId);
        if (string.Equals(zoneId, currentPartyZoneId, System.StringComparison.Ordinal))
        {
            EvaluateCurrentZoneThreat(false);
            return;
        }

        PauseZoneThreatCounter(currentPartyZoneId);
        currentPartyZoneId = zoneId;
        EvaluateCurrentZoneThreat(true);
    }

    public static GateThreatController EnsureSceneInstance()
    {
        if (Instance != null)
            return Instance;

        GateThreatController existing = FindFirstObjectByType<GateThreatController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (!Application.isPlaying)
            return null;

        GameObject created = new GameObject(nameof(GateThreatController));
        Instance = created.AddComponent<GateThreatController>();
        return Instance;
    }

    public static void NotifyEnemyDefeated(string placementKey)
    {
        if (string.IsNullOrWhiteSpace(placementKey))
            return;

        GateThreatController controller = EnsureSceneInstance();
        controller?.HandleEnemyDefeated(placementKey);
    }

    public void RegisterGate(GateRuntimeController gate)
    {
        if (gate == null || gates.Contains(gate))
            return;

        gates.Add(gate);
    }

    public void UnregisterGate(GateRuntimeController gate)
    {
        if (gate == null)
            return;

        gates.Remove(gate);
    }

    public void RegisterSpawnPoint(EnemySpawnPoint spawnPoint)
    {
        if (spawnPoint == null || spawnPoints.Contains(spawnPoint))
            return;

        spawnPoints.Add(spawnPoint);
    }

    public void UnregisterSpawnPoint(EnemySpawnPoint spawnPoint)
    {
        if (spawnPoint == null)
            return;

        spawnPoints.Remove(spawnPoint);
    }

    private void HandleOutpostClaimed(Outpost outpost)
    {
        if (outpost == null || !outpost.IsPlayerClaimed)
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        repository?.BeginZoneThreat(outpost.ZoneId, ResolveCurrentDay());
        OpenGatesForZone(outpost.ZoneId);
    }

    private void HandleHeroUnionStateChanged(HeroUnionUnit heroUnion)
    {
        if (heroUnion == null || !heroUnion.IsClaimedByHero)
            return;

        EndThreatsConnectedToZone(heroUnion.ZoneId);
    }

    private void HandleDayAdvanced(int day)
    {
        EvaluateCurrentZoneThreat(false);
    }

    private void HandleEnemyDefeated(string placementKey)
    {
        string normalizedPlacementKey = MapProgressKey.NormalizeSegment(placementKey);
        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        IReadOnlyList<ZoneThreatProgressState> states = repository.ZoneThreatStates;
        for (int i = 0; i < states.Count; i++)
        {
            ZoneThreatProgressState state = states[i];
            if (state == null ||
                !string.Equals(state.ActiveEnemyPlacementKey, normalizedPlacementKey, System.StringComparison.Ordinal))
            {
                continue;
            }

            repository.ClearZoneThreatEnemy(state.ZoneId);
            if (HasLiveThreatEnemyInZone(state.ZoneId, normalizedPlacementKey))
                continue;

            repository.BeginZoneThreat(state.ZoneId, ResolveCurrentDay());
            OpenGatesForZone(state.ZoneId);
        }
    }

    private void EnsureZoneEnemyLevel(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null || repository.TryGetZoneEnemyLevel(normalizedZoneId, out _))
            return;

        int enemyLevel = repository.EnsureZoneEnemyLevel(normalizedZoneId, CalculateCurrentPartyAverageLevel());
        ApplyZoneEnemyLevel(normalizedZoneId, enemyLevel);
        RefreshZoneEnemyLevelInspectors(normalizedZoneId);
    }

    private int CalculateCurrentPartyAverageLevel()
    {
        PartyPersistentRepository partyRepository = PartyPersistentRepository.Instance;
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        if (partyRepository == null || unitRepository == null)
            return 1;

        PartyPersistentData partyData = ResolveCurrentPartyData(partyRepository);
        if (partyData == null || partyData.UnitIndices == null || partyData.UnitIndices.Count == 0)
            return 1;

        int totalLevel = 0;
        int unitCount = 0;
        IReadOnlyList<int> unitIndices = partyData.UnitIndices;
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

    private PartyPersistentData ResolveCurrentPartyData(PartyPersistentRepository partyRepository)
    {
        if (partyRepository == null)
            return null;

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        PartyGridMover playerParty = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (playerParty != null)
        {
            PartyIdentity identity = playerParty.GetComponent<PartyIdentity>();
            if (identity != null && partyRepository.TryGetParty(identity.PartyId, out PartyPersistentData partyData))
                return partyData;
        }

        IReadOnlyList<PartyPersistentData> parties = partyRepository.Parties;
        for (int i = 0; i < parties.Count; i++)
        {
            if (parties[i] != null)
                return parties[i];
        }

        return null;
    }

    private static void ApplyZoneEnemyLevel(string zoneId, int enemyLevel)
    {
        MapProgressRepository mapRepository = MapProgressRepository.Instance;
        EnemyGroupPersistentRepository enemyGroupRepository = EnemyGroupPersistentRepository.Instance;
        PersistentEnemyRepository enemyRepository = PersistentEnemyRepository.Instance;
        if (mapRepository == null || enemyGroupRepository == null || enemyRepository == null)
            return;

        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        IReadOnlyList<EnemyWorldState> enemyStates = mapRepository.EnemyWorldStates;
        for (int i = 0; i < enemyStates.Count; i++)
        {
            EnemyWorldState worldState = enemyStates[i];
            if (worldState == null || worldState.Defeated || !string.Equals(worldState.ZoneId, normalizedZoneId, System.StringComparison.Ordinal))
                continue;

            if (!enemyGroupRepository.TryGetEnemy(worldState.EnemyId, out EnemyPersistentData enemyData) || enemyData == null)
                continue;

            ApplyEnemyGroupLevel(enemyRepository, enemyData, enemyLevel);
        }
    }

    private static void RefreshZoneEnemyLevelInspectors(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return;

        Outpost[] outposts = FindObjectsByType<Outpost>(FindObjectsSortMode.None);
        for (int i = 0; i < outposts.Length; i++)
        {
            Outpost outpost = outposts[i];
            if (outpost != null && string.Equals(outpost.ZoneId, normalizedZoneId, System.StringComparison.Ordinal))
                outpost.RefreshResolvedEnemyLevel();
        }

        VillainUnionBase[] villainUnionBases = FindObjectsByType<VillainUnionBase>(FindObjectsSortMode.None);
        for (int i = 0; i < villainUnionBases.Length; i++)
        {
            VillainUnionBase villainUnionBase = villainUnionBases[i];
            if (villainUnionBase != null && string.Equals(villainUnionBase.ZoneId, normalizedZoneId, System.StringComparison.Ordinal))
                villainUnionBase.RefreshResolvedEnemyLevel();
        }

        EnemySpawnPoint[] enemySpawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
        for (int i = 0; i < enemySpawnPoints.Length; i++)
        {
            EnemySpawnPoint enemySpawnPoint = enemySpawnPoints[i];
            if (enemySpawnPoint != null && string.Equals(enemySpawnPoint.ZoneId, normalizedZoneId, System.StringComparison.Ordinal))
                enemySpawnPoint.RefreshResolvedEnemyLevel();
        }
    }

    private static void ApplyEnemyGroupLevel(PersistentEnemyRepository enemyRepository, EnemyPersistentData enemyData, int enemyLevel)
    {
        IReadOnlyList<int> unitIndices = enemyData.UnitIndices;
        if (unitIndices == null)
            return;

        int safeLevel = Mathf.Max(1, enemyLevel);
        for (int i = 0; i < unitIndices.Count; i++)
        {
            int unitIndex = unitIndices[i];
            if (!enemyRepository.TryGetUnit(unitIndex, out EnemyUnitPersistentData unitData) || unitData == null)
                continue;

            if (unitData.Level < safeLevel)
                enemyRepository.ApplyLevelUp(unitIndex, safeLevel - unitData.Level);
            else if (unitData.Level > safeLevel)
                enemyRepository.SetUnitLevel(unitIndex, safeLevel);
        }
    }
    private void EvaluateCurrentZoneThreat(bool enteredZone)
    {
        if (string.IsNullOrWhiteSpace(currentPartyZoneId))
            return;

        if (!ShouldRunThreatForZone(currentPartyZoneId))
        {
            EndThreatForZone(currentPartyZoneId);
            return;
        }

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        int day = ResolveCurrentDay();
        ZoneThreatProgressState state;
        if (!repository.TryGetZoneThreatState(currentPartyZoneId, out state) || state == null || !state.Active)
        {
            if (!HasClaimedOutpostInZone(currentPartyZoneId))
                return;

            state = repository.BeginZoneThreat(currentPartyZoneId, day);
        }

        if (state == null || !state.Active)
            return;

        if (enteredZone)
            state.PauseAtDay(day);

        if (HasLiveThreatEnemy(state))
            return;

        if (HasLiveThreatEnemyInZone(currentPartyZoneId, string.Empty))
            return;

        int elapsedTurns = state.AccumulateUntilDay(day);
        if (elapsedTurns < GateLifecycleController.ResolveOpenDurationTurns())
            return;

        TriggerThreat(currentPartyZoneId);
    }

    private void PauseZoneThreatCounter(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null ||
            !repository.TryGetZoneThreatState(normalizedZoneId, out ZoneThreatProgressState state) ||
            state == null ||
            !state.Active)
        {
            return;
        }

        state.PauseAtDay(ResolveCurrentDay());
    }

    private void TriggerThreat(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        if (HasLiveThreatEnemyInZone(normalizedZoneId, string.Empty))
            return;

        CloseGatesForZone(normalizedZoneId);

        if (repository.TryGetZoneThreatState(normalizedZoneId, out ZoneThreatProgressState state) &&
            state != null &&
            HasLiveThreatEnemy(state))
        {
            return;
        }

        if (enemySpawnController == null)
            enemySpawnController = FindFirstObjectByType<EnemySpawnController>();

        if (enemySpawnController == null)
            return;

        if (TrySpawnThreatEnemy(normalizedZoneId, out string placementKey, out EnemySpawnPoint spawnPoint))
        {
            repository.SetZoneThreatEnemy(normalizedZoneId, placementKey);
            if (!TryShowSpawnChat(spawnPoint, placementKey))
                TryOpenSpawnedEnemyCombatPrompt(placementKey);
        }
    }

    private bool HasLiveThreatEnemy(ZoneThreatProgressState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.ActiveEnemyPlacementKey))
            return false;

        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository == null || !repository.IsEnemyDefeated(state.ActiveEnemyPlacementKey);
    }

    private static bool HasLiveThreatEnemyInZone(string zoneId, string ignoredPlacementKey)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return false;

        string ignoredKey = MapProgressKey.NormalizeSegment(ignoredPlacementKey);
        string threatPrefix = $"runtime_enemy_gate_threat_{normalizedZoneId}_";
        IReadOnlyList<EnemyWorldState> enemyStates = repository.EnemyWorldStates;
        for (int i = 0; i < enemyStates.Count; i++)
        {
            EnemyWorldState enemyState = enemyStates[i];
            if (enemyState == null || enemyState.Defeated)
                continue;

            string placementKey = MapProgressKey.NormalizeSegment(enemyState.PlacementKey);
            if (string.IsNullOrWhiteSpace(placementKey) ||
                string.Equals(placementKey, ignoredKey, System.StringComparison.Ordinal) ||
                !placementKey.StartsWith(threatPrefix, System.StringComparison.Ordinal))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TrySpawnThreatEnemy(string zoneId, out string placementKey, out EnemySpawnPoint usedSpawnPoint)
    {
        placementKey = string.Empty;
        usedSpawnPoint = null;
        List<EnemySpawnPoint> candidates = new List<EnemySpawnPoint>();
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            EnemySpawnPoint candidate = spawnPoints[i];
            if (candidate == null ||
                !string.Equals(candidate.ZoneId, normalizedZoneId, System.StringComparison.Ordinal))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return false;

        int startIndex = Random.Range(0, candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            EnemySpawnPoint spawnPoint = candidates[(startIndex + i) % candidates.Count];
            if (spawnPoint == null)
                continue;

            if (enemySpawnController.TrySpawnGateThreatEnemy(normalizedZoneId, spawnPoint.GridPosition, spawnPoint.EnemyGroupKey, out placementKey))
            {
                usedSpawnPoint = spawnPoint;
                return true;
            }
        }

        placementKey = string.Empty;
        usedSpawnPoint = null;
        return false;
    }

    private bool TryShowSpawnChat(EnemySpawnPoint spawnPoint, string placementKey)
    {
        if (spawnPoint == null || spawnPoint.SpawnChatZoneId <= 0 || spawnPoint.SpawnChatId <= 0)
            return false;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null || !catalog.TryGetChat(spawnPoint.SpawnChatZoneId, spawnPoint.SpawnChatId, out ChatDBEventData chat) || chat == null)
            return false;

        ChatModalController.Show(spawnPoint.SpawnChatZoneId, spawnPoint.SpawnChatId, () => TryOpenSpawnedEnemyCombatPrompt(placementKey));
        return true;
    }

    private void TryOpenSpawnedEnemyCombatPrompt(string placementKey)
    {
        string normalizedPlacementKey = MapProgressKey.NormalizeSegment(placementKey);
        if (string.IsNullOrWhiteSpace(normalizedPlacementKey))
            return;

        ResolveReferences();

        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null || gridManager == null || combatEncounterManager == null)
            return;

        if (!gridManager.TryGetEnemyEncounterZoneOwner(party.GetCurrentGrid(), out EnemyGridMover enemy) ||
            enemy == null ||
            !IsMatchingEnemyPlacement(enemy, normalizedPlacementKey))
        {
            return;
        }

        if (combatPromptService != null &&
            combatPromptService.TryOpenEnemyCombatPrompt(party, enemy, combatEncounterManager, _ => { }))
        {
            return;
        }

        combatEncounterManager.BeginCombat(party, enemy);
    }

    private static bool IsMatchingEnemyPlacement(EnemyGridMover enemy, string normalizedPlacementKey)
    {
        EnemyIdentity identity = enemy != null ? enemy.GetComponent<EnemyIdentity>() : null;
        return identity != null &&
            string.Equals(
                MapProgressKey.NormalizeSegment(identity.PlacementKey),
                normalizedPlacementKey,
                System.StringComparison.Ordinal);
    }

    private void OpenGatesForZone(string zoneId)
    {
        int day = ResolveCurrentDay();
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        for (int i = 0; i < gates.Count; i++)
        {
            GateRuntimeController gate = gates[i];
            if (gate != null && gate.ContainsZone(normalizedZoneId))
                gate.OpenGate(day);
        }
    }

    private void CloseGatesForZone(string zoneId)
    {
        int day = ResolveCurrentDay();
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        for (int i = 0; i < gates.Count; i++)
        {
            GateRuntimeController gate = gates[i];
            if (gate != null && gate.ContainsZone(normalizedZoneId))
                gate.CloseGate(day);
        }
    }

    private void EndThreatForZone(string zoneId)
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        repository?.EndZoneThreat(zoneId);
    }

    private void EndThreatsConnectedToZone(string zoneId)
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        repository.EndZoneThreat(normalizedZoneId);

        for (int i = 0; i < gates.Count; i++)
        {
            GateRuntimeController gate = gates[i];
            if (gate == null || !gate.ContainsZone(normalizedZoneId))
                continue;

            repository.EndZoneThreat(gate.FirstZoneId);
            repository.EndZoneThreat(gate.SecondZoneId);
        }
    }

    private bool ShouldRunThreatForZone(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        for (int i = 0; i < gates.Count; i++)
        {
            GateRuntimeController gate = gates[i];
            if (gate == null || !gate.ContainsZone(normalizedZoneId))
                continue;

            string firstZoneId = gate.FirstZoneId;
            string secondZoneId = gate.SecondZoneId;
            if (!string.Equals(firstZoneId, normalizedZoneId, System.StringComparison.Ordinal) &&
                !IsHeroUnionClaimed(firstZoneId))
            {
                return true;
            }

            if (!string.Equals(secondZoneId, normalizedZoneId, System.StringComparison.Ordinal) &&
                !IsHeroUnionClaimed(secondZoneId))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasClaimedOutpostInZone(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (outpostRegistry == null)
            return false;

        IReadOnlyList<Outpost> outposts = outpostRegistry.Outposts;
        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost outpost = outposts[i];
            if (outpost == null || !outpost.IsPlayerClaimed)
                continue;

            if (string.Equals(outpost.ZoneId, normalizedZoneId, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool IsHeroUnionClaimed(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        if (heroUnionRegistry != null && heroUnionRegistry.TryGetClaimedByZoneId(normalizedZoneId, out _))
            return true;

        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null &&
            repository.TryGetHeroUnionState(normalizedZoneId, out HeroUnionState state) &&
            state == HeroUnionState.ClaimedByHero;
    }

    private string ResolvePartyZoneId()
    {
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null)
            return string.Empty;

        Vector2Int grid = party.GetCurrentGrid();
        ResolveReferences();

        if (layoutLoader == null)
            return string.Empty;

        IReadOnlyList<LoadedLevelZoneData> zones = layoutLoader.LoadedZones;
        for (int i = 0; i < zones.Count; i++)
        {
            LoadedLevelZoneData zone = zones[i];
            Vector2Int min = zone.Anchor;
            Vector2Int max = zone.Anchor + zone.Size - Vector2Int.one;
            if (grid.x < min.x || grid.x > max.x || grid.y < min.y || grid.y > max.y)
                continue;

            return MapProgressKey.NormalizeSegment(zone.ZoneId);
        }

        return string.Empty;
    }

    private void ResolveReferences()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();
        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();
        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();
        if (enemySpawnController == null)
            enemySpawnController = FindFirstObjectByType<EnemySpawnController>();
        if (layoutLoader == null)
            layoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
        if (gridManager == null)
            gridManager = Game.Grid != null ? Game.Grid : FindFirstObjectByType<GridManager>();
        if (combatPromptService == null)
            combatPromptService = FindFirstObjectByType<CombatPromptService>();
        if (combatEncounterManager == null)
            combatEncounterManager = FindFirstObjectByType<CombatEncounterManager>();

        SubscribeTurnManager();
    }

    private void SubscribeTurnManager()
    {
        if (turnManager == null)
            return;

        turnManager.DayAdvanced -= HandleDayAdvanced;
        turnManager.DayAdvanced += HandleDayAdvanced;
    }

    private int ResolveCurrentDay()
    {
        if (turnManager != null)
            return turnManager.GetDay();

        return GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.CurrentDay) : 1;
    }
}
