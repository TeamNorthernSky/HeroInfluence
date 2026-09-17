using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "TutorialObjectPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Tutorial Object Prefab Catalog")]
public class TutorialObjectPrefabCatalog : ScriptableObject
{
    [Header("Tutorial Building Prefabs")]
    [SerializeField] private List<TutorialObjectPrefabEntry> tutorialObjectPrefabs = new List<TutorialObjectPrefabEntry>();

    [Header("Tutorial Enemy Prefabs")]
    [SerializeField] private List<TutorialObjectPrefabEntry> tutorialEnemyPrefabs = new List<TutorialObjectPrefabEntry>();

    [Header("Tutorial Item Prefabs")]
    [SerializeField] private List<TutorialItemPrefabEntry> tutorialItemPrefabs = new List<TutorialItemPrefabEntry>();

    [Header("Tutorial Hero Prefabs")]
    [SerializeField] private List<TutorialHeroPrefabEntry> tutorialHeroPrefabs = new List<TutorialHeroPrefabEntry>();

    public IReadOnlyList<TutorialObjectPrefabEntry> TutorialObjectPrefabs =>
        tutorialObjectPrefabs != null
            ? tutorialObjectPrefabs
            : Array.Empty<TutorialObjectPrefabEntry>();
    public IReadOnlyList<TutorialObjectPrefabEntry> TutorialEnemyPrefabs =>
        tutorialEnemyPrefabs != null
            ? tutorialEnemyPrefabs
            : Array.Empty<TutorialObjectPrefabEntry>();
    public IReadOnlyList<TutorialHeroPrefabEntry> TutorialHeroPrefabs =>
        tutorialHeroPrefabs != null
            ? tutorialHeroPrefabs
            : Array.Empty<TutorialHeroPrefabEntry>();
    public IReadOnlyList<TutorialItemPrefabEntry> TutorialItemPrefabs =>
        tutorialItemPrefabs != null
            ? tutorialItemPrefabs
            : Array.Empty<TutorialItemPrefabEntry>();

    private void OnValidate()
    {
        tutorialObjectPrefabs ??= new List<TutorialObjectPrefabEntry>();
        for (int i = 0; i < tutorialObjectPrefabs.Count; i++)
            tutorialObjectPrefabs[i] = tutorialObjectPrefabs[i].Normalized();

        tutorialEnemyPrefabs ??= new List<TutorialObjectPrefabEntry>();
        for (int i = 0; i < tutorialEnemyPrefabs.Count; i++)
            tutorialEnemyPrefabs[i] = tutorialEnemyPrefabs[i].Normalized();

        tutorialItemPrefabs ??= new List<TutorialItemPrefabEntry>();
        for (int i = 0; i < tutorialItemPrefabs.Count; i++)
            tutorialItemPrefabs[i] = tutorialItemPrefabs[i].Normalized();

        tutorialHeroPrefabs ??= new List<TutorialHeroPrefabEntry>();
        for (int i = 0; i < tutorialHeroPrefabs.Count; i++)
            tutorialHeroPrefabs[i] = tutorialHeroPrefabs[i].Normalized();
    }

    public bool TryGetTutorialObjectPrefab(string prefabKey, out GameObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (TryGetPrefabFromEntries(tutorialObjectPrefabs, normalizedKey, out prefab))
            return true;

        if (TryGetPrefabFromEntries(tutorialEnemyPrefabs, normalizedKey, out prefab))
            return true;

        prefab = null;
        return false;
    }

    private static bool TryGetPrefabFromEntries(
        IReadOnlyList<TutorialObjectPrefabEntry> entries,
        string normalizedKey,
        out GameObject prefab)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (!string.Equals(entries[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = entries[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }

    public bool TryGetTutorialItemPrefab(string itemPrefabKey, out GameObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(itemPrefabKey) ? string.Empty : itemPrefabKey.Trim();
        if (tutorialItemPrefabs != null)
        {
            for (int i = 0; i < tutorialItemPrefabs.Count; i++)
            {
                if (!string.Equals(tutorialItemPrefabs[i].ItemPrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = tutorialItemPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }

    public bool TryGetTutorialHeroPrefab(string unitTemplateKey, out GameObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
        if (tutorialHeroPrefabs != null)
        {
            for (int i = 0; i < tutorialHeroPrefabs.Count; i++)
            {
                if (!string.Equals(tutorialHeroPrefabs[i].UnitTemplateKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = tutorialHeroPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[Serializable]
public struct TutorialItemPrefabEntry
{
    [SerializeField] private string itemPrefabKey;
    [SerializeField] private GameObject prefab;

    public string ItemPrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(itemPrefabKey))
                return itemPrefabKey.Trim();

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public GameObject Prefab => prefab;

    public TutorialItemPrefabEntry Normalized()
    {
        TutorialItemPrefabEntry entry = this;
        entry.itemPrefabKey = ItemPrefabKey;
        return entry;
    }
}

[Serializable]
public struct TutorialObjectPrefabEntry
{
    [SerializeField] private string prefabKey;
    [SerializeField] private GameObject prefab;

    public string PrefabKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(prefabKey))
                return prefabKey.Trim();

            TutorialBuildingObject tutorialObject = prefab != null ? prefab.GetComponent<TutorialBuildingObject>() : null;
            if (tutorialObject != null && !string.IsNullOrWhiteSpace(tutorialObject.BuildingKey))
                return tutorialObject.BuildingKey;

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public GameObject Prefab => prefab;

    public TutorialObjectPrefabEntry Normalized()
    {
        TutorialObjectPrefabEntry entry = this;
        entry.prefabKey = PrefabKey;
        return entry;
    }
}

[Serializable]
public struct TutorialHeroPrefabEntry
{
    [SerializeField] private string unitTemplateKey;
    [SerializeField] private GameObject prefab;

    public string UnitTemplateKey
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(unitTemplateKey))
                return unitTemplateKey.Trim();

            TutorialUnitState tutorialUnit = prefab != null ? prefab.GetComponent<TutorialUnitState>() : null;
            if (tutorialUnit != null && !string.IsNullOrWhiteSpace(tutorialUnit.UnitTemplateKey))
                return tutorialUnit.UnitTemplateKey;

            return prefab != null ? prefab.name : string.Empty;
        }
    }

    public GameObject Prefab => prefab;

    public TutorialHeroPrefabEntry Normalized()
    {
        TutorialHeroPrefabEntry entry = this;
        entry.unitTemplateKey = UnitTemplateKey;
        return entry;
    }
}
