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
    public StatBlock IngameStats => ingameStats;
    public float CurrentHp => currentHp;
    public int CurrentSkillIndex => currentSkillIndex;
    public int CurrentWeaponIndex => currentWeaponIndex;
    public EquipmentStatBlock CurrentWeaponStats => currentWeaponStats;

    [UnityEngine.SerializeField] private int unitIndex;
    [UnityEngine.SerializeField] private string unitTemplateKey;
    [UnityEngine.SerializeField] private int level;
    [UnityEngine.SerializeField] private int favorability;
    [UnityEngine.SerializeField] private StatBlock baseStats;
    [UnityEngine.SerializeField] private StatBlock levelupStats;
    [UnityEngine.SerializeField] private StatBlock ingameStats;
    [UnityEngine.SerializeField] private float currentHp;
    [UnityEngine.SerializeField] private int currentSkillIndex;
    [UnityEngine.SerializeField] private int currentWeaponIndex;
    [UnityEngine.SerializeField] private EquipmentStatBlock currentWeaponStats;

    public UnitPersistentData(int unitIndex)
        : this(unitIndex, string.Empty, 1, 0, default, default, 0, 0, default, default, 0f)
    {
    }

    public UnitPersistentData(int unitIndex, string unitTemplateKey, int level, int favorability, StatBlock baseStats, StatBlock levelupStats, int currentSkillIndex, int currentWeaponIndex, EquipmentStatBlock currentWeaponStats, StatBlock ingameStats, float currentHp)
    {
        this.unitIndex = unitIndex;
        this.unitTemplateKey = unitTemplateKey ?? string.Empty;
        this.level = Math.Max(1, level);
        this.favorability = Math.Max(0, favorability);
        this.baseStats = baseStats;
        this.levelupStats = levelupStats;
        this.ingameStats = ingameStats;
        this.currentHp = Math.Max(0f, currentHp);
        this.currentSkillIndex = Math.Max(0, currentSkillIndex);
        this.currentWeaponIndex = Math.Max(0, currentWeaponIndex);
        this.currentWeaponStats = currentWeaponStats;
    }

    public void ApplyRuntimeState(string nextUnitTemplateKey, int nextLevel, int nextFavorability, StatBlock nextBaseStats, StatBlock nextLevelupStats, int nextCurrentSkillIndex, int nextCurrentWeaponIndex, EquipmentStatBlock nextCurrentWeaponStats, StatBlock nextIngameStats, float nextCurrentHp)
    {
        unitTemplateKey = nextUnitTemplateKey ?? string.Empty;
        level = Math.Max(1, nextLevel);
        favorability = Math.Max(0, nextFavorability);
        baseStats = nextBaseStats;
        levelupStats = nextLevelupStats;
        currentSkillIndex = Math.Max(0, nextCurrentSkillIndex);
        currentWeaponIndex = Math.Max(0, nextCurrentWeaponIndex);
        currentWeaponStats = nextCurrentWeaponStats;
        ingameStats = nextIngameStats;
        currentHp = Math.Max(0f, nextCurrentHp);
    }
}
