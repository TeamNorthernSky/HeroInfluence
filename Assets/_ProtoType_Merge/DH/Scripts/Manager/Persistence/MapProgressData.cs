using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PartyWorldState
{
    public const string DefaultPrefabKey = "default";

    [SerializeField] private string placementKey;
    [SerializeField] private string partyId;
    [SerializeField] private Vector2Int grid;
    [SerializeField] private bool removed;
    [SerializeField] private PartyPlacementSource placementSource = PartyPlacementSource.Scene;
    [SerializeField] private string prefabKey = DefaultPrefabKey;

    public string PlacementKey => placementKey;
    public string PartyId => partyId;
    public Vector2Int Grid => grid;
    public bool Removed => removed;
    public PartyPlacementSource PlacementSource => placementSource;
    public string PrefabKey => prefabKey;

    public PartyWorldState(string placementKey, string partyId, Vector2Int grid)
        : this(placementKey, partyId, grid, PartyPlacementSource.Scene, DefaultPrefabKey)
    {
    }

    public PartyWorldState(
        string placementKey,
        string partyId,
        Vector2Int grid,
        PartyPlacementSource placementSource,
        string prefabKey)
    {
        this.placementKey = placementKey;
        this.partyId = partyId;
        this.grid = grid;
        this.placementSource = placementSource;
        this.prefabKey = string.IsNullOrWhiteSpace(prefabKey) ? DefaultPrefabKey : prefabKey;
        removed = false;
    }

    public void SetPartyId(string nextPartyId)
    {
        partyId = nextPartyId;
    }

    public void SetGrid(Vector2Int nextGrid)
    {
        grid = nextGrid;
    }

    public void SetRemoved(bool nextRemoved)
    {
        removed = nextRemoved;
    }

    public void SetPlacementSource(PartyPlacementSource nextPlacementSource)
    {
        placementSource = nextPlacementSource;
    }

    public void SetPrefabKey(string nextPrefabKey)
    {
        prefabKey = string.IsNullOrWhiteSpace(nextPrefabKey) ? DefaultPrefabKey : nextPrefabKey;
    }
}

[Serializable]
public class FogProgressCell
{
    [SerializeField] private Vector2Int grid;
    [SerializeField] private FogVisibilityState visibility;
    [SerializeField] private int lastRevealedDay;

    public Vector2Int Grid => grid;
    public FogVisibilityState Visibility => visibility;
    public int LastRevealedDay => lastRevealedDay;

    public FogProgressCell(Vector2Int grid, FogVisibilityState visibility, int lastRevealedDay)
    {
        this.grid = grid;
        this.visibility = visibility;
        this.lastRevealedDay = Mathf.Max(1, lastRevealedDay);
    }

    public void Apply(FogVisibilityState nextVisibility, int nextLastRevealedDay)
    {
        visibility = nextVisibility;
        lastRevealedDay = Mathf.Max(1, nextLastRevealedDay);
    }
}

[Serializable]
public class LevelZoneSelectionState
{
    [SerializeField] private string layoutId;
    [SerializeField] private string zoneId;
    [SerializeField] private int selectedCandidateIndex;

    public string LayoutId => layoutId;
    public string ZoneId => zoneId;
    public int SelectedCandidateIndex => selectedCandidateIndex;
    public string SelectionKey => BuildSelectionKey(layoutId, zoneId);

    public LevelZoneSelectionState(string layoutId, string zoneId, int selectedCandidateIndex)
    {
        this.layoutId = MapProgressKey.NormalizeSegment(layoutId);
        this.zoneId = MapProgressKey.NormalizeSegment(zoneId);
        this.selectedCandidateIndex = Mathf.Max(0, selectedCandidateIndex);
    }

    public void SetSelectedCandidateIndex(int nextSelectedCandidateIndex)
    {
        selectedCandidateIndex = Mathf.Max(0, nextSelectedCandidateIndex);
    }

