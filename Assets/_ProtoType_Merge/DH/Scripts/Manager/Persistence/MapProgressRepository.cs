using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MapProgressRepository : MonoBehaviour
{
    public static MapProgressRepository Instance { get; private set; }

    [Header("Map Progress")]
    [SerializeField] private string mapId = "default";
    [SerializeField] private List<string> collectedItemKeys = new List<string>();
    [SerializeField] private List<string> completedEventKeys = new List<string>();
    [SerializeField] private List<string> completedMainEventKeys = new List<string>();
    [SerializeField] private List<string> completedSubEventKeys = new List<string>();
    [SerializeField] private List<string> completedHeroUnionChatKeys = new List<string>();
    [SerializeField] private List<PartyWorldState> partyWorldStates = new List<PartyWorldState>();
    [SerializeField] private List<EnemyWorldState> enemyWorldStates = new List<EnemyWorldState>();
    [SerializeField] private List<OutpostProgressState> outpostStates = new List<OutpostProgressState>();
    [SerializeField] private List<FogProgressCell> fogCells = new List<FogProgressCell>();
    [SerializeField] private List<LevelZoneSelectionState> levelZoneSelections = new List<LevelZoneSelectionState>();
    [SerializeField] private List<HeroUnionProgressState> heroUnionStates = new List<HeroUnionProgressState>();
    [SerializeField] private List<GateProgressState> gateStates = new List<GateProgressState>();
    [SerializeField] private List<ZoneThreatProgressState> zoneThreatStates = new List<ZoneThreatProgressState>();
    [SerializeField] private List<ZoneEnemyLevelState> zoneEnemyLevelStates = new List<ZoneEnemyLevelState>();
    [SerializeField] private ZoneEntryGuidanceProgressState zoneEntryGuidanceState = new ZoneEntryGuidanceProgressState();

    private readonly HashSet<string> collectedItemLookup = new HashSet<string>();
    private readonly HashSet<string> completedEventLookup = new HashSet<string>();
    private readonly HashSet<string> completedMainEventLookup = new HashSet<string>();
    private readonly HashSet<string> completedSubEventLookup = new HashSet<string>();
    private readonly HashSet<string> completedHeroUnionChatLookup = new HashSet<string>();
    private readonly Dictionary<string, PartyWorldState> partyWorldLookup = new Dictionary<string, PartyWorldState>();
    private readonly Dictionary<string, EnemyWorldState> enemyWorldLookup = new Dictionary<string, EnemyWorldState>();
    private readonly Dictionary<string, OutpostProgressState> outpostStateLookup = new Dictionary<string, OutpostProgressState>();
    private readonly Dictionary<string, LevelZoneSelectionState> levelZoneSelectionLookup = new Dictionary<string, LevelZoneSelectionState>();
    private readonly Dictionary<string, HeroUnionProgressState> heroUnionStateLookup = new Dictionary<string, HeroUnionProgressState>();
    private readonly Dictionary<string, GateProgressState> gateStateLookup = new Dictionary<string, GateProgressState>();
    private readonly Dictionary<string, ZoneThreatProgressState> zoneThreatStateLookup = new Dictionary<string, ZoneThreatProgressState>();
    private readonly Dictionary<string, ZoneEnemyLevelState> zoneEnemyLevelStateLookup = new Dictionary<string, ZoneEnemyLevelState>();

    public string MapId => mapId;
    public IReadOnlyList<string> CollectedItemKeys => collectedItemKeys;
    public IReadOnlyList<string> CompletedEventKeys => completedEventKeys;
    public IReadOnlyList<string> CompletedMainEventKeys => completedMainEventKeys;
    public IReadOnlyList<string> CompletedSubEventKeys => completedSubEventKeys;
    public IReadOnlyList<string> CompletedHeroUnionChatKeys => completedHeroUnionChatKeys;
    public IReadOnlyList<PartyWorldState> PartyWorldStates => partyWorldStates;
    public IReadOnlyList<EnemyWorldState> EnemyWorldStates => enemyWorldStates;
    public IReadOnlyList<OutpostProgressState> OutpostStates => outpostStates;
    public IReadOnlyList<FogProgressCell> FogCells => fogCells;
    public IReadOnlyList<LevelZoneSelectionState> LevelZoneSelections => levelZoneSelections;
    public IReadOnlyList<HeroUnionProgressState> HeroUnionStates => heroUnionStates;
    public IReadOnlyList<GateProgressState> GateStates => gateStates;
    public IReadOnlyList<ZoneThreatProgressState> ZoneThreatStates => zoneThreatStates;
    public IReadOnlyList<ZoneEnemyLevelState> ZoneEnemyLevelStates => zoneEnemyLevelStates;
    public ZoneEntryGuidanceProgressState ZoneEntryGuidanceState => zoneEntryGuidanceState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildLookups();
    }

    private void OnValidate()
    {
        RebuildLookups();
    }

    public void SetMapId(string nextMapId)
    {
        if (string.IsNullOrWhiteSpace(nextMapId))
            return;

        mapId = MapProgressKey.NormalizeSegment(nextMapId);
    }

    public bool IsItemCollected(string itemKey)
    {
        return IsValidKey(itemKey) && collectedItemLookup.Contains(NormalizeKey(itemKey));
    }

    public void MarkItemCollected(string itemKey)
    {
        AddUniqueKey(itemKey, collectedItemKeys, collectedItemLookup);
    }

    public bool IsEventCompleted(string eventKey)
    {
        return IsValidKey(eventKey) && completedEventLookup.Contains(NormalizeKey(eventKey));
    }

    public void MarkEventCompleted(string eventKey)
    {
        AddUniqueKey(eventKey, completedEventKeys, completedEventLookup);
    }

    public bool IsMainEventCompleted(string eventKey)
    {
        return IsValidKey(eventKey) && completedMainEventLookup.Contains(NormalizeKey(eventKey));
    }

    public void MarkMainEventCompleted(string eventKey)
    {
        AddUniqueKey(eventKey, completedMainEventKeys, completedMainEventLookup);
    }

    public bool IsSubEventCompleted(string eventKey)
    {
        return IsValidKey(eventKey) && completedSubEventLookup.Contains(NormalizeKey(eventKey));
    }

    public void MarkSubEventCompleted(string eventKey)
    {
        AddUniqueKey(eventKey, completedSubEventKeys, completedSubEventLookup);
    }

    public bool IsHeroUnionChatCompleted(string chatKey)
    {
        return IsValidKey(chatKey) && completedHeroUnionChatLookup.Contains(NormalizeKey(chatKey));
    }

    public void MarkHeroUnionChatCompleted(string chatKey)
    {
        AddUniqueKey(chatKey, completedHeroUnionChatKeys, completedHeroUnionChatLookup);
    }

    public bool TryGetOutpostState(string outpostKey, out OutpostState state)
    {
        state = OutpostState.Unclaimed;

        if (!IsValidKey(outpostKey))
            return false;

        if (!outpostStateLookup.TryGetValue(NormalizeKey(outpostKey), out OutpostProgressState progressState))
            return false;

        state = progressState.State;
        return true;
    }

    public bool TryGetOutpostProgress(string outpostKey, out OutpostProgressState progressState)
    {
        progressState = null;

        if (!IsValidKey(outpostKey))
            return false;

        return outpostStateLookup.TryGetValue(NormalizeKey(outpostKey), out progressState);
    }

    public void SetOutpostState(string outpostKey, OutpostState state)
    {
        if (!IsValidKey(outpostKey))
            return;

        OutpostProgressState progressState = GetOrCreateOutpostState(NormalizeKey(outpostKey), state);
        progressState.SetState(state);
    }

    public void SetOutpostState(
        string outpostKey,
        OutpostState state,
        string enemyDefenderGroupKey,
        string defenderEnemyId)
    {
        if (!IsValidKey(outpostKey))
            return;

        OutpostProgressState progressState = GetOrCreateOutpostState(NormalizeKey(outpostKey), state);
        progressState.SetState(state);
        progressState.SetEnemyDefender(enemyDefenderGroupKey, defenderEnemyId);
    }

    public void SetOutpostDefender(string outpostKey, string enemyDefenderGroupKey, string defenderEnemyId)
    {
        if (!IsValidKey(outpostKey))
            return;

        OutpostProgressState progressState = GetOrCreateOutpostState(NormalizeKey(outpostKey), OutpostState.EnemyClaimed);
        progressState.SetEnemyDefender(enemyDefenderGroupKey, defenderEnemyId);
    }

    public bool TryFindOutpostByDefenderEnemyId(string defenderEnemyId, out OutpostProgressState progressState)
    {
        progressState = null;

        if (string.IsNullOrWhiteSpace(defenderEnemyId))
            return false;

        for (int i = 0; i < outpostStates.Count; i++)
        {
            OutpostProgressState candidate = outpostStates[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.DefenderEnemyId, defenderEnemyId, System.StringComparison.Ordinal))
                continue;

            progressState = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetPartyState(string placementKey, out PartyWorldState state)
    {
        state = null;
        return IsValidKey(placementKey) && partyWorldLookup.TryGetValue(NormalizeKey(placementKey), out state);
    }

    public bool TryGetPartyId(string placementKey, out string partyId)
    {
        partyId = null;

        if (!TryGetPartyState(placementKey, out PartyWorldState state) ||
            string.IsNullOrWhiteSpace(state.PartyId) ||
            state.Removed)
            return false;

        partyId = state.PartyId;
        return true;
    }

    public void BindParty(string placementKey, string partyId, Vector2Int grid)
    {
        BindParty(placementKey, partyId, grid, PartyPlacementSource.Scene, PartyWorldState.DefaultPrefabKey);
    }

    public void BindParty(
        string placementKey,
        string partyId,
        Vector2Int grid,
        PartyPlacementSource placementSource,
        string prefabKey)
    {
        if (!IsValidKey(placementKey) || string.IsNullOrWhiteSpace(partyId))
            return;

        PartyWorldState state = GetOrCreatePartyState(NormalizeKey(placementKey), partyId, grid, placementSource, prefabKey);
        state.SetPartyId(partyId);
        state.SetGrid(grid);
        state.SetPlacementSource(placementSource);
        state.SetPrefabKey(prefabKey);
        state.SetRemoved(false);
    }

    public void SetPartyGrid(string placementKey, Vector2Int grid)
    {
        if (TryGetPartyState(placementKey, out PartyWorldState state))
            state.SetGrid(grid);
    }

    public void MarkPartyRemoved(string placementKey)
    {
        if (TryGetPartyState(placementKey, out PartyWorldState state))
            state.SetRemoved(true);
    }

    public void MarkPartyActive(string placementKey)
    {
        if (TryGetPartyState(placementKey, out PartyWorldState state))
            state.SetRemoved(false);
    }

    public bool TryGetEnemyState(string placementKey, out EnemyWorldState state)
    {
        state = null;
        return IsValidKey(placementKey) && enemyWorldLookup.TryGetValue(NormalizeKey(placementKey), out state);
    }

    public bool TryGetEnemyId(string placementKey, out string enemyId)
    {
        enemyId = null;

        if (!TryGetEnemyState(placementKey, out EnemyWorldState state) ||
            string.IsNullOrWhiteSpace(state.EnemyId) ||
            state.Defeated)
            return false;

        enemyId = state.EnemyId;
        return true;
    }

    public bool IsEnemyDefeated(string placementKey)
    {
        return TryGetEnemyState(placementKey, out EnemyWorldState state) && state.Defeated;
    }

    public void BindEnemy(string placementKey, string enemyId, Vector2Int grid)
    {
        BindEnemy(placementKey, enemyId, grid, EnemyPlacementSource.Scene, EnemyWorldState.DefaultPrefabKey);
    }

    public void BindEnemy(
        string placementKey,
        string enemyId,
        Vector2Int grid,
        EnemyPlacementSource placementSource,
        string prefabKey)
    {
        BindEnemy(placementKey, enemyId, grid, placementSource, prefabKey, string.Empty);
    }

    public void BindEnemy(
        string placementKey,
        string enemyId,
        Vector2Int grid,
        EnemyPlacementSource placementSource,
        string prefabKey,
        string zoneId)
    {
        if (!IsValidKey(placementKey) || string.IsNullOrWhiteSpace(enemyId))
            return;

        EnemyWorldState state = GetOrCreateEnemyState(NormalizeKey(placementKey), enemyId, grid, placementSource, prefabKey, zoneId);
        state.SetEnemyId(enemyId);
        state.SetGrid(grid);
        state.SetPlacementSource(placementSource);
        state.SetPrefabKey(prefabKey);
        state.SetZoneId(zoneId);
        state.SetDefeated(false);
    }

    public void SetEnemyEventEncounter(
        string placementKey,
        int encounterChatZoneId,
        int encounterChatId,
        string eventBattleKey)
    {
        if (TryGetEnemyState(placementKey, out EnemyWorldState state))
            state.SetEventEncounter(encounterChatZoneId, encounterChatId, eventBattleKey);
    }

    public void SetEnemyGrid(string placementKey, Vector2Int grid)
    {
        if (TryGetEnemyState(placementKey, out EnemyWorldState state))
            state.SetGrid(grid);
    }

    public void MarkEnemyDefeated(string placementKey)
    {
        if (TryGetEnemyState(placementKey, out EnemyWorldState state))
            state.SetDefeated(true);
    }

    public void SetEnemyZone(string placementKey, string zoneId)
    {
        if (TryGetEnemyState(placementKey, out EnemyWorldState state))
            state.SetZoneId(zoneId);
    }

    public void MarkEnemyActive(string placementKey)
    {
        if (TryGetEnemyState(placementKey, out EnemyWorldState state))
            state.SetDefeated(false);
    }

    public void ReplaceFogCells(IEnumerable<FogGridManager.FogCellSnapshot> snapshots)
    {
        fogCells.Clear();

        if (snapshots == null)
            return;

        foreach (FogGridManager.FogCellSnapshot snapshot in snapshots)
        {
            if (snapshot.Visibility == FogVisibilityState.Unexplored)
                continue;

            fogCells.Add(new FogProgressCell(snapshot.Grid, snapshot.Visibility, snapshot.LastRevealedDay));
        }
    }

    public bool HasFogProgress()
    {
        return fogCells.Count > 0;
    }

    public void ClearFogProgress()
    {
        fogCells.Clear();
    }

    public bool TryGetLevelZoneSelection(string layoutId, string zoneId, out int selectedCandidateIndex)
    {
        selectedCandidateIndex = -1;

        string selectionKey = LevelZoneSelectionState.BuildSelectionKey(layoutId, zoneId);
        if (!IsValidKey(selectionKey))
            return false;

        if (!levelZoneSelectionLookup.TryGetValue(selectionKey, out LevelZoneSelectionState state))
            return false;

        selectedCandidateIndex = state.SelectedCandidateIndex;
        return true;
    }

    public void SetLevelZoneSelection(string layoutId, string zoneId, int selectedCandidateIndex)
    {
        string selectionKey = LevelZoneSelectionState.BuildSelectionKey(layoutId, zoneId);
        if (!IsValidKey(selectionKey))
            return;

        LevelZoneSelectionState state = GetOrCreateLevelZoneSelection(layoutId, zoneId, selectedCandidateIndex);
        state.SetSelectedCandidateIndex(selectedCandidateIndex);
    }

    public void ClearLevelZoneSelections()
    {
        levelZoneSelections.Clear();
        levelZoneSelectionLookup.Clear();
    }
    public bool TryGetHeroUnionState(string zoneId, out HeroUnionState state)
    {
        state = HeroUnionState.Neutral;

        string progressKey = HeroUnionProgressState.BuildProgressKey(zoneId);
        if (!IsValidKey(progressKey))
            return false;

        if (!heroUnionStateLookup.TryGetValue(progressKey, out HeroUnionProgressState progressState))
            return false;

        state = progressState.State;
        return true;
    }

    public void SetHeroUnionState(string zoneId, HeroUnionState state)
    {
        string progressKey = HeroUnionProgressState.BuildProgressKey(zoneId);
        if (!IsValidKey(progressKey))
            return;

        HeroUnionProgressState progressState = GetOrCreateHeroUnionState(zoneId, state);
        progressState.SetState(state);
    }

    public bool TryGetGateState(string gateId, out GateProgressState state)
    {
        state = null;
        string normalizedGateId = NormalizeKey(gateId);
        return IsValidKey(normalizedGateId) && gateStateLookup.TryGetValue(normalizedGateId, out state);
    }

    public void SetGateState(string gateId, bool open, int openedDay)
    {
        string normalizedGateId = NormalizeKey(gateId);
        if (!IsValidKey(normalizedGateId))
            return;

        GateProgressState state = GetOrCreateGateState(normalizedGateId, open, openedDay);
        state.Apply(open, openedDay);
    }

    public bool TryGetZoneThreatState(string zoneId, out ZoneThreatProgressState state)
    {
        state = null;
        string normalizedZoneId = NormalizeKey(zoneId);
        return IsValidKey(normalizedZoneId) && zoneThreatStateLookup.TryGetValue(normalizedZoneId, out state);
    }

    public ZoneThreatProgressState BeginZoneThreat(string zoneId, int enteredDay)
    {
        string normalizedZoneId = NormalizeKey(zoneId);
        if (!IsValidKey(normalizedZoneId))
            return null;

        ZoneThreatProgressState state = GetOrCreateZoneThreatState(normalizedZoneId);
        state.Begin(enteredDay);
        return state;
    }

    public void EndZoneThreat(string zoneId)
    {
        if (TryGetZoneThreatState(zoneId, out ZoneThreatProgressState state))
            state.End();
    }

    public void SetZoneThreatEnemy(string zoneId, string placementKey)
    {
        string normalizedZoneId = NormalizeKey(zoneId);
        if (!IsValidKey(normalizedZoneId))
            return;

        ZoneThreatProgressState state = GetOrCreateZoneThreatState(normalizedZoneId);
        state.SetActiveEnemy(placementKey);
    }

    public void MarkZoneThreatSpawnPending(string zoneId)
    {
        string normalizedZoneId = NormalizeKey(zoneId);
        if (!IsValidKey(normalizedZoneId))
            return;

        ZoneThreatProgressState state = GetOrCreateZoneThreatState(normalizedZoneId);
        state.MarkThreatSpawnPending();
    }

    public void ClearZoneThreatSpawnPending(string zoneId)
    {
        if (TryGetZoneThreatState(zoneId, out ZoneThreatProgressState state))
            state.ClearThreatSpawnPending();
    }

    public void ClearZoneThreatEnemy(string zoneId)
    {
        if (TryGetZoneThreatState(zoneId, out ZoneThreatProgressState state))
            state.ClearActiveEnemy();
    }

    public bool TryGetZoneEnemyLevel(string zoneId, out int enemyLevel)
    {
        enemyLevel = 1;
        string normalizedZoneId = NormalizeKey(zoneId);
        if (!IsValidKey(normalizedZoneId))
            return false;

        if (!zoneEnemyLevelStateLookup.TryGetValue(normalizedZoneId, out ZoneEnemyLevelState state) || state == null || !state.Initialized)
            return false;

        enemyLevel = state.EnemyLevel;
        return true;
    }

    public int EnsureZoneEnemyLevel(string zoneId, int enemyLevel)
    {
        string normalizedZoneId = NormalizeKey(zoneId);
        if (!IsValidKey(normalizedZoneId))
            return Mathf.Max(1, enemyLevel);

        ZoneEnemyLevelState state = GetOrCreateZoneEnemyLevelState(normalizedZoneId, enemyLevel);
        state.Initialize(enemyLevel);
        return state.EnemyLevel;
    }

    public bool IsZoneEntryGuidanceCompleted(string zoneId)
    {
        EnsureZoneEntryGuidanceState();
        return zoneEntryGuidanceState.IsZoneCompleted(zoneId);
    }

    public void BeginZoneEntryGuidance(string zoneId, string requiredHeroUnionId, IReadOnlyList<Vector2Int> allowedPathCells)
    {
        EnsureZoneEntryGuidanceState();
        zoneEntryGuidanceState.Begin(zoneId, requiredHeroUnionId, allowedPathCells);
    }

    public void CompleteZoneEntryGuidance(string zoneId)
    {
        EnsureZoneEntryGuidanceState();
        zoneEntryGuidanceState.AddCompletedZone(zoneId);

        if (zoneEntryGuidanceState.Active &&
            string.Equals(zoneEntryGuidanceState.ZoneId, NormalizeKey(zoneId), System.StringComparison.Ordinal))
        {
            zoneEntryGuidanceState.ClearActive();
        }
    }

    public void ClearActiveZoneEntryGuidance()
    {
        EnsureZoneEntryGuidanceState();
        zoneEntryGuidanceState.ClearActive();
    }

    public void RestoreZoneEntryGuidance(ZoneEntryGuidanceProgressState restoredState)
    {
        zoneEntryGuidanceState = restoredState != null
            ? new ZoneEntryGuidanceProgressState(restoredState)
            : new ZoneEntryGuidanceProgressState();
    }

    public void RestoreFromSave(
        string restoredMapId,
        IEnumerable<string> restoredCollectedItemKeys,
        IEnumerable<string> restoredCompletedEventKeys,
        IEnumerable<string> restoredCompletedMainEventKeys,
        IEnumerable<string> restoredCompletedSubEventKeys,
        IEnumerable<string> restoredCompletedHeroUnionChatKeys,
        IEnumerable<PartyWorldState> restoredPartyWorldStates,
        IEnumerable<EnemyWorldState> restoredEnemyWorldStates,
        IEnumerable<OutpostProgressState> restoredOutpostStates,
        IEnumerable<FogProgressCell> restoredFogCells,
        IEnumerable<LevelZoneSelectionState> restoredLevelZoneSelections,
        IEnumerable<HeroUnionProgressState> restoredHeroUnionStates,
        IEnumerable<GateProgressState> restoredGateStates,
        IEnumerable<ZoneThreatProgressState> restoredZoneThreatStates,
        IEnumerable<ZoneEnemyLevelState> restoredZoneEnemyLevelStates,
        ZoneEntryGuidanceProgressState restoredZoneEntryGuidanceState)
    {
        mapId = string.IsNullOrWhiteSpace(restoredMapId)
            ? "default"
            : MapProgressKey.NormalizeSegment(restoredMapId);

        ReplaceList(collectedItemKeys, restoredCollectedItemKeys);
        ReplaceList(completedEventKeys, restoredCompletedEventKeys);
        ReplaceList(completedMainEventKeys, restoredCompletedMainEventKeys);
        ReplaceList(completedSubEventKeys, restoredCompletedSubEventKeys);
        ReplaceList(completedHeroUnionChatKeys, restoredCompletedHeroUnionChatKeys);
        ReplaceList(partyWorldStates, restoredPartyWorldStates);
        ReplaceList(enemyWorldStates, restoredEnemyWorldStates);
        ReplaceList(outpostStates, restoredOutpostStates);
        ReplaceList(fogCells, restoredFogCells);
        ReplaceList(levelZoneSelections, restoredLevelZoneSelections);
        ReplaceList(heroUnionStates, restoredHeroUnionStates);
        ReplaceList(gateStates, restoredGateStates);
        ReplaceList(zoneThreatStates, restoredZoneThreatStates);
        ReplaceList(zoneEnemyLevelStates, restoredZoneEnemyLevelStates);
        RestoreZoneEntryGuidance(restoredZoneEntryGuidanceState);
        RebuildLookups();
    }
    public void ClearAllProgress()
    {
        collectedItemKeys.Clear();
        completedEventKeys.Clear();
        completedMainEventKeys.Clear();
        completedSubEventKeys.Clear();
        completedHeroUnionChatKeys.Clear();
        partyWorldStates.Clear();
        enemyWorldStates.Clear();
        outpostStates.Clear();
        fogCells.Clear();
        levelZoneSelections.Clear();
        heroUnionStates.Clear();
        gateStates.Clear();
        zoneThreatStates.Clear();
        zoneEnemyLevelStates.Clear();
        zoneEntryGuidanceState = new ZoneEntryGuidanceProgressState();
        RebuildLookups();
    }

    private HeroUnionProgressState GetOrCreateHeroUnionState(string zoneId, HeroUnionState state)
    {
        string progressKey = HeroUnionProgressState.BuildProgressKey(zoneId);
        if (heroUnionStateLookup.TryGetValue(progressKey, out HeroUnionProgressState progressState))
            return progressState;

        progressState = new HeroUnionProgressState(zoneId, state);
        heroUnionStates.Add(progressState);
        heroUnionStateLookup[progressKey] = progressState;
        return progressState;
    }

    private GateProgressState GetOrCreateGateState(string gateId, bool open, int openedDay)
    {
        if (gateStateLookup.TryGetValue(gateId, out GateProgressState state))
            return state;

        state = new GateProgressState(gateId, open, openedDay);
        gateStates.Add(state);
        gateStateLookup[gateId] = state;
        return state;
    }

    private ZoneThreatProgressState GetOrCreateZoneThreatState(string zoneId)
    {
        if (zoneThreatStateLookup.TryGetValue(zoneId, out ZoneThreatProgressState state))
            return state;

        state = new ZoneThreatProgressState(zoneId, false, 1, string.Empty);
        zoneThreatStates.Add(state);
        zoneThreatStateLookup[zoneId] = state;
        return state;
    }

    private ZoneEnemyLevelState GetOrCreateZoneEnemyLevelState(string zoneId, int enemyLevel)
    {
        if (zoneEnemyLevelStateLookup.TryGetValue(zoneId, out ZoneEnemyLevelState state))
            return state;

        state = new ZoneEnemyLevelState(zoneId, enemyLevel);
        zoneEnemyLevelStates.Add(state);
        zoneEnemyLevelStateLookup[zoneId] = state;
        return state;
    }
    private PartyWorldState GetOrCreatePartyState(string placementKey, string partyId, Vector2Int grid)
    {
        return GetOrCreatePartyState(placementKey, partyId, grid, PartyPlacementSource.Scene, PartyWorldState.DefaultPrefabKey);
    }

    private PartyWorldState GetOrCreatePartyState(
        string placementKey,
        string partyId,
        Vector2Int grid,
        PartyPlacementSource placementSource,
        string prefabKey)
    {
        if (partyWorldLookup.TryGetValue(placementKey, out PartyWorldState state))
            return state;

        state = new PartyWorldState(placementKey, partyId, grid, placementSource, prefabKey);
        partyWorldStates.Add(state);
        partyWorldLookup[placementKey] = state;
        return state;
    }

    private EnemyWorldState GetOrCreateEnemyState(string placementKey, string enemyId, Vector2Int grid)
    {
        return GetOrCreateEnemyState(placementKey, enemyId, grid, EnemyPlacementSource.Scene, EnemyWorldState.DefaultPrefabKey);
    }

    private EnemyWorldState GetOrCreateEnemyState(
        string placementKey,
        string enemyId,
        Vector2Int grid,
        EnemyPlacementSource placementSource,
        string prefabKey)
    {
        return GetOrCreateEnemyState(placementKey, enemyId, grid, placementSource, prefabKey, string.Empty);
    }

    private EnemyWorldState GetOrCreateEnemyState(
        string placementKey,
        string enemyId,
        Vector2Int grid,
        EnemyPlacementSource placementSource,
        string prefabKey,
        string zoneId)
    {
        if (enemyWorldLookup.TryGetValue(placementKey, out EnemyWorldState state))
            return state;

        state = new EnemyWorldState(placementKey, enemyId, grid, placementSource, prefabKey, zoneId);
        enemyWorldStates.Add(state);
        enemyWorldLookup[placementKey] = state;
        return state;
    }

    private OutpostProgressState GetOrCreateOutpostState(string outpostKey, OutpostState state)
    {
        if (outpostStateLookup.TryGetValue(outpostKey, out OutpostProgressState progressState))
            return progressState;

        progressState = new OutpostProgressState(outpostKey, state);
        outpostStates.Add(progressState);
        outpostStateLookup[outpostKey] = progressState;
        return progressState;
    }

    private LevelZoneSelectionState GetOrCreateLevelZoneSelection(
        string layoutId,
        string zoneId,
        int selectedCandidateIndex)
    {
        string selectionKey = LevelZoneSelectionState.BuildSelectionKey(layoutId, zoneId);
        if (levelZoneSelectionLookup.TryGetValue(selectionKey, out LevelZoneSelectionState state))
            return state;

        state = new LevelZoneSelectionState(layoutId, zoneId, selectedCandidateIndex);
        levelZoneSelections.Add(state);
        levelZoneSelectionLookup[selectionKey] = state;
        return state;
    }

    private void RebuildLookups()
    {
        EnsureZoneEntryGuidanceState();
        collectedItemLookup.Clear();
        completedEventLookup.Clear();
        completedMainEventLookup.Clear();
        completedSubEventLookup.Clear();
        completedHeroUnionChatLookup.Clear();
        partyWorldLookup.Clear();
        enemyWorldLookup.Clear();
        outpostStateLookup.Clear();
        levelZoneSelectionLookup.Clear();
        heroUnionStateLookup.Clear();
        gateStateLookup.Clear();
        zoneThreatStateLookup.Clear();
        zoneEnemyLevelStateLookup.Clear();

        RebuildKeyLookup(collectedItemKeys, collectedItemLookup, "collected item key");
        RebuildKeyLookup(completedEventKeys, completedEventLookup, "completed event key");
        RebuildKeyLookup(completedMainEventKeys, completedMainEventLookup, "completed main event key");
        RebuildKeyLookup(completedSubEventKeys, completedSubEventLookup, "completed sub event key");
        RebuildKeyLookup(completedHeroUnionChatKeys, completedHeroUnionChatLookup, "completed hero union chat key");

        for (int i = 0; i < partyWorldStates.Count; i++)
        {
            PartyWorldState state = partyWorldStates[i];
            if (state == null || !IsValidKey(state.PlacementKey))
                continue;

            string key = NormalizeKey(state.PlacementKey);
            if (partyWorldLookup.ContainsKey(key))
            {
                Debug.LogWarning($"MapProgressRepository has duplicate party placementKey '{key}'.", this);
                continue;
            }

            partyWorldLookup.Add(key, state);
        }

        for (int i = 0; i < enemyWorldStates.Count; i++)
        {
            EnemyWorldState state = enemyWorldStates[i];
            if (state == null || !IsValidKey(state.PlacementKey))
                continue;

            string key = NormalizeKey(state.PlacementKey);
            if (enemyWorldLookup.ContainsKey(key))
            {
                Debug.LogWarning($"MapProgressRepository has duplicate enemy placementKey '{key}'.", this);
                continue;
            }

            enemyWorldLookup.Add(key, state);
        }

        for (int i = 0; i < outpostStates.Count; i++)
        {
            OutpostProgressState state = outpostStates[i];
            if (state == null || !IsValidKey(state.OutpostKey))
                continue;

            string key = NormalizeKey(state.OutpostKey);
            if (outpostStateLookup.ContainsKey(key))
            {
                Debug.LogWarning($"MapProgressRepository has duplicate outpostKey '{key}'.", this);
                continue;
            }

            outpostStateLookup.Add(key, state);
        }


        for (int i = 0; i < heroUnionStates.Count; i++)
        {
            HeroUnionProgressState state = heroUnionStates[i];
            if (state == null || !IsValidKey(state.ProgressKey))
                continue;

            string key = NormalizeKey(state.ProgressKey);
            if (heroUnionStateLookup.ContainsKey(key))
            {
                Debug.LogWarning($"MapProgressRepository has duplicate hero union progress key '{key}'.", this);
                continue;
            }

            heroUnionStateLookup.Add(key, state);
        }
        for (int i = 0; i < levelZoneSelections.Count; i++)
        {
            LevelZoneSelectionState state = levelZoneSelections[i];
            if (state == null || !IsValidKey(state.SelectionKey))
                continue;

            string key = NormalizeKey(state.SelectionKey);
            if (levelZoneSelectionLookup.ContainsKey(key))
            {
                Debug.LogWarning($"MapProgressRepository has duplicate level zone selection key '{key}'.", this);
                continue;
            }

            levelZoneSelectionLookup.Add(key, state);
        }

        for (int i = 0; i < gateStates.Count; i++)
        {
            GateProgressState state = gateStates[i];
            if (state == null || !IsValidKey(state.GateId))
                continue;

            string key = NormalizeKey(state.GateId);
            if (gateStateLookup.ContainsKey(key))
            {
                Debug.LogWarning($"MapProgressRepository has duplicate gate id '{key}'.", this);
                continue;
            }

            gateStateLookup.Add(key, state);
        }

        for (int i = 0; i < zoneThreatStates.Count; i++)
        {
            ZoneThreatProgressState state = zoneThreatStates[i];
            if (state == null || !IsValidKey(state.ZoneId))
                continue;

            string key = NormalizeKey(state.ZoneId);
            if (zoneThreatStateLookup.ContainsKey(key))
            {
                Debug.LogWarning($"MapProgressRepository has duplicate zone threat id '{key}'.", this);
                continue;
            }

            zoneThreatStateLookup.Add(key, state);
        }
        for (int i = 0; i < zoneEnemyLevelStates.Count; i++)
        {
            ZoneEnemyLevelState state = zoneEnemyLevelStates[i];
            if (state == null || !IsValidKey(state.ZoneId))
                continue;

            string key = NormalizeKey(state.ZoneId);
            if (zoneEnemyLevelStateLookup.ContainsKey(key))
            {
                Debug.LogWarning($"MapProgressRepository has duplicate zone enemy level id '{key}'.", this);
                continue;
            }

            zoneEnemyLevelStateLookup.Add(key, state);
        }
    }

    private void RebuildKeyLookup(List<string> source, HashSet<string> target, string label)
    {
        for (int i = 0; i < source.Count; i++)
        {
            if (!IsValidKey(source[i]))
                continue;

            string key = NormalizeKey(source[i]);
            if (!target.Add(key))
                Debug.LogWarning($"MapProgressRepository has duplicate {label} '{key}'.", this);
        }
    }

    private static void AddUniqueKey(string key, List<string> source, HashSet<string> lookup)
    {
        if (!IsValidKey(key))
            return;

        string normalizedKey = NormalizeKey(key);
        if (!lookup.Add(normalizedKey))
            return;

        source.Add(normalizedKey);
    }

    private static void ReplaceList<T>(List<T> target, IEnumerable<T> source)
    {
        target.Clear();
        if (source == null)
            return;

        foreach (T item in source)
            target.Add(item);
    }
    private static bool IsValidKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key);
    }

    private static string NormalizeKey(string key)
    {
        return MapProgressKey.NormalizeSegment(key);
    }

    private void EnsureZoneEntryGuidanceState()
    {
        if (zoneEntryGuidanceState == null)
            zoneEntryGuidanceState = new ZoneEntryGuidanceProgressState();
    }
}
