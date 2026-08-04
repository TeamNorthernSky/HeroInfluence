using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "OutpostPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Outpost Prefab Catalog")]
public class OutpostPrefabCatalog : ScriptableObject
{
    [SerializeField] private List<OutpostPrefabEntry> outpostPrefabs = new List<OutpostPrefabEntry>();

    private void OnValidate()
    {
        outpostPrefabs ??= new List<OutpostPrefabEntry>();
        for (int i = 0; i < outpostPrefabs.Count; i++)
            outpostPrefabs[i] = outpostPrefabs[i].Normalized();
    }

    public bool TryGetOutpostPrefab(OutpostType outpostType, out Outpost prefab)
    {
        outpostType = OutpostTypeUtility.Normalize(outpostType);
        if (outpostPrefabs != null)
        {
            for (int i = 0; i < outpostPrefabs.Count; i++)
            {
                if (outpostPrefabs[i].OutpostType != outpostType)
                    continue;

                prefab = outpostPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[System.Serializable]
public struct OutpostPrefabEntry
{
    [UnityEngine.Serialization.FormerlySerializedAs("resourceType")]
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
