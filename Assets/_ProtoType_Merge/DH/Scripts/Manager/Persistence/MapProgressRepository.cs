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
    [SerializeField] private List<PartyWorldState> partyWorldStates = new List<PartyWorldState>();
    [SerializeField] private List<EnemyWorldState> enemyWorldStates = new List<EnemyWorldState>();
    [SerializeField] private List<OutpostProgressState> outpostStates = new List<OutpostProgressState>();
    [SerializeField] private List<FogProgressCell> fogCells = new List<FogProgressCell>();
    [SerializeField] private List<LevelZoneSelectionState> levelZoneSelections = new List<LevelZoneSelectionState>();

    private readonly HashSet<string> collectedItemLookup = new HashSet<string>();
    private readonly HashSet<string> completedEventLookup = new HashSet<string>();
    private readonly Dictionary<string, PartyWorldState> partyWorldLookup = new Dictionary<string, PartyWorldState>();
    private readonly Dictionary<string, EnemyWorldState> enemyWorldLookup = new Dictionary<string, EnemyWorldState>();
    private readonly Dictionary<string, OutpostProgressState> outpostStateLookup = new Dictionary<string, OutpostProgressState>();
    private readonly Dictionary<string, LevelZoneSelectionState> levelZoneSelectionLookup = new Dictionary<string, LevelZoneSelectionState>();

    public string MapId => mapId;
    public IReadOnlyList<string> CollectedItemKeys => collectedItemKeys;
    public IReadOnlyList<string> CompletedEventKeys => completedEventKeys;
    public IReadOnlyList<PartyWorldState> PartyWorldStates => partyWorldStates;
    public IReadOnlyList<EnemyWorldState> EnemyWorldStates => enemyWorldStates;
    public IReadOnlyList<OutpostProgressState> OutpostStates => outpostStates;
    public IReadOnlyList<FogProgressCell> FogCells => fogCells;
    public IReadOnlyList<LevelZoneSelectionState> LevelZoneSelections => levelZoneSelections;

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
        int enemyDefenderGroupIndex,
        string defenderEnemyId)
    {
        if (!IsValidKey(outpostKey))
            return;

        OutpostProgressState progressState = GetOrCreateOutpostState(NormalizeKey(outpostKey), state);
        progressState.SetState(state);
        progressState.SetEnemyDefender(enemyDefenderGroupIndex, defenderEnemyId);
    }

    public void SetOutpostDefender(string outpostKey, int enemyDefenderGroupIndex, string defenderEnemyId)
    {
        if (!IsValidKey(outpostKey))
            return;

        OutpostProgressState progressState = GetOrCreateOutpostState(NormalizeKey(outpostKey), OutpostState.EnemyClaimed);
        progressState.SetEnemyDefender(enemyDefenderGroupIndex, defenderEnemyId);
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
        if (!IsValidKey(placementKey) || string.IsNullOrWhiteSpace(enemyId))
            return;

        EnemyWorldState state = GetOrCreateEnemyState(NormalizeKey(placementKey), enemyId, grid, placementSource, prefabKey);
        state.SetEnemyId(enemyId);
        state.SetGrid(grid);
        state.SetPlacementSource(placementSource);
        state.SetPrefabKey(prefabKey);
        state.SetDefeated(false);
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

    public void ClearAllProgress()
    {
        collectedItemKeys.Clear();
        completedEventKeys.Clear();
        partyWorldStates.Clear();
        enemyWorldStates.Clear();
        outpostStates.Clear();
        fogCells.Clear();
        levelZoneSelections.Clear();
        RebuildLookups();
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
        if (enemyWorldLookup.TryGetValue(placementKey, out EnemyWorldState state))
            return state;

        state = new EnemyWorldState(placementKey, enemyId, grid, placementSource, prefabKey);
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
        collectedItemLookup.Clear();
        completedEventLookup.Clear();
        partyWorldLookup.Clear();
        enemyWorldLookup.Clear();
        outpostStateLookup.Clear();
        levelZoneSelectionLookup.Clear();

        RebuildKeyLookup(collectedItemKeys, collectedItemLookup, "collected item key");
        RebuildKeyLookup(completedEventKeys, completedEventLookup, "completed event key");

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

    private static bool IsValidKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key);
    }

    private static string NormalizeKey(string key)
    {
        return MapProgressKey.NormalizeSegment(key);
    }
}
