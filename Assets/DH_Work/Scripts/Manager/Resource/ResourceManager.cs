using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    [SerializeField] private int money;
    [SerializeField] private int chip;
    [SerializeField] private int crystal;
    [SerializeField] private int supply;

    public int GetAmount(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Money: return money;
            case ResourceType.Chip: return chip;
            case ResourceType.Crystal: return crystal;
            case ResourceType.Supply: return supply;
            default: return 0;
        }
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;

        switch (type)
        {
            case ResourceType.Money:
                money += amount;
                break;
            case ResourceType.Chip:
                chip += amount;
                break;
            case ResourceType.Crystal:
                crystal += amount;
                break;
            case ResourceType.Supply:
                supply += amount;
                break;
        }
    }

    public bool HasResource(ResourceType type, int amount)
    {
        if (amount <= 0) return true;
        return GetAmount(type) >= amount;
    }

    public bool SpendResource(ResourceType type, int amount)
    {
        if (amount <= 0) return false;
        if (!HasResource(type, amount)) return false;

        switch (type)
        {
            case ResourceType.Money:
                money -= amount;
                break;
            case ResourceType.Chip:
                chip -= amount;
                break;
            case ResourceType.Crystal:
                crystal -= amount;
                break;
            case ResourceType.Supply:
                supply -= amount;
                break;
        }

        return true;
    }
}
