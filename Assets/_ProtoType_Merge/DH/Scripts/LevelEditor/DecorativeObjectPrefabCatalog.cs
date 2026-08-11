using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "DecorativeObjectPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Decorative Object Prefab Catalog")]
public class DecorativeObjectPrefabCatalog : ScriptableObject
{
    [FormerlySerializedAs("decorativeBuildingPrefabs")]
    [SerializeField] private List<DecorativeObjectPrefabEntry> decorativeObjectPrefabs = new List<DecorativeObjectPrefabEntry>();

    public IReadOnlyList<DecorativeObjectPrefabEntry> DecorativeObjectPrefabs =>
        decorativeObjectPrefabs != null
            ? decorativeObjectPrefabs
            : Array.Empty<DecorativeObjectPrefabEntry>();

    private void OnValidate()
    {
        decorativeObjectPrefabs ??= new List<DecorativeObjectPrefabEntry>();
        for (int i = 0; i < decorativeObjectPrefabs.Count; i++)
            decorativeObjectPrefabs[i] = decorativeObjectPrefabs[i].Normalized();
    }

    public bool TryGetDecorativeObjectPrefab(string prefabKey, out GameObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (decorativeObjectPrefabs != null)
        {
            for (int i = 0; i < decorativeObjectPrefabs.Count; i++)
            {
                if (!string.Equals(decorativeObjectPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = decorativeObjectPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct DecorativeObjectPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private GameObject prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            DecorativeObjectPlacement placement = prefab != null ? prefab.GetComponent<DecorativeObjectPlacement>() : null;
            return placement != null ? placement.PrefabKey : string.Empty;
        }
    }

    public GameObject Prefab => prefab;

    public DecorativeObjectPrefabEntry Normalized()
    {
        DecorativeObjectPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}
