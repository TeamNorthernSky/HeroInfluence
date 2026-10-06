using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GateThreatController : MonoBehaviour
{
    private const string DefaultStartZoneId = "zone_001";

    [System.Serializable]
    private struct ZoneGateOpenCondition
    {
        public string zoneId;
        public string clearFlagName;
    }

    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private EnemySpawnController enemySpawnController;
    [SerializeField] private LevelZoneLayoutLoader layoutLoader;
    [SerializeField] private GridManager gridManager;

    [Header("Gate Open Conditions")]
    [SerializeField] private List<ZoneGateOpenCondition> zoneGateOpenConditions = new List<ZoneGateOpenCondition>();

    private readonly List<GateRuntimeController> gates = new List<GateRuntimeController>();
    private readonly List<EnemySpawnPoint> spawnPoints = new List<EnemySpawnPoint>();
    private readonly HashSet<string> gateBlockerDeferredThreatZones = new HashSet<string>();
    private string currentPartyZoneId;
    private bool initialThreatTimerEnsured;
    private DHEventStateRepository eventStateRepository;

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
        Outpost.OutpostClaimed += HandleOutpostClaimed;
        HeroUnionUnit.HeroUnionStateChanged += HandleHeroUnionStateChanged;
        SubscribeEventStateRepository();
    }

    private void OnDisable()
    {
        Outpost.OutpostClaimed -= HandleOutpostClaimed;
        HeroUnionUnit.HeroUnionStateChanged -= HandleHeroUnionStateChanged;
        if (eventStateRepository != null)
            eventStateRepository.FlagChanged -= HandleEventFlagChanged;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        ResolveReferences();

        if (DHGameEndState.IsEnding)
            return;

        EnsureInitialZoneThreatTimer();

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

        bool matchingGateAlreadyOpen = HasOpenGateWithSameId(gate);
        gates.Add(gate);

        if (!Application.isPlaying)
            return;

        if (matchingGateAlreadyOpen)
            gate.SetOpen(true, ResolveCurrentDay(), true);

        RefreshGateOpenStateForZone(gate.FirstZoneId);
        RefreshGateOpenStateForZone(gate.SecondZoneId);
    }

    public void UnregisterGate(GateRuntimeController gate)
    {
        if (gate == null)
            return;

        gates.Remove(gate);
    }

    private bool HasOpenGateWithSameId(GateRuntimeController gate)
    {
        if (gate == null || string.IsNullOrWhiteSpace(gate.GateId))
            return false;

        for (int i = 0; i < gates.Count; i++)
        {
            GateRuntimeController existingGate = gates[i];
            if (existingGate == null ||
                existingGate == gate ||
                !existingGate.IsOpen ||
                !string.Equals(existingGate.GateId, gate.GateId, System.StringComparison.Ordinal))
            {
                continue;
            }

            return true;
        }

        return false;
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
        // Outpost claims refresh threat state. Gates open only after the configured zone-clear flag is set.
        repository?.BeginZoneThreat(outpost.ZoneId, ResolveCurrentDay());
        RefreshGateOpenStateForZone(outpost.ZoneId);
    }

    private void HandleHeroUnionStateChanged(HeroUnionUnit heroUnion)
    {
        if (heroUnion == null || !heroUnion.IsClaimedByHero)
            return;

        // A zone starts counting from its HeroUnion claim. Gate opening itself remains tied to outpost clear.
        BeginThreatTimerIfNeeded(heroUnion.ZoneId, ResolveCurrentDay());
    }

    public IEnumerator ResolvePendingThreatsBeforeEnemyTurn()
    {
        ResolveReferences();

        // TurnManager calls this just before the enemy turn so spawned threats can act immediately.
        string zoneId = ResolvePartyZoneId();
        EnsureZoneEnemyLevel(zoneId);
        if (!string.Equals(zoneId, currentPartyZoneId, System.StringComparison.Ordinal))
        {
            PauseZoneThreatCounter(currentPartyZoneId);
            currentPartyZoneId = zoneId;
            EvaluateCurrentZoneThreat(true);
        }

        EvaluateCurrentZoneThreat(false, ResolveCurrentDay() + 1);

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            yield break;

        if (repository.TryGetZoneThreatState(currentPartyZoneId, out ZoneThreatProgressState state) &&
            state != null &&
            state.Active &&
            state.PendingThreatSpawn)
        {
            yield return ResolvePendingThreatSpawn(state.ZoneId);
        }

        if (gateBlockerDeferredThreatZones.Count == 0)
            yield break;

        List<string> deferredZones = new List<string>(gateBlockerDeferredThreatZones);
        for (int i = 0; i < deferredZones.Count; i++)
        {
            string deferredZoneId = deferredZones[i];
            if (string.Equals(deferredZoneId, currentPartyZoneId, System.StringComparison.Ordinal))
                continue;

            if (!repository.TryGetZoneThreatState(deferredZoneId, out ZoneThreatProgressState deferredState) ||
                deferredState == null ||
                !deferredState.Active ||
                !deferredState.PendingThreatSpawn)
            {
                gateBlockerDeferredThreatZones.Remove(deferredZoneId);
                continue;
            }

            yield return ResolvePendingThreatSpawn(deferredState.ZoneId);
        }
    }

    private void HandleEnemyDefeated(string placementKey)
    {
        string normalizedPlacementKey = MapProgressKey.NormalizeSegment(placementKey);
        if (string.IsNullOrWhiteSpace(normalizedPlacementKey))
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        HashSet<string> affectedZoneIds = new HashSet<string>();
        IReadOnlyList<ZoneThreatProgressState> states = repository.ZoneThreatStates;
        for (int i = 0; i < states.Count; i++)
        {
            ZoneThreatProgressState state = states[i];
            if (state == null ||
                !string.Equals(state.ActiveEnemyPlacementKey, normalizedPlacementKey, System.StringComparison.Ordinal))
            {
                continue;
            }

            string normalizedZoneId = MapProgressKey.NormalizeSegment(state.ZoneId);
            if (!string.IsNullOrWhiteSpace(normalizedZoneId))
                affectedZoneIds.Add(normalizedZoneId);
        }

        // Runtime threat keys carry their zone. This keeps gate reopening resilient even if
        // the active-threat key was reset or missed before the enemy defeat callback arrives.
        string zoneIdFromPlacementKey = TryResolveZoneIdFromGateThreatPlacementKey(normalizedPlacementKey);
        if (!string.IsNullOrWhiteSpace(zoneIdFromPlacementKey))
            affectedZoneIds.Add(zoneIdFromPlacementKey);

        if (repository.TryGetEnemyState(normalizedPlacementKey, out EnemyWorldState defeatedEnemyState) &&
            IsGateThreatPlacementKey(normalizedPlacementKey))
        {
            string zoneIdFromEnemyState = MapProgressKey.NormalizeSegment(defeatedEnemyState.ZoneId);
            if (!string.IsNullOrWhiteSpace(zoneIdFromEnemyState))
                affectedZoneIds.Add(zoneIdFromEnemyState);
        }

        foreach (string affectedZoneId in affectedZoneIds)
        {
            HandleZoneThreatEnemyDefeated(repository, affectedZoneId, normalizedPlacementKey);
        }
    }

    private void HandleZoneThreatEnemyDefeated(MapProgressRepository repository, string zoneId, string defeatedPlacementKey)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return;

        repository.ClearZoneThreatEnemy(normalizedZoneId);
        if (HasLiveThreatEnemyInZone(normalizedZoneId, defeatedPlacementKey))
            return;

        // Defeating a threat happens during the current player/enemy flow. The next pre-enemy-turn
        // evaluation uses current day + 1, so restart from that boundary to avoid counting this turn twice.
        repository.BeginZoneThreat(normalizedZoneId, ResolveCurrentDay() + 1);
        RefreshGateOpenStateForZone(normalizedZoneId);
    }

    private static bool IsGateThreatPlacementKey(string placementKey)
    {
        string normalizedPlacementKey = MapProgressKey.NormalizeSegment(placementKey);
        return !string.IsNullOrWhiteSpace(normalizedPlacementKey) &&
               normalizedPlacementKey.StartsWith("runtime_enemy_gate_threat_", System.StringComparison.Ordinal);
    }

    private static string TryResolveZoneIdFromGateThreatPlacementKey(string placementKey)
    {
        string normalizedPlacementKey = MapProgressKey.NormalizeSegment(placementKey);
        const string prefix = "runtime_enemy_gate_threat_";
        if (string.IsNullOrWhiteSpace(normalizedPlacementKey) ||
            !normalizedPlacementKey.StartsWith(prefix, System.StringComparison.Ordinal))
        {
            return string.Empty;
        }

        string remainder = normalizedPlacementKey.Substring(prefix.Length);
        int lastSeparator = remainder.LastIndexOf('_');
        if (lastSeparator <= 0)
            return string.Empty;

        return MapProgressKey.NormalizeSegment(remainder.Substring(0, lastSeparator));
    }

    private void HandleEventFlagChanged(string flagName, bool value)
    {
        if (!value || string.IsNullOrWhiteSpace(flagName))
            return;

        string normalizedFlagName = DHEventStateRepository.NormalizeFlagName(flagName);
        for (int i = 0; i < zoneGateOpenConditions.Count; i++)
        {
            ZoneGateOpenCondition condition = zoneGateOpenConditions[i];
            if (!string.Equals(
                    DHEventStateRepository.NormalizeFlagName(condition.clearFlagName),
                    normalizedFlagName,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            RefreshGateOpenStateForZone(condition.zoneId);
        }
    }

    private void EnsureInitialZoneThreatTimer()
    {
        if (initialThreatTimerEnsured)
            return;

        if (BeginThreatTimerIfNeeded(DefaultStartZoneId, ResolveCurrentDay()))
            initialThreatTimerEnsured = true;
    }

    private bool BeginThreatTimerIfNeeded(string zoneId, int day)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        if (!ShouldRunThreatForZone(normalizedZoneId))
            return false;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return false;

        if (repository.TryGetZoneThreatState(normalizedZoneId, out ZoneThreatProgressState state) &&
            state != null &&
            state.Active)
        {
            return true;
        }

        return repository.BeginZoneThreat(normalizedZoneId, day) != null;
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
        // Enemy unit instances are now rebuilt from EnemyGroupKey + zone level.
        // The level itself is stored in MapProgressRepository; no per-unit repository patching is needed.
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

    private void EvaluateCurrentZoneThreat(bool enteredZone)
    {
        EvaluateCurrentZoneThreat(enteredZone, ResolveCurrentDay());
    }

    private void EvaluateCurrentZoneThreat(bool enteredZone, int evaluationDay)
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

        int day = Mathf.Max(1, evaluationDay);
        ZoneThreatProgressState state;
        if (!repository.TryGetZoneThreatState(currentPartyZoneId, out state) || state == null || !state.Active)
            return;

        if (state == null || !state.Active)
            return;

        if (state.PendingThreatSpawn)
            return;

        if (enteredZone)
            state.PauseAtDay(day);

        // Only one active gate threat enemy is allowed per zone.
        if (HasLiveThreatEnemy(state))
            return;

        if (HasLiveThreatEnemyInZone(currentPartyZoneId, string.Empty))
            return;

        if (!HasSpawnPointInZone(currentPartyZoneId))
            return;

        int elapsedTurns = state.AccumulateUntilDay(day);
        if (elapsedTurns < GateLifecycleController.ResolveOpenDurationTurns())
            return;

        repository.MarkZoneThreatSpawnPending(currentPartyZoneId);
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

        // Threat timers pause while the party is outside that zone and resume on re-entry.
        state.PauseAtDay(ResolveCurrentDay());
    }

    private IEnumerator ResolvePendingThreatSpawn(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            yield break;

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            yield break;

        if (HasLiveThreatEnemyInZone(normalizedZoneId, string.Empty))
        {
            gateBlockerDeferredThreatZones.Remove(normalizedZoneId);
            repository.ClearZoneThreatSpawnPending(normalizedZoneId);
            yield break;
        }

        if (repository.TryGetZoneThreatState(normalizedZoneId, out ZoneThreatProgressState state) &&
            state != null &&
            HasLiveThreatEnemy(state))
        {
            gateBlockerDeferredThreatZones.Remove(normalizedZoneId);
            repository.ClearZoneThreatSpawnPending(normalizedZoneId);
            yield break;
        }

        if (IsPlayerPartyOnGateBlockerForZone(normalizedZoneId))
        {
            gateBlockerDeferredThreatZones.Add(normalizedZoneId);
            yield break;
        }

        if (enemySpawnController == null)
            enemySpawnController = FindFirstObjectByType<EnemySpawnController>();

        if (enemySpawnController == null)
        {
            gateBlockerDeferredThreatZones.Remove(normalizedZoneId);
            repository.ClearZoneThreatSpawnPending(normalizedZoneId);
            yield break;
        }

        if (TrySpawnThreatEnemy(normalizedZoneId, out string placementKey, out EnemySpawnPoint spawnPoint))
        {
            // Close the connected gates only after a threat enemy was actually spawned.
            CloseGatesForZone(normalizedZoneId);
            gateBlockerDeferredThreatZones.Remove(normalizedZoneId);
            repository.ClearZoneThreatSpawnPending(normalizedZoneId);
            repository.SetZoneThreatEnemy(normalizedZoneId, placementKey);
            bool chatClosed = false;
            // Spawn chat is optional; when present, enemy movement waits until the modal closes.
            if (TryShowSpawnChat(spawnPoint, placementKey, () => chatClosed = true))
            {
                yield return new WaitUntil(() => chatClosed);
                // ChatModalController destroys its modal at the end of the close frame.
                // Give it one frame before the enemy turn can open the encounter chat.
                yield return null;
            }

            yield break;
        }

        gateBlockerDeferredThreatZones.Remove(normalizedZoneId);
        repository.ClearZoneThreatSpawnPending(normalizedZoneId);
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

            if (enemySpawnController.TrySpawnGateThreatEnemy(
                    normalizedZoneId,
                    spawnPoint.GridPosition,
                    spawnPoint.EnemyGroupKey,
                    out placementKey,
                    out EnemyGridMover spawnedEnemy))
            {
                ApplySpawnPointEventEncounter(spawnPoint, placementKey, spawnedEnemy);
                usedSpawnPoint = spawnPoint;
                return true;
            }
        }

        placementKey = string.Empty;
        usedSpawnPoint = null;
        return false;
    }

    private bool HasSpawnPointInZone(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            EnemySpawnPoint spawnPoint = spawnPoints[i];
            if (spawnPoint != null &&
                string.Equals(spawnPoint.ZoneId, normalizedZoneId, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryShowSpawnChat(EnemySpawnPoint spawnPoint, string placementKey, System.Action onClosed = null)
    {
        if (spawnPoint == null || spawnPoint.SpawnChatZoneId <= 0 || spawnPoint.SpawnChatId <= 0)
            return false;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null || !catalog.TryGetChatTemplate(spawnPoint.SpawnChatZoneId, spawnPoint.SpawnChatId, out DHEventChatTemplate chat) || chat == null)
            return false;

        ChatModalController.Show(spawnPoint.SpawnChatZoneId, spawnPoint.SpawnChatId, onClosed);
        return true;
    }

    private static void ApplySpawnPointEventEncounter(
        EnemySpawnPoint spawnPoint,
        string placementKey,
        EnemyGridMover spawnedEnemy)
    {
        if (spawnPoint == null ||
            spawnedEnemy == null ||
            spawnPoint.EncounterChatZoneId <= 0 ||
            spawnPoint.EncounterChatId <= 0)
        {
            return;
        }

        EnemySpawnController.ApplyEventEncounterBinding(
            spawnedEnemy,
            placementKey,
            spawnPoint.EncounterChatZoneId,
            spawnPoint.EncounterChatId,
            spawnPoint.EventBattleKey);

        // Persist the encounter binding so runtime-spawned enemies restore with the same chat route.
        MapProgressRepository.Instance?.SetEnemyEventEncounter(
            placementKey,
            spawnPoint.EncounterChatZoneId,
            spawnPoint.EncounterChatId,
            spawnPoint.EventBattleKey);
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

    private void RefreshGateOpenStateForZone(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return;

        if (!IsZoneClearedForGateOpen(normalizedZoneId))
            return;

        if (HasLiveThreatEnemyInZone(normalizedZoneId, string.Empty))
            return;

        OpenGatesForZone(normalizedZoneId);
    }

    private bool IsZoneClearedForGateOpen(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        SubscribeEventStateRepository();
        if (eventStateRepository == null)
            return false;

        for (int i = 0; i < zoneGateOpenConditions.Count; i++)
        {
            ZoneGateOpenCondition condition = zoneGateOpenConditions[i];
            if (!string.Equals(
                    MapProgressKey.NormalizeSegment(condition.zoneId),
                    normalizedZoneId,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            string clearFlagName = DHEventStateRepository.NormalizeFlagName(condition.clearFlagName);
            if (!string.IsNullOrWhiteSpace(clearFlagName) && eventStateRepository.GetFlag(clearFlagName))
                return true;
        }

        return false;
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

    private bool ShouldRunThreatForZone(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        // Threat timers belong to the zone that owns the spawn point.
        // Adjacent HeroUnion state must not stop a previous zone's timer after the player crosses a gate.
        return HasSpawnPointInZone(normalizedZoneId);
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

    private bool IsPlayerPartyOnGateBlockerForZone(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null)
            return false;

        Vector2Int partyGrid = party.GetCurrentGrid();
        for (int i = 0; i < gates.Count; i++)
        {
            GateRuntimeController gate = gates[i];
            if (gate != null &&
                gate.ContainsZone(normalizedZoneId) &&
                gate.ContainsBlockerCell(partyGrid))
            {
                return true;
            }
        }

        return false;
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
    }

    private void SubscribeEventStateRepository()
    {
        DHEventStateRepository repository = Application.isPlaying
            ? DHEventStateRepository.EnsureInstance()
            : DHEventStateRepository.Instance;
        if (eventStateRepository == repository)
            return;

        if (eventStateRepository != null)
            eventStateRepository.FlagChanged -= HandleEventFlagChanged;

        eventStateRepository = repository;
        if (eventStateRepository != null)
            eventStateRepository.FlagChanged += HandleEventFlagChanged;
    }

    private int ResolveCurrentDay()
    {
        if (turnManager != null)
            return turnManager.GetDay();

        return GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.CurrentDay) : 1;
    }
}
