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
    public int WeaponSkillEffect;
    public int WeaponSkillRange;
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
            classSkillEffect = WeaponSkillEffect,
            classSkillRange = WeaponSkillRange,
            classSkillTarget = WeaponSkillTarget,
            multiTargetType = WeaponSkillMultiTargetType,
            multiTargetCount = WeaponSkillMultiTargetCount,
            skillValue = WeaponSkillValue,
            skillSubValue = WeaponSkillSubValue,
            boundary = BuildManhattanBoundary(WeaponSkillRange),
            aoePatternIndices = WeaponSkillMultiTarget != null
                ? new List<int>(WeaponSkillMultiTarget)
                : new List<int>()
        };

        return result;
    }

    private static List<Vector2Int> BuildManhattanBoundary(int range)
    {
        int safeRange = Mathf.Max(0, range);
        var boundary = new List<Vector2Int>();

        for (int x = -safeRange; x <= safeRange; x++)
        {
            for (int y = -safeRange; y <= safeRange; y++)
            {
                int dist = Mathf.Abs(x) + Mathf.Abs(y);
                if (dist == 0)
                {
                    continue;
                }

                if (dist <= safeRange)
                {
                    boundary.Add(new Vector2Int(x, y));
                }
            }
        }

        return boundary;
    }
}
