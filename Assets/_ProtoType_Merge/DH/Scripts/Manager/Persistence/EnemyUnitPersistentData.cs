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
    public float CurrentInfluence => currentInfluence;
    public bool IsIncapacitated => isIncapacitated;

    [UnityEngine.SerializeField] private int unitIndex;
    [UnityEngine.SerializeField] private string unitTemplateKey;
    [UnityEngine.SerializeField] private int level;
    [UnityEngine.SerializeField] private StatBlock baseStats;
    [UnityEngine.SerializeField] private StatBlock ingameStats;
    [UnityEngine.SerializeField] private float currentHp;
    [UnityEngine.SerializeField] private float currentInfluence;
    [UnityEngine.SerializeField] private bool isIncapacitated;

    public EnemyUnitPersistentData(int unitIndex, string unitTemplateKey, int level, StatBlock baseStats, StatBlock ingameStats, float currentHp, float currentInfluence = -1f, bool isIncapacitated = false)
    {
        this.unitIndex = Math.Max(1, unitIndex);
        this.unitTemplateKey = unitTemplateKey ?? string.Empty;
        this.level = Math.Max(1, level);
        this.baseStats = baseStats;
        this.ingameStats = ingameStats;
        this.currentHp = Math.Max(0f, currentHp);
        this.currentInfluence = ResolveInitialInfluence(ingameStats, currentInfluence);
        this.isIncapacitated = isIncapacitated;
    }

    public void ApplyRuntimeState(string nextUnitTemplateKey, int nextLevel, StatBlock nextBaseStats, StatBlock nextIngameStats, float nextCurrentHp, float nextCurrentInfluence = -1f, bool? nextIsIncapacitated = null)
    {
        unitTemplateKey = nextUnitTemplateKey ?? string.Empty;
        level = Math.Max(1, nextLevel);
        baseStats = nextBaseStats;
        ingameStats = nextIngameStats;
        currentHp = Math.Max(0f, nextCurrentHp);
        if (nextCurrentInfluence >= 0f)
        {
            currentInfluence = ResolveInitialInfluence(nextIngameStats, nextCurrentInfluence);
        }

        if (nextIsIncapacitated.HasValue)
            isIncapacitated = nextIsIncapacitated.Value;
    }

    private static float ResolveInitialInfluence(StatBlock stats, float requestedCurrentInfluence)
    {
        float maxInfluence = Math.Max(0f, stats.Influence);
        if (requestedCurrentInfluence < 0f)
            return maxInfluence;

        return Math.Min(Math.Max(0f, requestedCurrentInfluence), maxInfluence);
    }
}
