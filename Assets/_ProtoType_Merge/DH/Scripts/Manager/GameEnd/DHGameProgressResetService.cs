public static class DHGameProgressResetService
{
    public static void ResetDHProgress()
    {
        DHGameEndState.Reset();
        MapProgressRepository.Instance?.ClearAllProgress();
        PersistentUnitRepository.Instance?.ClearAllUnits();
        PersistentEnemyRepository.Instance?.ClearAllEnemies();
        EnemyGroupPersistentRepository.Instance?.ClearAllEnemies();
        PartyPersistentRepository.Instance?.ClearAllParties();
        WeaponPersistentRepository.Instance?.ClearAllWeapons();
        CombatContext.Instance?.Clear();
        HQVisitState.Instance?.ClearVisitingParties();
        DHEventStateRepository.EnsureInstance().ClearAllState();
        DHTurnStartSnapshotStore.Instance?.ClearSnapshot();
    }
}
