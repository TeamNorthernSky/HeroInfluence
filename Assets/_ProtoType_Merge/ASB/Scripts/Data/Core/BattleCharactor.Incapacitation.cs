using UnityEngine;

/// <summary>
/// 무력화(다운 후 라운드 경과 복귀). (구현지시서: 증폭기창구_페이즈상태머신 §5-A)
///
/// 대신맞기의 <see cref="BattleCharactor"/> Guard 파샬과 동형 — 코어는 블랙보드/참여자를 모른다(단방향 의존).
/// "사망 대신 무력화"는 <c>TakeDamage</c>의 Die 직전 seam(<see cref="TryEnterIncapacitationInsteadOfDeath"/>)에서만 발동.
/// 카운트다운은 자기 턴이 아니라 라운드 경계(EncounterParticipant가 OnTurnStarted에서 구동)로 진행한다.
/// </summary>
public partial class BattleCharactor
{
    /// <summary>참여자가 phase>=2일 때 true로 세팅. 코어는 블랙보드를 참조하지 않는다.</summary>
    public bool CanBeIncapacitated { get; set; }

    /// <summary>무력화(다운) 상태. 사망(IsDead)과 별개 — 살아있으나 행동 불가/불사.</summary>
    public bool IsIncapacitated { get; private set; }

    private int _incapRoundsLeft;

    /// <summary>사망 직전 호출. 무력화 대상이면 죽지 않고 다운 상태로 전환하고 true 반환.</summary>
    internal bool TryEnterIncapacitationInsteadOfDeath()
    {
        if (!CanBeIncapacitated || IsIncapacitated) return false;

        IsIncapacitated = true;
        _incapRoundsLeft = 2;               // 2 라운드(확정). 라운드 경계마다 감소.
        currentHp = 0f;                     // 다운. IsDead=false / OnDied 미발화 → 파괴 카운트 무관.
        OnHpChanged?.Invoke(CurrentHp, MaxHp);
        return true;
    }

    /// <summary>라운드 경계에서 1회 호출(자기 턴 무관). 0이 되면 HP 50%로 재활성.</summary>
    public void TickIncapacitationRound()
    {
        if (!IsIncapacitated) return;

        _incapRoundsLeft--;
        if (_incapRoundsLeft <= 0)
        {
            IsIncapacitated = false;
            currentHp = Mathf.Clamp(MaxHp * 0.5f, 1f, MaxHp);   // 복귀 HP = 최대의 50%(확정)
            OnHpChanged?.Invoke(CurrentHp, MaxHp);
        }
    }

    /// <summary>전투 시작 리셋(MarkInitializedFromDataPipeline에서 호출).</summary>
    public void ResetIncapacitation()
    {
        IsIncapacitated = false;
        CanBeIncapacitated = false;
        _incapRoundsLeft = 0;
    }
}
