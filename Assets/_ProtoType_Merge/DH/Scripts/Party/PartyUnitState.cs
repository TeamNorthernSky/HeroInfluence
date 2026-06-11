using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class PartyUnitState : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private int unitIndex = -1;
    [SerializeField] private string unitTemplateKey;
    [FormerlySerializedAs("jobIndex")]
    [SerializeField, HideInInspector] private int legacyJobIndex;

    [Header("Runtime State")]
    [SerializeField] private int level = 1;
    [SerializeField] private int favorability;
    [SerializeField] private int exp;
    [SerializeField] private int maxExp;
    [FormerlySerializedAs("initialSkillIndex")]
    [SerializeField] private int currentSkillIndex;
    [FormerlySerializedAs("initialWeaponIndex")]
    [SerializeField] private int currentWeaponIndex;
    [SerializeField] private StatBlock baseStats;
    [SerializeField] private StatBlock levelupStats;
    [SerializeField] private StatBlock eventBonusStats;
    [SerializeField] private EquipmentStatBlock currentWeaponStats;
    [SerializeField] private StatBlock ingameStats;
    [SerializeField] private float currentHp;

    public int UnitIndex => unitIndex;
    public string UnitTemplateKey => unitTemplateKey;
    public int Level => Mathf.Max(1, level);
    public int Favorability => Mathf.Max(0, favorability);
    public int Exp => Mathf.Max(0, exp);
    public int MaxExp => Mathf.Max(0, maxExp);
    public int CurrentSkillIndex => Mathf.Max(0, currentSkillIndex);
    public int CurrentWeaponIndex => Mathf.Max(0, currentWeaponIndex);
    public StatBlock BaseStats => baseStats;
    public StatBlock LevelupStats => levelupStats;
    public StatBlock EventBonusStats => eventBonusStats;
    public EquipmentStatBlock CurrentWeaponStats => currentWeaponStats;
    public StatBlock IngameStats => ingameStats;
    public float CurrentHp => currentHp;

    public void InitializeFromTemplate(UnitData template, EquipmentStatBlock weaponStats)
    {
        if (template == null)
            return;

        baseStats = template.baseStats;
        levelupStats = template.levelupStats;
        eventBonusStats = default;
        currentWeaponStats = weaponStats;
        RecalculateIngameStats();
        exp = 0;
        maxExp = ResolveMaxExp(Level);
        currentHp = Mathf.Max(0f, ingameStats.HP);
    }

    public void AssignUnitIndex(int nextUnitIndex)
    {
        unitIndex = Mathf.Max(1, nextUnitIndex);
    }

    public void ApplyPersistentData(UnitPersistentData data)
    {
        if (data == null)
            return;

        unitIndex = data.UnitIndex;
        unitTemplateKey = data.UnitTemplateKey;
        level = Mathf.Max(1, data.Level);
        favorability = Mathf.Max(0, data.Favorability);
        maxExp = data.MaxExp > 0 ? data.MaxExp : ResolveMaxExp(data.Level);
        exp = Mathf.Clamp(data.Exp, 0, MaxExp);
        baseStats = data.BaseStats;
        levelupStats = data.LevelupStats;
        eventBonusStats = data.EventBonusStats;
        currentSkillIndex = Mathf.Max(0, data.CurrentSkillIndex);
        currentWeaponIndex = Mathf.Max(0, data.CurrentWeaponIndex);
        currentWeaponStats = data.CurrentWeaponStats;
        ingameStats = data.IngameStats;
        currentHp = Mathf.Max(0f, data.CurrentHp);
    }

    public bool SyncToRepository()
    {
        if (unitIndex <= 0)
            return false;

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null)
            return false;

        return repository.UpdateUnitRuntimeState(
            unitIndex,
            unitTemplateKey,
            Level,
            Favorability,
            baseStats,
            levelupStats,
            CurrentSkillIndex,
            CurrentWeaponIndex,
            currentWeaponStats,
            ingameStats,
            currentHp,
            Exp,
            MaxExp);
    }

    public bool RefreshFromRepository()
    {
        if (unitIndex <= 0)
            return false;

        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (repository == null || !repository.TryGetUnit(unitIndex, out UnitPersistentData data))
            return false;

        ApplyPersistentData(data);
        return true;
    }

    public void SetLevel(int nextLevel)
    {
        level = Mathf.Max(1, nextLevel);
        RecalculateIngameStats();
        maxExp = ResolveMaxExp(Level);
        exp = Mathf.Clamp(exp, 0, MaxExp);
        currentHp = Mathf.Clamp(currentHp, 0f, Mathf.Max(0f, ingameStats.HP));
    }

    public void SetExp(int nextExp)
    {
        exp = Mathf.Clamp(nextExp, 0, MaxExp);
    }

    public void AddExp(int amount)
    {
        if (amount <= 0)
            return;

        exp = Exp + amount;
        while (MaxExp > 0 && exp >= MaxExp)
        {
            exp -= MaxExp;
            ApplyLevelUp(1, false);
        }
    }

    public void SetFavorability(int nextFavorability)
    {
        favorability = Mathf.Max(0, nextFavorability);
    }

    public void SetCurrentSkillIndex(int nextSkillIndex)
    {
        currentSkillIndex = Mathf.Max(0, nextSkillIndex);
    }

    public void SetCurrentWeapon(int nextWeaponIndex, EquipmentStatBlock weaponStats)
    {
        currentWeaponIndex = Mathf.Max(0, nextWeaponIndex);
        currentWeaponStats = weaponStats;
        RecalculateIngameStats();
        currentHp = Mathf.Clamp(currentHp <= 0f ? ingameStats.HP : currentHp, 0f, ingameStats.HP);
    }

    public void SetCurrentHp(float nextCurrentHp)
    {
        currentHp = Mathf.Clamp(nextCurrentHp, 0f, Mathf.Max(0f, ingameStats.HP));
    }

    public void ApplyLevelUp(int amount = 1)
    {
        ApplyLevelUp(amount, true);
    }

    private void ApplyLevelUp(int amount, bool resetExp)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return;

        level = Mathf.Max(1, level + safeAmount);
        RecalculateIngameStats();
        if (resetExp)
            exp = 0;
        maxExp = ResolveMaxExp(Level);
        currentHp = Mathf.Max(0f, ingameStats.HP);
    }

    public void RecalculateIngameStats()
    {
        ingameStats = UnitStatCalculator.CalculateIngameStats(
            baseStats,
            levelupStats,
            Level,
            currentWeaponStats,
            eventBonusStats,
            ResolveLevelUpTemplates());
    }

    private static IReadOnlyList<LevelUpData> ResolveLevelUpTemplates()
    {
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        return catalog != null ? catalog.GetLevelUpTemplates() : null;
    }

    private static int ResolveMaxExp(int level)
    {
        IReadOnlyList<LevelUpData> levelUpTable = ResolveLevelUpTemplates();
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

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(unitTemplateKey) && legacyJobIndex > 0)
            unitTemplateKey = legacyJobIndex.ToString();

        level = Mathf.Max(1, level);
        favorability = Mathf.Max(0, favorability);
        exp = Mathf.Max(0, exp);
        currentSkillIndex = Mathf.Max(0, currentSkillIndex);
        currentWeaponIndex = Mathf.Max(0, currentWeaponIndex);

        RecalculateIngameStats();
        maxExp = ResolveMaxExp(Level);
        exp = Mathf.Clamp(exp, 0, MaxExp);
        currentHp = Mathf.Clamp(currentHp, 0f, Mathf.Max(0f, ingameStats.HP));
    }
}
