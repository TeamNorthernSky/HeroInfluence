using System;
using System.Collections.Generic;
using UnityEngine;

// [KJ 260701] 의무실(Infirmary) 도메인 매니저. GameManager 자식(영속).
// enum은 HQDepartment.Recruit=5(옛 이름)를 참조하지만, 기능은 유닛 회복/부활.
// 3계층 규약: 자금은 UI가(Economy.Spend/Add), 데이터 쓰기·턴추적만 매니저.
public class InfirmaryManager : MonoBehaviour
{
    // 기존 프리팹 데이터 보존용. 회복 비용/회복량은 협회 테이블에서 조회한다.
    [Tooltip("호환용 보존. 실제 회복량은 AssociationResourceCatalog 사용")]
    [SerializeField] private int healPercent = 100;
    [Tooltip("호환용 보존. 실제 회복 비용은 AssociationResourceCatalog 사용")]
    [SerializeField] private int[] healCost    = { 1000, 800, 500 };  // 레벨↑ → 비용↓
    [SerializeField] private int[] reviveHpPercent = { 30, 40, 50 };
    [SerializeField] private int reviveCost  = 0;

    private readonly HashSet<int> healedThisTurn = new HashSet<int>();

    // [KJ 260706] 저장 기능(GameSaveService)용 읽기 노출
    public IEnumerable<int> HealedUnitsThisTurn => healedThisTurn;
    private HQStateManager hq;
    public event Action OnStateChanged;

    public bool IsUnlocked => Level >= 1;
    public int Level => hq != null ? hq.GetLevel(HQDepartment.Infirmary) : 0;

    private int LvIdx => Mathf.Clamp(Level - 1, 0, 2);
    public int GetHealPercent() => AssociationCosts.TryInfirmary(Level, out var row)
        ? Mathf.RoundToInt(row.HpRecoveryFraction * 100f) : 0;
    public int GetHealCost() => AssociationCosts.TryInfirmary(Level, out var row)
        && AssociationCosts.TryMoney(row.Cost, out int money) ? money : -1;
    public int GetRevivePercent() =>  reviveHpPercent[LvIdx];
    public int GetReviveCost()    =>  reviveCost;

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
        int cost = GetHealCost();
        if (cost < 0 || GetHealPercent() <= 0) return false;
        var eco = GameManager.Instance?.Economy;
        return eco != null && eco.Has(ResourceType.Money, cost);
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
        int percent = GetHealPercent();
        if (percent <= 0 || GetHealCost() < 0) return false;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.HealUnitByPercent(idx, percent, false, out _)) return false;
        healedThisTurn.Add(idx);
        OnStateChanged?.Invoke();
        return true;
    }

    public bool TryRevive(int idx)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.HealUnitByPercent(idx, GetRevivePercent(), true, out _)) return false;
        // [KJ] 부활은 healedThisTurn에 넣지 않는다 — 같은 턴에 부활 후 회복까지 허용.
        // 이중 부활은 CanRevive의 cur > 0 가드가 막고, 이중 회복은 TryHeal의 등록이 막는다.
        OnStateChanged?.Invoke();
        return true;
    }

    public void OnTurnAdvanced()
    {
        healedThisTurn.Clear();
        OnStateChanged?.Invoke();
    }
}
