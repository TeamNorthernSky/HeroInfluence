using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DecorativeBuildingPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Decorative Building Prefab Catalog")]
public class DecorativeBuildingPrefabCatalog : ScriptableObject
{
    [SerializeField] private List<DecorativeBuildingPrefabEntry> decorativeBuildingPrefabs = new List<DecorativeBuildingPrefabEntry>();

    public IReadOnlyList<DecorativeBuildingPrefabEntry> DecorativeBuildingPrefabs =>
        decorativeBuildingPrefabs != null
            ? decorativeBuildingPrefabs
            : Array.Empty<DecorativeBuildingPrefabEntry>();

    private void OnValidate()
    {
        decorativeBuildingPrefabs ??= new List<DecorativeBuildingPrefabEntry>();
        for (int i = 0; i < decorativeBuildingPrefabs.Count; i++)
            decorativeBuildingPrefabs[i] = decorativeBuildingPrefabs[i].Normalized();
    }

    public bool TryGetDecorativeBuildingPrefab(string prefabKey, out GameObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (decorativeBuildingPrefabs != null)
        {
            for (int i = 0; i < decorativeBuildingPrefabs.Count; i++)
            {
                if (!string.Equals(decorativeBuildingPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = decorativeBuildingPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct DecorativeBuildingPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private GameObject prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            DecorativeBuildingPlacement placement = prefab != null ? prefab.GetComponent<DecorativeBuildingPlacement>() : null;
            return placement != null ? placement.PrefabKey : string.Empty;
        }
    }

    public GameObject Prefab => prefab;

    public DecorativeBuildingPrefabEntry Normalized()
    {
        DecorativeBuildingPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}
