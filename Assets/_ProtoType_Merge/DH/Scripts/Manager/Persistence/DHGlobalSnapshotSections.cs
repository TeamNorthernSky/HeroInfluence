using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DHGameStateSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private int currentDay = 1;

    public override void CaptureFromRuntime()
    {
        currentDay = GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.CurrentDay) : 1;
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data != null)
            data.currentDay = Mathf.Max(1, currentDay);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        currentDay = data != null ? Mathf.Max(1, data.currentDay) : 1;
    }

    public override void ClearSnapshot()
    {
        currentDay = 1;
    }
}

public sealed class DHEconomySnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private List<GameSaveData.ResourceEntry> resources = new List<GameSaveData.ResourceEntry>();

    public override void CaptureFromRuntime()
    {
        resources.Clear();

        EconomyManager economy = GameManager.Instance != null ? GameManager.Instance.Economy : null;
        if (economy == null)
            return;

        foreach (KeyValuePair<ResourceType, int> pair in economy.GetAll())
        {
            resources.Add(new GameSaveData.ResourceEntry
            {
                type = pair.Key,
                amount = pair.Value
            });
        }
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.resources.Clear();
        data.resources.AddRange(resources);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        resources.Clear();
        if (data != null && data.resources != null)
            resources.AddRange(data.resources);
    }

    public override void ClearSnapshot()
    {
        resources.Clear();
    }
}

public sealed class DHHQSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private List<GameSaveData.HQLevelEntry> hqLevels = new List<GameSaveData.HQLevelEntry>();
    [SerializeField] private bool hqUpgradedThisTurn;

    public override void CaptureFromRuntime()
    {
        hqLevels.Clear();
        hqUpgradedThisTurn = false;

        HQStateManager hq = GameManager.Instance != null ? GameManager.Instance.HQ : null;
        if (hq == null)
            return;

        foreach (HQDepartment department in Enum.GetValues(typeof(HQDepartment)))
        {
            hqLevels.Add(new GameSaveData.HQLevelEntry
            {
                department = department,
                level = hq.GetLevel(department)
            });
        }

        hqUpgradedThisTurn = hq.UpgradedThisTurn;
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.hqLevels.Clear();
        data.hqLevels.AddRange(hqLevels);
        data.hqUpgradedThisTurn = hqUpgradedThisTurn;
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        hqLevels.Clear();
        hqUpgradedThisTurn = false;

        if (data == null)
            return;

        if (data.hqLevels != null)
            hqLevels.AddRange(data.hqLevels);

        hqUpgradedThisTurn = data.hqUpgradedThisTurn;
    }

    public override void ClearSnapshot()
    {
        hqLevels.Clear();
        hqUpgradedThisTurn = false;
    }
}

