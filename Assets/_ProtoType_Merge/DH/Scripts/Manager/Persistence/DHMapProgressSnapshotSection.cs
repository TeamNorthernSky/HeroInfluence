using System.Collections.Generic;
using UnityEngine;

public sealed class DHMapProgressSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private string mapId;
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

    public override void CaptureFromRuntime()
    {
        ClearSnapshot();

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository == null)
            return;

        mapId = repository.MapId;
        CopyStrings(repository.CollectedItemKeys, collectedItemKeys);
        CopyStrings(repository.CompletedEventKeys, completedEventKeys);
        CopyStrings(repository.CompletedMainEventKeys, completedMainEventKeys);
        CopyStrings(repository.CompletedSubEventKeys, completedSubEventKeys);
        CopyStrings(repository.CompletedHeroUnionChatKeys, completedHeroUnionChatKeys);
        CopyPartyWorldStates(repository.PartyWorldStates, partyWorldStates);
        CopyEnemyWorldStates(repository.EnemyWorldStates, enemyWorldStates);
        CopyOutpostStates(repository.OutpostStates, outpostStates);
        CopyFogCells(repository.FogCells, fogCells);
        CopyLevelZoneSelections(repository.LevelZoneSelections, levelZoneSelections);
        CopyHeroUnionStates(repository.HeroUnionStates, heroUnionStates);
        CopyGateStates(repository.GateStates, gateStates);
        CopyZoneThreatStates(repository.ZoneThreatStates, zoneThreatStates);
        CopyZoneEnemyLevelStates(repository.ZoneEnemyLevelStates, zoneEnemyLevelStates);
        zoneEntryGuidanceState = CopyZoneEntryGuidanceState(repository.ZoneEntryGuidanceState);
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.mapId = mapId;
        ReplaceStrings(data.collectedItemKeys, collectedItemKeys);
        ReplaceStrings(data.completedEventKeys, completedEventKeys);
        ReplaceStrings(data.completedMainEventKeys, completedMainEventKeys);
        ReplaceStrings(data.completedSubEventKeys, completedSubEventKeys);
        ReplaceStrings(data.completedHeroUnionChatKeys, completedHeroUnionChatKeys);
        ReplacePartyWorldStates(data.partyWorldStates, partyWorldStates);
        ReplaceEnemyWorldStates(data.enemyWorldStates, enemyWorldStates);
        ReplaceOutpostStates(data.outpostStates, outpostStates);
        ReplaceFogCells(data.fogCells, fogCells);
        ReplaceLevelZoneSelections(data.levelZoneSelections, levelZoneSelections);
        ReplaceHeroUnionStates(data.heroUnionStates, heroUnionStates);
        ReplaceGateStates(data.gateStates, gateStates);
        ReplaceZoneThreatStates(data.zoneThreatStates, zoneThreatStates);
        ReplaceZoneEnemyLevelStates(data.zoneEnemyLevelStates, zoneEnemyLevelStates);
        data.zoneEntryGuidanceState = CopyZoneEntryGuidanceState(zoneEntryGuidanceState);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        ClearSnapshot();
        if (data == null)
            return;

        mapId = data.mapId;
        CopyStrings(data.collectedItemKeys, collectedItemKeys);
        CopyStrings(data.completedEventKeys, completedEventKeys);
        CopyStrings(data.completedMainEventKeys, completedMainEventKeys);
        CopyStrings(data.completedSubEventKeys, completedSubEventKeys);
        CopyStrings(data.completedHeroUnionChatKeys, completedHeroUnionChatKeys);
        CopyPartyWorldStates(data.partyWorldStates, partyWorldStates);
        CopyEnemyWorldStates(data.enemyWorldStates, enemyWorldStates);
        CopyOutpostStates(data.outpostStates, outpostStates);
        CopyFogCells(data.fogCells, fogCells);
        CopyLevelZoneSelections(data.levelZoneSelections, levelZoneSelections);
        CopyHeroUnionStates(data.heroUnionStates, heroUnionStates);
        CopyGateStates(data.gateStates, gateStates);
        CopyZoneThreatStates(data.zoneThreatStates, zoneThreatStates);
        CopyZoneEnemyLevelStates(data.zoneEnemyLevelStates, zoneEnemyLevelStates);
        zoneEntryGuidanceState = CopyZoneEntryGuidanceState(data.zoneEntryGuidanceState);
    }

    public override void ClearSnapshot()
    {
        mapId = string.Empty;
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
    }

    private static void ReplaceStrings(List<string> target, IReadOnlyList<string> source)
    {
        target.Clear();
        CopyStrings(source, target);
    }

    private static void CopyStrings(IReadOnlyList<string> source, List<string> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
            target.Add(source[i]);
    }

    private static void ReplacePartyWorldStates(List<PartyWorldState> target, IReadOnlyList<PartyWorldState> source)
    {
        target.Clear();
        CopyPartyWorldStates(source, target);
    }

    private static void CopyPartyWorldStates(IReadOnlyList<PartyWorldState> source, List<PartyWorldState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            PartyWorldState state = source[i];
            if (state == null)
                continue;

            PartyWorldState copy = new PartyWorldState(
                state.PlacementKey,
                state.PartyId,
                state.Grid,
                state.PlacementSource,
                state.PrefabKey);
            copy.SetRemoved(state.Removed);
            target.Add(copy);
        }
    }

    private static void ReplaceEnemyWorldStates(List<EnemyWorldState> target, IReadOnlyList<EnemyWorldState> source)
    {
        target.Clear();
        CopyEnemyWorldStates(source, target);
    }

    private static void CopyEnemyWorldStates(IReadOnlyList<EnemyWorldState> source, List<EnemyWorldState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            EnemyWorldState state = source[i];
            if (state == null)
                continue;

            EnemyWorldState copy = new EnemyWorldState(
                state.PlacementKey,
                state.EnemyId,
                state.Grid,
                state.PlacementSource,
                state.PrefabKey,
                state.ZoneId);
            copy.SetDefeated(state.Defeated);
            target.Add(copy);
        }
    }

    private static void ReplaceOutpostStates(List<OutpostProgressState> target, IReadOnlyList<OutpostProgressState> source)
    {
        target.Clear();
        CopyOutpostStates(source, target);
    }

    private static void CopyOutpostStates(IReadOnlyList<OutpostProgressState> source, List<OutpostProgressState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            OutpostProgressState state = source[i];
            if (state != null)
                target.Add(new OutpostProgressState(state.OutpostKey, state.State, state.EnemyDefenderGroupKey, state.DefenderEnemyId));
        }
    }

    private static void ReplaceFogCells(List<FogProgressCell> target, IReadOnlyList<FogProgressCell> source)
    {
        target.Clear();
        CopyFogCells(source, target);
    }

    private static void CopyFogCells(IReadOnlyList<FogProgressCell> source, List<FogProgressCell> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            FogProgressCell cell = source[i];
            if (cell != null)
                target.Add(new FogProgressCell(cell.Grid, cell.Visibility, cell.LastRevealedDay));
        }
    }

    private static void ReplaceLevelZoneSelections(List<LevelZoneSelectionState> target, IReadOnlyList<LevelZoneSelectionState> source)
    {
        target.Clear();
        CopyLevelZoneSelections(source, target);
    }

    private static void CopyLevelZoneSelections(IReadOnlyList<LevelZoneSelectionState> source, List<LevelZoneSelectionState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            LevelZoneSelectionState state = source[i];
            if (state != null)
                target.Add(new LevelZoneSelectionState(state.LayoutId, state.ZoneId, state.SelectedCandidateIndex));
        }
    }

    private static void ReplaceHeroUnionStates(List<HeroUnionProgressState> target, IReadOnlyList<HeroUnionProgressState> source)
    {
        target.Clear();
        CopyHeroUnionStates(source, target);
    }

    private static void CopyHeroUnionStates(IReadOnlyList<HeroUnionProgressState> source, List<HeroUnionProgressState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            HeroUnionProgressState state = source[i];
            if (state != null)
                target.Add(new HeroUnionProgressState(state.ZoneId, state.State));
        }
    }

    private static void ReplaceGateStates(List<GateProgressState> target, IReadOnlyList<GateProgressState> source)
    {
        target.Clear();
        CopyGateStates(source, target);
    }

    private static void CopyGateStates(IReadOnlyList<GateProgressState> source, List<GateProgressState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            GateProgressState state = source[i];
            if (state != null)
                target.Add(new GateProgressState(state.GateId, state.Open, state.OpenedDay));
        }
    }

    private static void ReplaceZoneThreatStates(List<ZoneThreatProgressState> target, IReadOnlyList<ZoneThreatProgressState> source)
    {
        target.Clear();
        CopyZoneThreatStates(source, target);
    }

    private static void CopyZoneThreatStates(IReadOnlyList<ZoneThreatProgressState> source, List<ZoneThreatProgressState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            ZoneThreatProgressState state = source[i];
            if (state != null)
                target.Add(new ZoneThreatProgressState(
                    state.ZoneId,
                    state.Active,
                    state.EnteredDay,
                    state.AccumulatedTurns,
                    state.LastEvaluatedDay,
                    state.ActiveEnemyPlacementKey));
        }
    }

    private static void ReplaceZoneEnemyLevelStates(List<ZoneEnemyLevelState> target, IReadOnlyList<ZoneEnemyLevelState> source)
    {
        target.Clear();
        CopyZoneEnemyLevelStates(source, target);
    }

    private static void CopyZoneEnemyLevelStates(IReadOnlyList<ZoneEnemyLevelState> source, List<ZoneEnemyLevelState> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            ZoneEnemyLevelState state = source[i];
            if (state != null && state.Initialized)
                target.Add(new ZoneEnemyLevelState(state.ZoneId, state.EnemyLevel));
        }
    }

    private static ZoneEntryGuidanceProgressState CopyZoneEntryGuidanceState(ZoneEntryGuidanceProgressState source)
    {
        return source != null
            ? new ZoneEntryGuidanceProgressState(source)
            : new ZoneEntryGuidanceProgressState();
    }
}
