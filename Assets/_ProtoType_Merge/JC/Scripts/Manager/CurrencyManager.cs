using System;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public event Action<CurrencyType, int> OnCurrencyChanged;

    private Dictionary<CurrencyType, int> currencies = new Dictionary<CurrencyType, int>();

    public void Initialize()
    {
        foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
        {
            if (!currencies.ContainsKey(type))
            {
                currencies[type] = 0;
            }
        }
    }

    public int Get(CurrencyType type)
    {
        return currencies.TryGetValue(type, out int value) ? value : 0;
    }

    public void Set(CurrencyType type, int value)
    {
        int oldValue = Get(type);
        currencies[type] = value;
        Debug.Log($"[Currency] {type}: {oldValue} → {value}");
        OnCurrencyChanged?.Invoke(type, value);
    }

    public void Add(CurrencyType type, int amount)
    {
        try
        {
            int newValue = checked(Get(type) + amount);
            Set(type, newValue);
        }
        catch (OverflowException)
        {
            Debug.LogWarning($"[Currency] {type} 오버플로우 발생 — 값 변경 취소");
        }
    }

    public bool Has(CurrencyType type, int amount)
    {
        return Get(type) >= amount;
    }

    public bool IsNegative(CurrencyType type)
    {
        return Get(type) < 0;
    }

    public void Reset(CurrencyType type)
    {
        Debug.Log($"[Currency] {type} 초기화");
        Set(type, 0);
    }

    public void ResetAll()
    {
        Debug.Log("[Currency] 전체 초기화");
        foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
        {
            currencies[type] = 0;
            Debug.Log($"[Currency] {type}: → 0");
            OnCurrencyChanged?.Invoke(type, 0);
        }
    }

    public Dictionary<CurrencyType, int> GetAll()
    {
        return new Dictionary<CurrencyType, int>(currencies);
    }
}
