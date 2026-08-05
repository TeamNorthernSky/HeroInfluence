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
        return CalculateIngameStats(templateBaseStats, levelupStats, level, weaponStats, default, (IReadOnlyList<DHUnitGrowthTemplate>)null);
    }

    public static StatBlock CalculateIngameStats(
        StatBlock templateBaseStats,
        StatBlock levelupStats,
        int level,
        EquipmentStatBlock weaponStats,
        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTable)
    {
        return CalculateIngameStats(templateBaseStats, levelupStats, level, weaponStats, default, unitGrowthTable);
    }

    public static StatBlock CalculateIngameStats(
        StatBlock templateBaseStats,
        StatBlock levelupStats,
        int level,
        EquipmentStatBlock weaponStats,
        StatBlock eventBonusStats,
        IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTable)
    {
        StatBlock adjustedBaseStats = CalculateLevelAdjustedBaseStats(templateBaseStats, levelupStats, level);
        StatBlock result = adjustedBaseStats + weaponStats.ToStatBlock() + eventBonusStats;
        result.Influence += CalculateLevelInfluenceBonus(level, unitGrowthTable);
        result.ClampToMinimumOne();
        return result;
    }

    private static float CalculateLevelInfluenceBonus(int level, IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTable)
    {
        if (unitGrowthTable == null || unitGrowthTable.Count == 0)
            return 0f;

        int safeLevel = Mathf.Max(1, level);
        float bonus = 0f;
        for (int i = 0; i < unitGrowthTable.Count; i++)
        {
            DHUnitGrowthTemplate row = unitGrowthTable[i];
            if (row == null || row.AddInfluence <= 0)
                continue;

            if (row.Level <= safeLevel)
                bonus += row.AddInfluence;
        }

        return bonus;
    }
}
