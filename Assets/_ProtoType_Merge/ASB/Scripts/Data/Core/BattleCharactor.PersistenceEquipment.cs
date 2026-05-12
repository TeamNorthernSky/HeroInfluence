using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 영속 전투 입장: SkillDataLoader / WeaponDataLoader 인덱스로 장착 주입,
/// MarkInitializedFromDataPipeline(preserve:true)로 문자열 기반 재탐색으로 덮어쓰지 않습니다.
/// </summary>
public partial class BattleCharactor
{
    /// <summary>
    /// 로더 인덱스로 스킬·무기 데이터를 직접 주입합니다.
    /// </summary>
    public void LoadPersistentEquipment(int skillIdx, int weaponIdx)
    {
        bool skillLoaded = false;
        bool weaponLoaded = false;

        SkillDataLoader skillLoader = skillDataLoader != null
            ? skillDataLoader
            : Object.FindObjectOfType<SkillDataLoader>(true);

        WeaponDataLoader weaponLoader = Object.FindObjectOfType<WeaponDataLoader>(true);

        if (skillLoader != null && skillIdx > 0)
        {
            SkillData resolved = null;
            if (skillLoader.TryGetSkill(skillIdx, out SkillData direct) && direct != null)
            {
                resolved = direct;
            }
            else
            {
                resolved = TryResolveSkillDataForPlayerPattern(skillLoader, skillIdx);
            }

            if (resolved != null)
            {
                if (availableSkills == null)
                {
                    availableSkills = new List<SkillData>();
                }

                availableSkills.Clear();
                availableSkills.Add(resolved);
                SelectedSkillData = resolved;
                classSkillIndex = resolved.skillIndex;
                selectedSkillIndex = 0;
                skillLoaded = true;
            }
        }

        if (weaponLoader != null && weaponIdx > 0)
        {
            if (weaponLoader.TryGetWeapon(weaponIdx, out WeaponData weaponData) && weaponData != null)
            {
                if (availableWeapons == null)
                {
                    availableWeapons = new List<WeaponData>();
                }

                availableWeapons.Clear();
                availableWeapons.Add(weaponData);
                EquippedWeaponData = weaponData;
                equippedWeaponIndex = 0;
                weaponLoaded = true;
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
        IsDead = false;

        if (!preserveInjectedEquipment)
        {
            RefreshAvailableSkillsForInspector(forceRefresh: true);
            ResolveSelectedSkill(true);
            RefreshAvailableWeapons(forceRefresh: true);
            ResolveEquippedWeapon(true);
        }

        isInitialized = true;
    }

    private static SkillData TryResolveSkillDataForPlayerPattern(SkillDataLoader skillLoader, int skillIdx)
    {
        List<SkillData> all = skillLoader.GetAllSkills();
        for (int i = 0; i < all.Count; i++)
        {
            SkillData s = all[i];
            if (s != null && s.skillIndex == skillIdx)
            {
                return s;
            }
        }

        const int pattern010 = 10;
        for (int i = 0; i < all.Count; i++)
        {
            SkillData s = all[i];
            if (s == null)
            {
                continue;
            }

            if (skillIdx % 1000 == pattern010 && s.skillIndex % 1000 == pattern010)
            {
                int hi = skillIdx / 1000;
                int shi = s.skillIndex / 1000;
                if (hi == 0 || shi == hi || (hi > 0 && s.skillIndex % 1000000 == skillIdx % 1000000))
                {
                    return s;
                }
            }
        }

        return null;
    }
}
