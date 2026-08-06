using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemPrefabCatalog",
    menuName = "DH Work/Prefab Catalogs/Item Prefab Catalog")]
public class ItemPrefabCatalog : ScriptableObject
{
    [SerializeField] private List<ItemPrefabEntry> itemPrefabs = new List<ItemPrefabEntry>();

    public bool TryGetItemPrefab(ResourceType resourceType, out ItemObject prefab)
    {
        if (itemPrefabs != null)
        {
            for (int i = 0; i < itemPrefabs.Count; i++)
            {
                if (itemPrefabs[i].ResourceType != resourceType)
                    continue;

                prefab = itemPrefabs[i].Prefab;
                return prefab != null;
            }
        }

        prefab = null;
        return false;
    }
}

[System.Serializable]
public struct ItemPrefabEntry
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private ItemObject prefab;

    public ResourceType ResourceType => resourceType;
    public ItemObject Prefab => prefab;
}
