using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class LevelPrefabRegistry : MonoBehaviour
{
    [Header("Default Prefabs")]
    [SerializeField] private GameObject obstaclePrefab;

    [Header("Item Prefabs")]
    [SerializeField] private List<ItemPrefabEntry> itemPrefabs = new List<ItemPrefabEntry>();

    [Header("Outpost Prefabs")]
    [FormerlySerializedAs("minePrefabs")]
    [SerializeField] private List<OutpostPrefabEntry> outpostPrefabs = new List<OutpostPrefabEntry>();

    public GameObject ObstaclePrefab => obstaclePrefab;

    public bool TryGetItemPrefab(ResourceType resourceType, out ItemObject prefab)
    {
        for (int i = 0; i < itemPrefabs.Count; i++)
        {
            if (itemPrefabs[i].ResourceType != resourceType)
                continue;

            prefab = itemPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetOutpostPrefab(ResourceType resourceType, out Outpost prefab)
    {
        for (int i = 0; i < outpostPrefabs.Count; i++)
        {
            if (outpostPrefabs[i].ResourceType != resourceType)
                continue;

            prefab = outpostPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct ItemPrefabEntry
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private ItemObject prefab;

    public ResourceType ResourceType => resourceType;
    public ItemObject Prefab => prefab;
}

[Serializable]
public struct OutpostPrefabEntry
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private Outpost prefab;

    public ResourceType ResourceType => resourceType;
    public Outpost Prefab => prefab;
}
