using UnityEngine;

/// <summary>튜토리얼 저장소와 수명을 공유하는 홍보 예산. 일반 게임 데이터는 사용하지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class TutorialPublicityState : MonoBehaviour
{
    public const int CostPerProgress = 100;
    public const int WeeklyAllowance = 50;
    private TutorialProgressRepository repository;
    private int pool = WeeklyAllowance;
    private int lastChargeTurn;
    private bool processing;
    public int Pool { get { RefreshBudget(); return pool; } }

    public static TutorialPublicityState Get(TutorialProgressRepository repository)
    {
        if (repository == null) return null;
        var state = repository.GetComponent<TutorialPublicityState>();
        if (state == null) state = repository.gameObject.AddComponent<TutorialPublicityState>();
        if (state.repository == null)
        {
            state.repository = repository;
            state.lastChargeTurn = repository.CurrentTurn;
            repository.ProgressChanged += state.RefreshBudget;
        }
        return state;
    }

    private void OnDestroy()
    {
        if (repository != null) repository.ProgressChanged -= RefreshBudget;
    }

    private void RefreshBudget()
    {
        if (repository == null) return;
        int turn = repository.CurrentTurn;
        if (turn < lastChargeTurn || repository.UnitStates.Count == 0)
        {
            pool = WeeklyAllowance;
            lastChargeTurn = turn;
        }
        int weeks = Mathf.Max(0, (turn - lastChargeTurn) / 7);
        pool += weeks * WeeklyAllowance;
        lastChargeTurn += weeks * 7;
    }

    public bool TryProgress(string key, int count)
    {
        if (processing || repository == null || count <= 0 || count > Pool) return false;
        if (!repository.TryGetUnitState(key, out var unit)) return false;
        if (count > unit.MaxIp - unit.CurrentIp) return false;
        long cost = (long)CostPerProgress * count;
        if (cost > int.MaxValue || !repository.HasResource(ResourceType.Money, (int)cost)) return false;
        processing = true;
        try
        {
            if (!repository.SpendResource(ResourceType.Money, (int)cost)) return false;
            pool -= count;
            repository.SetUnitStats(key, unit.CurrentHp, unit.MaxHp, unit.CurrentIp + count,
                unit.MaxIp, unit.Atk, unit.Level, unit.Exp, unit.MaxExp);
            return true;
        }
        finally { processing = false; }
    }
}