    public static string BuildSelectionKey(string layoutId, string zoneId)
    {
        string normalizedLayoutId = MapProgressKey.NormalizeSegment(layoutId);
        string normalizedZoneId = MapProgressKey.NormalizeSegment(zoneId);
        if (string.IsNullOrWhiteSpace(normalizedLayoutId) || string.IsNullOrWhiteSpace(normalizedZoneId))
            return string.Empty;

        return $"{normalizedLayoutId}_{normalizedZoneId}";
    }
}

[Serializable]
public class HeroUnionProgressState
{
    [SerializeField] private string zoneId;
    [SerializeField] private HeroUnionState state;

    public string ZoneId => zoneId;
    public HeroUnionState State => state;
    public string ProgressKey => BuildProgressKey(zoneId);

    public HeroUnionProgressState(string zoneId, HeroUnionState state)
    {
        this.zoneId = NormalizeZoneId(zoneId);
        this.state = state;
    }

    public void SetState(HeroUnionState nextState)
    {
        state = nextState;
    }

    public static string BuildProgressKey(string zoneId)
    {
        string normalizedZoneId = NormalizeZoneId(zoneId);
        return string.IsNullOrWhiteSpace(normalizedZoneId) ? string.Empty : $"hero_union_{normalizedZoneId}";
    }

    private static string NormalizeZoneId(string zoneId)
    {
        string normalized = MapProgressKey.NormalizeSegment(zoneId);
        return string.IsNullOrWhiteSpace(normalized) ? "zone_001" : normalized;
    }
}

[Serializable]
public class GateProgressState
{
    [SerializeField] private string gateId;
    [SerializeField] private bool open;
    [SerializeField] private int openedDay;

    public string GateId => MapProgressKey.NormalizeSegment(gateId);
    public bool Open => open;
    public int OpenedDay => Mathf.Max(1, openedDay);

    public GateProgressState(string gateId, bool open, int openedDay)
    {
        this.gateId = MapProgressKey.NormalizeSegment(gateId);
        this.open = open;
        this.openedDay = Mathf.Max(1, openedDay);
    }

    public void Apply(bool nextOpen, int nextOpenedDay)
    {
        open = nextOpen;
        openedDay = Mathf.Max(1, nextOpenedDay);
    }
}

[Serializable]
public class ZoneThreatProgressState
{
    [SerializeField] private string zoneId;
    [SerializeField] private bool active;
    [SerializeField] private int enteredDay;
    [SerializeField] private string activeEnemyPlacementKey;

    public string ZoneId => MapProgressKey.NormalizeSegment(zoneId);
    public bool Active => active;
    public int EnteredDay => Mathf.Max(1, enteredDay);
    public string ActiveEnemyPlacementKey => MapProgressKey.NormalizeSegment(activeEnemyPlacementKey);

    public ZoneThreatProgressState(string zoneId, bool active, int enteredDay, string activeEnemyPlacementKey)
    {
        this.zoneId = MapProgressKey.NormalizeSegment(zoneId);
        this.active = active;
        this.enteredDay = Mathf.Max(1, enteredDay);
        this.activeEnemyPlacementKey = MapProgressKey.NormalizeSegment(activeEnemyPlacementKey);
    }

    public void Begin(int nextEnteredDay)
    {
        active = true;
        enteredDay = Mathf.Max(1, nextEnteredDay);
        activeEnemyPlacementKey = string.Empty;
    }

    public void End()
    {
        active = false;
        activeEnemyPlacementKey = string.Empty;
    }

    public void SetActiveEnemy(string placementKey)
    {
        activeEnemyPlacementKey = MapProgressKey.NormalizeSegment(placementKey);
    }

    public void ClearActiveEnemy()
    {
        activeEnemyPlacementKey = string.Empty;
    }
}

[Serializable]
public class ZoneEnemyLevelState
{
    [SerializeField] private string zoneId;
    [SerializeField] private bool initialized;
    [SerializeField] private int enemyLevel;

    public string ZoneId => MapProgressKey.NormalizeSegment(zoneId);
    public bool Initialized => initialized;
    public int EnemyLevel => Mathf.Max(1, enemyLevel);

