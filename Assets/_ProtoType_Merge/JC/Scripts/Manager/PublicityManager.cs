using System;
using UnityEngine;

/// <summary>
/// 홍보 시스템 영속 매니저. GameManager 영속 자식.
/// 홍보 예산과 Catalog에 지정된 주기의 충전·이월을 담당한다.
/// [JC 260616] 영웅별 I.P 저장은 DH UnitPersistentData.CurrentInfluence로 일원화됨
///   → 자체 heroIPEntries 보관을 폐기하고, GetIP/AddIP/잔여용량은 모두 CurrentInfluence에 위임.
///   상한 = 유닛별 IngameStats.Influence(레벨 스케일링). 본 매니저는 "예산"만 영속 보관.
/// </summary>
[DisallowMultipleComponent]
public class PublicityManager : MonoBehaviour
{
    public event Action OnStateChanged;

    [Header("전체 공유 진행 가능 풀")]
    [SerializeField] private int currentPool;
    [SerializeField] private int lastChargeDay; // 마지막 충전이 발생한 day. OnUnlocked/Advance 시 갱신.

    // 비용·횟수·주기·IP 증가량은 DH 협회 Catalog에서 조회한다.
    public static int[] CostPerLevel
    {
        get
        {
            var result = new int[MaxLevel];
            for (int i = 0; i < result.Length; i++) result[i] = GetProgressCostAt(i + 1);
            return result;
        }
    }
    public const int MaxLevel = 5;

    // 영웅별 IP 한계 — 미등록/폴백용 기본값. 실제 상한은 유닛별 IngameStats.Influence.
    // MaxIP는 일부 안내 메시지의 폴백 표기로만 잔존(실 상한은 per-unit Influence).
    public const int DefaultIP = 100;
    public int CurrentPool => currentPool;

    // [KJ 260706] 저장 기능(GameSaveService)용 읽기 노출
    public int LastChargeDay => lastChargeDay;

    public void Initialize()
    {
        Reset();
    }

