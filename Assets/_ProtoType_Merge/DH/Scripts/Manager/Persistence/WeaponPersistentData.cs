using System;
using UnityEngine;

[Serializable]
public class WeaponPersistentData
{
    public int WeaponIndex => weaponIndex;
    public string WeaponTemplateKey => weaponTemplateKey;
    public int Level => level;
    public EquipmentStatBlock CachedWeaponStats => cachedWeaponStats;

    [SerializeField] private int weaponIndex;
    [SerializeField] private string weaponTemplateKey;
    [SerializeField] private int level = WeaponPersistentRepository.BaseWeaponLevel;
    [SerializeField] private EquipmentStatBlock cachedWeaponStats;

    public WeaponPersistentData(int weaponIndex, string weaponTemplateKey)
    {
        this.weaponIndex = weaponIndex;
        this.weaponTemplateKey = weaponTemplateKey ?? string.Empty;
        level = WeaponPersistentRepository.BaseWeaponLevel;
    }

    public void SetWeaponTemplateKey(string nextWeaponTemplateKey)
    {
        weaponTemplateKey = nextWeaponTemplateKey ?? string.Empty;
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
