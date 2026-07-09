using System.Collections.Generic;
using UnityEngine;

public class GateThreatController : MonoBehaviour
{
    [SerializeField, Min(1)] private int threatDelayTurns = 3;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private EnemySpawnController enemySpawnController;
    [SerializeField] private LevelZoneLayoutLoader layoutLoader;

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
        if (string.Equals(zoneId, currentPartyZoneId, System.StringComparison.Ordinal))
        {
            EvaluateCurrentZoneThreat(false);
            return;
        }

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
        CloseGatesForZone(heroUnion.ZoneId);
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

        if (HasLiveThreatEnemy(state))
            return;

        int elapsedTurns = Mathf.Max(0, day - state.EnteredDay);
        if (!enteredZone && elapsedTurns < threatDelayTurns)
            return;

        if (enteredZone && elapsedTurns < threatDelayTurns)
            return;

        TriggerThreat(currentPartyZoneId);
    }

    private void TriggerThreat(string zoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return;

        CloseGatesForZone(normalizedZoneId);

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

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

        if (TrySpawnThreatEnemy(normalizedZoneId, out string placementKey))
            repository.SetZoneThreatEnemy(normalizedZoneId, placementKey);
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

    private bool TrySpawnThreatEnemy(string zoneId, out string placementKey)
    {
        placementKey = string.Empty;
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

            if (enemySpawnController.TrySpawnGateThreatEnemy(normalizedZoneId, spawnPoint.GridPosition, out placementKey))
                return true;
        }

        placementKey = string.Empty;
        return false;
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
