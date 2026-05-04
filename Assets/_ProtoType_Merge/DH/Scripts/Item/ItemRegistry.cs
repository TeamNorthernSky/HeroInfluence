using System.Collections.Generic;
using UnityEngine;

public class ItemRegistry : MonoBehaviour
{
    private readonly List<ItemObject> items = new List<ItemObject>();

    public IReadOnlyList<ItemObject> Items => items;

    private void Awake()
    {
        RegisterExistingItems();
    }

    public void Register(ItemObject item)
    {
        if (item == null || items.Contains(item))
            return;

        items.Add(item);
    }

    public void Unregister(ItemObject item)
    {
        if (item == null)
            return;

        items.Remove(item);
    }

    [ContextMenu("Rebuild Registry")]
    public void RegisterExistingItems()
    {
        items.Clear();

        ItemObject[] sceneItems = FindObjectsByType<ItemObject>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneItems.Length; i++)
        {
            ItemObject item = sceneItems[i];
            if (item == null)
                continue;

            Register(item);
        }
    }
}
