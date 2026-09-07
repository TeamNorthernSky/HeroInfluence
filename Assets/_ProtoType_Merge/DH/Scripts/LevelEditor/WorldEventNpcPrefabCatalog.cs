using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WorldEventNpcPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/World Event NPC Prefab Catalog")]
public sealed class WorldEventNpcPrefabCatalog : ScriptableObject
{
    [SerializeField] private List<WorldEventNpcPrefabEntry> npcPrefabs = new List<WorldEventNpcPrefabEntry>();

    public bool TryGetNpcPrefab(int npcType, out GameObject prefab)
    {
        if (npcPrefabs != null)
        {
            for (int i = 0; i < npcPrefabs.Count; i++)
            {
                WorldEventNpcPrefabEntry entry = npcPrefabs[i];
                if (entry.NpcType != npcType)
                    continue;

                prefab = entry.Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct WorldEventNpcPrefabEntry
{
    [SerializeField] private int npcType;
    [SerializeField] private GameObject prefab;

    public int NpcType => npcType;
    public GameObject Prefab => prefab;
}
