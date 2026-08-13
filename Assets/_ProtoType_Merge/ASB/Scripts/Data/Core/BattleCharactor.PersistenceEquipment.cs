using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영속 전투 입장: DHCsvTemplateCatalog 인덱스로 장착 주입,
/// MarkInitializedFromDataPipeline(preserve:true)로 문자열 기반 재탐색으로 덮어쓰지 않습니다.
/// </summary>
public partial class BattleCharactor
{
    /// <summary>
    /// 카탈로그 인덱스로 스킬·무기 데이터를 직접 주입합니다.
    /// </summary>
    public void LoadPersistentEquipment(int skillIdx, int weaponIdx, int skillLevel = 1, int equippedWeaponInstanceIndex = 0)
    {
        bool skillLoaded  = false;
        bool weaponLoaded = false;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        int safeSkillLevel = Mathf.Max(1, skillLevel);
        int resolvedWeaponLevel = ResolveWeaponLevel(equippedWeaponInstanceIndex);

        if (catalog != null && skillIdx > 0)
        {
            SkillData resolved = catalog.GetSkillTemplate(skillIdx);
            if (resolved == null)
            {
                resolved = TryResolveSkillDataForPlayerPattern(catalog, skillIdx);
            }

            if (resolved != null)
            {
                SkillData leveledSkill = CloneSkillData(resolved);
                leveledSkill.skillValue = catalog.GetClassSkillValueAtLevel(skillIdx, safeSkillLevel);
                leveledSkill.skillSubValue = catalog.GetClassSkillSubValueAtLevel(skillIdx, safeSkillLevel);

                if (availableSkills == null) availableSkills = new List<SkillData>();
                availableSkills.Clear();
                availableSkills.Add(leveledSkill);
                SelectedSkillData    = leveledSkill;
                classSkillIndex      = leveledSkill.skillIndex;
                selectedSkillIndex   = 0;
                skillLoaded          = true;
            }
        }

        if (catalog != null && weaponIdx > 0)
        {
            if (catalog.TryGetWeapon(weaponIdx, out WeaponData weaponData) && weaponData != null)
            {
                WeaponData leveledWeapon = CloneWeaponData(weaponData);
                leveledWeapon.WeaponSkillValue = catalog.GetWeaponSkillValueAtLevel(weaponIdx, resolvedWeaponLevel);
                leveledWeapon.WeaponSkillSubValue = catalog.GetWeaponSkillSubValueAtLevel(weaponIdx, resolvedWeaponLevel);

                if (availableWeapons == null) availableWeapons = new List<WeaponData>();
                availableWeapons.Clear();
                availableWeapons.Add(leveledWeapon);
                EquippedWeaponData  = leveledWeapon;
                equippedWeaponIndex = 0;
                weaponLoaded        = true;
            }
        }

        if (skillLoaded || weaponLoaded)
        {
            Debug.Log($"[Combat/Init] {UnitName} Load Success (S:{skillIdx}, W:{weaponIdx})");
        }
    }

    /// <summary>
    /// 데이터 파이프라인 완료 표시. preserveInjectedEquipment가 true면 로더로 주입한 스킬/무기를
    /// RefreshAvailableSkills/Weapons로 덮어쓰지 않습니다.
    /// </summary>
    public void MarkInitializedFromDataPipeline(bool preserveInjectedEquipment = false)
    {
        IsPlayer = teamType == TeamType.Player;
        IsDead   = false;
        HasUsedRevive = false;
        ClearCharge();      // 충전 예약(+charging 마커) 리셋 — AI 인스턴스 재사용 대비
        ResetEnergyStack(); // 에너지 스택 리셋

        if (!preserveInjectedEquipment)
        {
            RefreshAvailableSkillsForInspector(forceRefresh: true);
            ResolveSelectedSkill(true);
            RefreshAvailableWeapons(forceRefresh: true);
            ResolveEquippedWeapon(true);
        }

        isInitialized = true;
    }

