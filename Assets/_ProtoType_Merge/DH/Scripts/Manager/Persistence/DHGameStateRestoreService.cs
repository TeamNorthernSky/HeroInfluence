using UnityEngine;

public static class DHGameStateRestoreService
{
    public static void RestoreFromGameSaveData(GameSaveData data)
    {
        RestoreRepositories(data);
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
            data.partyWorldStates,
            data.enemyWorldStates,
            data.outpostStates,
            data.fogCells,
            data.levelZoneSelections,
            data.heroUnionStates,
            data.gateStates,
            data.zoneThreatStates,
            data.zoneEnemyLevelStates);
    }

    public static void ApplySceneState()
    {
        ApplyPartyWorldStates();
        PartyRepositorySync.ApplyAllPartiesToScene(rebuildVisuals: true);
        RefreshUnitStates();
        ApplyHeroUnionStates();
        RefreshGateStates();
        RefreshSceneRegistriesAndViews();
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