public sealed class DHDepartmentSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private List<LabManager.SkillLevelEntry> labSkillLevels = new List<LabManager.SkillLevelEntry>();
    [SerializeField] private int publicityCurrentPool;
    [SerializeField] private int publicityLastChargeDay;
    [SerializeField] private List<TrainingManager.TrainingEntry> trainingEntries = new List<TrainingManager.TrainingEntry>();
    [SerializeField] private List<int> infirmaryHealedThisTurn = new List<int>();
    [SerializeField] private List<WorkshopManager.WeaponEntry> workshopWeaponEntries = new List<WorkshopManager.WeaponEntry>();

    public override void CaptureFromRuntime()
    {
        ClearSnapshot();

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        if (gameManager.Lab != null)
            CopyLabEntries(gameManager.Lab.Entries, labSkillLevels);

        if (gameManager.Publicity != null)
        {
            publicityCurrentPool = gameManager.Publicity.CurrentPool;
            publicityLastChargeDay = gameManager.Publicity.LastChargeDay;
        }

        if (gameManager.Training != null)
            CopyTrainingEntries(gameManager.Training.Entries, trainingEntries);

        if (gameManager.Infirmary != null)
        {
            foreach (int unitIndex in gameManager.Infirmary.HealedUnitsThisTurn)
                infirmaryHealedThisTurn.Add(unitIndex);
        }

        if (gameManager.Workshop != null)
            CopyWorkshopEntries(gameManager.Workshop.Entries, workshopWeaponEntries);
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.labSkillLevels.Clear();
        CopyLabEntries(labSkillLevels, data.labSkillLevels);
        data.publicityCurrentPool = publicityCurrentPool;
        data.publicityLastChargeDay = publicityLastChargeDay;
        data.trainingEntries.Clear();
        CopyTrainingEntries(trainingEntries, data.trainingEntries);
        data.infirmaryHealedThisTurn.Clear();
        data.infirmaryHealedThisTurn.AddRange(infirmaryHealedThisTurn);
        data.workshopWeaponEntries.Clear();
        CopyWorkshopEntries(workshopWeaponEntries, data.workshopWeaponEntries);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        ClearSnapshot();
        if (data == null)
            return;

        CopyLabEntries(data.labSkillLevels, labSkillLevels);
        publicityCurrentPool = data.publicityCurrentPool;
        publicityLastChargeDay = data.publicityLastChargeDay;
        CopyTrainingEntries(data.trainingEntries, trainingEntries);
        if (data.infirmaryHealedThisTurn != null)
            infirmaryHealedThisTurn.AddRange(data.infirmaryHealedThisTurn);
        CopyWorkshopEntries(data.workshopWeaponEntries, workshopWeaponEntries);
    }

    public override void ClearSnapshot()
    {
        labSkillLevels.Clear();
        publicityCurrentPool = 0;
        publicityLastChargeDay = 0;
        trainingEntries.Clear();
        infirmaryHealedThisTurn.Clear();
        workshopWeaponEntries.Clear();
    }

    private static void CopyLabEntries(IReadOnlyList<LabManager.SkillLevelEntry> source, List<LabManager.SkillLevelEntry> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            LabManager.SkillLevelEntry entry = source[i];
            if (entry == null)
                continue;

            target.Add(new LabManager.SkillLevelEntry
            {
                unitIndex = entry.unitIndex,
                skillIndex = entry.skillIndex,
                level = entry.level
            });
        }
    }

    private static void CopyTrainingEntries(IReadOnlyList<TrainingManager.TrainingEntry> source, List<TrainingManager.TrainingEntry> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            TrainingManager.TrainingEntry entry = source[i];
            if (entry == null)
                continue;

            target.Add(new TrainingManager.TrainingEntry
            {
                unitIndex = entry.unitIndex,
                atkLevel = entry.atkLevel,
                hpLevel = entry.hpLevel
            });
        }
    }

    private static void CopyWorkshopEntries(IReadOnlyList<WorkshopManager.WeaponEntry> source, List<WorkshopManager.WeaponEntry> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            WorkshopManager.WeaponEntry entry = source[i];
            if (entry == null)
                continue;

            target.Add(new WorkshopManager.WeaponEntry
            {
                weaponIndex = entry.weaponIndex,
                instanceIndex = entry.instanceIndex
            });
        }
    }
}

public sealed class DHVisitSnapshotSection : DHTurnStartSnapshotSection
{
    [SerializeField] private List<GameSaveData.VisitEntry> hqVisitSources = new List<GameSaveData.VisitEntry>();

    public override void CaptureFromRuntime()
    {
        hqVisitSources.Clear();

        HQVisitState visitState = HQVisitState.Instance;
        if (visitState == null)
            return;

        foreach (KeyValuePair<string, HashSet<string>> pair in visitState.VisitingBySource)
        {
            hqVisitSources.Add(new GameSaveData.VisitEntry
            {
                source = pair.Key,
                partyIds = pair.Value != null ? new List<string>(pair.Value) : new List<string>()
            });
        }
    }

    public override void FillGameSaveData(GameSaveData data)
    {
        if (data == null)
            return;

        data.hqVisitSources.Clear();
        CopyVisitEntries(hqVisitSources, data.hqVisitSources);
    }

    public override void LoadFromGameSaveData(GameSaveData data)
    {
        hqVisitSources.Clear();
        if (data != null)
            CopyVisitEntries(data.hqVisitSources, hqVisitSources);
    }

    public override void ClearSnapshot()
    {
        hqVisitSources.Clear();
    }

    private static void CopyVisitEntries(IReadOnlyList<GameSaveData.VisitEntry> source, List<GameSaveData.VisitEntry> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            GameSaveData.VisitEntry entry = source[i];
            target.Add(new GameSaveData.VisitEntry
            {
                source = entry.source,
                partyIds = entry.partyIds != null ? new List<string>(entry.partyIds) : new List<string>()
            });
        }
    }
}
