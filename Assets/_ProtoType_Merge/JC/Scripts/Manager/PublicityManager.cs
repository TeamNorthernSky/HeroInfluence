using System;
using UnityEngine;

/// <summary>
/// 홍보 시스템 영속 매니저. GameManager 영속 자식.
/// "홍보 예산(전체 공유 진행 가능 풀) + 7턴 주기 충전·이월"을 담당한다.
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

    // 단계별 수치 — 추후 CSV로 갈아끼움. 인스펙터 변경 불필요(사용자 결정).
    public static readonly int[] CostPerLevel = { 100, 90, 80, 70, 60 };
    public static readonly int[] WeeklyMaxPerLevel = { 50, 55, 60, 65, 70 };
    public const int WeekTurnInterval = 7;
    public const int MaxLevel = 5;

    // 영웅별 IP 한계 — 미등록/폴백용 기본값. 실제 상한은 유닛별 IngameStats.Influence.
    // MaxIP는 일부 안내 메시지의 폴백 표기로만 잔존(실 상한은 per-unit Influence).
    public const int DefaultIP = 100;
    public const int MaxIP = 200;
    public const int MinIP = 0;

    public int CurrentPool => currentPool;

    // [KJ 260706] 저장 기능(GameSaveService)용 읽기 노출
    public int LastChargeDay => lastChargeDay;

    public void Initialize()
    {
        // 영웅별 IP는 DH CurrentInfluence가 보관 → 별도 lookup 재구성 불필요.
        // HQ 상태 변화 구독은 GameManager가 InitializeManagers 후 호출
    }

    /// <summary>[JC 260617] 새 게임 초기화 — 공유 예산/충전 상태를 첫 실행값으로.</summary>
    public void Reset()
    {
        currentPool = 0;
        lastChargeDay = 0;
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
            // 0 → 1: 해금. 초기 충전 = 1단계 weeklyMax (50).
            OnPublicityUnlocked();
        }
        cachedPublicityLevel = level;
    }

    private void OnPublicityUnlocked()
    {
        var gm = GameManager.Instance;
        int day = gm != null ? gm.CurrentDay : 1;
        currentPool += WeeklyMaxPerLevel[0];
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
        if (currentDay < lastChargeDay + WeekTurnInterval) return;

        // 충전 — 누적(이월) 방식. clamp 없음(int 자료형 상한까지 허용).
        int weekly = WeeklyMaxPerLevel[Mathf.Clamp(level - 1, 0, WeeklyMaxPerLevel.Length - 1)];
        currentPool += weekly;
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
        int level = GetCurrentLevel();
        if (level <= 0) return CostPerLevel[0];
        return CostPerLevel[Mathf.Clamp(level - 1, 0, CostPerLevel.Length - 1)];
    }

    public int GetIP(int unitIndex)
    {
        // 영웅별 IP = 유닛 CurrentInfluence(일원화). 미등록 유닛은 폴백 DefaultIP.
        var repo = PersistentUnitRepository.Instance;
        if (repo != null && repo.TryGetUnit(unitIndex, out var d) && d != null)
            return Mathf.RoundToInt(d.CurrentInfluence);
        return DefaultIP;
    }

    /// <summary>해당 영웅이 최대 IP(=IngameStats.Influence)까지 더 받을 수 있는 잔여 용량.</summary>
    public int GetRemainingCapacity(int unitIndex)
    {
        var repo = PersistentUnitRepository.Instance;
        if (repo != null && repo.TryGetUnit(unitIndex, out var d) && d != null)
            return Mathf.Max(0, Mathf.RoundToInt(d.IngameStats.Influence - d.CurrentInfluence));
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
        float next = Mathf.Clamp(d.CurrentInfluence + amount, MinIP, max);
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
        currentPool -= count;
        AddIP(unitIndex, count); // AddIP가 OnStateChanged 발화하므로 중복 발화 없음
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
