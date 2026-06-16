using System;
using System.Collections.Generic;
using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public event Action<ResourceType, int> OnResourceChanged;

    [Header("자원 상한 (임시 — 교환소 수령 Max 판정 등)")]
    [Tooltip("전 자원 공통 상한. 추후 자원별로 분기 가능. 기본 999,999.")]
    [SerializeField] private int resourceMax = 999999;

    private readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    /// <summary>자원별 상한. 현재는 전 자원 공통값(resourceMax). 추후 자원별 분기 가능.</summary>
    public int GetMax(ResourceType type) => resourceMax;

    /// <summary>해당 자원이 상한에 도달했는지.</summary>
    public bool IsAtMax(ResourceType type) => Get(type) >= GetMax(type);

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