    private static SkillData TryResolveSkillDataForPlayerPattern(DHCsvTemplateCatalog catalog, int skillIdx)
    {
        List<SkillData> all = catalog.GetAllSkills();
        for (int i = 0; i < all.Count; i++)
        {
            SkillData s = all[i];
            if (s != null && s.skillIndex == skillIdx) return s;
        }

        const int pattern010 = 10;
        for (int i = 0; i < all.Count; i++)
        {
            SkillData s = all[i];
            if (s == null) continue;

            if (skillIdx % 1000 == pattern010 && s.skillIndex % 1000 == pattern010)
            {
                int hi  = skillIdx / 1000;
                int shi = s.skillIndex / 1000;
                if (hi == 0 || shi == hi || (hi > 0 && s.skillIndex % 1000000 == skillIdx % 1000000))
                {
                    return s;
                }
            }
        }

        return null;
    }

    private static int ResolveWeaponLevel(int equippedWeaponInstanceIndex)
    {
        const int baseLevel = 1;
        if (equippedWeaponInstanceIndex <= 0)
        {
            return baseLevel;
        }

        WeaponPersistentRepository weaponRepository = WeaponPersistentRepository.Instance;
        if (weaponRepository != null &&
            weaponRepository.TryGetWeapon(equippedWeaponInstanceIndex, out WeaponPersistentData persistentWeapon) &&
            persistentWeapon != null)
        {
            return Mathf.Max(baseLevel, persistentWeapon.Level);
        }

        return baseLevel;
    }

    private static SkillData CloneSkillData(SkillData source)
    {
        if (source == null)
        {
            return null;
        }

        return new SkillData
        {
            skillIndex = source.skillIndex,
            skillKey = source.skillKey,
            category = source.category,
            slot = source.slot,
            skillClass = source.skillClass,
            acquireLevel = source.acquireLevel,
            skillName = source.skillName,
            description = source.description,
            ipCost = source.ipCost,
            classSkillEffect = source.classSkillEffect,
            classSkillRange = source.classSkillRange,
            EnemySkill1Range = source.EnemySkill1Range,
            EnemySkill2Range = source.EnemySkill2Range,
            classSkillRangeLine = source.classSkillRangeLine,
            classSkillTarget = source.classSkillTarget,
            boundary = source.boundary != null ? new List<int>(source.boundary) : new List<int>(),
            multiTargetType = source.multiTargetType,
            multiTargetCount = source.multiTargetCount,
            skillValue = source.skillValue,
            skillSubValue = source.skillSubValue,
            AnimationTrigger = source.AnimationTrigger,
            StateName = source.StateName,
            UseAnimEvent = source.UseAnimEvent,
            HitDelay = source.HitDelay,
            TotalDelay = source.TotalDelay,
            TargetAnimationTrigger = source.TargetAnimationTrigger
        };
    }

    private static WeaponData CloneWeaponData(WeaponData source)
    {
        if (source == null)
        {
            return null;
        }

        return new WeaponData
        {
            WeaponIndex = source.WeaponIndex,
            weaponKey = source.weaponKey,
            weaponClass = source.weaponClass,
            WeaponName = source.WeaponName,
            WeaponDescription = source.WeaponDescription,
            BonusHP = source.BonusHP,
            BonusATK = source.BonusATK,
            BonusDEF = source.BonusDEF,
            BonusCriticalRate = source.BonusCriticalRate,
            BonusCounterRate = source.BonusCounterRate,
            BonusReduceRate = source.BonusReduceRate,
            BonusSpeed = source.BonusSpeed,
            WeaponSkillIndex = source.WeaponSkillIndex,
            weaponSkillKey = source.weaponSkillKey,
            WeaponSkillName = source.WeaponSkillName,
            WeaponSkillDescription = source.WeaponSkillDescription,
            IPCost = source.IPCost,
            WeaponSkillEffect = source.WeaponSkillEffect,
            WeaponSkillRange = source.WeaponSkillRange,
            WeaponSkillRangeLine = source.WeaponSkillRangeLine,
            WeaponSkillTarget = source.WeaponSkillTarget,
            WeaponSkillMultiTarget = source.WeaponSkillMultiTarget != null
                ? new List<int>(source.WeaponSkillMultiTarget)
                : new List<int>(),
            WeaponSkillMultiTargetType = source.WeaponSkillMultiTargetType,
            WeaponSkillMultiTargetCount = source.WeaponSkillMultiTargetCount,
            WeaponSkillValue = source.WeaponSkillValue,
            WeaponSkillSubValue = source.WeaponSkillSubValue
        };
    }
}
