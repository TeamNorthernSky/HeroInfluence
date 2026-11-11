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
        StatBlock unitStats = CalculateUnitStats(templateBaseStats, levelupStats, level,
            eventBonusStats, unitGrowthTable);
        return ApplyCoreBonus(unitStats, weaponStats);
    }

    /// <summary>코어를 제외한 유닛 자체의 능력치: 기본값 + 성장 + 이벤트 보상.</summary>
    public static StatBlock CalculateUnitStats(
        StatBlock templateBaseStats, StatBlock levelupStats, int level,
        StatBlock eventBonusStats, IReadOnlyList<DHUnitGrowthTemplate> unitGrowthTable)
    {
        StatBlock unitStats = CalculateLevelAdjustedBaseStats(templateBaseStats, levelupStats, level)
            + eventBonusStats;
        unitStats.Influence += CalculateLevelInfluenceBonus(level, unitGrowthTable);
        return unitStats;
    }

    /// <summary>장착 코어 종류와 레벨에서 조회한 추가 능력치 구조체를 합산한다. 미장착은 default.</summary>
    public static StatBlock ApplyCoreBonus(StatBlock unitStats, EquipmentStatBlock coreBonusStats)
    {
        StatBlock result = unitStats + coreBonusStats.ToStatBlock();
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
