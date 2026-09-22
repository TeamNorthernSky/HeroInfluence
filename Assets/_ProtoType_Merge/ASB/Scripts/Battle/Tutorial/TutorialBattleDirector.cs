using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 러너. 전투 이벤트를 현재 ITutorialBattleFlow의 콜백으로 포워딩하고,
/// flow가 요청한 효과(IDisposable)를 수명별로 추적·정리한다. 전투 규칙은 직접 소유하지 않고
/// BattleFlowManager/BattleCharactor가 돌려주는 인스턴스 핸들만 추적한다(ITutorialBattleFlowHost).
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialBattleDirector : MonoBehaviour, ITutorialBattleFlowHost
{
    private sealed class TrackedEffect
    {
        public IDisposable Handle;
        public TutorialEffectLifetime Lifetime;
    }

    private readonly List<TrackedEffect> trackedEffects = new List<TrackedEffect>();
    private readonly HashSet<BattleCharactor> boundUnits = new HashSet<BattleCharactor>();
    private readonly Dictionary<BattleCharactor, Action<float, float>> hpHandlers =
        new Dictionary<BattleCharactor, Action<float, float>>();
    private readonly List<Action> boundaryQueue = new List<Action>();

    private ITutorialBattleFlow currentFlow;
    private BattleFlowManager flowManager;
    private BattleManager battleManager;
    private EnemySpawner enemySpawner;
    private PlayerSpawner playerSpawner;
    private TutorialBattleUI tutorialUI;
    private string battleKey = string.Empty;
    private int zoneId = -1;
    private bool combatEventsAttached;

    [Header("Tutorial UI 콘텐츠")]
    [SerializeField] private TutorialUiCatalog uiCatalog;
    private TutorialUiSheet uiSheet;

    public bool IsRunning { get; private set; }
    public event Action<ITutorialBattleFlow> OnTutorialBattleCompleted;

    // --- ITutorialBattleFlowHost 접근자 ---
    public BattleFlowManager FlowManager => flowManager;
    public BattleManager BattleManager => battleManager;
    public EnemySpawner EnemySpawner => enemySpawner;
    public PlayerSpawner PlayerSpawner => playerSpawner;
    public TutorialBattleUI UI => tutorialUI;
    public string BattleKey => battleKey;
    public int ZoneId => zoneId;

    public bool Initialize(
        ITutorialBattleFlow flow,
        BattleFlowManager battleFlowManager,
        BattleManager runtimeBattleManager,
        EnemySpawner enemySpawnerRef,
        PlayerSpawner playerSpawnerRef,
        TutorialBattleUI ui,
        string battleKeyValue,
        int zoneIdValue)
    {
        Shutdown();

        if (flow == null || battleFlowManager == null || runtimeBattleManager == null)
        {
            Debug.LogWarning(
                "[TutorialBattle] Director 초기화 실패: flow, BattleFlowManager, BattleManager가 모두 필요합니다.",
                this);
            return false;
        }

        currentFlow = flow;
        flowManager = battleFlowManager;
        battleManager = runtimeBattleManager;
        enemySpawner = enemySpawnerRef;
        playerSpawner = playerSpawnerRef;
        tutorialUI = ui;
        battleKey = string.IsNullOrWhiteSpace(battleKeyValue)
            ? (flow.BattleKey ?? string.Empty)
            : battleKeyValue.Trim();
        zoneId = zoneIdValue;
        uiSheet = uiCatalog != null ? uiCatalog.Find(zoneId, battleKey) : null;
        IsRunning = true;

        flowManager.OnTurnStarted += HandleTurnStarted;
        flowManager.OnTurnResolved += HandleTurnResolved;
        flowManager.OnBattleEnded += HandleBattleEnded;
        flowManager.OnParticipantRegistered += HandleParticipantRegistered;
        battleManager.OnSkillResolved += HandleSkillResolved;
        combatEventsAttached = true;
        if (tutorialUI != null)
        {
            tutorialUI.ActionPerformed += HandleUIActionPerformed;
        }

        IReadOnlyList<BattleCharactor> participants = flowManager.Participants;
        for (int i = 0; i < participants.Count; i++)
        {
            BindUnit(participants[i]);
        }

        SafeFlow(() =>
        {
            currentFlow.Attach(this);
            currentFlow.OnBattleEntered();
        });
        return true;
    }

    // --- ITutorialBattleFlowHost API ---

    public BattleCharactor FindUnit(TutorialUnitSide side, string templateId, UnitMatchMode mode = UnitMatchMode.First)
    {
        if (flowManager == null)
        {
            return null;
        }

        // 현재는 First 시맨틱만 지원(단일 반환). All/Slot은 예약.
        IReadOnlyList<BattleCharactor> participants = flowManager.Participants;
        for (int i = 0; i < participants.Count; i++)
        {
            if (TutorialUnitMatcher.MatchesTemplateId(participants[i], side, templateId))
            {
                return participants[i];
            }
        }

        return null;
    }

    public void Track(IDisposable handle, TutorialEffectLifetime lifetime)
    {
        if (handle == null)
        {
            return;
        }

        if (!IsRunning)
        {
            SafeDispose(handle);
            return;
        }

        trackedEffects.Add(new TrackedEffect { Handle = handle, Lifetime = lifetime });
    }

    public void ReleaseStepEffects() => DisposeEffects(TutorialEffectLifetime.Step);

    /// <summary>전투 종료 시: 전투 영향(Step/Battle/Manual) 효과 해제. ResultPhase는 결과창까지 유지.</summary>
    public void ReleaseCombatEffects()
    {
        DisposeEffects(TutorialEffectLifetime.Step);
        DisposeEffects(TutorialEffectLifetime.Battle);
        DisposeEffects(TutorialEffectLifetime.Manual);
    }

    public void CompleteFlow() => CompleteCurrentFlow();

    public void RequestBoundaryIntervention(Action intervention)
    {
        if (intervention != null)
        {
            boundaryQueue.Add(intervention);
        }
    }

    public bool ShowUi(string key) => ShowResolved(key, null);

    public bool ShowUi(string key, params object[] formatArgs) => ShowResolved(key, formatArgs);

    public void HideUi() => tutorialUI?.Hide();

    private bool ShowResolved(string key, object[] formatArgs)
    {
        if (tutorialUI == null)
        {
            Debug.LogWarning($"[Tutorial] TutorialBattleUI 미할당: key={key}", this);
            return false;
        }

        // 코드-온리: 표시 문구/버튼은 씬 stepTexts/actionButtons가 key로 소유한다.
        // 시트는 '있으면' 문구·하이라이트 보조로만 쓰고, 엔트리가 없어도(step3/step4 등) key만으로 표시한다.
        TutorialUiEntry entry = uiSheet != null ? uiSheet.Get(key) : null;
        string message = entry != null ? entry.message : null;
        string highlight = entry != null ? entry.highlightActionId : null;

        if (entry != null && formatArgs != null && formatArgs.Length > 0)
        {
            try
            {
                message = string.Format(entry.message ?? string.Empty, formatArgs);
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"[Tutorial] UI 문구 형식 오류(key={key}): {e.Message}", this);
                message = null;
            }
        }

        return tutorialUI.TryShow(key, message, highlight);
    }

    // --- 결과창(전투 루프 밖) — BattleSceneManager가 호출 ---

    public void NotifyBattleResultShown(BattleResult result)
    {
        SafeFlow(() => currentFlow.OnBattleResultShown(result));
    }

    public void NotifyBattleResultAccepted(BattleResult result)
    {
        SafeFlow(() => currentFlow.OnBattleResultAccepted(result));
        Shutdown();
    }

    public void CompleteCurrentFlow()
    {
        if (!IsRunning)
        {
            return;
        }

        ITutorialBattleFlow completed = currentFlow;
        // 완료 후에도 결과창 단계가 남을 수 있으므로 Step 효과만 정리(ResultPhase 유지).
        DisposeEffects(TutorialEffectLifetime.Step);
        OnTutorialBattleCompleted?.Invoke(completed);
    }

    public void Shutdown()
    {
        IsRunning = false;
        DetachEvents();
        DisposeAllEffects();
        boundaryQueue.Clear();
        tutorialUI?.Hide();

        currentFlow = null;
        flowManager = null;
        battleManager = null;
        enemySpawner = null;
        playerSpawner = null;
        tutorialUI = null;
        uiSheet = null;
        battleKey = string.Empty;
        zoneId = -1;
    }

    private void OnDisable() => Shutdown();
    private void OnDestroy() => Shutdown();

    // --- 이벤트 → flow 포워딩 ---

    private void HandleTurnStarted(int round, BattleCharactor unit)
        => SafeFlow(() => currentFlow.OnTurnStarted(round, unit));

    private void HandleTurnResolved(TurnResolutionContext ctx)
    {
        DrainBoundaryQueue();
        SafeFlow(() => currentFlow.OnTurnResolved(ctx));
    }

    private void HandleSkillResolved(SkillResolutionContext ctx)
    {
        DrainBoundaryQueue();
        SafeFlow(() => currentFlow.OnSkillResolved(ctx));
    }

    private void HandleParticipantRegistered(BattleCharactor unit)
    {
        BindUnit(unit);
        SafeFlow(() => currentFlow.OnParticipantRegistered(unit));
    }

    private void HandleUIActionPerformed(string actionId)
        => SafeFlow(() => currentFlow.OnUIAction(actionId));

    private void HandleUnitHpChanged(BattleCharactor unit, float current, float max)
        => SafeFlow(() => currentFlow.OnUnitHpChanged(unit, current, max));

    private void HandleUnitDied(BattleCharactor unit)
        => SafeFlow(() => currentFlow.OnUnitDied(unit));

    private void HandleBattleEnded(BattleResult result)
    {
        // 전투 영향 효과만 해제하고 flow는 결과창 단계까지 생존. 전투 이벤트 구독만 해제.
        ReleaseCombatEffects();
        DetachCombatEvents();
        tutorialUI?.Hide();   // 전투 단계 UI가 결과창 위에 남지 않도록(§6). 결과 UI는 OnBattleResultShown에서 새로 표시.
    }

    // --- 내부 헬퍼 ---

    private void DrainBoundaryQueue()
    {
        if (boundaryQueue.Count == 0)
        {
            return;
        }

        Action[] snapshot = boundaryQueue.ToArray();
        boundaryQueue.Clear();
        for (int i = 0; i < snapshot.Length; i++)
        {
            try { snapshot[i]?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    private void SafeFlow(Action action)
    {
        if (!IsRunning || currentFlow == null || action == null)
        {
            return;
        }

        try { action(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    private void BindUnit(BattleCharactor unit)
    {
        if (unit == null || !boundUnits.Add(unit))
        {
            return;
        }

        Action<float, float> hpHandler = (current, max) => HandleUnitHpChanged(unit, current, max);
        hpHandlers[unit] = hpHandler;
        unit.OnHpChanged += hpHandler;
        unit.OnDied += HandleUnitDied;
    }

    private void DetachEvents()
    {
        DetachCombatEvents();

        if (tutorialUI != null)
        {
            tutorialUI.ActionPerformed -= HandleUIActionPerformed;
        }

        foreach (BattleCharactor unit in boundUnits)
        {
            if (unit == null)
            {
                continue;
            }

            if (hpHandlers.TryGetValue(unit, out Action<float, float> hpHandler))
            {
                unit.OnHpChanged -= hpHandler;
            }
            unit.OnDied -= HandleUnitDied;
        }

        hpHandlers.Clear();
        boundUnits.Clear();
    }

    private void DetachCombatEvents()
    {
        if (!combatEventsAttached)
        {
            return;
        }

        combatEventsAttached = false;
        if (flowManager != null)
        {
            flowManager.OnTurnStarted -= HandleTurnStarted;
            flowManager.OnTurnResolved -= HandleTurnResolved;
            flowManager.OnBattleEnded -= HandleBattleEnded;
            flowManager.OnParticipantRegistered -= HandleParticipantRegistered;
        }

        if (battleManager != null)
        {
            battleManager.OnSkillResolved -= HandleSkillResolved;
        }
    }

    private void DisposeEffects(TutorialEffectLifetime lifetime)
    {
        for (int i = trackedEffects.Count - 1; i >= 0; i--)
        {
            if (trackedEffects[i].Lifetime != lifetime)
            {
                continue;
            }

            IDisposable handle = trackedEffects[i].Handle;
            trackedEffects.RemoveAt(i);
            SafeDispose(handle);
        }
    }

    private void DisposeAllEffects()
    {
        for (int i = trackedEffects.Count - 1; i >= 0; i--)
        {
            IDisposable handle = trackedEffects[i].Handle;
            trackedEffects.RemoveAt(i);
            SafeDispose(handle);
        }
    }

    private static void SafeDispose(IDisposable disposable)
    {
        if (disposable == null)
        {
            return;
        }

        try { disposable.Dispose(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