    public ZoneEnemyLevelState(string zoneId, int enemyLevel)
    {
        this.zoneId = MapProgressKey.NormalizeSegment(zoneId);
        initialized = true;
        this.enemyLevel = Mathf.Max(1, enemyLevel);
    }

    public void Initialize(int nextEnemyLevel)
    {
        if (initialized)
            return;

        initialized = true;
        enemyLevel = Mathf.Max(1, nextEnemyLevel);
    }
}

[Serializable]
public class ZoneEntryGuidanceProgressState
{
    [SerializeField] private bool active;
    [SerializeField] private string zoneId;
    [SerializeField] private string requiredHeroUnionId;
    [SerializeField] private List<Vector2Int> allowedPathCells = new List<Vector2Int>();
    [SerializeField] private List<string> completedZoneIds = new List<string>();

    public bool Active => active;
    public string ZoneId => MapProgressKey.NormalizeSegment(zoneId);
    public string RequiredHeroUnionId => MapProgressKey.NormalizeSegment(requiredHeroUnionId);
    public IReadOnlyList<Vector2Int> AllowedPathCells => allowedPathCells;
    public IReadOnlyList<string> CompletedZoneIds => completedZoneIds;

    public ZoneEntryGuidanceProgressState()
    {
        active = false;
        zoneId = string.Empty;
        requiredHeroUnionId = string.Empty;
    }

    public ZoneEntryGuidanceProgressState(ZoneEntryGuidanceProgressState source)
        : this()
    {
        if (source == null)
            return;

        active = source.Active;
        zoneId = source.ZoneId;
        requiredHeroUnionId = source.RequiredHeroUnionId;
        ReplaceAllowedPathCells(source.AllowedPathCells);
        ReplaceCompletedZoneIds(source.CompletedZoneIds);
    }

    public void Begin(string nextZoneId, string nextRequiredHeroUnionId, IReadOnlyList<Vector2Int> nextAllowedPathCells)
    {
        zoneId = MapProgressKey.NormalizeSegment(nextZoneId);
        requiredHeroUnionId = MapProgressKey.NormalizeSegment(nextRequiredHeroUnionId);
        ReplaceAllowedPathCells(nextAllowedPathCells);
        active = !string.IsNullOrWhiteSpace(zoneId) && allowedPathCells.Count > 0;
    }

    public void CompleteActive()
    {
        if (!string.IsNullOrWhiteSpace(ZoneId))
            AddCompletedZone(ZoneId);

        ClearActive();
    }

    public void ClearActive()
    {
        active = false;
        zoneId = string.Empty;
        requiredHeroUnionId = string.Empty;
        allowedPathCells.Clear();
    }

    public bool IsZoneCompleted(string candidateZoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(candidateZoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId))
            return false;

