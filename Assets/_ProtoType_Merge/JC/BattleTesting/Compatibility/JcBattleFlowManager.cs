// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs
// 원본 객체: BattleFlowManager -> JcBattleFlowManager
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System;
using System.Collections;
using System.Collections.Generic;
using GridCellRef = ASB.Work.BattleGrid.GridCell;
using GridManagerRef = ASB.Work.BattleGrid.JcBattleGridManager;
using System.Linq;
using ASB.Work.Battle.SkillExecution;
using UnityEngine;

// [JC 260513] DH의 CombatResult와 명세 일치화. ASB BattleFlowManager는 현재 Victory/Defeat만 발화.
// None/Escape/Cancelled는 미래 확장 슬롯 (ASB 발화 흐름 추가 또는 외부 시스템이 직접 set 가능).
//
// 향후 계획(미확정): Battle/Combat 명명 분리 전반을 Combat으로 통일 예정.
//   - 그 시점에 본 enum과 DH CombatResult 통합 + ASB OnBattleEnded<CombatResult>로 시그니처 변경 + 매핑 함수 폐기.
/// <summary>
/// 전투 전체 흐름 제어기.
/// - 참가자 관리 + 속도 기반 턴 정렬
/// - 코루틴 기반 무한 전투 루프
/// - 플레이어 행동 완료(PlayerSkillActionResolved) 대기
/// </summary>
[DisallowMultipleComponent]
public class JcBattleFlowManager : MonoBehaviour
{
    [Header("Turn/Loop")]
    [Tooltip("초기화 직후 턴 루프를 시작합니다. JC 세션은 연결 완료 후 직접 시작하므로 끕니다.")]
    [SerializeField] private bool autoStartOnInitialize = true;
    [Tooltip("씬에 배치한 전투 시작/턴 안내입니다. 연결하면 안내가 닫힌 뒤 행동을 시작하며, 비워 두면 기존 흐름을 유지합니다.")]
    [SerializeField] private JcBattleTurnBanner turnBanner;
    private bool turnPresentationPending;
    //[SerializeField] private float enemyThinkSeconds = 3f;

    [Header("Debug")]
    [Tooltip("턴 순서와 흐름 진단 로그를 Console에 출력합니다.")]
    [SerializeField] private bool verboseLog = true;

    [Header("Input")]
    [Tooltip("JC 대상 선택 입력입니다. 자동전투 중에는 수동 선택을 차단합니다.")]
    [SerializeField] private JcInputHandler inputHandler;

    [Header("Runtime lookup")]
    [Tooltip("JC 전투의 스킬과 연출을 실행하는 컴포넌트입니다.")]
    [SerializeField] private JcBattleManager battleManager;

    private readonly List<BattleCharactor> participants = new List<BattleCharactor>();
    private Queue<BattleCharactor> turnQueue = new Queue<BattleCharactor>();

    private readonly Dictionary<string, BattleCharactor> battleById = new Dictionary<string, BattleCharactor>();

    private Coroutine battleLoopRoutine;
    private int roundIndex = 0;

    private bool playerActionResolved;

    // 이번 플레이어 턴의 '행동 실행 권한'을 누군가 가져갔는지.
    // JcInputHandler(수동)와 JcAutoBattleController(자동)가 같은 턴에 동시에 행동하는 것을 막는다.
    // 이 플래그가 없던 시절에는 턴 도중 자동전투를 끄면 같은 액터가 두 번 행동하고 IP도 두 번 빠졌다.
    private bool playerActionClaimed;
    private bool enemyTurnRunning;
    private bool battleEnded;
    private bool battleEndRequested;
    private BattleResult requestedBattleResult = BattleResult.Defeat;
    // 이번 전투의 적 그룹 키(일반 전투만). 그룹별 즉시 승리 조건 판정에 쓴다.
    private string enemyGroupKey = string.Empty;

    private readonly Dictionary<int, FlowLockRecord> flowLocks = new Dictionary<int, FlowLockRecord>();
    private readonly Dictionary<int, PlayerActionConstraintRecord> playerActionConstraints =
        new Dictionary<int, PlayerActionConstraintRecord>();
    private int nextFlowExtensionId = 1;

    // 원본 대응: BattleFlowManager.IsBattleOver. JC는 행동 완료 후 확정된 독립 목표를 사용합니다.
    public Func<BattleResult> ResultPolicy;
    public Func<BattleCharactor, bool, IEnumerator> AfterAction;
    public Func<BattleCharactor, string, IEnumerator> SkippedBossAction;
    private bool IsBattleOver => ResultPolicy != null ? ResultPolicy() != BattleResult.None : !CheckSideAlive(true) || !CheckSideAlive(false);

