using System.Collections.Generic;
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

        return DHCsvTemplateCatalog.Instance.GetWeaponsByClass(className);
    }
}
