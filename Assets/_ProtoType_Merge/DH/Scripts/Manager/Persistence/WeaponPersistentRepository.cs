using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WeaponPersistentRepository : MonoBehaviour
{
    public static WeaponPersistentRepository Instance { get; private set; }

    [Header("Persistent Weapons")]
    [SerializeField] private int nextWeaponIndex = 1;
    [SerializeField] private List<WeaponPersistentData> weapons = new List<WeaponPersistentData>();

    private readonly Dictionary<int, WeaponPersistentData> weaponLookup = new Dictionary<int, WeaponPersistentData>();

    public IReadOnlyList<WeaponPersistentData> Weapons => weapons;

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

    public int CreateWeapon(string weaponTemplateKey)
    {
        if (string.IsNullOrWhiteSpace(weaponTemplateKey))
            return -1;

        int weaponIndex = Mathf.Max(1, nextWeaponIndex);
        nextWeaponIndex = weaponIndex + 1;

        WeaponPersistentData newData = new WeaponPersistentData(weaponIndex, weaponTemplateKey);
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
        }

        if (nextWeaponIndex <= highestWeaponIndex)
            nextWeaponIndex = highestWeaponIndex + 1;
    }
}
