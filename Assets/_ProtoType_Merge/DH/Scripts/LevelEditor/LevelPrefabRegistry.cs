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

    [Header("Event Prefabs")]
    [SerializeField] private MapEventObject defaultEventPrefab;
    [SerializeField] private List<EventPrefabEntry> eventPrefabs = new List<EventPrefabEntry>();

    [Header("Unique Building Prefabs")]
    [SerializeField] private CastleUnit castlePrefab;
    [SerializeField] private VillainUnionBase villainUnionBasePrefab;

    [Header("Enemy Prefabs")]
    [SerializeField] private EnemyGridMover stayEnemyPrefab;

    public GameObject ObstaclePrefab => obstaclePrefab;
    public CastleUnit CastlePrefab => castlePrefab;
    public VillainUnionBase VillainUnionBasePrefab => villainUnionBasePrefab;

    private void OnValidate()
    {
        for (int i = 0; i < outpostPrefabs.Count; i++)
            outpostPrefabs[i] = outpostPrefabs[i].Normalized();
    }

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

    public bool TryGetOutpostPrefab(OutpostType outpostType, out Outpost prefab)
    {
        outpostType = OutpostTypeUtility.Normalize(outpostType);

        for (int i = 0; i < outpostPrefabs.Count; i++)
        {
            if (outpostPrefabs[i].OutpostType != outpostType)
                continue;

            prefab = outpostPrefabs[i].Prefab;
            return prefab != null;
        }

        prefab = null;
        return false;
    }

    public bool TryGetEventPrefab(string eventKey, out MapEventObject prefab)
    {
        if (!string.IsNullOrWhiteSpace(eventKey))
        {
            for (int i = 0; i < eventPrefabs.Count; i++)
            {
                EventPrefabEntry entry = eventPrefabs[i];
                if (!string.Equals(entry.EventKey, eventKey, StringComparison.Ordinal))
                    continue;

                prefab = entry.Prefab;
                return prefab != null;
            }
        }

        prefab = defaultEventPrefab;
        return prefab != null;
    }

    public bool TryGetCastlePrefab(out CastleUnit prefab)
    {
        prefab = castlePrefab;
        return prefab != null;
    }

    public bool TryGetVillainUnionBasePrefab(out VillainUnionBase prefab)
    {
        prefab = villainUnionBasePrefab;
        return prefab != null;
    }

    public bool TryGetStayEnemyPrefab(out EnemyGridMover prefab)
    {
        prefab = stayEnemyPrefab;
        return prefab != null;
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
    [FormerlySerializedAs("resourceType")]
    [SerializeField] private OutpostType outpostType;
    [SerializeField] private Outpost prefab;

    public OutpostType OutpostType => OutpostTypeUtility.Normalize(outpostType);
    public Outpost Prefab => prefab;

    public OutpostPrefabEntry Normalized()
    {
        OutpostPrefabEntry entry = this;
        entry.outpostType = OutpostType;
        return entry;
    }
}

[Serializable]
public struct EventPrefabEntry
{
    [SerializeField] private string eventKey;
    [SerializeField] private MapEventObject prefab;

    public string EventKey => eventKey;
    public MapEventObject Prefab => prefab;
}
