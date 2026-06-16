using System.Collections.Generic;
using System.IO;
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

    private void Start()
    {
        RepairAllMaxExpIfNeeded();
    }

    public int CreateUnit()
    {
        return CreateUnit(string.Empty, 1, 0, default, default, 0, 0, default, default, 0f, 0, ResolveMaxExp(1));
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
        IReadOnlyList<LevelUpData> levelUpTemplates = ResolveLevelUpTemplates();
        StatBlock ingameStats = UnitStatCalculator.CalculateIngameStats(
            baseStats,
            levelupStats,
            level,
            currentWeaponStats,
            default,
            levelUpTemplates);
        return CreateUnit(unitTemplateKey, level, favorability, baseStats, levelupStats, currentSkillIndex, currentWeaponIndex, currentWeaponStats, ingameStats, ingameStats.HP, 0, ResolveMaxExp(level, levelUpTemplates));
    }

    public int CreateUnit(string unitTemplateKey, int level, int favorability, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp, int exp = 0, int maxExp = 0, float currentInfluence = -1f)
    {
        int unitIndex = Mathf.Max(1, nextUnitIndex);
        nextUnitIndex = unitIndex + 1;

        if (maxExp <= 0)
            maxExp = ResolveMaxExp(level);

        var data = new UnitPersistentData(unitIndex, unitTemplateKey, level, favorability, baseStats, levelupStats, currentSkillIndex, currentWeaponIndex, currentWeaponStats, ingameStats, currentHp, exp, maxExp, default, 1, currentInfluence);
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
        if (!unitLookup.TryGetValue(unitIndex, out data))
            return false;

        RepairMaxExpIfNeeded(data);
        return true;
    }

    public bool RemoveUnit(int unitIndex)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        unitLookup.Remove(unitIndex);
        units.Remove(data);
        return true;
    }

    public bool UpdateUnitRuntimeState(int unitIndex, string unitTemplateKey, int level, int favorability, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp, int exp = -1, int maxExp = -1, int skillLevel = -1, float currentInfluence = -1f)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        int effectiveMaxExp = maxExp;
        if (effectiveMaxExp < 0 && data.MaxExp <= 0)
            effectiveMaxExp = ResolveMaxExp(level);

        data.ApplyRuntimeState(unitTemplateKey, level, favorability, baseStats, levelupStats, currentSkillIndex, currentWeaponIndex, currentWeaponStats, ingameStats, currentHp, exp, effectiveMaxExp, skillLevel, currentInfluence);
        return true;
    }

    public bool SetSkillLevel(int unitIndex, int skillLevel)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.Favorability,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponIndex,
            data.CurrentWeaponStats,
            data.IngameStats,
            data.CurrentHp,
            data.Exp,
            data.MaxExp,
            skillLevel);
        return true;
    }

    public bool HealUnitToIngameMaxHp(int unitIndex, out float healedHp)
    {
        healedHp = 0f;

        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        float maxHp = Mathf.Max(0f, data.IngameStats.HP);
        healedHp = maxHp;

        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.Favorability,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponIndex,
            data.CurrentWeaponStats,
            data.IngameStats,
            maxHp,
            data.Exp,
            data.MaxExp);
        return true;
    }

    public bool AddEventBonusStats(int unitIndex, float hpBonus, float atkBonus)
    {
        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        StatBlock nextEventBonusStats = data.EventBonusStats;
        nextEventBonusStats.HP += hpBonus;
        nextEventBonusStats.Atk += atkBonus;

        IReadOnlyList<LevelUpData> levelUpTemplates = ResolveLevelUpTemplates();
        StatBlock nextIngameStats = UnitStatCalculator.CalculateIngameStats(
            data.BaseStats,
            data.LevelupStats,
            data.Level,
            data.CurrentWeaponStats,
            nextEventBonusStats,
            levelUpTemplates);

        float nextCurrentHp = Mathf.Clamp(data.CurrentHp, 0f, Mathf.Max(0f, nextIngameStats.HP));
        data.SetEventBonusStats(nextEventBonusStats);
        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.Favorability,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponIndex,
            data.CurrentWeaponStats,
            nextIngameStats,
            nextCurrentHp,
            data.Exp,
            data.MaxExp);
        return true;
    }

    public bool AddExp(int unitIndex, int amount)
    {
        if (amount <= 0)
            return false;

        if (!unitLookup.TryGetValue(unitIndex, out UnitPersistentData data))
            return false;

        RepairMaxExpIfNeeded(data);
        ApplyExpWithLevelUps(data, amount);
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
        IReadOnlyList<LevelUpData> levelUpTemplates = ResolveLevelUpTemplates();
        StatBlock nextIngameStats = UnitStatCalculator.CalculateIngameStats(
            data.BaseStats,
            data.LevelupStats,
            nextLevel,
            data.CurrentWeaponStats,
            data.EventBonusStats,
            levelUpTemplates);
        int nextMaxExp = ResolveMaxExp(nextLevel, levelUpTemplates);
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
            nextIngameStats.HP,
            0,
            nextMaxExp);
        return true;
    }

    private static IReadOnlyList<LevelUpData> ResolveLevelUpTemplates()
    {
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        return catalog != null ? catalog.GetLevelUpTemplates() : null;
    }

    private static int ResolveMaxExp(int level)
    {
        return ResolveMaxExp(level, ResolveLevelUpTemplates());
    }

    private static int ResolveMaxExp(int level, IReadOnlyList<LevelUpData> levelUpTable)
    {
        if (levelUpTable == null || levelUpTable.Count == 0)
            return 0;

        int safeLevel = Mathf.Max(1, level);
        int nextLevel = int.MaxValue;
        int nextExp = 0;

        for (int i = 0; i < levelUpTable.Count; i++)
        {
            LevelUpData row = levelUpTable[i];
            if (row == null)
                continue;

            int rowLevel = Mathf.RoundToInt(row.level);
            if (rowLevel <= safeLevel || rowLevel >= nextLevel)
                continue;

            nextLevel = rowLevel;
            nextExp = Mathf.Max(0, row.expPerLevel);
        }

        return nextExp;
    }

    /// <summary>Repository 상태를 변경하지 않고 expAmount를 더했을 때 도달하는 레벨을 반환합니다.</summary>
    public static int SimulateFinalLevel(UnitPersistentData data, int expAmount)
    {
        if (data == null || expAmount <= 0)
            return data?.Level ?? 1;

        IReadOnlyList<LevelUpData> levelUpTemplates = ResolveLevelUpTemplates();
        int level = Mathf.Max(1, data.Level);
        int exp = Mathf.Max(0, data.Exp) + expAmount;
        int maxExp = data.MaxExp > 0 ? data.MaxExp : ResolveMaxExp(level, levelUpTemplates);

        while (maxExp > 0 && exp >= maxExp)
        {
            exp -= maxExp;
            level++;
            maxExp = ResolveMaxExp(level, levelUpTemplates);
        }

        return level;
    }

    private static void ApplyExpWithLevelUps(UnitPersistentData data, int amount)
    {
        if (data == null || amount <= 0)
            return;

        IReadOnlyList<LevelUpData> levelUpTemplates = ResolveLevelUpTemplates();
        int nextLevel = Mathf.Max(1, data.Level);
        int nextExp = Mathf.Max(0, data.Exp) + amount;
        int nextMaxExp = data.MaxExp > 0 ? data.MaxExp : ResolveMaxExp(nextLevel, levelUpTemplates);
        StatBlock nextIngameStats = data.IngameStats;
        float nextCurrentHp = data.CurrentHp;

        while (nextMaxExp > 0 && nextExp >= nextMaxExp)
        {
            nextExp -= nextMaxExp;
            nextLevel++;
            nextIngameStats = UnitStatCalculator.CalculateIngameStats(
                data.BaseStats,
                data.LevelupStats,
                nextLevel,
                data.CurrentWeaponStats,
                data.EventBonusStats,
                levelUpTemplates);
            nextCurrentHp = nextIngameStats.HP;
            nextMaxExp = ResolveMaxExp(nextLevel, levelUpTemplates);
        }

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
            nextCurrentHp,
            nextExp,
            nextMaxExp);
    }

    private static void RepairMaxExpIfNeeded(UnitPersistentData data)
    {
        if (data == null || data.MaxExp > 0)
            return;

        int maxExp = ResolveMaxExp(data.Level);
        if (maxExp <= 0)
            return;

        data.ApplyRuntimeState(
            data.UnitTemplateKey,
            data.Level,
            data.Favorability,
            data.BaseStats,
            data.LevelupStats,
            data.CurrentSkillIndex,
            data.CurrentWeaponIndex,
            data.CurrentWeaponStats,
            data.IngameStats,
            data.CurrentHp,
            data.Exp,
            maxExp);
    }

    private void RepairAllMaxExpIfNeeded()
    {
        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
            RepairMaxExpIfNeeded(units[i]);
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

            RepairMaxExpIfNeeded(data);
            unitLookup.Add(unitIndex, data);
            if (unitIndex > highestIndex)
                highestIndex = unitIndex;
        }

        if (nextUnitIndex <= highestIndex)
            nextUnitIndex = highestIndex + 1;
    }

    private const string DiskFileName = "persistent_units_repo.json";

    [System.Serializable]
    private class UnitRepositoryDiskPayload
    {
        public int nextUnitIndex;
        public UnitPersistentDataDiskRow[] units;
    }

    /// <summary>메모리상 유닛 목록을 Application.persistentDataPath JSON으로 저장합니다.</summary>
    public void SaveRuntimeStateToDisk()
    {
        try
        {
            UnitPersistentDataDiskRow[] rows;
            if (units == null || units.Count == 0)
                rows = System.Array.Empty<UnitPersistentDataDiskRow>();
            else
            {
                rows = new UnitPersistentDataDiskRow[units.Count];
                for (int i = 0; i < units.Count; i++)
                    rows[i] = UnitPersistentDataDiskRow.From(units[i]);
            }

            var payload = new UnitRepositoryDiskPayload
            {
                nextUnitIndex = nextUnitIndex,
                units = rows
            };

            string path = Path.Combine(Application.persistentDataPath, DiskFileName);
            File.WriteAllText(path, JsonUtility.ToJson(payload));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PersistentUnitRepository] SaveRuntimeStateToDisk 실패: {ex.Message}", this);
        }
    }
}
