using System;
using UnityEngine;

/// <summary>
/// JsonUtility는 public 필드만 직렬화하므로, 영속 JSON 입출력용 DTO입니다.
/// </summary>
[Serializable]
public struct StatBlockDisk
{
    public float HP;
    public float Atk;
    public float DEF;
    public float Luck;
    public float Speed;
    public float CriticalRate;
    public float CritMultiplier;
    public float CounterRate;
    public float AvoidRate;
    public float Influence;

    public static StatBlockDisk From(StatBlock s)
    {
        return new StatBlockDisk
        {
            HP = s.HP,
            Atk = s.Atk,
            DEF = s.DEF,
            Luck = s.Luck,
            Speed = s.Speed,
            CriticalRate = s.CriticalRate,
            CritMultiplier = s.CritMultiplier,
            CounterRate = s.CounterRate,
            AvoidRate = s.AvoidRate,
            Influence = s.Influence
        };
    }

    public StatBlock ToStatBlock()
    {
        return new StatBlock(
            HP,
            Atk,
            DEF,
            Luck,
            Speed,
            CriticalRate,
            CritMultiplier,
            CounterRate,
            AvoidRate,
            Influence);
    }
}

[Serializable]
public class UnitPersistentDataDiskRow
{
    public int unitIndex;
    public string unitTemplateKey;
    public int level;
    public int favorability;
    public float currentHp;
    public float currentInfluence;
    public bool hasCurrentInfluence;
    public int exp;
    public int maxExp;
    public int currentSkillIndex;
    public int skillLevel;
    public int currentWeaponIndex;
    public int equippedWeaponInstanceIndex;
    public EquipmentStatBlock currentWeaponStats;
    public bool isIncapacitated;
    public StatBlockDisk baseStats;
    public StatBlockDisk levelupStats;
    public StatBlockDisk eventBonusStats;
    public StatBlockDisk ingameStats;

    public static UnitPersistentDataDiskRow From(UnitPersistentData u)
    {
        if (u == null)
            return null;

        return new UnitPersistentDataDiskRow
        {
            unitIndex = u.UnitIndex,
            unitTemplateKey = u.UnitTemplateKey ?? string.Empty,
            level = u.Level,
            favorability = u.Favorability,
            currentHp = u.CurrentHp,
            currentInfluence = u.CurrentInfluence,
            hasCurrentInfluence = true,
            exp = u.Exp,
            maxExp = u.MaxExp,
            currentSkillIndex = u.CurrentSkillIndex,
            skillLevel = u.SkillLevel,
            currentWeaponIndex = u.CurrentWeaponIndex,
            equippedWeaponInstanceIndex = u.EquippedWeaponInstanceIndex,
            currentWeaponStats = u.CurrentWeaponStats,
            isIncapacitated = u.IsIncapacitated,
            baseStats = StatBlockDisk.From(u.BaseStats),
            levelupStats = StatBlockDisk.From(u.LevelupStats),
            eventBonusStats = StatBlockDisk.From(u.EventBonusStats),
            ingameStats = StatBlockDisk.From(u.IngameStats)
        };
    }

    public UnitPersistentData ToUnitPersistentData()
    {
        return new UnitPersistentData(
            unitIndex,
            unitTemplateKey ?? string.Empty,
            level,
            favorability,
            baseStats.ToStatBlock(),
            levelupStats.ToStatBlock(),
            currentSkillIndex,
            currentWeaponIndex,
            currentWeaponStats,
            ingameStats.ToStatBlock(),
            currentHp,
            exp,
            maxExp,
            eventBonusStats.ToStatBlock(),
            skillLevel,
            equippedWeaponInstanceIndex,
            currentInfluence,
            hasCurrentInfluence,
            isIncapacitated);
    }
}

[Serializable]
public class EnemyUnitPersistentDataDiskRow
{
    public int unitIndex;
    public string unitTemplateKey;
    public int level;
    public float currentHp;
    public float currentInfluence;
    public bool hasCurrentInfluence;
    public bool isIncapacitated;
    public StatBlockDisk baseStats;
    public StatBlockDisk ingameStats;

    public static EnemyUnitPersistentDataDiskRow From(EnemyUnitPersistentData u)
    {
        if (u == null)
            return null;

        return new EnemyUnitPersistentDataDiskRow
        {
            unitIndex = u.UnitIndex,
            unitTemplateKey = u.UnitTemplateKey ?? string.Empty,
            level = u.Level,
            currentHp = u.CurrentHp,
            currentInfluence = u.CurrentInfluence,
            hasCurrentInfluence = true,
            isIncapacitated = u.IsIncapacitated,
            baseStats = StatBlockDisk.From(u.BaseStats),
            ingameStats = StatBlockDisk.From(u.IngameStats)
        };
    }

    public EnemyUnitPersistentData ToEnemyUnitPersistentData()
    {
        return new EnemyUnitPersistentData(
            unitIndex,
            unitTemplateKey ?? string.Empty,
            level,
            baseStats.ToStatBlock(),
            ingameStats.ToStatBlock(),
            currentHp,
            currentInfluence,
            hasCurrentInfluence,
            isIncapacitated);
    }
}
