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
    [SerializeField] private int exp;
    [SerializeField] private int maxExp;
    [FormerlySerializedAs("initialSkillIndex")]
    [SerializeField] private int currentSkillIndex;
    [SerializeField] private int skillLevel = 1;
    [SerializeField] private string currentWeaponKey;
    [FormerlySerializedAs("initialWeaponIndex")]
    [SerializeField, HideInInspector] private int currentWeaponIndex;
    [SerializeField] private StatBlock baseStats;
    [SerializeField] private StatBlock levelupStats;
    [SerializeField] private StatBlock eventBonusStats;
    [SerializeField] private EquipmentStatBlock currentWeaponStats;
    [SerializeField] private StatBlock ingameStats;
    [SerializeField] private float currentHp;
    [SerializeField] private bool isIncapacitated;

    public int UnitIndex => unitIndex;
    public string UnitTemplateKey => unitTemplateKey;
    public int Level => Mathf.Max(1, level);
    public int Exp => Mathf.Max(0, exp);
    public int MaxExp => Mathf.Max(0, maxExp);
    public int CurrentSkillIndex => Mathf.Max(0, currentSkillIndex);
    public int SkillLevel => Mathf.Max(1, skillLevel);
    public string CurrentWeaponKey => string.IsNullOrWhiteSpace(currentWeaponKey) ? string.Empty : currentWeaponKey.Trim();
    public int CurrentWeaponIndex => Mathf.Max(0, currentWeaponIndex);
    public StatBlock BaseStats => baseStats;
    public StatBlock LevelupStats => levelupStats;
    public StatBlock EventBonusStats => eventBonusStats;
    public EquipmentStatBlock CurrentWeaponStats => currentWeaponStats;
    public StatBlock IngameStats => ingameStats;
    public float CurrentHp => currentHp;
    public bool IsIncapacitated => isIncapacitated;

    public void InitializeFromTemplate(DHPlayerUnitTemplate template, EquipmentStatBlock weaponStats)
    {
        if (template == null)
            return;

        baseStats = template.BaseStats;
        levelupStats = template.LevelupStats;
        eventBonusStats = default;
        currentWeaponStats = weaponStats;
        skillLevel = Mathf.Max(1, skillLevel);
        RecalculateIngameStats();
        exp = 0;
        maxExp = ResolveMaxExp(Level);
        currentHp = Mathf.Max(0f, ingameStats.HP);
        SetIncapacitated(false);
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
        maxExp = data.MaxExp > 0 ? data.MaxExp : ResolveMaxExp(data.Level);
        exp = Mathf.Clamp(data.Exp, 0, MaxExp);
        baseStats = data.BaseStats;
        levelupStats = data.LevelupStats;
        eventBonusStats = data.EventBonusStats;
        currentSkillIndex = Mathf.Max(0, data.CurrentSkillIndex);
        skillLevel = Mathf.Max(1, data.SkillLevel);
        currentWeaponKey = data.CurrentWeaponKey;
        currentWeaponIndex = Mathf.Max(0, data.CurrentWeaponIndex);
        currentWeaponStats = data.CurrentWeaponStats;
        ingameStats = data.IngameStats;
        currentHp = Mathf.Max(0f, data.CurrentHp);
        SetIncapacitated(data.IsIncapacitated);
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
            baseStats,
            levelupStats,
            CurrentSkillIndex,
            CurrentWeaponKey,
            currentWeaponStats,
            ingameStats,
            currentHp,
            Exp,
            MaxExp,
            SkillLevel,
            isIncapacitated: isIncapacitated);
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

    public void SetCurrentSkillIndex(int nextSkillIndex)
    {
        currentSkillIndex = Mathf.Max(0, nextSkillIndex);
    }

    public void SetSkillLevel(int nextSkillLevel)
    {
        skillLevel = Mathf.Max(1, nextSkillLevel);
    }

    public void SetCurrentWeapon(int nextWeaponIndex, EquipmentStatBlock weaponStats)
    {
        SetCurrentWeapon(string.Empty, nextWeaponIndex, weaponStats);
    }

    public void SetCurrentWeapon(string nextWeaponKey, EquipmentStatBlock weaponStats)
    {
        SetCurrentWeapon(nextWeaponKey, 0, weaponStats);
    }

    public void SetCurrentWeapon(string nextWeaponKey, int nextWeaponIndex, EquipmentStatBlock weaponStats)
    {
        currentWeaponKey = string.IsNullOrWhiteSpace(nextWeaponKey) ? string.Empty : nextWeaponKey.Trim();
        currentWeaponIndex = Mathf.Max(0, nextWeaponIndex);
        currentWeaponStats = weaponStats;
        RecalculateIngameStats();
        currentHp = Mathf.Clamp(currentHp <= 0f ? ingameStats.HP : currentHp, 0f, ingameStats.HP);
    }

    public void SetCurrentHp(float nextCurrentHp)
    {
        currentHp = Mathf.Clamp(nextCurrentHp, 0f, Mathf.Max(0f, ingameStats.HP));
    }

    public void SetIncapacitated(bool nextIsIncapacitated)
    {
        isIncapacitated = nextIsIncapacitated;
        ApplyIncapacitatedVisualState();
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
            ResolveUnitGrowthTemplates());
    }

    private static IReadOnlyList<DHUnitGrowthTemplate> ResolveUnitGrowthTemplates()
    {
        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        return catalog != null ? catalog.GetUnitGrowthTemplates() : null;
    }

    private static int ResolveMaxExp(int level)
    {
        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTable = ResolveUnitGrowthTemplates();
        if (unitGrowthTable == null || unitGrowthTable.Count == 0)
            return 0;

        int safeLevel = Mathf.Max(1, level);
        int nextLevel = int.MaxValue;
        int nextExp = 0;

        for (int i = 0; i < unitGrowthTable.Count; i++)
        {
            DHUnitGrowthTemplate row = unitGrowthTable[i];
            if (row == null)
                continue;

            int rowLevel = row.Level;
            if (rowLevel <= safeLevel || rowLevel >= nextLevel)
                continue;

            nextLevel = rowLevel;
            nextExp = Mathf.Max(0, row.RequiredExperience);
        }

        return nextExp;
    }

    private void ApplyIncapacitatedVisualState()
    {
        bool visible = !isIncapacitated;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
                animators[i].enabled = visible;
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(unitTemplateKey) && legacyJobIndex > 0)
            unitTemplateKey = legacyJobIndex.ToString();

        level = Mathf.Max(1, level);
        exp = Mathf.Max(0, exp);
        currentSkillIndex = Mathf.Max(0, currentSkillIndex);
        skillLevel = Mathf.Max(1, skillLevel);
        currentWeaponKey = string.IsNullOrWhiteSpace(currentWeaponKey) ? string.Empty : currentWeaponKey.Trim();
        currentWeaponIndex = Mathf.Max(0, currentWeaponIndex);

        RecalculateIngameStats();
        maxExp = ResolveMaxExp(Level);
        exp = Mathf.Clamp(exp, 0, MaxExp);
        currentHp = Mathf.Clamp(currentHp, 0f, Mathf.Max(0f, ingameStats.HP));
    }
}
