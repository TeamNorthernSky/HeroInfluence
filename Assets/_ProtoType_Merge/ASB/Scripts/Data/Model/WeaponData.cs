using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponData
{
    public int WeaponIndex;             // [TEMP:STRKEY] 레거시 int 키 브리지 / 제거조건: 무기 소비부 string 이행 완료 후
    public string weaponKey;            // 원본 무기 키(HC001) 보존 — 손실 없음
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

    public int WeaponSkillIndex;        // [TEMP:STRKEY] 레거시 int 브리지 / 제거조건: SkillData.skillIndex string화 + 무기스킬 아이콘 string화 완료 후
    public string weaponSkillKey;       // 원본 무기 스킬 키(HCS001) 보존 — 손실 없음
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
            skillIndex = WeaponSkillIndex,   // [TEMP:STRKEY] 레거시 int 브리지
            skillKey = weaponSkillKey,
            category = SkillCategory.Weapon,
            skillClass = weaponClass,
            acquireLevel = 1,
            skillName = WeaponSkillName,
            description = WeaponSkillDescription,
            ipCost = IPCost,
            classSkillEffect = WeaponSkillEffect,
            classSkillRange = WeaponSkillRange,
            classSkillRangeLine = WeaponSkillRangeLine,
            classSkillTarget = WeaponSkillTarget,
            multiTargetType = WeaponSkillMultiTargetType,
            multiTargetCount = WeaponSkillMultiTargetCount,
            skillValue = WeaponSkillValue,
            skillSubValue = WeaponSkillSubValue,
            boundary = WeaponSkillMultiTarget != null
                ? new List<int>(WeaponSkillMultiTarget)
                : new List<int>(),
            AnimationTrigger = "Attack",
            StateName = string.Empty,
            UseAnimEvent = false,
            HitDelay = 0.25f,
            TotalDelay = 0.5f,
            TargetAnimationTrigger = string.Empty
        };

        return result;
    }
}
