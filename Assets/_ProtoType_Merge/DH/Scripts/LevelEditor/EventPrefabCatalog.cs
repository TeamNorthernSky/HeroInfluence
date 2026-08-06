using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "EventPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Event Prefab Catalog")]
public class EventPrefabCatalog : ScriptableObject
{
    [Header("Map Event Prefabs")]
    [SerializeField] private List<EventPrefabEntry> mapEventPrefabs = new List<EventPrefabEntry>();

    [Header("Main Event Prefabs")]
    [SerializeField] private List<MainEventPrefabEntry> mainEventPrefabs = new List<MainEventPrefabEntry>();

    [Header("Sub Event Prefabs")]
    [SerializeField] private List<SubEventPrefabEntry> subEventPrefabs = new List<SubEventPrefabEntry>();

    public IReadOnlyList<MainEventPrefabEntry> MainEventPrefabs =>
        mainEventPrefabs != null
            ? mainEventPrefabs
            : Array.Empty<MainEventPrefabEntry>();

    public IReadOnlyList<SubEventPrefabEntry> SubEventPrefabs =>
        subEventPrefabs != null
            ? subEventPrefabs
            : Array.Empty<SubEventPrefabEntry>();

    private void OnValidate()
    {
        mainEventPrefabs ??= new List<MainEventPrefabEntry>();
        for (int i = 0; i < mainEventPrefabs.Count; i++)
            mainEventPrefabs[i] = mainEventPrefabs[i].Normalized();

        subEventPrefabs ??= new List<SubEventPrefabEntry>();
        for (int i = 0; i < subEventPrefabs.Count; i++)
            subEventPrefabs[i] = subEventPrefabs[i].Normalized();
    }

    public bool TryGetEventPrefab(MapEventType eventType, out MapEventObject prefab)
    {
        if (mapEventPrefabs != null)
        {
            for (int i = 0; i < mapEventPrefabs.Count; i++)
            {
                EventPrefabEntry entry = mapEventPrefabs[i];
                if (entry.EventType != eventType)
                    continue;

                prefab = entry.Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }

    public bool TryGetMainEventPrefab(string prefabKey, out MainEventObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (mainEventPrefabs != null)
        {
            for (int i = 0; i < mainEventPrefabs.Count; i++)
            {
                if (!string.Equals(mainEventPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = mainEventPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }

    public bool TryGetSubEventPrefab(string prefabKey, out SubEventObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (subEventPrefabs != null)
        {
            for (int i = 0; i < subEventPrefabs.Count; i++)
            {
                if (!string.Equals(subEventPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = subEventPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct EventPrefabEntry
{
    [SerializeField] private MapEventType eventType;
    [SerializeField] private MapEventObject prefab;

    public MapEventType EventType => eventType;
    public string EventKey => MapEventTypeUtility.ToEventKey(eventType);
    public MapEventObject Prefab => prefab;
}

[Serializable]
public struct MainEventPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private MainEventObject prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public MainEventObject Prefab => prefab;

    public MainEventPrefabEntry Normalized()
    {
        MainEventPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}

[Serializable]
public struct SubEventPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private SubEventObject prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public SubEventObject Prefab => prefab;

    public SubEventPrefabEntry Normalized()
    {
        SubEventPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}
