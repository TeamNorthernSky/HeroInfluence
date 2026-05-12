using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PersistentUnitRepository : MonoBehaviour
{
    public static PersistentUnitRepository Instance { get; private set; }

    [Header("Persistent Units")]
    [SerializeField] private int nextUnitIndex = 1;
    [SerializeField] private List<UnitPersistentData> units = new List<UnitPersistentData>();

    private readonly Dictionary<int, UnitPersistentData> unitLookup = new Dictionary<int, UnitPersistentData>();

    public IReadOnlyList<UnitPersistentData> Units => units;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public int CreateUnit()
    {
        return CreateUnit(string.Empty, 1, 0, default, default, 0, 0, default, default, 0f);
    }

    public int CreateUnit(string unitTemplateKey, int level, int favorability, StatBlock baseStats)
    {
        return CreateUnit(unitTemplateKey, level, favorability, baseStats, default, 0, 0, default, default, 0f);
    }

    public int CreateUnit(string unitTemplateKey, int level, int favorability, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex)
    {
        return CreateUnit(unitTemplateKey, level, favorability, baseStats, levelupStats, currentSkillIndex, currentWeaponIndex, default, default, 0f);
    }

    public int CreateUnit(string unitTemplateKey, int level, int favorability, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats)
    {
        StatBlock ingameStats = UnitStatCalculator.CalculateIngameStats(baseStats, levelupStats, level, currentWeaponStats);
        return CreateUnit(unitTemplateKey, level, favorability, baseStats, levelupStats, currentSkillIndex, currentWeaponIndex, currentWeaponStats, ingameStats, ingameStats.HP);
    }

    public int CreateUnit(string unitTemplateKey, int level, int favorability, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp)
    {
        int unitIndex = Mathf.Max(1, nextUnitIndex);
        nextUnitIndex = unitIndex + 1;

        var data = new UnitPersistentData(unitIndex, unitTemplateKey, level, favorability, baseStats, levelupStats, currentSkillIndex, currentWeaponIndex, currentWeaponStats, ingameStats, currentHp);
        units.Add(data);
        unitLookup[unitIndex] = data;
        return unitIndex;
    }

    public bool ContainsUnit(int unitIndex)
    {
        return unitLookup.ContainsKey(unitIndex);
    }

    public bool TryGetUnit(int unitIndex, out UnitPersistentData data)
    {
        return unitLookup.TryGetValue(unitIndex, out data);
    }

    public bool RemoveUnit(int unitIndex)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        unitLookup.Remove(unitIndex);
        units.Remove(data);
        return true;
    }

    public bool UpdateUnitRuntimeState(int unitIndex, string unitTemplateKey, int level, int favorability, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        data.ApplyRuntimeState(unitTemplateKey, level, favorability, baseStats, levelupStats, currentSkillIndex, currentWeaponIndex, currentWeaponStats, ingameStats, currentHp);
        return true;
    }

    public bool ApplyLevelUp(int unitIndex, int amount = 1)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return true;

        int nextLevel = Mathf.Max(1, data.Level + safeAmount);
        StatBlock nextIngameStats = UnitStatCalculator.CalculateIngameStats(data.BaseStats, data.LevelupStats, nextLevel, data.CurrentWeaponStats);
        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            nextLevel,
            data.Favorability,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponIndex,
            data.CurrentWeaponStats,
            nextIngameStats,
            nextIngameStats.HP);
        return true;
    }

    public void ClearAllUnits()
    {
        units.Clear();
        unitLookup.Clear();
        nextUnitIndex = 1;
    }

    private void RebuildLookup()
    {
        unitLookup.Clear();

        int highestIndex = 0;
        for (int i = 0; i < units.Count; i++)
        {
            UnitPersistentData data = units[i];
            if (data == null)
                continue;

            int unitIndex = data.UnitIndex;
            if (unitIndex <= 0)
                continue;

            if (unitLookup.ContainsKey(unitIndex))
            {
                Debug.LogWarning($"PersistentUnitRepository has duplicate unitIndex '{unitIndex}'.", this);
                continue;
            }

            unitLookup.Add(unitIndex, data);
            if (unitIndex > highestIndex)
                highestIndex = unitIndex;
        }

        if (nextUnitIndex <= highestIndex)
            nextUnitIndex = highestIndex + 1;
    }
}
