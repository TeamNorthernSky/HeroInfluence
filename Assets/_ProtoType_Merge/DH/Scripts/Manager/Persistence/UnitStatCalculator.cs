using UnityEngine;
using System.Collections.Generic;

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
        return CalculateIngameStats(templateBaseStats, levelupStats, level, weaponStats, null);
    }

    public static StatBlock CalculateIngameStats(
        StatBlock templateBaseStats,
        StatBlock levelupStats,
        int level,
        EquipmentStatBlock weaponStats,
        IReadOnlyList<LevelUpData> levelUpTable)
    {
        StatBlock adjustedBaseStats = CalculateLevelAdjustedBaseStats(templateBaseStats, levelupStats, level);
        StatBlock result = adjustedBaseStats + weaponStats.ToStatBlock();
        result.Influence += CalculateLevelInfluenceBonus(level, levelUpTable);
        result.ClampToMinimumOne();
        return result;
    }

    private static float CalculateLevelInfluenceBonus(int level, IReadOnlyList<LevelUpData> levelUpTable)
    {
        if (levelUpTable == null || levelUpTable.Count == 0)
            return 0f;

        int safeLevel = Mathf.Max(1, level);
        float bonus = 0f;
        for (int i = 0; i < levelUpTable.Count; i++)
        {
            LevelUpData row = levelUpTable[i];
            if (row == null || row.MaxIP <= 0)
                continue;

            if (Mathf.FloorToInt(row.level) <= safeLevel)
                bonus += row.MaxIP;
        }

        return bonus;
    }
}
