using System;
using System.Collections.Generic;
using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public event Action<ResourceType, int> OnResourceChanged;

    private readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    public void Initialize()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (!resources.ContainsKey(type))
            {
                resources[type] = 0;
            }
        }
    }

    public int Get(ResourceType type)
    {
        return resources.TryGetValue(type, out int value) ? value : 0;
    }

    public void Set(ResourceType type, int value)
    {
        int oldValue = Get(type);
        resources[type] = value;
        Debug.Log($"[Economy] {type}: {oldValue} → {value}");
        OnResourceChanged?.Invoke(type, value);
    }

    public void Add(ResourceType type, int amount)
    {
        if (amount == 0) return;
        try
        {
            int newValue = checked(Get(type) + amount);
            Set(type, newValue);
        }
        catch (OverflowException)
        {
            Debug.LogWarning($"[Economy] {type} 오버플로우 발생 — 값 변경 취소");
        }
    }

    public bool Has(ResourceType type, int amount)
    {
        if (amount <= 0) return true;
        return Get(type) >= amount;
    }

    public bool Spend(ResourceType type, int amount)
    {
        if (amount <= 0) return false;
        if (!Has(type, amount)) return false;
        Set(type, Get(type) - amount);
        return true;
    }

    public bool IsNegative(ResourceType type)
    {
        return Get(type) < 0;
    }

    public void Reset(ResourceType type)
    {
        Debug.Log($"[Economy] {type} 초기화");
        Set(type, 0);
    }

    public void ResetAll()
    {
        Debug.Log("[Economy] 전체 초기화");
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
            OnResourceChanged?.Invoke(type, 0);
        }
    }

    public Dictionary<ResourceType, int> GetAll()
    {
        return new Dictionary<ResourceType, int>(resources);
    }
}
