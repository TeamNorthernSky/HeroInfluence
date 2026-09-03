using System.Collections.Generic;
using UnityEngine;

public class ItemRegistry : MonoBehaviour
{
    [System.Serializable]
    public struct PickupMotionSettings
    {
        public bool flyVisualToTargetOnGet;
        [Min(0f)] public float destroyDelay;
        [Min(0f)] public float flyDuration;
        [Min(0f)] public float arcHeight;
        public float targetYOffset;
    }

    [SerializeField]
    private PickupMotionSettings pickupMotion = new PickupMotionSettings
    {
        flyVisualToTargetOnGet = true,
        destroyDelay = 0.45f,
        flyDuration = 0.65f,
        arcHeight = 0.45f,
        targetYOffset = 0.35f
    };

    private readonly List<ItemObject> items = new List<ItemObject>();

    public IReadOnlyList<ItemObject> Items => items;
    public PickupMotionSettings PickupMotion => pickupMotion;

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
