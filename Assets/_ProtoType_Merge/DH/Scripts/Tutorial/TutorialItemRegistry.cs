using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialItemRegistry : MonoBehaviour
{
    [System.Serializable]
    public struct PickupMotionSettings
    {
        public bool flyVisualToTargetOnGet;
        [Min(0f)] public float duration;
        [Min(0f)] public float arcHeight;
        public float targetYOffset;
    }

    [SerializeField]
    private PickupMotionSettings pickupMotion = new PickupMotionSettings
    {
        flyVisualToTargetOnGet = true,
        duration = 0.45f,
        arcHeight = 0.25f,
        targetYOffset = 0.25f
    };

    private readonly List<TutorialItemObject> items = new List<TutorialItemObject>();

    public IReadOnlyList<TutorialItemObject> Items => items;
    public PickupMotionSettings PickupMotion => pickupMotion;

    private void Awake()
    {
        RegisterExistingItems();
    }

    public void Register(TutorialItemObject item)
    {
        if (item == null || items.Contains(item))
            return;

        items.Add(item);
    }

    public void Unregister(TutorialItemObject item)
    {
        if (item == null)
            return;

        items.Remove(item);
    }

    [ContextMenu("Rebuild Registry")]
    public void RegisterExistingItems()
    {
        items.Clear();

        TutorialItemObject[] sceneItems = FindObjectsByType<TutorialItemObject>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneItems.Length; i++)
            Register(sceneItems[i]);
    }
}
