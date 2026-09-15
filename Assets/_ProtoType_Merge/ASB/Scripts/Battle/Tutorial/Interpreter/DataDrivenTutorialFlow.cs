using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스케줄 시트(트리거+액션)를 해석하는 ITutorialBattleFlow 구현체.
/// 오케스트레이션만 담당: 신호 FIFO 큐 · step/flag/once · 경계예약 · activeUiRequest.
/// 판정은 TutorialTriggerEvaluator, 실행은 TutorialActionExecutor.
/// </summary>
public sealed class DataDrivenTutorialFlow : TutorialBattleFlow, IDataDrivenTutorialFlow
{
    private const int MaxImmediateTransitionsPerDispatch = 64;

    private readonly TutorialScheduleSheet sheet;
    private readonly TutorialHookRegistry hooks;

    private int currentStep;
    private readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> firedRuleIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly Queue<TutorialEventContext> signalQueue = new Queue<TutorialEventContext>();
    private readonly HashSet<string> pendingRuleIds = new HashSet<string>(StringComparer.Ordinal);
    private bool isDispatching;
    private TutorialUiRequest activeUiRequest;
    private int lastRoundSeen = -1;

    public DataDrivenTutorialFlow(TutorialScheduleSheet sheet, TutorialHookRegistry hooks)
    {
        this.sheet = sheet;
        this.hooks = hooks;
    }

    public override string BattleKey => sheet != null ? sheet.BattleKey : string.Empty;
    public override int ZoneId => sheet != null ? sheet.ZoneId : -1;

    // ── IDataDrivenTutorialFlow ──
    ITutorialBattleFlowHost IDataDrivenTutorialFlow.Host => Host;
    public int CurrentStep => currentStep;
    public bool HasFlag(string flag) => !string.IsNullOrWhiteSpace(flag) && flags.Contains(flag.Trim());
    public void SetStep(int step) => currentStep = step;
    public void SetFlag(string flag) { if (!string.IsNullOrWhiteSpace(flag)) flags.Add(flag.Trim()); }
    public void CompleteTutorial() => Complete();
    public void SetActiveUiRequest(TutorialUiRequest request) => activeUiRequest = request;
    BattleCharactor IDataDrivenTutorialFlow.FindUnit(TutorialUnitSide side, string templateId)
        => Host != null ? Host.FindUnit(side, templateId, UnitMatchMode.First) : null;

    // ── 콜백 → 큐잉 → Drain ──
    public override void OnBattleEntered()
    {
        int round = Host != null && Host.FlowManager != null ? Host.FlowManager.RoundIndex : 0;
        lastRoundSeen = round;
        Enqueue(TutorialTriggerType.BattleEntered, round: round);
        Enqueue(TutorialTriggerType.RoundStarted, round: round);   // 첫 라운드 합성
        Enqueue(TutorialTriggerType.Immediate);
        Drain();
    }

