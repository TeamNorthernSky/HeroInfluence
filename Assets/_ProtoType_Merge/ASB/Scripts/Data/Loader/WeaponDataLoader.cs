using System.Collections.Generic;
using UnityEngine;

public class WeaponDataLoader : MonoBehaviour
{
    private readonly Dictionary<int, WeaponData> weaponsByIndex = new Dictionary<int, WeaponData>();

    public void ReplaceAll(IEnumerable<WeaponData> weapons)
    {
        weaponsByIndex.Clear();
        if (weapons == null)
        {
            return;
        }

        foreach (var w in weapons)
        {
            if (w == null)
            {
                continue;
            }

            weaponsByIndex[w.WeaponIndex] = w;
        }
    }

    public bool TryGetWeapon(int weaponIndex, out WeaponData data)
    {
        return weaponsByIndex.TryGetValue(weaponIndex, out data);
    }

    public List<WeaponData> GetAllWeapons()
    {
        return new List<WeaponData>(weaponsByIndex.Values);
    }

    public int Count => weaponsByIndex.Count;
}
