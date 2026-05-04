using System;

[Serializable]
public class EnemyUnitPersistentData
{
    public int UnitIndex => unitIndex;
    public string UnitTemplateKey => unitTemplateKey;
    public int Level => level;
    public StatBlock BaseStats => baseStats;

    [UnityEngine.SerializeField] private int unitIndex;
    [UnityEngine.SerializeField] private string unitTemplateKey;
    [UnityEngine.SerializeField] private int level;
    [UnityEngine.SerializeField] private StatBlock baseStats;

    public EnemyUnitPersistentData(int unitIndex, string unitTemplateKey, int level, StatBlock baseStats)
    {
        this.unitIndex = Math.Max(1, unitIndex);
        this.unitTemplateKey = unitTemplateKey ?? string.Empty;
        this.level = Math.Max(1, level);
        this.baseStats = baseStats;
    }
}
