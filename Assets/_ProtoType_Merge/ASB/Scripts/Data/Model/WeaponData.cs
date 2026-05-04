using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponData
{
    public int WeaponIndex;
    public string weaponClass;
    public string WeaponName;
    public string WeaponDescription;

    public float BonusHP;
    public float BonusATK;
    public float BonusDEF;
    public float BonusCriticalRate;
    public float BonusCounterRate;
    public float BonusReduceRate;
    public float BonusSpeed;

    public int WeaponSkillIndex;
    public string WeaponSkillName;
    public string WeaponSkillDescription;
    public int IPCost;
    public int WeaponSkillEffect;
    public int WeaponSkillRange;
    public int WeaponSkillRangeLine;
    public int WeaponSkillTarget;
    // TODO: 멀티타겟 실행 로직 추가
    public List<int> WeaponSkillMultiTarget = new List<int>();
    public int WeaponSkillMultiTargetType;
    public int WeaponSkillMultiTargetCount;
    public float WeaponSkillValue;
    public float WeaponSkillSubValue;

    public StatBlock GetBonusStatBlock()
    {
        return new StatBlock(
            hp: BonusHP,
            atk: BonusATK,
            def: BonusDEF,
            luck: 0f,
            speed: BonusSpeed,
            criticalRate: BonusCriticalRate,
            counterRate: BonusCounterRate,
            avoidRate: BonusReduceRate);
    }

    public SkillData ToSkillData()
    {
        var result = new SkillData
        {
            skillIndex = WeaponSkillIndex,
            skillClass = weaponClass,
            acquireLevel = 1,
            skillName = WeaponSkillName,
            description = WeaponSkillDescription,
            ipCost = IPCost,
            classSkillEffect = WeaponSkillEffect,
            classSkillRange = WeaponSkillRange,
            classSkillRangeLine = WeaponSkillRangeLine,
            classSkillTarget = WeaponSkillTarget,
            multiTargetCount = WeaponSkillMultiTargetCount,
            skillValue = WeaponSkillValue,
            skillSubValue = WeaponSkillSubValue,
            boundary = WeaponSkillMultiTarget != null
                ? new List<int>(WeaponSkillMultiTarget)
                : new List<int>()
        };

        return result;
    }
}
