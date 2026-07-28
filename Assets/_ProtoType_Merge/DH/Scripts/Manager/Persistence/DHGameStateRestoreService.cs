using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public static class DHGameStateRestoreService
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool IsRestoring { get; private set; }

    public static void BeginRestoreSession()
    {
        IsRestoring = true;
        DHTurnStartSnapshotStore.SetCaptureSuppressed(true);
        DHTurnStartSnapshotStore.EnsureInstance().ClearSnapshot();
    }

    public static void CompleteRestoreSession(bool captureTurnStartSnapshot = true)
    {
        IsRestoring = false;
        DHTurnStartSnapshotStore.SetCaptureSuppressed(false);

        if (captureTurnStartSnapshot)
            DHTurnStartSnapshotStore.CaptureTurnStartSnapshot();
    }

    public static void RestoreFromGameSaveData(GameSaveData data)
    {
        DHTurnStartSnapshotStore.EnsureInstance().LoadFromGameSaveData(data);
        RestoreEventState(data);
        RestoreRepositories(data);
        RestoreGlobalState(data);
    }

    private static void RestoreEventState(GameSaveData data)
    {
        DHEventStateRepository repository = DHEventStateRepository.EnsureInstance();
        repository.RestoreFlagSnapshot(data != null ? data.chatFlags : null);
        repository.RestoreNumericSnapshot(data != null ? data.eventNumericStates : null);
    }

    public static void RestoreRepositories(GameSaveData data)
    {
        if (data == null)
            return;

        PersistentUnitRepository.Instance?.RestoreFromSave(data.nextUnitIndex, data.units);
        PersistentEnemyRepository.Instance?.RestoreFromSave(data.nextEnemyUnitIndex, data.enemyUnits);
        EnemyGroupPersistentRepository.Instance?.RestoreFromSave(data.nextEnemySequence, data.enemyGroups);
        WeaponPersistentRepository.Instance?.RestoreFromSave(data.nextWeaponIndex, data.weapons);
        PartyPersistentRepository.Instance?.RestoreFromSave(data.parties);

        MapProgressRepository.Instance?.RestoreFromSave(
            data.mapId,
            data.collectedItemKeys,
            data.completedEventKeys,
            data.completedMainEventKeys,
            data.completedSubEventKeys,
            data.completedHeroUnionChatKeys,
            data.partyWorldStates,
            data.enemyWorldStates,
            data.outpostStates,
            data.fogCells,
            data.levelZoneSelections,
            data.heroUnionStates,
            data.gateStates,
            data.zoneThreatStates,
            data.zoneEnemyLevelStates,
            data.zoneEntryGuidanceState);
    }

    public static void RestoreGlobalState(GameSaveData data)
    {
        if (data == null)
            return;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        RestoreCurrentDay(gameManager, data.currentDay);
        RestoreEconomy(gameManager.Economy, data.resources);
        RestoreHQ(gameManager.HQ, data.hqLevels, data.hqUpgradedThisTurn);
        RestoreLab(gameManager.Lab, data.labSkillLevels);
        RestorePublicity(gameManager.Publicity, data.publicityCurrentPool, data.publicityLastChargeDay);
        RestoreTraining(gameManager.Training, data.trainingEntries);
        RestoreInfirmary(gameManager.Infirmary, data.infirmaryHealedThisTurn);
        RestoreWorkshop(gameManager.Workshop, data.workshopWeaponEntries);
        RestoreHQVisits(data.hqVisitSources);
    }

    private static void RestoreCurrentDay(GameManager gameManager, int currentDay)
    {
        SetPrivateField(gameManager, "currentDay", Mathf.Max(1, currentDay));
    }

    private static void RestoreEconomy(EconomyManager economy, IReadOnlyList<GameSaveData.ResourceEntry> resources)
    {
        if (economy == null)
            return;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            economy.Set(type, 0);

        if (resources == null)
            return;

        for (int i = 0; i < resources.Count; i++)
            economy.Set(resources[i].type, resources[i].amount);
    }

    private static void RestoreHQ(HQStateManager hq, IReadOnlyList<GameSaveData.HQLevelEntry> hqLevels, bool upgradedThisTurn)
    {
        if (hq == null)
            return;

        hq.Initialize();

        Dictionary<HQDepartment, int> levels = GetPrivateField<Dictionary<HQDepartment, int>>(hq, "levels");
        if (levels != null && hqLevels != null)
        {
            for (int i = 0; i < hqLevels.Count; i++)
                levels[hqLevels[i].department] = Mathf.Max(0, hqLevels[i].level);
        }

        SetPrivateField(hq, "upgradedThisTurn", upgradedThisTurn);
        InvokeEvent(hq, "OnStateChanged");
    }

    private static void RestoreLab(LabManager lab, IReadOnlyList<LabManager.SkillLevelEntry> savedEntries)
    {
        if (lab == null)
            return;

        List<LabManager.SkillLevelEntry> entries = GetPrivateField<List<LabManager.SkillLevelEntry>>(lab, "entries");
        if (entries == null)
            return;

        entries.Clear();
        if (savedEntries != null)
        {
            for (int i = 0; i < savedEntries.Count; i++)
            {
                LabManager.SkillLevelEntry entry = savedEntries[i];
                if (entry == null)
                    continue;

                entries.Add(new LabManager.SkillLevelEntry
                {
                    unitIndex = entry.unitIndex,
                    skillIndex = entry.skillIndex,
                    level = Mathf.Clamp(entry.level, LabManager.BaseSkillLevel, LabManager.MaxSkillLevel)
                });
            }
        }

        lab.Initialize();
        InvokeEvent(lab, "OnStateChanged");
    }

    private static void RestorePublicity(PublicityManager publicity, int currentPool, int lastChargeDay)
    {
        if (publicity == null)
            return;

        SetPrivateField(publicity, "currentPool", Mathf.Max(0, currentPool));
        SetPrivateField(publicity, "lastChargeDay", Mathf.Max(0, lastChargeDay));
        InvokeEvent(publicity, "OnStateChanged");
    }

    private static void RestoreTraining(TrainingManager training, IReadOnlyList<TrainingManager.TrainingEntry> savedEntries)
    {
        if (training == null)
            return;

        List<TrainingManager.TrainingEntry> entries = GetPrivateField<List<TrainingManager.TrainingEntry>>(training, "entries");
        if (entries == null)
            return;

        entries.Clear();
        if (savedEntries != null)
        {
            for (int i = 0; i < savedEntries.Count; i++)
            {
                TrainingManager.TrainingEntry entry = savedEntries[i];
                if (entry == null)
                    continue;

                entries.Add(new TrainingManager.TrainingEntry
                {
                    unitIndex = entry.unitIndex,
                    atkLevel = Mathf.Clamp(entry.atkLevel, 0, TrainingManager.MaxTrainingLevel),
                    hpLevel = Mathf.Clamp(entry.hpLevel, 0, TrainingManager.MaxTrainingLevel)
                });
            }
        }

        training.Initialize();
        InvokeEvent(training, "OnStateChanged");
    }

    private static void RestoreInfirmary(InfirmaryManager infirmary, IReadOnlyList<int> healedUnitsThisTurn)
    {
        if (infirmary == null)
            return;

        HashSet<int> healedThisTurn = GetPrivateField<HashSet<int>>(infirmary, "healedThisTurn");
        if (healedThisTurn == null)
            return;

        healedThisTurn.Clear();
        if (healedUnitsThisTurn != null)
        {
            for (int i = 0; i < healedUnitsThisTurn.Count; i++)
            {
                int unitIndex = healedUnitsThisTurn[i];
                if (unitIndex > 0)
                    healedThisTurn.Add(unitIndex);
            }
        }

        InvokeEvent(infirmary, "OnStateChanged");
    }

    private static void RestoreWorkshop(WorkshopManager workshop, IReadOnlyList<WorkshopManager.WeaponEntry> savedEntries)
    {
        if (workshop == null)
            return;

        List<WorkshopManager.WeaponEntry> entries = GetPrivateField<List<WorkshopManager.WeaponEntry>>(workshop, "entries");
        if (entries == null)
            return;

        entries.Clear();
        if (savedEntries != null)
        {
            for (int i = 0; i < savedEntries.Count; i++)
            {
                WorkshopManager.WeaponEntry entry = savedEntries[i];
                if (entry == null)
                    continue;

                entries.Add(new WorkshopManager.WeaponEntry
                {
                    unitIndex = entry.unitIndex,
                    weaponIndex = entry.weaponIndex,
                    instanceIndex = entry.instanceIndex
                });
            }
        }

        workshop.Initialize();
        InvokeEvent(workshop, "OnStateChanged");
    }

    private static void RestoreHQVisits(IReadOnlyList<GameSaveData.VisitEntry> hqVisitSources)
    {
        HQVisitState visitState = HQVisitState.Instance;
        if (visitState == null)
            return;

        visitState.ClearVisitingParties();
        if (hqVisitSources == null)
            return;

        for (int i = 0; i < hqVisitSources.Count; i++)
        {
            GameSaveData.VisitEntry entry = hqVisitSources[i];
            if (string.IsNullOrWhiteSpace(entry.source))
                continue;

            visitState.SetVisitingParties(entry.source, entry.partyIds);
        }
    }

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        if (target == null)
            return null;

        FieldInfo field = target.GetType().GetField(fieldName, InstanceFields);
        return field != null ? field.GetValue(target) as T : null;
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        if (target == null)
            return;

        FieldInfo field = target.GetType().GetField(fieldName, InstanceFields);
        field?.SetValue(target, value);
    }

    private static void InvokeEvent(object target, string eventFieldName)
    {
        if (target == null)
            return;

        FieldInfo field = target.GetType().GetField(eventFieldName, InstanceFields);
        if (field?.GetValue(target) is Action action)
            action.Invoke();
    }

    public static void ApplySceneState()
    {
        ApplyPartyWorldStates();
        PartyRepositorySync.ApplyAllPartiesToScene(rebuildVisuals: true);
        RefreshUnitStates();
        ApplyHeroUnionStates();
        ApplyMainEventStates();
        ApplySubEventStates();
        RefreshGateStates();
        RefreshSceneRegistriesAndViews();
    }

    public static void ApplySceneStateAndCompleteRestore(bool captureTurnStartSnapshot = true)
    {
        ApplySceneState();
        CompleteRestoreSession(captureTurnStartSnapshot);
    }

    private static void ApplyPartyWorldStates()
    {
        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        if (progressRepository == null)
            return;

        PartyGridMover[] parties = Object.FindObjectsByType<PartyGridMover>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < parties.Length; i++)
        {
            PartyGridMover party = parties[i];
            if (party == null)
                continue;

            PartyIdentity identity = party.GetComponent<PartyIdentity>();
            if (identity == null || string.IsNullOrWhiteSpace(identity.PartyId))
                continue;

            string placementKey = !string.IsNullOrWhiteSpace(identity.PlacementKey)
                ? identity.PlacementKey
                : MapProgressKey.ForSceneParty(identity.PartyId);
            identity.SetPlacementKey(placementKey);

            if (!progressRepository.TryGetPartyState(placementKey, out PartyWorldState state) || state == null)
                continue;

            party.gameObject.SetActive(!state.Removed);
            if (!state.Removed)
                party.SnapToGridPosition(state.Grid, notifyMoveCompleted: false);
        }
    }

    private static void RefreshUnitStates()
    {
        PartyUnitState[] partyUnits = Object.FindObjectsByType<PartyUnitState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < partyUnits.Length; i++)
            partyUnits[i]?.RefreshFromRepository();

        EnemyUnitState[] enemyUnits = Object.FindObjectsByType<EnemyUnitState>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemyUnits.Length; i++)
            enemyUnits[i]?.RefreshFromRepository();
    }

    private static void ApplyHeroUnionStates()
    {
        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        if (progressRepository == null)
            return;

        HeroUnionUnit[] heroUnions = Object.FindObjectsByType<HeroUnionUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < heroUnions.Length; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (heroUnion == null)
                continue;

            if (progressRepository.TryGetHeroUnionState(heroUnion.ZoneId, out HeroUnionState state))
                heroUnion.ApplyProgressState(state);
        }
    }

    private static void ApplyMainEventStates()
    {
        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        if (progressRepository == null)
            return;

        MainEventObject[] mainEvents = Object.FindObjectsByType<MainEventObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < mainEvents.Length; i++)
        {
            MainEventObject mainEvent = mainEvents[i];
            if (mainEvent == null)
                continue;

            mainEvent.gameObject.SetActive(!progressRepository.IsMainEventCompleted(mainEvent.EventKey));
        }
    }

    private static void ApplySubEventStates()
    {
        MapProgressRepository progressRepository = MapProgressRepository.Instance;
        if (progressRepository == null)
            return;

        SubEventObject[] subEvents = Object.FindObjectsByType<SubEventObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < subEvents.Length; i++)
        {
            SubEventObject subEvent = subEvents[i];
            if (subEvent == null)
                continue;

            subEvent.gameObject.SetActive(!progressRepository.IsSubEventCompleted(subEvent.EventKey));
        }
    }

    private static void RefreshGateStates()
    {
        GateRuntimeController[] gates = Object.FindObjectsByType<GateRuntimeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < gates.Length; i++)
            gates[i]?.RefreshFromProgress();
    }

    private static void RefreshSceneRegistriesAndViews()
    {
        Object.FindFirstObjectByType<HeroUnionRegistry>()?.RefreshSceneHeroUnions();
        Object.FindFirstObjectByType<EnemyRegistry>()?.RefreshSceneEnemies();
        Object.FindFirstObjectByType<VillainUnionBaseRegistry>()?.RefreshSceneBases();
        Object.FindFirstObjectByType<MainEventRegistry>()?.RegisterExistingMainEvents();
        Object.FindFirstObjectByType<SubEventRegistry>()?.RegisterExistingSubEvents();
        Object.FindFirstObjectByType<MultiGridOccupantRegistry>()?.RefreshSceneOccupants();
        Object.FindFirstObjectByType<OutpostRegistry>()?.RegisterExistingOutposts();
        Object.FindFirstObjectByType<EnemyFogVisibilitySystem>()?.RefreshAll();
        Object.FindFirstObjectByType<MinimapController>()?.Refresh();
        Object.FindFirstObjectByType<MinimapCameraViewportOverlay>()?.Refresh();
        Object.FindFirstObjectByType<InteractionCellOverlayController>()?.RequestRefresh();
        Object.FindFirstObjectByType<PartyMovePointGaugeManager>()?.RefreshExistingParties();
        Object.FindFirstObjectByType<PartyMovePointWorldGaugeManager>()?.RefreshExistingParties();
    }
}
