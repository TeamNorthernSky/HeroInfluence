using System;

[Serializable]
public class UnitPersistentData
{
    public int UnitIndex => unitIndex;
    public string UnitTemplateKey => unitTemplateKey;
    public int Level => level;
    public int Favorability => favorability;
    public StatBlock BaseStats => baseStats;
    public StatBlock LevelupStats => levelupStats;
    public StatBlock EventBonusStats => eventBonusStats;
    public StatBlock IngameStats => ingameStats;
    public float CurrentHp => currentHp;
    public int Exp => exp;
    public int MaxExp => maxExp;
    public int CurrentSkillIndex => currentSkillIndex;
    public int SkillLevel => skillLevel;
    public int CurrentWeaponIndex => currentWeaponIndex;
    public int EquippedWeaponInstanceIndex => equippedWeaponInstanceIndex;
    public EquipmentStatBlock CurrentWeaponStats => currentWeaponStats;

    [UnityEngine.SerializeField] private int unitIndex;
    [UnityEngine.SerializeField] private string unitTemplateKey;
    [UnityEngine.SerializeField] private int level;
    [UnityEngine.SerializeField] private int favorability;
    [UnityEngine.SerializeField] private StatBlock baseStats;
    [UnityEngine.SerializeField] private StatBlock levelupStats;
    [UnityEngine.SerializeField] private StatBlock eventBonusStats;
    [UnityEngine.SerializeField] private StatBlock ingameStats;
    [UnityEngine.SerializeField] private float currentHp;
    [UnityEngine.SerializeField] private int exp;
    [UnityEngine.SerializeField] private int maxExp;
    [UnityEngine.SerializeField] private int currentSkillIndex;
    [UnityEngine.SerializeField] private int skillLevel;
    [UnityEngine.SerializeField] private int currentWeaponIndex;
    [UnityEngine.SerializeField] private int equippedWeaponInstanceIndex;
    [UnityEngine.SerializeField] private EquipmentStatBlock currentWeaponStats;

    public UnitPersistentData(int unitIndex)
        : this(unitIndex, string.Empty, 1, 0, default, default, 0, 0, default, default, 0f)
    {
    }

    public UnitPersistentData(int unitIndex, string unitTemplateKey, int level, int favorability, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp, int exp = 0, int maxExp = 0, StatBlock eventBonusStats = default, int skillLevel = 1, int equippedWeaponInstanceIndex = 0)
    {
        this.unitIndex = unitIndex;
        this.unitTemplateKey = unitTemplateKey ?? string.Empty;
        this.level = Math.Max(1, level);
        this.favorability = Math.Max(0, favorability);
        this.baseStats = baseStats;
        this.levelupStats = levelupStats;
        this.eventBonusStats = eventBonusStats;
        this.ingameStats = ingameStats;
        this.currentHp = Math.Max(0f, currentHp);
        this.exp = Math.Max(0, exp);
        this.maxExp = Math.Max(0, maxExp);
        this.currentSkillIndex = Math.Max(0, currentSkillIndex);
        this.skillLevel = Math.Max(1, skillLevel);
        this.currentWeaponIndex = Math.Max(0, currentWeaponIndex);
        this.equippedWeaponInstanceIndex = Math.Max(0, equippedWeaponInstanceIndex);
        this.currentWeaponStats = currentWeaponStats;
    }

    public void ApplyRuntimeState(string nextUnitTemplateKey, int nextLevel, int nextFavorability, StatBlock nextBaseStats, StatBlock nextLevelupStats, int nextCurrentSkillIndex, int nextCurrentWeaponIndex, EquipmentStatBlock nextCurrentWeaponStats, StatBlock nextIngameStats, float nextCurrentHp, int nextExp = -1, int nextMaxExp = -1, int nextSkillLevel = -1, int nextEquippedWeaponInstanceIndex = -1)
    {
        unitTemplateKey = nextUnitTemplateKey ?? string.Empty;
        level = Math.Max(1, nextLevel);
        favorability = Math.Max(0, nextFavorability);
        baseStats = nextBaseStats;
        levelupStats = nextLevelupStats;
        currentSkillIndex = Math.Max(0, nextCurrentSkillIndex);
        if (nextSkillLevel >= 0)
            skillLevel = Math.Max(1, nextSkillLevel);
        currentWeaponIndex = Math.Max(0, nextCurrentWeaponIndex);
        if (nextEquippedWeaponInstanceIndex >= 0)
            equippedWeaponInstanceIndex = Math.Max(0, nextEquippedWeaponInstanceIndex);
        currentWeaponStats = nextCurrentWeaponStats;
        ingameStats = nextIngameStats;
        currentHp = Math.Max(0f, nextCurrentHp);

        if (nextExp >= 0)
            exp = Math.Max(0, nextExp);

        if (nextMaxExp >= 0)
            maxExp = Math.Max(0, nextMaxExp);
    }

    public void SetEventBonusStats(StatBlock nextEventBonusStats)
    {
        eventBonusStats = nextEventBonusStats;
    }
}
