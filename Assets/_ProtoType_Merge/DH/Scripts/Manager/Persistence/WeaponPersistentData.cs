using System;
using UnityEngine;

[Serializable]
public class WeaponPersistentData
{
    public int WeaponIndex => weaponIndex;
    public string WeaponTemplateKeyString => weaponTemplateKeyString ?? string.Empty;
    public int WeaponTemplateKey => weaponTemplateKey;
    public int Level => level;
    public EquipmentStatBlock CachedWeaponStats => cachedWeaponStats;

    [SerializeField] private int weaponIndex;
    [SerializeField] private string weaponTemplateKeyString;
    [SerializeField, HideInInspector] private int weaponTemplateKey;
    [SerializeField] private int level = WeaponPersistentRepository.BaseWeaponLevel;
    [SerializeField] private EquipmentStatBlock cachedWeaponStats;

    public WeaponPersistentData(int weaponIndex, int weaponTemplateKey)
        : this(weaponIndex, string.Empty, weaponTemplateKey)
    {
    }

    public WeaponPersistentData(int weaponIndex, string weaponTemplateKey)
        : this(weaponIndex, weaponTemplateKey, 0)
    {
    }

    public WeaponPersistentData(int weaponIndex, string weaponTemplateKey, int legacyNumericWeaponTemplateKey)
    {
        this.weaponIndex = weaponIndex;
        weaponTemplateKeyString = string.IsNullOrWhiteSpace(weaponTemplateKey) ? string.Empty : weaponTemplateKey.Trim();
        this.weaponTemplateKey = Math.Max(0, legacyNumericWeaponTemplateKey);
        level = WeaponPersistentRepository.BaseWeaponLevel;
    }

    public void SetWeaponTemplateKey(int nextWeaponTemplateKey)
    {
        weaponTemplateKey = Math.Max(0, nextWeaponTemplateKey);
    }

    public void SetWeaponTemplateKey(string nextWeaponTemplateKey, int legacyNumericWeaponTemplateKey = 0)
    {
        weaponTemplateKeyString = string.IsNullOrWhiteSpace(nextWeaponTemplateKey) ? string.Empty : nextWeaponTemplateKey.Trim();
        weaponTemplateKey = Math.Max(0, legacyNumericWeaponTemplateKey);
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
