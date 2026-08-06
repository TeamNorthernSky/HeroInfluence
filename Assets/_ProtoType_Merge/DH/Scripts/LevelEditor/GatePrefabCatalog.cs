using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GatePrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Gate Prefab Catalog")]
public class GatePrefabCatalog : ScriptableObject
{
    [SerializeField] private List<GatePrefabEntry> gatePrefabs = new List<GatePrefabEntry>();

    public IReadOnlyList<GatePrefabEntry> GatePrefabs =>
        gatePrefabs != null
            ? gatePrefabs
            : Array.Empty<GatePrefabEntry>();

    private void OnValidate()
    {
        gatePrefabs ??= new List<GatePrefabEntry>();
        for (int i = 0; i < gatePrefabs.Count; i++)
            gatePrefabs[i] = gatePrefabs[i].Normalized();
    }

    public bool TryGetGatePrefab(string prefabKey, out GateFootprint prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (gatePrefabs != null)
        {
            for (int i = 0; i < gatePrefabs.Count; i++)
            {
                if (!string.Equals(gatePrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = gatePrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct GatePrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private GateFootprint prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public GateFootprint Prefab => prefab;

    public GatePrefabEntry Normalized()
    {
        GatePrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}
