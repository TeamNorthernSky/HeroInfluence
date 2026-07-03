using System;
using System.Collections.Generic;
using UnityEngine;

// [KJ 260701] 의무실(Infirmary) 도메인 매니저. GameManager 자식(영속).
// enum은 HQDepartment.Recruit=5(옛 이름)를 참조하지만, 기능은 유닛 회복/부활.
// 3계층 규약: 자금은 UI가(Economy.Spend/Add), 데이터 쓰기·턴추적만 매니저.
public class InfirmaryManager : MonoBehaviour
{
    [SerializeField] private int[] healPercent = { 30, 40, 50 };    // 레벨↑ → %↑
    [SerializeField] private int[] healCost    = { 300, 250, 200 };  // 레벨↑ → 비용↓
    [SerializeField] private int[] reviveHpPercent = { 30, 40, 50 };
    [SerializeField] private int[] reviveCost  = { 1000, 850, 700 };

    private readonly HashSet<int> healedThisTurn = new HashSet<int>();
    private HQStateManager hq;
    public event Action OnStateChanged;

    public bool IsUnlocked => Level >= 1;
    public int Level => hq != null ? hq.GetLevel(HQDepartment.Recruit) : 0;

    private int LvIdx => Mathf.Clamp(Level - 1, 0, 2);
    public int GetHealPercent()   => IsUnlocked ? healPercent[LvIdx] : 0;
    public int GetHealCost()      => IsUnlocked ? healCost[LvIdx] : int.MaxValue;
    public int GetRevivePercent() => IsUnlocked ? reviveHpPercent[LvIdx] : 0;
    public int GetReviveCost()    => IsUnlocked ? reviveCost[LvIdx] : int.MaxValue;

    public bool HealedThisTurn(int idx) => healedThisTurn.Contains(idx);

    public void SubscribeHQ(HQStateManager hqManager)
    {
        if (hq != null) hq.OnStateChanged -= RaiseChanged;
        hq = hqManager;
        if (hq != null) hq.OnStateChanged += RaiseChanged;
    }

    private void RaiseChanged() => OnStateChanged?.Invoke();

    private bool VisitedHQ => HQVisitState.Instance != null && HQVisitState.Instance.HasVisitingParty;

    private bool TryGetHp(int idx, out float cur, out float max)
    {
        cur = 0f; max = 0f;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(idx, out UnitPersistentData d)) return false;
        cur = d.CurrentHp; max = Mathf.Max(0f, d.IngameStats.HP);
        return true;
    }

    public bool CanHeal(int idx)
    {
        if (!IsUnlocked || !VisitedHQ || HealedThisTurn(idx)) return false;
        if (!TryGetHp(idx, out float cur, out float max)) return false;
        if (cur <= 0f || cur >= max) return false;
        var eco = GameManager.Instance?.Economy;
        return eco != null && eco.Has(ResourceType.Money, GetHealCost());
    }

    public bool CanRevive(int idx)
    {
        if (!IsUnlocked || !VisitedHQ || HealedThisTurn(idx)) return false;
        if (!TryGetHp(idx, out float cur, out _)) return false;
        if (cur > 0f) return false;
        var eco = GameManager.Instance?.Economy;
        return eco != null && eco.Has(ResourceType.Money, GetReviveCost());
    }

    public bool TryHeal(int idx)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.HealUnitByPercent(idx, GetHealPercent(), false, out _)) return false;
        healedThisTurn.Add(idx);
        OnStateChanged?.Invoke();
        return true;
    }

    public bool TryRevive(int idx)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.HealUnitByPercent(idx, GetRevivePercent(), true, out _)) return false;
        healedThisTurn.Add(idx);
        OnStateChanged?.Invoke();
        return true;
    }

    public void OnTurnAdvanced()
    {
        healedThisTurn.Clear();
        OnStateChanged?.Invoke();
    }
}