        for (int i = 0; i < completedZoneIds.Count; i++)
        {
            if (string.Equals(completedZoneIds[i], normalizedZoneId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public void AddCompletedZone(string completedZoneId)
    {
        string normalizedZoneId = MapProgressKey.NormalizeSegment(completedZoneId);
        if (string.IsNullOrWhiteSpace(normalizedZoneId) || IsZoneCompleted(normalizedZoneId))
            return;

        completedZoneIds.Add(normalizedZoneId);
    }

    public bool ContainsAllowedPathCell(Vector2Int grid)
    {
        for (int i = 0; i < allowedPathCells.Count; i++)
        {
            if (allowedPathCells[i] == grid)
                return true;
        }

        return false;
    }

    private void ReplaceAllowedPathCells(IReadOnlyList<Vector2Int> source)
    {
        allowedPathCells.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            Vector2Int cell = source[i];
            if (!allowedPathCells.Contains(cell))
                allowedPathCells.Add(cell);
        }
    }

    private void ReplaceCompletedZoneIds(IReadOnlyList<string> source)
    {
        completedZoneIds.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            AddCompletedZone(source[i]);
    }
}

[Serializable]
public class EnemyWorldState
{
    public const string DefaultPrefabKey = "default";

    [SerializeField] private string placementKey;
    [SerializeField] private string enemyId;
    [SerializeField] private Vector2Int grid;
    [SerializeField] private bool defeated;
    [SerializeField] private EnemyPlacementSource placementSource = EnemyPlacementSource.Scene;
    [SerializeField] private string prefabKey = DefaultPrefabKey;
    [SerializeField] private string zoneId;

    public string PlacementKey => placementKey;
    public string EnemyId => enemyId;
    public Vector2Int Grid => grid;
    public bool Defeated => defeated;
    public EnemyPlacementSource PlacementSource => placementSource;
    public string PrefabKey => prefabKey;
    public string ZoneId => MapProgressKey.NormalizeSegment(zoneId);

    public EnemyWorldState(string placementKey, string enemyId, Vector2Int grid)
        : this(placementKey, enemyId, grid, EnemyPlacementSource.Scene, DefaultPrefabKey, string.Empty)
    {
    }

    public EnemyWorldState(
        string placementKey,
        string enemyId,
        Vector2Int grid,
        EnemyPlacementSource placementSource,
        string prefabKey)
        : this(placementKey, enemyId, grid, placementSource, prefabKey, string.Empty)
    {
    }

    public EnemyWorldState(
        string placementKey,
        string enemyId,
        Vector2Int grid,
        EnemyPlacementSource placementSource,
        string prefabKey,
        string zoneId)
    {
        this.placementKey = placementKey;
        this.enemyId = enemyId;
        this.grid = grid;
        this.placementSource = placementSource;
        this.prefabKey = string.IsNullOrWhiteSpace(prefabKey) ? DefaultPrefabKey : prefabKey;
        this.zoneId = MapProgressKey.NormalizeSegment(zoneId);
        defeated = false;
    }

    public void SetEnemyId(string nextEnemyId)
    {
        enemyId = nextEnemyId;
    }

    public void SetGrid(Vector2Int nextGrid)
    {
        grid = nextGrid;
    }

    public void SetDefeated(bool nextDefeated)
    {
        defeated = nextDefeated;
    }

    public void SetPlacementSource(EnemyPlacementSource nextPlacementSource)
    {
        placementSource = nextPlacementSource;
    }

    public void SetPrefabKey(string nextPrefabKey)
    {
        prefabKey = string.IsNullOrWhiteSpace(nextPrefabKey) ? DefaultPrefabKey : nextPrefabKey;
    }

    public void SetZoneId(string nextZoneId)
    {
        zoneId = MapProgressKey.NormalizeSegment(nextZoneId);
    }
}
[Serializable]
public class OutpostProgressState
{
    [SerializeField] private string outpostKey;
    [SerializeField] private OutpostState state;
    [SerializeField] private int enemyDefenderGroupIndex;
    [SerializeField] private string defenderEnemyId;

    public string OutpostKey => outpostKey;
    public OutpostState State => state;
    public int EnemyDefenderGroupIndex => enemyDefenderGroupIndex;
    public string DefenderEnemyId => defenderEnemyId;

    public OutpostProgressState(string outpostKey, OutpostState state)
        : this(outpostKey, state, 0, string.Empty)
    {
    }

    public OutpostProgressState(
        string outpostKey,
        OutpostState state,
        int enemyDefenderGroupIndex,
        string defenderEnemyId)
    {
        this.outpostKey = outpostKey;
        this.state = state;
        this.enemyDefenderGroupIndex = Mathf.Max(0, enemyDefenderGroupIndex);
        this.defenderEnemyId = string.IsNullOrWhiteSpace(defenderEnemyId) ? string.Empty : defenderEnemyId;
    }

    public void SetState(OutpostState nextState)
    {
        state = nextState;
    }

    public void SetEnemyDefender(int groupIndex, string enemyId)
    {
        enemyDefenderGroupIndex = Mathf.Max(0, groupIndex);
        defenderEnemyId = string.IsNullOrWhiteSpace(enemyId) ? string.Empty : enemyId;
    }
}
