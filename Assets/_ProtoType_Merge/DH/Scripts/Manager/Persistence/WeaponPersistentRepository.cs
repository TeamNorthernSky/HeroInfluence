using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponPersistentRepository : MonoBehaviour
{
    public static WeaponPersistentRepository Instance { get; private set; }
    public const int BaseWeaponLevel = 1;
    public const int MaxWeaponLevel = 5;

    [Header("Persistent Weapons")]
    [SerializeField] private int nextWeaponIndex = 1;
    [SerializeField] private List<WeaponPersistentData> weapons = new List<WeaponPersistentData>();

    private readonly Dictionary<int, WeaponPersistentData> weaponLookup = new Dictionary<int, WeaponPersistentData>();

    public IReadOnlyList<WeaponPersistentData> Weapons => weapons;

    // [KJ 260703] 저장 기능(GameSaveService)용 읽기 노출
    public int NextWeaponIndex => nextWeaponIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public int CreateWeapon(int weaponTemplateKey)
    {
        if (weaponTemplateKey <= 0)
            return -1;

        int weaponIndex = Mathf.Max(1, nextWeaponIndex);
        nextWeaponIndex = weaponIndex + 1;

        WeaponPersistentData newData = new WeaponPersistentData(weaponIndex, weaponTemplateKey);
        RefreshWeaponStats(newData);
        weapons.Add(newData);
        weaponLookup[weaponIndex] = newData;
        return weaponIndex;
    }

    public bool ContainsWeapon(int weaponIndex)
    {
        return weaponIndex > 0 && weaponLookup.ContainsKey(weaponIndex);
    }

    public bool TryGetWeapon(int weaponIndex, out WeaponPersistentData data)
    {
        if (weaponIndex <= 0)
        {
            data = null;
            return false;
        }

        return weaponLookup.TryGetValue(weaponIndex, out data);
    }

    public bool RemoveWeapon(int weaponIndex)
    {
        if (weaponIndex <= 0 || !weaponLookup.TryGetValue(weaponIndex, out WeaponPersistentData data))
            return false;

        weaponLookup.Remove(weaponIndex);
        weapons.Remove(data);
        return true;
    }

    public bool TryGetWeaponTemplateKey(int weaponIndex, out int weaponTemplateKey)
    {
        weaponTemplateKey = 0;

        if (!TryGetWeapon(weaponIndex, out WeaponPersistentData data) || data == null)
            return false;

        weaponTemplateKey = data.WeaponTemplateKey;
        return weaponTemplateKey > 0;
    }

    public bool TryGetWeaponStats(int weaponIndex, out EquipmentStatBlock weaponStats)
    {
        weaponStats = default;

        if (!TryGetWeapon(weaponIndex, out WeaponPersistentData data) || data == null)
            return false;

        if (IsDefault(data.CachedWeaponStats))
            RefreshWeaponStats(data);

        weaponStats = data.CachedWeaponStats;
        return true;
    }

    public bool TryEnhanceWeapon(int weaponIndex, int amount = 1)
    {
        if (amount <= 0)
            return false;

        if (!TryGetWeapon(weaponIndex, out WeaponPersistentData data) || data == null)
            return false;

        if (data.Level >= MaxWeaponLevel)
            return false;

        int nextLevel = Mathf.Clamp(data.Level + amount, BaseWeaponLevel, MaxWeaponLevel);
        if (nextLevel == data.Level)
            return false;

        data.SetLevel(nextLevel);
        RefreshWeaponStats(data);
        PersistentUnitRepository.Instance?.RefreshUnitsEquippedWithWeapon(weaponIndex);
        return true;
    }

    public bool RefreshWeaponStats(int weaponIndex)
    {
        return TryGetWeapon(weaponIndex, out WeaponPersistentData data) && RefreshWeaponStats(data);
    }

    public void RestoreFromSave(int restoredNextWeaponIndex, IReadOnlyList<WeaponPersistentData> savedWeapons)
    {
        weapons.Clear();
        weaponLookup.Clear();

        if (savedWeapons != null)
        {
            for (int i = 0; i < savedWeapons.Count; i++)
            {
                WeaponPersistentData data = savedWeapons[i];
                if (data == null || data.WeaponIndex <= 0 || weaponLookup.ContainsKey(data.WeaponIndex))
                    continue;

                weapons.Add(data);
                weaponLookup.Add(data.WeaponIndex, data);
            }
        }

        nextWeaponIndex = Mathf.Max(1, restoredNextWeaponIndex);
        RebuildLookup();
        PersistentUnitRepository unitRepository = PersistentUnitRepository.Instance;
        if (unitRepository != null)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponPersistentData data = weapons[i];
                if (data != null)
                    unitRepository.RefreshUnitsEquippedWithWeapon(data.WeaponIndex);
            }
        }
    }
    public void ClearAllWeapons()
    {
        weapons.Clear();
        weaponLookup.Clear();
        nextWeaponIndex = 1;
    }

    private void RebuildLookup()
    {
        weaponLookup.Clear();
        int highestWeaponIndex = 0;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponPersistentData data = weapons[i];
            if (data == null)
                continue;

            int weaponIndex = data.WeaponIndex;
            if (weaponIndex <= 0)
                continue;

            if (weaponLookup.ContainsKey(weaponIndex))
            {
                Debug.LogWarning($"WeaponPersistentRepository has duplicate weaponIndex '{weaponIndex}'.", this);
                continue;
            }

            weaponLookup.Add(weaponIndex, data);
            if (weaponIndex > highestWeaponIndex)
                highestWeaponIndex = weaponIndex;

            if (data.Level < BaseWeaponLevel)
                data.SetLevel(BaseWeaponLevel);

            if (IsDefault(data.CachedWeaponStats))
                RefreshWeaponStats(data);
        }

        if (nextWeaponIndex <= highestWeaponIndex)
            nextWeaponIndex = highestWeaponIndex + 1;
    }

    private bool RefreshWeaponStats(WeaponPersistentData data)
    {
        if (data == null)
            return false;

        EquipmentStatBlock stats = ResolveWeaponStats(data);
        data.SetCachedWeaponStats(stats);
        return !IsDefault(stats);
    }

    private static EquipmentStatBlock ResolveWeaponStats(WeaponPersistentData data)
    {
        if (data == null || data.WeaponTemplateKey <= 0)
            return default;

        int weaponTemplateKey = data.WeaponTemplateKey;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null)
            return default;

        int level = Mathf.Clamp(data.Level, BaseWeaponLevel, MaxWeaponLevel);
        if (catalog.TryGetWeaponBonusAtLevel(weaponTemplateKey, level, out StatBlock leveledStats))
            return EquipmentStatBlock.FromStatBlock(leveledStats);

        return catalog.TryGetWeaponStats(weaponTemplateKey, out EquipmentStatBlock baseStats)
            ? baseStats
            : default;
    }

    private static bool IsDefault(EquipmentStatBlock stats)
    {
        return stats.HP == 0f &&
               stats.Atk == 0f &&
               stats.DEF == 0f &&
               stats.CriticalRate == 0f &&
               stats.CounterRate == 0f &&
               stats.AvoidRate == 0f &&
               stats.Speed == 0f;
    }
}