    // 원본 대응: RemoveUnit/RegisterRuntimeParticipant. 교체만 수행하여 라운드/행동권을 보존합니다.
    public void ReplaceParticipant(BattleCharactor before, BattleCharactor after)
    {
        int index = participants.IndexOf(before);
        if (index < 0) throw new InvalidOperationException("교체 대상이 참가 목록에 없습니다.");
        before.OnDied -= HandleUnitDied;
        participants[index] = after;
        SubscribeUnitDeathEvent(after);
        turnQueue = new Queue<BattleCharactor>(turnQueue.Select(u => u == before ? after : u));
        if (CurrentUnit == before) CurrentUnit = after;
        RebuildRuntimeLookup();
        inputHandler?.BindUnitDeathEvents(participants);
        OnParticipantRegistered?.Invoke(after);
    }
    // 원본 대응: RemoveUnit. 일반 시체 복귀에는 이번 라운드에 새 행동권을 주지 않습니다.
    public void RemoveFromPendingQueue(BattleCharactor unit)
        => turnQueue = new Queue<BattleCharactor>(turnQueue.Where(u => u != unit));

    public BattleCharactor CurrentUnit { get; private set; }
    public IReadOnlyList<BattleCharactor> Participants => participants;
    public JcBattleManager JcBattleManager => battleManager;
    public int RoundIndex => roundIndex;
    public bool IsFlowBlocked => flowLocks.Count > 0;
    public bool IsTurnPresentationPending => turnPresentationPending || (turnBanner != null && turnBanner.BlocksInput);
    public bool IsEndingBattle => ShouldEndBattle();

    /// <summary>
    /// UI가 조회하는 행동 진행 상태입니다. 아군은 수동·자동 행동 점유 후 완료 통지 전,
    /// 적은 AI 판단 대기부터 공격·반격 처리가 반환될 때까지를 포함합니다.
    /// 행동을 취소하거나 턴 진행을 제어하는 용도로 사용하지 않습니다.
    /// </summary>
    public bool IsActionInProgress => !battleEnded &&
        (enemyTurnRunning || (CurrentUnit != null && CurrentUnit.IsPlayer &&
                              playerActionClaimed && !playerActionResolved));

    public event Action<int, BattleCharactor> OnTurnStarted;
    public event Action<TurnResolutionContext> OnTurnResolved;
    public event Action<BattleResult> OnBattleEnded;
    public event Action<BattleCharactor> OnParticipantRegistered;

    // 원본 함수 대응: BattleFlowManager.OnEnable (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void OnEnable()
    {
        JcInputHandler.PlayerSkillActionResolved += OnPlayerSkillActionResolved;
    }