    /// <summary>[JC 260617] 새 게임 초기화 — 공유 예산/충전 상태를 첫 실행값으로.</summary>
    public void Reset()
    {
        var gm = GameManager.Instance;
        int level = gm != null && gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Publicity) : 0;
        currentPool = TryGetPromotionRules(level, out var row) ? row.InfluenceChargeLimit : 0;
        if (level >= 1 && row == null)
            Debug.LogWarning("[PublicityManager] 홍보 초기 횟수 설정 실패: 협회 Catalog의 홍보 데이터를 확인하세요.", this);
        lastChargeDay = gm != null ? gm.CurrentDay : 1;
        cachedPublicityLevel = level;
        OnStateChanged?.Invoke();
    }

    public void SubscribeHQ(HQStateManager hq)
    {
        if (hq == null) return;
        hq.OnStateChanged += OnHQStateChanged;
        cachedPublicityLevel = hq.GetLevel(HQDepartment.Publicity);
    }

    private int cachedPublicityLevel = -1;

    private void OnHQStateChanged()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        int level = gm.HQ.GetLevel(HQDepartment.Publicity);
        if (cachedPublicityLevel <= 0 && level >= 1)
        {
            // 0 → 1: 해금. 1레벨 기준 횟수를 지급한다.
            OnPublicityUnlocked();
        }
        cachedPublicityLevel = level;
    }

    private void OnPublicityUnlocked()
    {
        var gm = GameManager.Instance;
        int day = gm != null ? gm.CurrentDay : 1;
        if (!TryGetPromotionRules(GetCurrentLevel(), out var row)) return;
        currentPool += row.InfluenceChargeLimit;
        lastChargeDay = day;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// GameManager.CurrentDay setter advance hook에서 호출. 미해금이면 충전·이월 모두 발생 안 함.
    /// </summary>
    public void OnTurnAdvanced(int currentDay)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        int level = gm.HQ.GetLevel(HQDepartment.Publicity);
        if (level <= 0) return;
        if (!TryGetPromotionRules(level, out var row)) return;
        if (currentDay < lastChargeDay + row.InfluenceChargeCycle) return;

        // 충전 — 누적(이월) 방식. clamp 없음(int 자료형 상한까지 허용).
        currentPool += row.InfluenceChargeLimit;
        lastChargeDay = currentDay;
        OnStateChanged?.Invoke();
    }

    public int GetCurrentLevel()
    {
        var gm = GameManager.Instance;
        return gm != null && gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Publicity) : 0;
    }

    public bool IsUnlocked() => GetCurrentLevel() >= 1;

    public int GetProgressCost()
    {
        return GetProgressCostAt(GetCurrentLevel());
    }

    public static int GetProgressCostAt(int level)
        => AssociationCosts.TryPromotion(level, out var row)
            && AssociationCosts.TryMoney(row.Cost, out int money) ? money : -1;

    public static bool TryGetPromotionRules(int level, out DHAssociationPromotionTemplate row)
    {
        if (AssociationCosts.TryPromotion(level, out row) && row.InfluenceChargeLimit >= 0 &&
            row.InfluenceChargeCycle > 0 && row.InfluenceAmountPerCharge > 0)
            return true;
        row = null;
        return false;
    }

    public int GetIP(int unitIndex)
    {
        // 영웅별 IP = 유닛 CurrentInfluence(일원화). 미등록 유닛은 폴백 DefaultIP.
        var repo = PersistentUnitRepository.Instance;
        if (repo != null && repo.TryGetUnit(unitIndex, out var d) && d != null)
            return Mathf.RoundToInt(d.CurrentInfluence);
        return DefaultIP;
    }

    /// <summary>남은 IP 용량을 CSV의 1회당 증가량으로 나눈 진행 가능 횟수.</summary>
    public int GetRemainingCapacity(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo != null && repo.TryGetUnit(unitIndex, out var d) && d != null)
        {
            if (!TryGetPromotionRules(GetCurrentLevel(), out var row)) return 0;
            return Mathf.Max(0, Mathf.FloorToInt((d.IngameStats.Influence - d.CurrentInfluence) / row.InfluenceAmountPerCharge));
        }
        return 0;
    }

    public void AddIP(int unitIndex, int amount)
    {
        if (amount == 0) return;
        var repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(unitIndex, out var d) || d == null) return;

        // 증가는 IngameStats.Influence 상한, 감소(전투 소모)는 MinIP 하한.
        // (DH ApplyRuntimeState도 [0, Influence]로 재clamp하므로 이중 안전)
        float max = Mathf.Max(0f, d.IngameStats.Influence);
        float next = Mathf.Clamp(d.CurrentInfluence + amount, 0, max);
        repo.UpdateUnitRuntimeState(
            unitIndex, d.UnitTemplateKey, d.Level, d.BaseStats, d.LevelupStats,
            d.CurrentSkillIndex, d.CurrentWeaponIndex, d.CurrentWeaponStats,
            d.IngameStats, d.CurrentHp,
            currentInfluence: next);
        OnStateChanged?.Invoke();
    }

    public bool CanProgress(int count)
    {
        if (!IsUnlocked()) return false;
        if (GetProgressCost() < 0) return false;
        if (!TryGetPromotionRules(GetCurrentLevel(), out _)) return false;
        if (count <= 0) return false;
        if (count > currentPool) return false;
        return true;
    }

    /// <summary>풀 + 해당 영웅의 IP 잔여 용량(MaxIP까지)을 모두 만족하는지.</summary>
    public bool CanProgress(int unitIndex, int count)
    {
        if (!CanProgress(count)) return false;
        if (count > GetRemainingCapacity(unitIndex)) return false;
        return true;
    }

    /// <summary>
    /// 횟수 차감 + IP 적립. 자금 차감은 호출자(PublicityModalController)가 별도 처리.
    /// MaxIP를 넘는 진행은 거부(풀·자금 낭비 방지) — 호출자가 횟수를 잔여 용량으로 제한할 것.
    /// </summary>
    public bool TryProgress(int unitIndex, int count)
    {
        if (!CanProgress(unitIndex, count)) return false;
        if (!TryGetPromotionRules(GetCurrentLevel(), out var row)) return false;
        currentPool -= count;
        AddIP(unitIndex, count * row.InfluenceAmountPerCharge); // 실제 IP는 CSV의 1회당 증가량 적용
        return true;
    }

    public void AddIPToAllHeroes(int amount)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo == null) return;
        for (int i = 0; i < repo.Units.Count; i++)
        {
            var u = repo.Units[i];
            if (u == null) continue;
            AddIP(u.UnitIndex, amount);
        }
    }
}
