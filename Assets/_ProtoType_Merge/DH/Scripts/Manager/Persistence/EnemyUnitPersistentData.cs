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
    public float CurrentInfluence => hasCurrentInfluence ? currentInfluence : Math.Max(0f, ingameStats.Influence);

    [UnityEngine.SerializeField] private int unitIndex;
    [UnityEngine.SerializeField] private string unitTemplateKey;
    [UnityEngine.SerializeField] private int level;
    [UnityEngine.SerializeField] private StatBlock baseStats;
    [UnityEngine.SerializeField] private StatBlock ingameStats;
    [UnityEngine.SerializeField] private float currentHp;
    [UnityEngine.SerializeField] private float currentInfluence;
    [UnityEngine.SerializeField] private bool hasCurrentInfluence;

    public EnemyUnitPersistentData(int unitIndex, string unitTemplateKey, int level, StatBlock baseStats, StatBlock ingameStats, float currentHp, float currentInfluence = -1f, bool hasCurrentInfluence = true)
    {
        this.unitIndex = Math.Max(1, unitIndex);
        this.unitTemplateKey = unitTemplateKey ?? string.Empty;
        this.level = Math.Max(1, level);
        this.baseStats = baseStats;
        this.ingameStats = ingameStats;
        this.currentHp = Math.Max(0f, currentHp);
        this.hasCurrentInfluence = hasCurrentInfluence;
        this.currentInfluence = hasCurrentInfluence ? ResolveInitialInfluence(ingameStats, currentInfluence) : 0f;
    }

    public void ApplyRuntimeState(string nextUnitTemplateKey, int nextLevel, StatBlock nextBaseStats, StatBlock nextIngameStats, float nextCurrentHp, float nextCurrentInfluence = -1f)
    {
        unitTemplateKey = nextUnitTemplateKey ?? string.Empty;
        level = Math.Max(1, nextLevel);
        baseStats = nextBaseStats;
        ingameStats = nextIngameStats;
        currentHp = Math.Max(0f, nextCurrentHp);
        if (nextCurrentInfluence >= 0f)
        {
            currentInfluence = ResolveInitialInfluence(nextIngameStats, nextCurrentInfluence);
            hasCurrentInfluence = true;
        }
    }

    private static float ResolveInitialInfluence(StatBlock stats, float requestedCurrentInfluence)
    {
        float maxInfluence = Math.Max(0f, stats.Influence);
        if (requestedCurrentInfluence < 0f)
            return maxInfluence;

        return Math.Min(Math.Max(0f, requestedCurrentInfluence), maxInfluence);
    }
}
