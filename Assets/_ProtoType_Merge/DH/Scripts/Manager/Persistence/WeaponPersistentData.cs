using System;
using UnityEngine;

[Serializable]
public class WeaponPersistentData
{
    public int WeaponIndex => weaponIndex;
    public string WeaponTemplateKey => weaponTemplateKey;

    [SerializeField] private int weaponIndex;
    [SerializeField] private string weaponTemplateKey;

    public WeaponPersistentData(int weaponIndex, string weaponTemplateKey)
    {
        this.weaponIndex = weaponIndex;
        this.weaponTemplateKey = weaponTemplateKey ?? string.Empty;
    }

    public void SetWeaponTemplateKey(string nextWeaponTemplateKey)
    {
        weaponTemplateKey = nextWeaponTemplateKey ?? string.Empty;
    }
}
