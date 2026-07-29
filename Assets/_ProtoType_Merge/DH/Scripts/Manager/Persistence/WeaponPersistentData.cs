using System;
using UnityEngine;

[Serializable]
public class WeaponPersistentData
{
    public int WeaponIndex => weaponIndex;
    public int WeaponTemplateKey => weaponTemplateKey;
    public int Level => level;
    public EquipmentStatBlock CachedWeaponStats => cachedWeaponStats;

    [SerializeField] private int weaponIndex;
    [SerializeField] private int weaponTemplateKey;
    [SerializeField] private int level = WeaponPersistentRepository.BaseWeaponLevel;
    [SerializeField] private EquipmentStatBlock cachedWeaponStats;

    public WeaponPersistentData(int weaponIndex, int weaponTemplateKey)
    {
        this.weaponIndex = weaponIndex;
        this.weaponTemplateKey = Math.Max(0, weaponTemplateKey);
        level = WeaponPersistentRepository.BaseWeaponLevel;
    }

    public void SetWeaponTemplateKey(int nextWeaponTemplateKey)
    {
        weaponTemplateKey = Math.Max(0, nextWeaponTemplateKey);
    }

    public void SetLevel(int nextLevel)
    {
        level = Math.Max(WeaponPersistentRepository.BaseWeaponLevel, nextLevel);
    }

    public void SetCachedWeaponStats(EquipmentStatBlock nextStats)
    {
        cachedWeaponStats = nextStats;
    }
}
