using System;
using System.Collections;
using System.Collections.Generic;
using GridCellRef = ASB.Work.BattleGrid.GridCell;
using GridManagerRef = ASB.Work.BattleGrid.BattleGridManager;
using System.Linq;
using ASB.Work.Battle.SkillExecution;
using UnityEngine;

// [JC 260513] DH의 CombatResult와 명세 일치화. ASB BattleFlowManager는 현재 Victory/Defeat만 발화.
// None/Escape/Cancelled는 미래 확장 슬롯 (ASB 발화 흐름 추가 또는 외부 시스템이 직접 set 가능).
//
// 향후 계획(미확정): Battle/Combat 명명 분리 전반을 Combat으로 통일 예정.
//   - 그 시점에 본 enum과 DH CombatResult 통합 + ASB OnBattleEnded<CombatResult>로 시그니처 변경 + 매핑 함수 폐기.
public enum BattleResult
{
    None,
    Victory,
    Defeat,
    Escape,
    Cancelled,
}

/// <summary>
/// 전투 전체 흐름 제어기.
/// - 참가자 관리 + 속도 기반 턴 정렬
/// - 코루틴 기반 무한 전투 루프
/// - 플레이어 행동 완료(PlayerSkillActionResolved) 대기
/// </summary>
[DisallowMultipleComponent]
public class BattleFlowManager : MonoBehaviour
{
    [Header("Turn/Loop")]
    [SerializeField] private bool autoStartOnInitialize = true;
    //[SerializeField] private float enemyThinkSeconds = 3f;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    [Header("Input")]
    [SerializeField] private InputHandler inputHandler;

    [Header("Runtime lookup")]
    [SerializeField] private BattleManager battleManager;

    private readonly List<BattleCharactor> participants = new List<BattleCharactor>();
    private Queue<BattleCharactor> turnQueue = new Queue<BattleCharactor>();

    private readonly Dictionary<string, BattleCharactor> battleById = new Dictionary<string, BattleCharactor>();

    private Coroutine battleLoopRoutine;
    private int roundIndex = 0;

    private bool playerActionResolved;

    // 이번 플레이어 턴의 '행동 실행 권한'을 누군가 가져갔는지.
    // InputHandler(수동)와 AutoBattleController(자동)가 같은 턴에 동시에 행동하는 것을 막는다.
    // 이 플래그가 없던 시절에는 턴 도중 자동전투를 끄면 같은 액터가 두 번 행동하고 IP도 두 번 빠졌다.
    private bool playerActionClaimed;
    private bool battleEnded;
    private bool battleEndRequested;
    private BattleResult requestedBattleResult = BattleResult.Defeat;

    private bool IsBattleOver => !CheckSideAlive(true) || !CheckSideAlive(false);

    public BattleCharactor CurrentUnit { get; private set; }
    public IReadOnlyList<BattleCharactor> Participants => participants;
    public BattleManager BattleManager => battleManager;
    public event Action<int, BattleCharactor> OnTurnStarted;
    public event Action<BattleResult> OnBattleEnded;

    private void OnEnable()
    {
        InputHandler.PlayerSkillActionResolved += OnPlayerSkillActionResolved;
    }

