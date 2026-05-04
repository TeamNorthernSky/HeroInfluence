using System;

[Serializable]
public class UnitPersistentData
{
    public int UnitIndex => unitIndex;
    public string UnitTemplateKey => unitTemplateKey;
    public int Level => level;
    public int Favorability => favorability;
    public StatBlock BaseStats => baseStats;

    [UnityEngine.SerializeField] private int unitIndex;
    [UnityEngine.SerializeField] private string unitTemplateKey;
    [UnityEngine.SerializeField] private int level;
    [UnityEngine.SerializeField] private int favorability;
    [UnityEngine.SerializeField] private StatBlock baseStats;

    public UnitPersistentData(int unitIndex)
        : this(unitIndex, string.Empty, 1, 0, default)
    {
    }

    public UnitPersistentData(int unitIndex, string unitTemplateKey, int level, int favorability, StatBlock baseStats)
    {
        this.unitIndex = unitIndex;
        this.unitTemplateKey = unitTemplateKey ?? string.Empty;
        this.level = Math.Max(1, level);
        this.favorability = Math.Max(0, favorability);
        this.baseStats = baseStats;
    }
}
