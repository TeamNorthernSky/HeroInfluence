using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "TutorialObjectPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Tutorial Object Prefab Catalog")]
public class TutorialObjectPrefabCatalog : ScriptableObject
{
    [SerializeField] private List<TutorialObjectPrefabEntry> tutorialObjectPrefabs = new List<TutorialObjectPrefabEntry>();

    public IReadOnlyList<TutorialObjectPrefabEntry> TutorialObjectPrefabs =>
        tutorialObjectPrefabs != null
            ? tutorialObjectPrefabs
            : Array.Empty<TutorialObjectPrefabEntry>();

    private void OnValidate()
    {
        tutorialObjectPrefabs ??= new List<TutorialObjectPrefabEntry>();
        for (int i = 0; i < tutorialObjectPrefabs.Count; i++)
            tutorialObjectPrefabs[i] = tutorialObjectPrefabs[i].Normalized();
    }

    public bool TryGetTutorialObjectPrefab(string prefabKey, out GameObject prefab)
    {
        string normalizedKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        if (tutorialObjectPrefabs != null)
        {
            for (int i = 0; i < tutorialObjectPrefabs.Count; i++)
            {
                if (!string.Equals(tutorialObjectPrefabs[i].PrefabKey, normalizedKey, StringComparison.Ordinal))
                    continue;

                prefab = tutorialObjectPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
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
