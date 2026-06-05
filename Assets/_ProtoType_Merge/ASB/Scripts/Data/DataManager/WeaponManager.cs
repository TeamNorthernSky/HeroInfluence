using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WeaponManager] 중복 인스턴스가 감지되었습니다.");
        }

        Instance = this;
    }

    public List<WeaponData> GetWeaponsForClass(string className)
    {
        if (DHCsvTemplateCatalog.Instance == null || string.IsNullOrWhiteSpace(className))
        {
            return new List<WeaponData>();
        }

        string normalized = className.Trim();
        return DHCsvTemplateCatalog.Instance.GetAllWeapons()
            .Where(x =>
                x != null &&
                !string.IsNullOrWhiteSpace(x.weaponClass) &&
                string.Equals(x.weaponClass.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.WeaponIndex)
            .ToList();
    }
}