    private void OnDisable()
    {
        InputHandler.PlayerSkillActionResolved -= OnPlayerSkillActionResolved;
        UnsubscribeAllUnitDeathEvents();
        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
            battleLoopRoutine = null;
        }
    }

    private void OnDestroy()
    {
        InputHandler.PlayerSkillActionResolved -= OnPlayerSkillActionResolved;
        UnsubscribeAllUnitDeathEvents();
    }

    public void Initialize(List<BattleCharactor> initialParticipants)
    {
        UnsubscribeAllUnitDeathEvents();
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
        battleEnded = false;
        battleEndRequested = false;
        requestedBattleResult = BattleResult.Defeat;
        RefreshQueue();

        Debug.Log($"[BattleFlow] Initialize 완료. participants={participants.Count}, queue={turnQueue.Count}");

        BeginPlayerTurnSelectionCleanup();

        if (autoStartOnInitialize)
        {
            StartBattleLoop();
        }
    }

    public void StartBattleLoop()
    {
        if (battleLoopRoutine != null)
        {
            StopCoroutine(battleLoopRoutine);
        }
        battleLoopRoutine = StartCoroutine(BattleLoop());
        Log("[BattleFlow] BattleLoop 시작");
    }

    public void StopBattleLoop()
    {
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
    /// </summary>
    public void RemoveUnit(BattleCharactor unit)
    {
        if (unit == null) return;

        unit.OnDied -= HandleUnitDied;
        unit.ClearOccupiedCell();
        participants.Remove(unit);
        if (CurrentUnit == unit)
        {
            CurrentUnit = null;
        }

        RefreshQueue();
        Log($"[BattleFlow] 유닛 제거: {GetUnitLabel(unit)}");
    }


    /// <summary>
    /// 플레이어 턴 중 도주 UI 등에서 호출. 적 턴·이미 행동 완료 시 무시됩니다.
    /// </summary>
    public void RequestFlee()
    {
        if (playerActionResolved || CurrentUnit == null || !CurrentUnit.IsPlayer)
        {
            return;
        }

        battleEndRequested = true;
        requestedBattleResult = BattleResult.Defeat;
        playerActionResolved = true;
    }

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

    private bool ShouldEndBattle()
    {
        return battleEnded || battleEndRequested || IsBattleOver;
    }

    private void CleanupTurn(BattleCharactor unit)
    {
        CurrentUnit = null;
        EndTurnSelectionCleanup();
    }

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

    private void CompleteBattle(BattleResult result)
    {
        if (battleEnded)
        {
            return;
        }

        battleEnded = true;
        battleLoopRoutine = null;
        OnBattleEnded?.Invoke(result);
    }

    private IEnumerator BattleLoop()
    {
        while (!ShouldEndBattle())
        {
            BattleCharactor unit = GetNextUnit();
            if (unit == null)
            {
                Log("[BattleFlow] GetNextUnit() == null. BattleLoop 종료.");
                break;
            }

            CurrentUnit = unit;
            CurrentUnit.ClearGuard(); // 대신 맞기: 가디언의 다음 턴 시작 시 미소비 보호 링크 만료(기절/스킵 포함)

            // 이전 턴 입력 상태를 먼저 정리한다 (UI의 BeginPendingAction이 덮어쓰이지 않도록).
            inputHandler?.ClearSelectionState();
            Log(FormatTurnStartLog(unit));

            CurrentUnit.ProcessTurnStartStatusEffects();
            if (CurrentUnit == null || CurrentUnit.IsDead)
            {
                CleanupTurn(unit);
                yield return null;
                continue;
            }

            if (CurrentUnit.IsStunned)
            {
                Debug.Log($"[Stun] {CurrentUnit.UnitName}은(는) 기절 상태여서 턴을 건너뜁니다!");
                CurrentUnit.AdvanceStatusEffectDuration();
                CleanupTurn(unit);
                yield return null;
                continue;
            }

            // 턴 점유권 초기화 후 이벤트 발행.
            // OnTurnStarted는 '행동 가능이 확정된 뒤'에만 나간다 — 도트 사망·기절로 스킵되는 턴에는 발행되지 않는다.
            // (이전에는 상태이상 처리 전에 발행돼, 기절한 유닛으로 자동전투가 행동하는 문제가 있었다.)
            playerActionResolved = false;
            playerActionClaimed = false;
            OnTurnStarted?.Invoke(roundIndex, CurrentUnit);

            if (unit.IsPlayer)
            {
                Log("[BattleFlow] 플레이어 턴: 적 선택 후 숫자키(1/2) 입력 대기");
                yield return new WaitUntil(() =>
                    playerActionResolved
                    || CurrentUnit == null
                    || CurrentUnit.IsDead
                    || ShouldEndBattle());

                Debug.Log($"[BattleFlow] 플레이어 턴 종료: resolved={playerActionResolved}, currentUnit={CurrentUnit?.UnitName ?? "null"}, isDead={CurrentUnit?.IsDead}, battleOver={IsBattleOver}");

                if (ShouldEndBattle())
                {
                    CleanupTurn(unit);
                    break;
                }
            }
            else
            {
                yield return RunEnemyTurn(CurrentUnit);

                if (ShouldEndBattle())
                {
                    CleanupTurn(unit);
                    break;
                }
            }

            CleanupTurn(unit);
            if (!unit.IsDead)
            {
                unit.AdvanceStatusEffectDuration();
            }

            yield return null;
        }

        HandleBattleCompletion();
    }

    private bool TryEvaluateBattleResult(out BattleResult result)
    {
        result = BattleResult.Defeat;

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

    private IEnumerator RunEnemyTurn(BattleCharactor enemyUnit)
    {
        if (enemyUnit == null)
        {
            yield break;
        }

        EnemyScript enemyScript = enemyUnit.GetComponent<EnemyScript>();
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

    /// <summary>플레이어 턴 진입 시 이전 타겟 선택이 남지 않도록 정리합니다.</summary>
    private void BeginPlayerTurnSelectionCleanup()
    {
        inputHandler?.ClearSelectionState();
    }

    /// <summary>턴 종료 시 InputHandler에 남은 타겟 선택/아웃라인을 정리합니다.</summary>
    private void EndTurnSelectionCleanup()
    {
        inputHandler?.ClearSelectionState();
    }

    /// <summary>적 턴 자동 행동 실행.</summary>
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

    public List<BattleCharactor> GetAlivePlayerUnits()
    {
        return participants
            .Where(u => u != null && u.IsPlayer && !u.IsDead)
            .ToList();
    }

    public int GetAlivePlayerCount()
    {
        return participants.Count(u => u != null && u.IsPlayer && !u.IsDead);
    }

    public int GetAliveEnemyCount()
    {
        return participants.Count(u => u != null && !u.IsPlayer && !u.IsDead);
    }

    /// <summary>
    /// UI용 예측 턴 순서 반환.
    /// 현재 유닛 → 남은 큐 → 이미 행동한 유닛(다음 라운드 예측, RefreshQueue와 동일 정렬)
    /// </summary>
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

    /// <summary>
    /// 이번 플레이어 턴의 행동 실행 권한을 요청한다. 성공한 쪽만 실제로 행동을 실행할 수 있다.
    ///
    /// 턴 해결의 유일한 소유자는 BattleFlowManager다. InputHandler(수동 입력·스킵)와
    /// AutoBattleController(자동전투)는 이 메서드로 '요청'만 하고, 동시에 두 곳이 행동하지 못한다.
    /// 턴이 바뀌면 BattleLoop가 점유권을 초기화한다.
    /// </summary>
    /// <returns>권한을 획득했으면 true. 이미 다른 주체가 가져갔거나 행동 불가 상태면 false.</returns>
    public bool TryClaimPlayerAction(BattleCharactor actor)
    {
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

    private void SubscribeAllUnitDeathEvents()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            SubscribeUnitDeathEvent(participants[i]);
        }
    }

    private void SubscribeUnitDeathEvent(BattleCharactor unit)
    {
        if (unit == null)
        {
            return;
        }

        unit.OnDied -= HandleUnitDied;
        unit.OnDied += HandleUnitDied;
    }

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

    private void HandleUnitDied(BattleCharactor deadUnit)
    {
        if (deadUnit == null)
        {
            return;
        }

        if (CurrentUnit == deadUnit)
        {
            if (deadUnit.IsPlayer)
            {
                playerActionResolved = true;
            }

            CurrentUnit = null;
        }
    }

    /// <summary>
    /// 참가자 ID 맵을 만든다.
    /// </summary>
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
        return true;
    }

    /// <summary>자동전투·적 공격 시 스킬 공격 범위 발판을 표시합니다.</summary>
    public void ShowTargetHighlight(BattleCharactor caster, BattleCharactor target, SkillData skill)
    {
        GridManagerRef.Instance?.ClearPreviewHighlight();
        if (target == null) return;

        if (!SkillAreaPreviewHelper.TryGetAreaCells(caster, target, skill, out GridCellRef mainCell, out List<GridCellRef> splashCells))
        {
            GridCellRef fallbackCell = target.OccupiedCell ?? GridManagerRef.Instance?.FindCellByUnit(target);
            if (fallbackCell == null) return;
            GridManagerRef.Instance?.ShowPreviewHighlight(null, fallbackCell, null);
            return;
        }

        GridManagerRef.Instance?.ShowPreviewHighlight(skill, mainCell, splashCells);
    }

    /// <summary>스킬 정보 없이 선택 대상 1칸만 표시합니다.</summary>
    public void ShowTargetHighlight(BattleCharactor target)
    {
        ShowTargetHighlight(null, target, null);
    }

    /// <summary>타겟 발판 하이라이트 제거.</summary>
    public void ClearTargetHighlight()
    {
        GridManagerRef.Instance?.ClearPreviewHighlight();
    }

    private string GetUnitLabel(BattleCharactor unit)
    {
        if (unit == null) return "null";
        string side = unit.IsPlayer ? "Player" : "Enemy";
        return $"{side}:{unit.UnitId}:{unit.UnitName}";
    }

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

    private void Log(string message)
    {
        if (verboseLog)
        {
            Debug.Log(message);
        }
    }
}
