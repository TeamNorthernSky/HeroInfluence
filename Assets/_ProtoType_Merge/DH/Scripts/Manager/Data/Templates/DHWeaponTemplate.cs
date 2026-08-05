using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHWeaponTemplate
{
    private readonly List<int> multiTargetCells;

    public string WeaponKey { get; }
    public int NumericWeaponId { get; }
    public string WeaponClass { get; }
    public string WeaponName { get; }
    public string WeaponDescription { get; }
    public float BonusHPLv1 { get; }
    public float BonusHPLv2 { get; }
    public float BonusHPLv3 { get; }
    public float BonusHPLv4 { get; }
    public float BonusHPLv5 { get; }
    public float BonusATKLv1 { get; }
    public float BonusATKLv2 { get; }
    public float BonusATKLv3 { get; }
    public float BonusATKLv4 { get; }
    public float BonusATKLv5 { get; }
    public float BonusDEFLv1 { get; }
    public float BonusDEFLv2 { get; }
    public float BonusDEFLv3 { get; }
    public float BonusDEFLv4 { get; }
    public float BonusDEFLv5 { get; }
    public float BonusCriticalRate { get; }
    public float BonusCounterRate { get; }
    public float BonusReduceRate { get; }
    public float BonusSpeed { get; }
    public int WeaponSkillIndex { get; }
    public string WeaponSkillName { get; }
    public string WeaponSkillDescription { get; }
    public int IpCost { get; }
    public int WeaponSkillEffect { get; }
    public int WeaponSkillRange { get; }
    public int WeaponSkillRangeLine { get; }
    public int WeaponSkillTarget { get; }
    public IReadOnlyList<int> WeaponSkillMultiTarget => multiTargetCells;
    public int WeaponSkillMultiTargetType { get; }
    public int WeaponSkillMultiTargetCount { get; }
    public float WeaponSkillValueLv1 { get; }
    public float WeaponSkillSubValueLv1 { get; }
    public float WeaponSkillValueLv2 { get; }
    public float WeaponSkillSubValueLv2 { get; }
    public float WeaponSkillValueLv3 { get; }
    public float WeaponSkillSubValueLv3 { get; }
    public float WeaponSkillValueLv4 { get; }
    public float WeaponSkillSubValueLv4 { get; }
    public float WeaponSkillValueLv5 { get; }
    public float WeaponSkillSubValueLv5 { get; }

    public DHWeaponTemplate(
        string weaponKey,
        int numericWeaponId,
        string weaponClass,
        string weaponName,
        string weaponDescription,
        float bonusHPLv1,
        float bonusHPLv2,
        float bonusHPLv3,
        float bonusHPLv4,
        float bonusHPLv5,
        float bonusATKLv1,
        float bonusATKLv2,
        float bonusATKLv3,
        float bonusATKLv4,
        float bonusATKLv5,
        float bonusDEFLv1,
        float bonusDEFLv2,
        float bonusDEFLv3,
        float bonusDEFLv4,
        float bonusDEFLv5,
        float bonusCriticalRate,
        float bonusCounterRate,
        float bonusReduceRate,
        float bonusSpeed,
        int weaponSkillIndex,
        string weaponSkillName,
        string weaponSkillDescription,
        int ipCost,
        int weaponSkillEffect,
        int weaponSkillRange,
        int weaponSkillRangeLine,
        int weaponSkillTarget,
        IEnumerable<int> weaponSkillMultiTarget,
        int weaponSkillMultiTargetType,
        int weaponSkillMultiTargetCount,
        float weaponSkillValueLv1,
        float weaponSkillSubValueLv1,
        float weaponSkillValueLv2,
        float weaponSkillSubValueLv2,
        float weaponSkillValueLv3,
        float weaponSkillSubValueLv3,
        float weaponSkillValueLv4,
        float weaponSkillSubValueLv4,
        float weaponSkillValueLv5,
        float weaponSkillSubValueLv5)
    {
        WeaponKey = string.IsNullOrWhiteSpace(weaponKey) ? string.Empty : weaponKey.Trim();
        NumericWeaponId = Math.Max(0, numericWeaponId);
        WeaponClass = string.IsNullOrWhiteSpace(weaponClass) ? string.Empty : weaponClass.Trim();
        WeaponName = string.IsNullOrWhiteSpace(weaponName) ? string.Empty : weaponName.Trim();
        WeaponDescription = string.IsNullOrWhiteSpace(weaponDescription) ? string.Empty : weaponDescription.Trim();
        BonusHPLv1 = bonusHPLv1;
        BonusHPLv2 = bonusHPLv2;
        BonusHPLv3 = bonusHPLv3;
        BonusHPLv4 = bonusHPLv4;
        BonusHPLv5 = bonusHPLv5;
        BonusATKLv1 = bonusATKLv1;
        BonusATKLv2 = bonusATKLv2;
        BonusATKLv3 = bonusATKLv3;
        BonusATKLv4 = bonusATKLv4;
        BonusATKLv5 = bonusATKLv5;
        BonusDEFLv1 = bonusDEFLv1;
        BonusDEFLv2 = bonusDEFLv2;
        BonusDEFLv3 = bonusDEFLv3;
        BonusDEFLv4 = bonusDEFLv4;
        BonusDEFLv5 = bonusDEFLv5;
        BonusCriticalRate = bonusCriticalRate;
        BonusCounterRate = bonusCounterRate;
        BonusReduceRate = bonusReduceRate;
        BonusSpeed = bonusSpeed;
        WeaponSkillIndex = Math.Max(0, weaponSkillIndex);
        WeaponSkillName = string.IsNullOrWhiteSpace(weaponSkillName) ? string.Empty : weaponSkillName.Trim();
        WeaponSkillDescription = string.IsNullOrWhiteSpace(weaponSkillDescription) ? string.Empty : weaponSkillDescription.Trim();
        IpCost = Math.Max(0, ipCost);
        WeaponSkillEffect = weaponSkillEffect;
        WeaponSkillRange = weaponSkillRange;
        WeaponSkillRangeLine = weaponSkillRangeLine;
        WeaponSkillTarget = weaponSkillTarget;
        multiTargetCells = weaponSkillMultiTarget != null ? new List<int>(weaponSkillMultiTarget) : new List<int>();
        WeaponSkillMultiTargetType = weaponSkillMultiTargetType;
        WeaponSkillMultiTargetCount = weaponSkillMultiTargetCount;
        WeaponSkillValueLv1 = weaponSkillValueLv1;
        WeaponSkillSubValueLv1 = weaponSkillSubValueLv1;
        WeaponSkillValueLv2 = weaponSkillValueLv2;
        WeaponSkillSubValueLv2 = weaponSkillSubValueLv2;
        WeaponSkillValueLv3 = weaponSkillValueLv3;
        WeaponSkillSubValueLv3 = weaponSkillSubValueLv3;
        WeaponSkillValueLv4 = weaponSkillValueLv4;
        WeaponSkillSubValueLv4 = weaponSkillSubValueLv4;
        WeaponSkillValueLv5 = weaponSkillValueLv5;
        WeaponSkillSubValueLv5 = weaponSkillSubValueLv5;
    }

    public StatBlock GetBonusStatsAtLevel(int level)
    {
        return new StatBlock(
            hp: Leveled(BonusHPLv1, BonusHPLv2, BonusHPLv3, BonusHPLv4, BonusHPLv5, level),
            atk: Leveled(BonusATKLv1, BonusATKLv2, BonusATKLv3, BonusATKLv4, BonusATKLv5, level),
            def: Leveled(BonusDEFLv1, BonusDEFLv2, BonusDEFLv3, BonusDEFLv4, BonusDEFLv5, level),
            luck: 0f,
            speed: BonusSpeed,
            criticalRate: BonusCriticalRate,
            counterRate: BonusCounterRate,
            avoidRate: BonusReduceRate);
    }

    public float GetSkillValueAtLevel(int level)
    {
        return Leveled(WeaponSkillValueLv1, WeaponSkillValueLv2, WeaponSkillValueLv3, WeaponSkillValueLv4, WeaponSkillValueLv5, level);
    }

    public float GetSkillSubValueAtLevel(int level)
    {
        return Leveled(WeaponSkillSubValueLv1, WeaponSkillSubValueLv2, WeaponSkillSubValueLv3, WeaponSkillSubValueLv4, WeaponSkillSubValueLv5, level);
    }

    private static float Leveled(float lv1, float lv2, float lv3, float lv4, float lv5, int level)
    {
        switch (level)
        {
            case 2: return lv2;
            case 3: return lv3;
            case 4: return lv4;
            case 5: return lv5;
            default: return lv1;
        }
    }
}
