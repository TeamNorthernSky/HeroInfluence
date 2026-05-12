using System;

[Serializable]
public class EnemyUnitPersistentData
{
    public int UnitIndex => unitIndex;
    public string UnitTemplateKey => unitTemplateKey;
    public int Level => level;
    public StatBlock BaseStats => baseStats;
    public StatBlock IngameStats => ingameStats;
    public float CurrentHp => currentHp;

    [UnityEngine.SerializeField] private int unitIndex;
    [UnityEngine.SerializeField] private string unitTemplateKey;
    [UnityEngine.SerializeField] private int level;
    [UnityEngine.SerializeField] private StatBlock baseStats;
    [UnityEngine.SerializeField] private StatBlock ingameStats;
    [UnityEngine.SerializeField] private float currentHp;

    public EnemyUnitPersistentData(int unitIndex, string unitTemplateKey, int level, StatBlock baseStats, StatBlock ingameStats, float currentHp)
    {
        this.unitIndex = Math.Max(1, unitIndex);
        this.unitTemplateKey = unitTemplateKey ?? string.Empty;
        this.level = Math.Max(1, level);
        this.baseStats = baseStats;
        this.ingameStats = ingameStats;
        this.currentHp = Math.Max(0f, currentHp);
    }

    public void ApplyRuntimeState(string nextUnitTemplateKey, int nextLevel, StatBlock nextBaseStats, StatBlock nextIngameStats, float nextCurrentHp)
    {
        unitTemplateKey = nextUnitTemplateKey ?? string.Empty;
        level = Math.Max(1, nextLevel);
        baseStats = nextBaseStats;
        ingameStats = nextIngameStats;
        currentHp = Math.Max(0f, nextCurrentHp);
    }
}
