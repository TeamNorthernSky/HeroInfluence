using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 홍보 시스템 영속 매니저. GameManager 영속 자식.
/// 영웅별 I.P 누적 + 전체 공유 진행 가능 풀 + 7턴 주기 충전·이월.
/// 데이터 형식은 DH PersistentUnitRepository 패턴을 따라(int unitIndex 키 + List+Dictionary lookup)
/// 추후 UnitPersistentData에 influencePoint 필드를 추가하는 마이그레이션이 단순 transfer로 끝나도록 설계.
/// </summary>
[DisallowMultipleComponent]
public class BroadcastManager : MonoBehaviour
{
    public event Action OnStateChanged;

    [Header("영웅별 IP (영속)")]
    [SerializeField] private List<HeroIPEntry> heroIPEntries = new List<HeroIPEntry>();

    [Header("전체 공유 진행 가능 풀")]
    [SerializeField] private int currentPool;
    [SerializeField] private int lastChargeDay; // 마지막 충전이 발생한 day. OnUnlocked/Advance 시 갱신.

    // 단계별 수치 — 추후 CSV로 갈아끼움. 인스펙터 변경 불필요(사용자 결정).
    public static readonly int[] CostPerLevel = { 100, 90, 80, 70, 60 };
    public static readonly int[] WeeklyMaxPerLevel = { 50, 55, 60, 65, 70 };
    public const int WeekTurnInterval = 7;
    public const int MaxLevel = 5;

    [Serializable]
    public class HeroIPEntry
    {
        public int unitIndex;
        public int ip;
    }

    private readonly Dictionary<int, HeroIPEntry> heroIPLookup = new Dictionary<int, HeroIPEntry>();

    public int CurrentPool => currentPool;

    public void Initialize()
    {
        RebuildLookup();
        // HQ 상태 변화 구독은 GameManager가 InitializeManagers 후 호출
    }

    public void SubscribeHQ(HQStateManager hq)
    {
        if (hq == null) return;
        hq.OnStateChanged += OnHQStateChanged;
        cachedBroadcastLevel = hq.GetLevel(HQDepartment.Broadcast);
    }

    private int cachedBroadcastLevel = -1;

    private void OnHQStateChanged()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.HQ == null) return;
        int level = gm.HQ.GetLevel(HQDepartment.Broadcast);
        if (cachedBroadcastLevel <= 0 && level >= 1)
        {
            // 0 → 1: 해금. 초기 충전 = 1단계 weeklyMax (50).
            OnBroadcastUnlocked();
        }
        cachedBroadcastLevel = level;
    }

    private void OnBroadcastUnlocked()
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
        int level = gm.HQ.GetLevel(HQDepartment.Broadcast);
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
        return gm != null && gm.HQ != null ? gm.HQ.GetLevel(HQDepartment.Broadcast) : 0;
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
        return heroIPLookup.TryGetValue(unitIndex, out var e) ? e.ip : 0;
    }

    public void AddIP(int unitIndex, int amount)
    {
        if (amount == 0) return;
        if (!heroIPLookup.TryGetValue(unitIndex, out var e))
        {
            e = new HeroIPEntry { unitIndex = unitIndex, ip = 0 };
            heroIPEntries.Add(e);
            heroIPLookup[unitIndex] = e;
        }
        try { e.ip = checked(e.ip + amount); }
        catch (OverflowException) { e.ip = amount > 0 ? int.MaxValue : 0; }
        OnStateChanged?.Invoke();
    }

    public bool CanProgress(int count)
    {
        if (!IsUnlocked()) return false;
        if (count <= 0) return false;
        if (count > currentPool) return false;
        return true;
    }

    /// <summary>
    /// 횟수 차감 + IP 적립. 자금 차감은 호출자(BroadcastModalController)가 별도 처리.
    /// </summary>
    public bool TryProgress(int unitIndex, int count)
    {
        if (!CanProgress(count)) return false;
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

    private void RebuildLookup()
    {
        heroIPLookup.Clear();
        for (int i = 0; i < heroIPEntries.Count; i++)
        {
            var e = heroIPEntries[i];
            if (e == null || e.unitIndex <= 0) continue;
            if (heroIPLookup.ContainsKey(e.unitIndex))
            {
                Debug.LogWarning($"[BroadcastManager] duplicate unitIndex {e.unitIndex}", this);
                continue;
            }
            heroIPLookup.Add(e.unitIndex, e);
        }
    }
}
