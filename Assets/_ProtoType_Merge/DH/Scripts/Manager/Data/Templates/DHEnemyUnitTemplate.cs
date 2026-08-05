using System;

[Serializable]
public sealed class DHEnemyUnitTemplate
{
    public string EnemyKey { get; }
    public int NumericEnemyId { get; }
    public string EnemyName { get; }
    public string EnemyConcept { get; }
    public string UnitAI { get; }
    public int ExperiencePoint { get; }
    public StatBlock BaseStats { get; }
    public StatBlock LevelupStats { get; }

    public DHEnemyUnitTemplate(
        string enemyKey,
        int numericEnemyId,
        string enemyName,
        string enemyConcept,
        string unitAI,
        int experiencePoint,
        StatBlock baseStats,
        StatBlock levelupStats)
    {
        EnemyKey = string.IsNullOrWhiteSpace(enemyKey) ? string.Empty : enemyKey.Trim();
        NumericEnemyId = Math.Max(0, numericEnemyId);
        EnemyName = string.IsNullOrWhiteSpace(enemyName) ? string.Empty : enemyName.Trim();
        EnemyConcept = string.IsNullOrWhiteSpace(enemyConcept) ? string.Empty : enemyConcept.Trim();
        UnitAI = string.IsNullOrWhiteSpace(unitAI) ? string.Empty : unitAI.Trim();
        ExperiencePoint = Math.Max(0, experiencePoint);
        BaseStats = baseStats;
        LevelupStats = levelupStats;
    }
}