    // 원본 함수 대응: BattleFlowManager.OnDisable (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void OnDisable()
    {
        turnPresentationPending = false;
        turnBanner?.Hide();
        enemyTurnRunning = false;
        JcInputHandler.PlayerSkillActionResolved -= OnPlayerSkillActionResolved;
        UnsubscribeAllUnitDeathEvents();
        ClearFlowExtensions();
        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
            battleLoopRoutine = null;
        }
    }

    // 원본 함수 대응: BattleFlowManager.OnDestroy (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void OnDestroy()
    {
        JcInputHandler.PlayerSkillActionResolved -= OnPlayerSkillActionResolved;
        UnsubscribeAllUnitDeathEvents();
    }

    // 원본 함수 대응: BattleFlowManager.Initialize (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    public void Initialize(List<BattleCharactor> initialParticipants)
    {
        Initialize(initialParticipants, autoStartOnInitialize);
    }

    /// <summary>
    /// 참가자를 초기화하고 필요하면 루프를 시작합니다. startImmediately=false는 외부 시나리오가
    /// 이벤트를 먼저 구독한 뒤 <see cref="StartBattleLoop"/>를 호출해야 하는 경우에 사용합니다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.Initialize (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public void Initialize(List<BattleCharactor> initialParticipants, bool startImmediately)
    {
        UnsubscribeAllUnitDeathEvents();
        ClearFlowExtensions();
        participants.Clear();
        if (initialParticipants != null)
        {
            participants.AddRange(initialParticipants.Where(u => u != null));
        }

        SubscribeAllUnitDeathEvents();
        inputHandler?.BindUnitDeathEvents(participants);

        // RebuildRuntimeLookup: 참가자 목록 확정 + 각 BattleCharactor.Awake(런타임 키 할당) 이후이므로
        // BattleSceneManager가 CollectParticipantsAfterInitialize까지 마친 뒤 Initialize를 호출하는 순서와 일치합니다.
        RebuildRuntimeLookup();
        CurrentUnit = null;
        roundIndex = 0;
        enemyTurnRunning = false;
        battleEnded = false;
        battleEndRequested = false;
        requestedBattleResult = BattleResult.Defeat;
        enemyGroupKey = ResolveEnemyGroupKey();
        RefreshQueue();

        Debug.Log($"[BattleFlow] Initialize 완료. participants={participants.Count}, queue={turnQueue.Count}");

        BeginPlayerTurnSelectionCleanup();

        if (startImmediately)
        {
            StartBattleLoop();
        }
    }

    /// <summary>전투 흐름을 안전 지점에서 대기시키는 소유권 기반 잠금입니다.</summary>
    // 원본 함수 대응: BattleFlowManager.AcquireFlowLock (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public IDisposable AcquireFlowLock(object owner, string reason = null)
    {
        if (battleEnded)
        {
            return EmptyDisposable.Instance;
        }

        int id = nextFlowExtensionId++;
        flowLocks[id] = new FlowLockRecord(owner, reason);
        return new ReleaseHandle(() => flowLocks.Remove(id));
    }

    /// <summary>
    /// 플레이어가 선택/실행하려는 행동을 제한합니다. 모든 등록 제약이 허용해야 행동할 수 있습니다.
    /// target=null 호출은 행동 종류를 고르는 단계의 사전 검사입니다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.AddPlayerActionConstraint (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public IDisposable AddPlayerActionConstraint(
        object owner,
        Func<BattleCharactor, PendingActionType, BattleCharactor, bool> predicate,
        string reason = null)
    {
        if (predicate == null)
        {
            return EmptyDisposable.Instance;
        }

        int id = nextFlowExtensionId++;
        playerActionConstraints[id] = new PlayerActionConstraintRecord(owner, predicate, reason);
        return new ReleaseHandle(() => playerActionConstraints.Remove(id));
    }

    // 원본 함수 대응: BattleFlowManager.IsPlayerActionAllowed (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    public bool IsPlayerActionAllowed(
        BattleCharactor actor,
        PendingActionType actionType,
        BattleCharactor target)
    {
        if (IsTurnPresentationPending) return false;
        foreach (PlayerActionConstraintRecord constraint in playerActionConstraints.Values)
        {
            try
            {
                if (!constraint.Predicate(actor, actionType, target))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        return true;
    }

    // 원본 함수 대응: BattleFlowManager.StartBattleLoop (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    public void StartBattleLoop()
    {
        turnPresentationPending = false;
        turnBanner?.Hide();
        enemyTurnRunning = false;
        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
        }
        battleLoopRoutine = StartCoroutine(BattleLoop());
        Log("[BattleFlow] BattleLoop 시작");
    }

    // 원본 함수 대응: BattleFlowManager.StopBattleLoop (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    public void StopBattleLoop()
    {
        turnPresentationPending = false;
        turnBanner?.Hide();
        enemyTurnRunning = false;
        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
            battleLoopRoutine = null;
            Log("[BattleFlow] BattleLoop 중지");
        }
    }

    /// <summary>
    /// 생존 참가자만으로 턴 큐를 새로 구성합니다. 큐가 비었을 때(새 라운드)에만 호출됩니다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.RefreshQueue (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public void RefreshQueue()
    {
        var ordered = participants
            .Where(u => u != null && !u.IsDead)
            .OrderByDescending(u => u.FinalStats.Speed)
            .ThenByDescending(u => u.IsPlayer)
            .ThenByDescending(u => GetRowTurnPriority(u))
            .ThenBy(u => GetGridYForTurnOrder(u))
            .ThenBy(u => u != null ? u.GetInstanceID() : 0)
            .ToList();

        turnQueue = new Queue<BattleCharactor>(ordered);
        roundIndex++;
        HostageScenarioController.Active?.AdvanceRound(roundIndex);
        Debug.Log($"[BattleFlow] Round {roundIndex} 시작. queue={turnQueue.Count}");

        Log("[TurnOrder] New round order:");
        for (int i = 0; i < ordered.Count; i++)
        {
            BattleCharactor u = ordered[i];
            if (u == null) continue;

            Log(
                $"[TurnOrder] #{i + 1} {u.UnitName} " +
                $"Speed={u.FinalStats.Speed}, " +
                $"IsPlayer={u.IsPlayer}, " +
                $"RowPriority={GetRowTurnPriority(u)}, " +
                $"GridY={GetGridYForTurnOrder(u)}, " +
                $"InstanceID={u.GetInstanceID()}");
        }
    }

    // 원본 함수 대응: BattleFlowManager.GetRowTurnPriority (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private int GetRowTurnPriority(BattleCharactor unit)
    {
        if (unit == null)
        {
            return 0;
        }

        // 요청 규칙: BackRow 우선 -> 더 높은 우선순위 값
        if (unit.IsInBackRow)
        {
            return 2;
        }

        if (unit.IsInFrontRow)
        {
            return 1;
        }

        return 0;
    }

    // 원본 함수 대응: BattleFlowManager.GetGridYForTurnOrder (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private int GetGridYForTurnOrder(BattleCharactor unit)
    {
        if (unit == null)
        {
            return int.MaxValue;
        }

        // 요청 규칙: GridCell의 y가 작은 유닛이 먼저.
        // 셀이 없으면 뒤로 밀기 위해 큰 값.
        GridCellRef cell = unit.OccupiedCell;
        if (cell == null)
        {
            return int.MaxValue;
        }

        return cell.Coords.y;
    }

    /// <summary>
    /// 큐에서 다음 행동 유닛을 꺼냅니다. 사망한 슬롯은 건너뛰고, 큐가 빌 때만 생존·진영을 검사한 뒤 필요 시 새 라운드를 시작합니다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.GetNextUnit (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public BattleCharactor GetNextUnit()
    {
        while (turnQueue != null && turnQueue.Count > 0)
        {
            BattleCharactor unit = turnQueue.Dequeue();
            if (unit != null && !unit.IsDead)
            {
                return unit;
            }
        }

        if (!CheckSideAlive(true) || !CheckSideAlive(false))
        {
            return null;
        }

        RefreshQueue();
        return turnQueue != null && turnQueue.Count > 0 ? turnQueue.Dequeue() : null;
    }

    /// <summary>
    /// 전투에서 유닛을 완전히 제거할 때만 사용(예: 오브젝트 파괴). 일반 사망(OnDied)에서는 호출하지 마세요.
    /// refreshQueue=false면 턴 큐를 재구성하지 않음(라운드 도중 제거 시 순서/라운드 초기화 방지).
    /// 이 경우 큐에 남은 참조는 GetNextUnit이 null/IsDead로 건너뛴다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.RemoveUnit (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public void RemoveUnit(BattleCharactor unit, bool refreshQueue = true)
    {
        if (unit == null) return;

        unit.OnDied -= HandleUnitDied;
        unit.ClearOccupiedCell();
        participants.Remove(unit);
        if (CurrentUnit == unit)
        {
            CurrentUnit = null;
        }

        if (refreshQueue) RefreshQueue();
        Log($"[BattleFlow] 유닛 제거: {GetUnitLabel(unit)} (refreshQueue={refreshQueue})");
    }


    /// <summary>
    /// 플레이어 턴 중 도주 UI 등에서 호출. 적 턴·이미 행동 완료 시 무시됩니다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.RequestFlee (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public void RequestFlee()
    {
        if (IsFlowBlocked || playerActionResolved || CurrentUnit == null || !CurrentUnit.IsPlayer)
        {
            return;
        }

        if (!IsPlayerActionAllowed(CurrentUnit, PendingActionType.Flee, null))
        {
            return;
        }

        battleEndRequested = true;
        requestedBattleResult = BattleResult.Defeat;
        playerActionResolved = true;
    }

    /// <summary>
    /// '적 전멸' 외의 승리 조건(예: 보스 처치)이 충족됐을 때 호출한다. 진행 중인 행동이 끝나면 루프가 승리로 종료한다.
    /// 이미 종료됐거나 다른 종료가 먼저 요청돼 있으면 무시한다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.RequestVictory (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public void RequestVictory(string reason)
    {
        if (battleEnded || battleEndRequested) return;

        battleEndRequested = true;
        requestedBattleResult = BattleResult.Victory;
        Log($"[BattleFlow] 승리 조건 충족: {reason}");
    }

    /// <summary>
    /// 그룹별 '처치하면 남은 적과 관계없이 즉시 승리'하는 유닛인지 판정한다.
    /// 아직 율리아 보스전 하나뿐이라 코드로 분기한다. 사례가 늘면 데이터(SO 또는 EnemyGroupDataTable 열)로 옮길 것.
    /// unitIndex는 BattleCharactor.TemplateIndex(숫자 형식, 예: "40001")다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.IsInstantVictoryUnit (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    private static bool IsInstantVictoryUnit(string groupKey, string unitIndex)
    {
        switch (groupKey)
        {
            case "BE490": // 율리아 보스전: 율리아(FV40001)
                return unitIndex == "40001";
            default:
                return false;
        }
    }

    /// <summary>
    /// 일반 전투의 적 그룹 키. 이벤트 전투는 그룹 키가 아니라 BattleKey로 스폰하므로(EnemySpawner와 같은 우선순위) 빈 값을 돌려준다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.ResolveEnemyGroupKey (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    private static string ResolveEnemyGroupKey()
    {
        CombatContext context = CombatContext.Instance;
        if (context == null || context.HasEventBattle || context.CombatEnemy == null)
        {
            return string.Empty;
        }

        return context.CombatEnemy.EnemyGroupKey;
    }

    // 원본 함수 대응: BattleFlowManager.CheckSideAlive (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private bool CheckSideAlive(bool isPlayer)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            BattleCharactor unit = participants[i];
            if (unit != null && !unit.IsDead && unit.IsPlayer == isPlayer)
            {
                return true;
            }
        }

        return false;
    }

    // 원본 함수 대응: BattleFlowManager.ShouldEndBattle (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private bool ShouldEndBattle()
    {
        return battleEnded || battleEndRequested || IsBattleOver;
    }

    // 원본 함수 대응: BattleFlowManager.CleanupTurn (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void CleanupTurn(BattleCharactor unit)
    {
        CurrentUnit = null;
        EndTurnSelectionCleanup();
    }

    // 원본 함수 대응: BattleFlowManager.HandleBattleCompletion (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void HandleBattleCompletion()
    {
        if (battleEnded) return;

        if (battleEndRequested)
        {
            CompleteBattle(requestedBattleResult);
            return;
        }

        if (TryEvaluateBattleResult(out BattleResult result))
        {
            CompleteBattle(result);
            return;
        }

        CompleteBattle(BattleResult.Defeat);
    }

    // 원본 함수 대응: BattleFlowManager.CompleteBattle (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void CompleteBattle(BattleResult result)
    {
        if (battleEnded)
        {
            return;
        }

        battleEnded = true;
        battleLoopRoutine = null;
        ClearFlowExtensions();
        OnBattleEnded?.Invoke(result);
    }

    // 원본 함수 대응: BattleFlowManager.WaitForFlowGateOrBattleEnd (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private IEnumerator WaitForFlowGateOrBattleEnd()
    {
        yield return new WaitUntil(() => !IsFlowBlocked || ShouldEndBattle());
    }

    // 원본 함수 대응: BattleFlowManager.BattleLoop (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private IEnumerator BattleLoop()
    {
        if (turnBanner != null) yield return PresentTurnIntro(null);
        while (!ShouldEndBattle())
        {
            yield return WaitForFlowGateOrBattleEnd();
            if (ShouldEndBattle()) break;
            BattleCharactor unit = GetNextUnit();
            if (unit == null) break;
            CurrentUnit = unit;
            playerActionClaimed = playerActionResolved = false;
            unit.ClearGuard();
            inputHandler?.ClearSelectionState();
            if (turnBanner != null) yield return PresentTurnIntro(unit);
            unit.ProcessTurnStartStatusEffects();
            bool skipped = unit.IsDead || unit.IsStunned || unit.IsIncapacitated;
            if (skipped)
            {
                if (SkippedBossAction != null)
                    yield return SkippedBossAction(unit, unit.IsDead ? "사망 상태입니다." : unit.IsIncapacitated ? "무력화 상태입니다." : "기절 상태입니다.");
            }
            else
            {
                OnTurnStarted?.Invoke(roundIndex, unit);
                yield return WaitForFlowGateOrBattleEnd();
                if (unit.IsPlayer)
                    yield return new WaitUntil(() => playerActionResolved || battleEndRequested);
                else yield return RunEnemyTurn(unit);
            }
            // 원본 BattleLoop 보완: 시전/반격/후속 효과 반환과 상태 지속시간 갱신 후 최종 페이즈를 한 번 판정합니다.
            if (!unit.IsDead) unit.AdvanceStatusEffectDuration();
            if (AfterAction != null) yield return AfterAction(unit, skipped);
            PublishTurnResolved(CurrentUnit != null ? CurrentUnit : unit, skipped);
            CleanupTurn(unit);
            yield return null;
        }
        HandleBattleCompletion();
    }

    // 원본 함수 대응: BattleFlowManager.PublishTurnResolved (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void PublishTurnResolved(BattleCharactor actor, bool wasSkipped)
    {
        OnTurnResolved?.Invoke(new TurnResolutionContext(actor, wasSkipped, roundIndex));
    }

    // 원본 함수 대응: BattleFlowManager.TryEvaluateBattleResult (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private bool TryEvaluateBattleResult(out BattleResult result)
    {
        result = BattleResult.Defeat;
        if (ResultPolicy != null)
        {
            result = ResultPolicy();
            return result != BattleResult.None;
        }
        if (!CheckSideAlive(false))
        {
            result = BattleResult.Victory;
            return true;
        }

        if (!CheckSideAlive(true))
        {
            result = BattleResult.Defeat;
            return true;
        }

        return false;
    }

    // 원본 함수 대응: BattleFlowManager.TryEndBattleImmediately (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private bool TryEndBattleImmediately()
    {
        if (!IsBattleOver)
        {
            return false;
        }

        if (TryEvaluateBattleResult(out BattleResult result))
        {
            CompleteBattle(result);
        }

        Log("[BattleFlow] 전투 즉시 종료(IsBattleOver 감지). BattleLoop 종료.");
        return true;
    }

    // 원본 함수 대응: BattleFlowManager.RunEnemyTurn (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private IEnumerator RunEnemyTurn(BattleCharactor enemyUnit)
    {
        if (enemyUnit == null)
        {
            yield break;
        }

        enemyTurnRunning = true;
        try
        {
            JcEnemyScript enemyScript = enemyUnit.GetComponent<JcEnemyScript>();
            if (enemyScript != null)
            {
                yield return StartCoroutine(enemyScript.RunAITurn(battleManager, this));
            }
            else
            {
                Debug.LogWarning($"[BattleFlow] EnemyScript가 없어 턴을 스킵합니다: {GetUnitLabel(enemyUnit)}");
                yield return null;
            }
        }
        finally
        {
            enemyTurnRunning = false;
        }
    }

    /// <summary>플레이어 턴 진입 시 이전 타겟 선택이 남지 않도록 정리합니다.</summary>
    // 원본 함수 대응: BattleFlowManager.BeginPlayerTurnSelectionCleanup (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    private void BeginPlayerTurnSelectionCleanup()
    {
        inputHandler?.ClearSelectionState();
    }

    /// <summary>턴 종료 시 InputHandler에 남은 타겟 선택/아웃라인을 정리합니다.</summary>
    // 원본 함수 대응: BattleFlowManager.EndTurnSelectionCleanup (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    private void EndTurnSelectionCleanup()
    {
        inputHandler?.ClearSelectionState();
    }

    /// <summary>적 턴 자동 행동 실행.</summary>
    // 원본 함수 대응: BattleFlowManager.ExecuteAction (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    protected virtual IEnumerator ExecuteAction(BattleCharactor unit, BattleCharactor target)
    {
        Log($"[BattleFlow] ExecuteAction: actor={GetUnitLabel(unit)}, target={GetUnitLabel(target)}");

        if (battleManager == null)
        {
            Debug.LogWarning("[BattleFlow] BattleManager가 할당되지 않아 행동 실행을 건너뜁니다.");
            yield return null;
            yield break;
        }

        if (CurrentUnit == null)
        {
            Debug.Log("Invalid target");
            yield return null;
            yield break;
        }

        // 현재 구현은 적 턴 자동 액션만 수행. 플레이어 액션은 InputHandler에서 즉시 처리된다.
        if (CurrentUnit.IsPlayer)
        {
            yield break;
        }

        // 적 턴 기본 타겟: 생존 플레이어 첫 대상
        BattleCharactor autoTarget = participants.FirstOrDefault(p => p != null && p.IsPlayer && !p.IsDead);
        if (autoTarget == null)
        {
            yield break;
        }

        CurrentUnit.ResolveSelectedSkill();
        SkillData selectedSkill = CurrentUnit.SelectedSkillData
                                  ?? CurrentUnit.availableSkills?.FirstOrDefault(s => s != null);
        if (selectedSkill == null)
        {
            Debug.LogWarning($"[BattleFlow] 실행 가능한 스킬이 없습니다: {GetUnitLabel(CurrentUnit)}");
            yield break;
        }

        bool executed = false;
        yield return StartCoroutine(battleManager.ExecuteGridSkill(CurrentUnit, autoTarget, selectedSkill, success => executed = success));
        if (!executed)
        {
            Debug.LogWarning($"[BattleFlow] 자동 행동 실행 실패: actor={GetUnitLabel(CurrentUnit)}, target={GetUnitLabel(autoTarget)}");
        }
    }

    /// <summary>
    /// 현재 플레이어 유닛의 스킬 데이터를 반환합니다. 플레이어 턴이 아니거나 스킬이 없으면 null.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.GetCurrentUnitSkill (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public SkillData GetCurrentUnitSkill(PendingActionType actionType)
    {
        if (CurrentUnit == null || !CurrentUnit.IsPlayer || CurrentUnit.IsDead)
            return null;

        switch (actionType)
        {
            case PendingActionType.ClassSkill:
                CurrentUnit.ResolveSelectedSkill();
                return CurrentUnit.SelectedSkillData;

            case PendingActionType.WeaponSkill:
                return CurrentUnit.EquippedWeaponData?.ToSkillData();

            default:
                return null;
        }
    }

    // 원본 함수 대응: BattleFlowManager.GetAlivePlayerUnits (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    public List<BattleCharactor> GetAlivePlayerUnits()
    {
        return participants
            .Where(u => u != null && u.IsPlayer && !u.IsDead)
            .ToList();
    }

    // 원본 함수 대응: BattleFlowManager.GetAlivePlayerCount (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    public int GetAlivePlayerCount()
    {
        return participants.Count(u => u != null && u.IsPlayer && !u.IsDead);
    }

    // 원본 함수 대응: BattleFlowManager.GetAliveEnemyCount (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    public int GetAliveEnemyCount()
    {
        return participants.Count(u => u != null && !u.IsPlayer && !u.IsDead);
    }

    /// <summary>
    /// UI용 예측 턴 순서 반환.
    /// 현재 유닛 → 남은 큐 → 이미 행동한 유닛(다음 라운드 예측, RefreshQueue와 동일 정렬)
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.GetPredictedTurnOrder (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public List<BattleCharactor> GetPredictedTurnOrder()
    {
        var result = new List<BattleCharactor>();

        if (CurrentUnit != null && !CurrentUnit.IsDead)
            result.Add(CurrentUnit);

        result.AddRange(turnQueue.Where(u => u != null && !u.IsDead));

        var acted = participants
            .Where(u => u != null && !u.IsDead && !result.Contains(u))
            .OrderByDescending(u => u.FinalStats.Speed)
            .ThenByDescending(u => u.IsPlayer)
            .ThenByDescending(u => GetRowTurnPriority(u))
            .ThenBy(u => GetGridYForTurnOrder(u))
            .ThenBy(u => u.GetInstanceID());

        result.AddRange(acted);
        return result;
    }

    /// <summary>행동 가능 이벤트와 구분된 UI 대기입니다. 기절/사망으로 건너뛸 순번도 여기서 표시합니다.</summary>
    // 원본 함수 대응: BattleFlowManager.PresentTurnIntro (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    private IEnumerator PresentTurnIntro(BattleCharactor unit)
    {
        if (turnBanner == null || !turnBanner.isActiveAndEnabled) yield break;
        turnPresentationPending = true;
        try
        {
            yield return unit == null ? turnBanner.ShowStart(this) : turnBanner.ShowTurn(this, unit.IsPlayer);
            yield return WaitForFlowGateOrBattleEnd();
        }
        finally
        {
            turnPresentationPending = false;
            if (turnBanner != null) turnBanner.Hide();
        }
    }

    /// <summary>이번 턴의 행동 권한을 요청합니다. 안내 중/이미 행동 중/행동 불가라면 false를 반환합니다.</summary>
    // 원본 함수 대응: BattleFlowManager.ReleaseFailedPlayerAction (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public void ReleaseFailedPlayerAction(BattleCharactor actor)
    {
        if (CurrentUnit == actor && !playerActionResolved) playerActionClaimed = false;
    }

    // 원본 함수 대응: BattleFlowManager.TryClaimPlayerAction (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    public bool TryClaimPlayerAction(BattleCharactor actor)
    {
        if (IsTurnPresentationPending) return false;
        if (IsFlowBlocked)
        {
            return false;
        }

        if (actor == null || CurrentUnit == null || actor != CurrentUnit)
        {
            return false;
        }

        if (!CurrentUnit.IsPlayer || CurrentUnit.IsDead || CurrentUnit.IsStunned)
        {
            return false;
        }

        if (playerActionClaimed || playerActionResolved)
        {
            return false;
        }

        playerActionClaimed = true;
        return true;
    }

    // 원본 함수 대응: BattleFlowManager.OnPlayerSkillActionResolved (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void OnPlayerSkillActionResolved(BattleCharactor actor, BattleCharactor target)
    {
        if (CurrentUnit == null || actor == null)
        {
            Debug.LogWarning($"[BattleFlow] PlayerSkillActionResolved 무시: CurrentUnit={CurrentUnit?.UnitName ?? "null"}, actor={actor?.UnitName ?? "null"}");
            return;
        }

        if (actor != CurrentUnit || !CurrentUnit.IsPlayer)
        {
            Debug.LogWarning($"[BattleFlow] PlayerSkillActionResolved 무시: actor={actor.UnitName}, CurrentUnit={CurrentUnit.UnitName}, IsPlayer={CurrentUnit.IsPlayer}");
            return;
        }

        Debug.Log($"[BattleFlow] PlayerSkillActionResolved 수신: actor={actor.UnitName}");
        playerActionResolved = true;
    }

    // 원본 함수 대응: BattleFlowManager.SubscribeAllUnitDeathEvents (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void SubscribeAllUnitDeathEvents()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            SubscribeUnitDeathEvent(participants[i]);
        }
    }

    // 원본 함수 대응: BattleFlowManager.SubscribeUnitDeathEvent (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void SubscribeUnitDeathEvent(BattleCharactor unit)
    {
        if (unit == null)
        {
            return;
        }

        unit.OnDied -= HandleUnitDied;
        unit.OnDied += HandleUnitDied;
    }

    // 원본 함수 대응: BattleFlowManager.UnsubscribeAllUnitDeathEvents (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void UnsubscribeAllUnitDeathEvents()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            var unit = participants[i];
            if (unit == null)
            {
                continue;
            }

            unit.OnDied -= HandleUnitDied;
        }
    }

    // 원본 함수 대응: BattleFlowManager.HandleUnitDied (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void HandleUnitDied(BattleCharactor deadUnit)
    {
        if (deadUnit == null) return;
        // JC 보완: 진행 중인 시전/반격이 끝나기 전에 CurrentUnit을 비우거나 승패를 확정하지 않습니다.
        if (ResultPolicy != null) return;
        if (CurrentUnit == deadUnit && !IsActionInProgress) CurrentUnit = null;
        if (!deadUnit.IsPlayer && IsInstantVictoryUnit(enemyGroupKey, deadUnit.TemplateIndex))
            RequestVictory("보스 처치");
    }

    /// <summary>
    /// 참가자 ID 맵을 만든다.
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.RebuildRuntimeLookup (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    private void RebuildRuntimeLookup()
    {
        battleById.Clear();

        int duplicateUnitIdKeys = 0;
        foreach (var battle in participants)
        {
            if (battle == null) continue;
            string key = battle.UnitId != null ? battle.UnitId.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(key)) continue;
            // 인스턴스별 고유 UnitId 가정. 동일 키가 있으면 최신 참가자로 덮어써 조용히 누락되지 않게 합니다.
            if (battleById.ContainsKey(key))
            {
                duplicateUnitIdKeys++;
                Log(
                    $"[BattleFlow/UnitIdDebug] Duplicate UnitId key before overwrite: '{key}' " +
                    $"existingGo='{battleById[key].gameObject.name}' newGo='{battle.gameObject.name}'");
            }

            battleById[key] = battle;
        }

        if (duplicateUnitIdKeys > 0)
        {
            Log($"[BattleFlow/UnitIdDebug] RebuildRuntimeLookup: {duplicateUnitIdKeys} duplicate key(s) (expected 0 after unique spawn names).");
        }

        Log($"[BattleFlow] Lookup 재구성: id={battleById.Count}");
    }

    /// <summary>
    /// 런타임에 소환된 유닛을 참가자로 등록한다(보스 미니언 등). 초기화 경로(Initialize)와 동일하게
    /// 죽음 이벤트 구독 + 입력 바인딩 + 조회 맵 재구성을 수행한다.
    /// RefreshQueue는 호출하지 않으므로 진행 중인 라운드 큐엔 끼어들지 않고, 다음 라운드부터 자연히 반영된다.
    /// (battleById 갱신은 현재 로직상 필수는 아니나 초기화 경로와 대칭 유지를 위해 함께 재구성.)
    /// </summary>
    // 원본 함수 대응: BattleFlowManager.RegisterRuntimeParticipant (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public bool RegisterRuntimeParticipant(BattleCharactor unit)
    {
        if (unit == null || participants.Contains(unit))
        {
            return false;
        }

        participants.Add(unit);
        SubscribeUnitDeathEvent(unit);
        inputHandler?.BindUnitDeathEvents(new[] { unit });
        RebuildRuntimeLookup();
        Debug.Log($"[BattleFlow] 런타임 참가자 등록: {unit.UnitName} (participants={participants.Count})");
        OnParticipantRegistered?.Invoke(unit);
        return true;
    }

    // 원본 함수 대응: BattleFlowManager.ClearFlowExtensions (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void ClearFlowExtensions()
    {
        flowLocks.Clear();
        playerActionConstraints.Clear();
    }

    private sealed class FlowLockRecord
    {
        public readonly object Owner;
        public readonly string Reason;

        public FlowLockRecord(object owner, string reason)
        {
            Owner = owner;
            Reason = reason ?? string.Empty;
        }
    }

    private sealed class PlayerActionConstraintRecord
    {
        public readonly object Owner;
        public readonly Func<BattleCharactor, PendingActionType, BattleCharactor, bool> Predicate;
        public readonly string Reason;

        public PlayerActionConstraintRecord(
            object owner,
            Func<BattleCharactor, PendingActionType, BattleCharactor, bool> predicate,
            string reason)
        {
            Owner = owner;
            Predicate = predicate;
            Reason = reason ?? string.Empty;
        }
    }

    private sealed class ReleaseHandle : IDisposable
    {
        private Action release;

        public ReleaseHandle(Action releaseAction)
        {
            release = releaseAction;
        }

        // 원본 함수 대응: BattleFlowManager.Dispose (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

        public void Dispose()
        {
            Action action = release;
            release = null;
            action?.Invoke();
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new EmptyDisposable();
        // 원본 함수 대응: BattleFlowManager.Dispose (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
        public void Dispose() { }
    }

    /// <summary>자동전투·적 공격 시 스킬 공격 범위 발판을 표시합니다.</summary>
    public void ShowTargetHighlight(BattleCharactor caster, BattleCharactor target, SkillData skill)
    {
        GridManagerRef.Instance?.ClearPreviewHighlight();
        if (target == null) return;

        if (!JcSkillAreaPreviewHelper.TryGetAreaCells(caster, target, skill, out GridCellRef mainCell, out List<GridCellRef> splashCells))
        {
            GridCellRef fallbackCell = target.OccupiedCell ?? GridManagerRef.Instance?.FindCellByUnit(target);
            if (fallbackCell == null) return;
            GridManagerRef.Instance?.ShowPreviewHighlight(null, fallbackCell, null);
            return;
        }

        GridManagerRef.Instance?.ShowPreviewHighlight(skill, mainCell, splashCells);
    }

    /// <summary>스킬 정보 없이 선택 대상 1칸만 표시합니다.</summary>
    // 원본 함수 대응: BattleFlowManager.ShowTargetHighlight (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public void ShowTargetHighlight(BattleCharactor target)
    {
        ShowTargetHighlight(null, target, null);
    }

    /// <summary>타겟 발판 하이라이트 제거.</summary>
    // 원본 함수 대응: BattleFlowManager.ClearTargetHighlight (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)
    public void ClearTargetHighlight()
    {
        GridManagerRef.Instance?.ClearPreviewHighlight();
    }

    // 원본 함수 대응: BattleFlowManager.GetUnitLabel (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private string GetUnitLabel(BattleCharactor unit)
    {
        if (unit == null) return "null";
        string side = unit.IsPlayer ? "Player" : "Enemy";
        return $"{side}:{unit.UnitId}:{unit.UnitName}";
    }

    // 원본 함수 대응: BattleFlowManager.FormatTurnStartLog (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private string FormatTurnStartLog(BattleCharactor unit)
    {
        if (unit == null)
        {
            return "[턴 시작] 유닛 정보 없음";
        }

        string side = unit.IsPlayer ? "플레이어 진영" : "적 진영";
        string name = string.IsNullOrWhiteSpace(unit.UnitName) ? "Unknown" : unit.UnitName;
        string id = string.IsNullOrWhiteSpace(unit.UnitId) ? "Unknown" : unit.UnitId;
        return $"[턴 시작] {side}: {name} (ID: {id})";
    }

    // 원본 함수 대응: BattleFlowManager.Log (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs)

    private void Log(string message)
    {
        if (verboseLog)
        {
            Debug.Log(message);
        }
    }
}
