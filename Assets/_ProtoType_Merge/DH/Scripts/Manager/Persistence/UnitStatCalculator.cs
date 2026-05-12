using UnityEngine;

public static class UnitStatCalculator
{
    public static StatBlock CalculateLevelAdjustedBaseStats(StatBlock templateBaseStats, StatBlock levelupStats, int level)
    {
        int safeLevel = Mathf.Max(1, level);
        StatBlock adjustedStats = templateBaseStats;

        if (safeLevel > 1)
            adjustedStats += levelupStats * (safeLevel - 1);

        adjustedStats.ClampToMinimumOne();
        return adjustedStats;
    }

    public static StatBlock CalculateIngameStats(StatBlock templateBaseStats, StatBlock levelupStats, int level, EquipmentStatBlock weaponStats)
    {
        StatBlock adjustedBaseStats = CalculateLevelAdjustedBaseStats(templateBaseStats, levelupStats, level);
        StatBlock result = adjustedBaseStats + weaponStats.ToStatBlock();
        result.ClampToMinimumOne();
        return result;
    }
}
