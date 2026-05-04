using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemObject : MonoBehaviour
{
    public ResourceType resourceType;
    public int amount;

    private ItemRegistry itemRegistry;

    private void OnEnable()
    {
        ResolveRegistry();
        itemRegistry?.Register(this);
    }

    private void OnDisable()
    {
        itemRegistry?.Unregister(this);
    }

    public void ApplyInitialAmount(int nextAmount)
    {
        amount = nextAmount;
    }

    public void GetItem(ResourceManager resourceManager)
    {
        resourceManager.AddResource(resourceType, amount);

        Destroy(gameObject);
    }

    public void RemoveWithoutReward()
    {
        Destroy(gameObject);
    }

    private void ResolveRegistry()
    {
        if (itemRegistry == null)
            itemRegistry = FindFirstObjectByType<ItemRegistry>();
    }
}
