using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "HeroUnionPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/HeroUnion Prefab Catalog")]
public class HeroUnionPrefabCatalog : ScriptableObject
{
    [SerializeField] private List<HeroUnionPrefabEntry> heroUnionPrefabs = new List<HeroUnionPrefabEntry>();

    public IReadOnlyList<HeroUnionPrefabEntry> HeroUnionPrefabs =>
        heroUnionPrefabs != null
            ? heroUnionPrefabs
            : Array.Empty<HeroUnionPrefabEntry>();

    private void OnValidate()
    {
        heroUnionPrefabs ??= new List<HeroUnionPrefabEntry>();
        for (int i = 0; i < heroUnionPrefabs.Count; i++)
            heroUnionPrefabs[i] = heroUnionPrefabs[i].Normalized();
    }

    public bool TryGetHeroUnionPrefab(string prefabKey, out HeroUnionUnit prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (!string.IsNullOrEmpty(normalizedKey) && heroUnionPrefabs != null)
        {
            for (int i = 0; i < heroUnionPrefabs.Count; i++)
            {
                if (!string.Equals(heroUnionPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = heroUnionPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct HeroUnionPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private HeroUnionUnit prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public HeroUnionUnit Prefab => prefab;

    public HeroUnionPrefabEntry Normalized()
    {
        HeroUnionPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}
