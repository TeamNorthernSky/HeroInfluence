using System.Collections.Generic;
using UnityEngine;

public sealed class DHUnitSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private int nextUnitIndex = 1;
    [SerializeField] private List<UnitPersistentDataDiskRow> units = new List<UnitPersistentDataDiskRow>();

    public override void CaptureFromRuntime()
    {
        units.Clear();

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
            return;

        nextUnitIndex = repository.NextUnitIndex;
        IReadOnlyList<UnitPersistentData> source = repository.Units;
        for (int i = 0; i < source.Count; i++)
        {
            UnitPersistentDataDiskRow row = UnitPersistentDataDiskRow.From(source[i]);
            if (row != null)
                units.Add(row);
        }
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.nextUnitIndex = nextUnitIndex;
        data.units.Clear();
        data.units.AddRange(units);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        ClearSnapshot();
        if (data == null)
            return;

        nextUnitIndex = data.nextUnitIndex;
        if (data.units == null)
            return;

        for (int i = 0; i < data.units.Count; i++)
        {
            UnitPersistentDataDiskRow row = data.units[i];
            if (row != null)
                units.Add(row);
        }
    }

    public override void ClearSnapshot()
    {
        nextUnitIndex = 1;
        units.Clear();
    }
}

public sealed class DHEnemyGroupSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private int nextEnemySequence = 1;
    [SerializeField] private List<EnemyPersistentData> enemyGroups = new List<EnemyPersistentData>();

    public override void CaptureFromRuntime()
    {
        enemyGroups.Clear();

        EnemyGroupPersistentRepository repository = EnemyGroupPersistentRepository.Instance;
        if (repository == null)
            return;

        nextEnemySequence = repository.NextEnemySequence;
        CopyEnemyGroups(repository.Enemies, enemyGroups);
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.nextEnemySequence = nextEnemySequence;
        data.enemyGroups.Clear();
        CopyEnemyGroups(enemyGroups, data.enemyGroups);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        ClearSnapshot();
        if (data == null)
            return;

        nextEnemySequence = data.nextEnemySequence;
        CopyEnemyGroups(data.enemyGroups, enemyGroups);
    }

    public override void ClearSnapshot()
    {
        nextEnemySequence = 1;
        enemyGroups.Clear();
    }

    private static void CopyEnemyGroups(IReadOnlyList<EnemyPersistentData> source, List<EnemyPersistentData> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            EnemyPersistentData data = source[i];
            if (data != null)
                target.Add(new EnemyPersistentData(data.EnemyId, data.UnitIndices, data.UnitSlots));
        }
    }
}

public sealed class DHWeaponSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private int nextWeaponIndex = 1;
    [SerializeField] private List<WeaponPersistentData> weapons = new List<WeaponPersistentData>();

    public override void CaptureFromRuntime()
    {
        weapons.Clear();

        WeaponPersistentRepository repository = WeaponPersistentRepository.Instance;
        if (repository == null)
            return;

        nextWeaponIndex = repository.NextWeaponIndex;
        CopyWeapons(repository.Weapons, weapons);
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.nextWeaponIndex = nextWeaponIndex;
        data.weapons.Clear();
        CopyWeapons(weapons, data.weapons);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        ClearSnapshot();
        if (data == null)
            return;

        nextWeaponIndex = data.nextWeaponIndex;
        CopyWeapons(data.weapons, weapons);
    }

    public override void ClearSnapshot()
    {
        nextWeaponIndex = 1;
        weapons.Clear();
    }

    private static void CopyWeapons(IReadOnlyList<WeaponPersistentData> source, List<WeaponPersistentData> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            WeaponPersistentData data = source[i];
            if (data == null)
                continue;

            WeaponPersistentData copy = new WeaponPersistentData(data.WeaponIndex, data.WeaponTemplateKeyString, data.WeaponTemplateKey);
            copy.SetLevel(data.Level);
            copy.SetCachedWeaponStats(data.CachedWeaponStats);
            target.Add(copy);
        }
    }
}

public sealed class DHPartySnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private List<PartyPersistentData> parties = new List<PartyPersistentData>();

    public override void CaptureFromRuntime()
    {
        parties.Clear();

        PartyPersistentRepository repository = PartyPersistentRepository.Instance;
        if (repository == null)
            return;

        CopyParties(repository.Parties, parties);
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.parties.Clear();
        CopyParties(parties, data.parties);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        ClearSnapshot();
        if (data != null)
            CopyParties(data.parties, parties);
    }

    public override void ClearSnapshot()
    {
        parties.Clear();
    }

    private static void CopyParties(IReadOnlyList<PartyPersistentData> source, List<PartyPersistentData> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            PartyPersistentData data = source[i];
            if (data == null)
                continue;

            PartyPersistentData copy = new PartyPersistentData(data.PartyId, data.UnitIndices, data.UnitSlots);
            if (data.HasLastGrid)
                copy.SetLastGrid(data.LastGrid);

            if (data.HasRemainingMovePoints)
                copy.SetRemainingMovePoints(data.RemainingMovePoints);

            target.Add(copy);
        }
    }
}
