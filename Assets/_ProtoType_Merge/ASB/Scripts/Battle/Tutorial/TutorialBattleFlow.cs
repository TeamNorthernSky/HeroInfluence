using System;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// 코드 Flow의 공용 베이스. 생명주기·효과추적·정리는 host가 소유하고, 서브클래스는 필요한 콜백만 override한다.
/// 스텝은 서브클래스가 자체 enum + 콜백 내 분기로 관리한다.
/// </summary>
public abstract class TutorialBattleFlow : ITutorialBattleFlow
{
    protected ITutorialBattleFlowHost Host { get; private set; }

    public abstract string BattleKey { get; }
    public virtual int ZoneId => -1;
    public bool IsComplete { get; private set; }

    public void Attach(ITutorialBattleFlowHost host)
    {
        Host = host;
        OnAttach();
    }

    /// <summary>host 주입 직후 1회. 필요 시 override.</summary>
    protected virtual void OnAttach() { }

    // --- host 위임 헬퍼 ---

    /// <summary>IDisposable 효과를 수명별로 추적(기본 Step). 정리는 host가 담당.</summary>
    protected void Track(IDisposable handle, TutorialEffectLifetime lifetime = TutorialEffectLifetime.Step)
        => Host?.Track(handle, lifetime);

    /// <summary>현재 스텝(Step 수명) 효과 해제 후 다음 스텝으로.</summary>
    protected void AdvanceStep() => Host?.ReleaseStepEffects();

    /// <summary>flow 완료 통지(중복 방지).</summary>
    protected void Complete()
    {
        if (IsComplete)
        {
            return;
        }

        IsComplete = true;
        Host?.CompleteFlow();
    }

    /// <summary>행동 도중 감지 → 다음 안전 경계에서 1회 실행하도록 예약.</summary>
    protected void RequestInterventionAtNextBoundary(Action intervention)
        => Host?.RequestBoundaryIntervention(intervention);

    /// <summary>안정 템플릿 ID로 참가자 조회.</summary>
    protected BattleCharactor FindUnit(TutorialUnitSide side, string templateId, UnitMatchMode mode = UnitMatchMode.First)
        => Host?.FindUnit(side, templateId, mode);

    // --- UI ---

    /// <summary>UI 시트 key로 표시(비차단). 표시 성공 여부 반환.</summary>
    protected bool ShowUi(string key) => Host != null && Host.ShowUi(key);

    /// <summary>동적 문구 UI 표시(string.Format).</summary>
    protected bool ShowUi(string key, params object[] formatArgs) => Host != null && Host.ShowUi(key, formatArgs);

    /// <summary>현재 UI 숨김.</summary>
    protected void HideUi() => Host?.HideUi();

    /// <summary>
    /// FlowLock을 잡고 UI를 띄운다. UI 표시 실패 시 잠금을 즉시 해제(fail-open)하고 false를 반환한다.
    /// → 작성자가 규칙을 암기할 필요 없이 데드락을 API가 방지한다.
    /// </summary>
    protected bool ShowBlockingUi(string key)
    {
        if (Host == null)
        {
            return false;
        }

        IDisposable gate = Host.FlowManager.AcquireFlowLock(this);
        if (!ShowUi(key))
        {
            gate.Dispose();   // UI 미표시 → 잠금 잔류 금지
            return false;
        }

        Track(gate);          // 성공 시에만 Step 수명으로 추적
        return true;
    }

    // --- 빈 가상 콜백 (필요한 것만 override) ---

    public virtual void OnBattleEntered() { }
    public virtual void OnTurnStarted(int round, BattleCharactor unit) { }
    public virtual void OnTurnResolved(TurnResolutionContext ctx) { }
    public virtual void OnSkillResolved(SkillResolutionContext ctx) { }
    public virtual void OnUnitHpChanged(BattleCharactor unit, float current, float max) { }
    public virtual void OnUnitDied(BattleCharactor unit) { }
    public virtual void OnParticipantRegistered(BattleCharactor unit) { }
    public virtual void OnUIAction(string actionId) { }
    public virtual void OnCellClicked(GridCellRef cell) { }
    public virtual void OnTargetSelected(BattleCharactor actor, BattleCharactor target) { }
    public virtual void OnCounterResolved(CounterResolutionContext ctx) { }
    public virtual void OnBattleResultShown(BattleResult result) { }
    public virtual void OnBattleResultAccepted(BattleResult result) { }
}
