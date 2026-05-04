using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 순수 스탯 계산. BattleCharactor와 결합하지 않습니다.
/// 공식: baseStats * unitCount * unitWeight + (levelWeight + classWeight) * level + 장비 StatBonus 합산 후 HP/Atk/DEF 클램프.
/// </summary>
public static class StatCalculator
{
    public static StatBlock CalculateFinalStats(
        StatBlock baseStats,
        int level,
        int unitCount,
        StatWeights unitWeight,
        StatWeights levelWeight,
        StatWeights classWeight,
        List<EquipmentData> equippedEquipments,
        StatBlock weaponBonus)
    {
        if (equippedEquipments == null)
        {
            equippedEquipments = new List<EquipmentData>();
        }

        level = Mathf.Max(1, level);
        unitCount = Mathf.Max(1, unitCount);

        StatBlock groupStat = baseStats * unitCount;
        groupStat = groupStat * unitWeight;

        StatBlock totalStat = groupStat + (levelWeight + classWeight) * level;

        for (int i = 0; i < equippedEquipments.Count; i++)
        {
            var equipment = equippedEquipments[i];
            if (equipment != null)
            {
                totalStat += equipment.StatBonus;
            }
        }

        totalStat += weaponBonus;

        totalStat.HP = Mathf.Max(1f, totalStat.HP);
        totalStat.Atk = Mathf.Max(1f, totalStat.Atk);
        totalStat.DEF = Mathf.Max(1f, totalStat.DEF);

        return totalStat;
    }
}