    public override void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (round != lastRoundSeen)
        {
            lastRoundSeen = round;
            Enqueue(TutorialTriggerType.RoundStarted, round: round);
        }
        Enqueue(TutorialTriggerType.UnitTurnStarted, round: round, unit: unit);
        Drain();
    }

    public override void OnTurnResolved(TurnResolutionContext ctx)
    {
        Enqueue(TutorialTriggerType.TurnResolved, round: ctx != null ? ctx.RoundIndex : 0, turn: ctx, unit: ctx != null ? ctx.Actor : null);
        Drain();
    }

    public override void OnSkillResolved(SkillResolutionContext ctx)
    {
        Enqueue(TutorialTriggerType.SkillResolved, skill: ctx, unit: ctx != null ? ctx.Actor : null);
        Drain();
    }

    public override void OnUnitHpChanged(BattleCharactor unit, float current, float max)
    {
        Enqueue(TutorialTriggerType.UnitHpAtOrBelow, unit: unit, currentHp: current, maxHp: max);
        Drain();
    }

    public override void OnUnitDied(BattleCharactor unit)
    {
        Enqueue(TutorialTriggerType.UnitDied, unit: unit);
        Enqueue(TutorialTriggerType.AliveCountAtOrBelow);
        Drain();
    }

    public override void OnParticipantRegistered(BattleCharactor unit)
    {
        Enqueue(TutorialTriggerType.ParticipantRegistered, unit: unit);
        Enqueue(TutorialTriggerType.AliveCountAtOrBelow);
        Drain();
    }

    public override void OnUIAction(string actionId)
    {
        // B 완료: activeUiRequest와 일치하면 그 규칙 잠금만 해제
        if (activeUiRequest != null &&
            string.Equals(actionId, activeUiRequest.RequiredActionId, StringComparison.Ordinal))
        {
            Host?.ReleaseEffects(activeUiRequest.RequestId);
            activeUiRequest = null;
        }
        Enqueue(TutorialTriggerType.UIAction, actionId: actionId);
        Drain();
    }

    public override void OnBattleResultShown(BattleResult result)
    {
        Enqueue(TutorialTriggerType.BattleResultShown, result: result);
        Drain();
    }

    public override void OnBattleResultAccepted(BattleResult result)
    {
        Enqueue(TutorialTriggerType.BattleResultAccepted, result: result);
        Drain();
    }

    // ── 내부 ──
    private void Enqueue(TutorialTriggerType type, int round = 0, BattleCharactor unit = null,
        float currentHp = 0f, float maxHp = 0f, SkillResolutionContext skill = null,
        TurnResolutionContext turn = null, string actionId = null, BattleResult result = BattleResult.None)
    {
        signalQueue.Enqueue(new TutorialEventContext
        {
            eventType = type, round = round, unit = unit,
            currentHp = currentHp, maxHp = maxHp, skill = skill, turn = turn,
            actionId = actionId, result = result,
        });
    }

    private void Drain()
    {
        if (isDispatching)
        {
            return;   // 액션이 유발한 이벤트는 큐에서 대기(재진입 방지)
        }

        isDispatching = true;
        try
        {
            while (signalQueue.Count > 0)
            {
                EvaluateAndFire(signalQueue.Dequeue());
                RunImmediateChain();
            }
        }
        finally
        {
            isDispatching = false;
        }
    }

    private void FillState(TutorialEventContext signal)
    {
        signal.currentStep = currentStep;
        signal.flags = flags;
        if (Host != null && Host.FlowManager != null)
        {
            signal.alivePlayerCount = Host.FlowManager.GetAlivePlayerCount();
            signal.aliveEnemyCount = Host.FlowManager.GetAliveEnemyCount();
        }
    }

    private void EvaluateAndFire(TutorialEventContext signal)
    {
        FillState(signal);

        // 스냅샷: 매칭 규칙 목록을 먼저 확정(앞 규칙의 SetStep이 뒤 규칙을 여는 순서의존 제거)
        var snapshot = CollectMatches(signal, immediateOnly: false);
        bool observeOnly = IsObserveOnly(signal.eventType);

        for (int i = 0; i < snapshot.Count; i++)
        {
            TutorialRuleEntry rule = snapshot[i];
            if (observeOnly && rule.HasPausingAction())
            {
                ReserveAtBoundary(rule, signal);
            }
            else
            {
                RunRule(rule, signal);
            }
        }
    }

    private void RunImmediateChain()
    {
        int guard = 0;
        while (guard++ < MaxImmediateTransitionsPerDispatch)
        {
            var signal = new TutorialEventContext { eventType = TutorialTriggerType.Immediate };
            FillState(signal);

            var snapshot = CollectMatches(signal, immediateOnly: true);
            if (snapshot.Count == 0)
            {
                return;
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                RunRule(snapshot[i], signal);
            }
        }

        Debug.LogError("[Tutorial] Immediate 연쇄 상한 초과 — 체인 중단(규칙 순환 의심).");
    }

    private List<TutorialRuleEntry> CollectMatches(TutorialEventContext signal, bool immediateOnly)
    {
        var result = new List<TutorialRuleEntry>();
        IReadOnlyList<TutorialRuleEntry> rules = sheet != null ? sheet.Rules : null;
        for (int i = 0; rules != null && i < rules.Count; i++)
        {
            TutorialRuleEntry rule = rules[i];
            if (rule == null || rule.trigger == null)
            {
                continue;
            }
            if (immediateOnly && rule.trigger.type != TutorialTriggerType.Immediate)
            {
                continue;
            }
            if (rule.once && firedRuleIds.Contains(rule.ruleId))
            {
                continue;
            }
            if (pendingRuleIds.Contains(rule.ruleId))
            {
                continue;   // 이미 경계예약됨
            }
            if (!TutorialTriggerEvaluator.Matches(rule.trigger, signal))
            {
                continue;
            }
            result.Add(rule);
        }
        return result;
    }

    private void ReserveAtBoundary(TutorialRuleEntry rule, TutorialEventContext signal)
    {
        if (!pendingRuleIds.Add(rule.ruleId))
        {
            return;   // 중복 예약 방지
        }

        TutorialRuleEntry captured = rule;
        TutorialEventContext capturedSignal = signal;
        Host?.RequestBoundaryIntervention(() =>
        {
            pendingRuleIds.Remove(captured.ruleId);
            RunRule(captured, capturedSignal);
        });
    }

    private void RunRule(TutorialRuleEntry rule, TutorialEventContext signal)
    {
        if (rule == null)
        {
            return;
        }

        if (rule.once)
        {
            firedRuleIds.Add(rule.ruleId);   // 실행 전 표기(재진입 방지)
        }

        var execCtx = new TutorialExecutionContext { Flow = this, Rule = rule, Event = signal };
        IList<TutorialAction> actions = rule.actions;
        for (int i = 0; actions != null && i < actions.Count; i++)
        {
            TutorialActionResult r = TutorialActionExecutor.Execute(actions[i], execCtx, hooks);
            if (r == TutorialActionResult.FailedAbortRule)
            {
                break;
            }
        }
    }

    private static bool IsObserveOnly(TutorialTriggerType t)
        => t == TutorialTriggerType.UnitHpAtOrBelow ||
           t == TutorialTriggerType.UnitDied ||
           t == TutorialTriggerType.AliveCountAtOrBelow;
}
