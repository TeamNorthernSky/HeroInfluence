using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BattleResult
{
    Victory,
    Defeat
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
    [Tooltip("Outline 등록 시 비활성 BattleCharactor도 FindObjects에 포함할지 여부")]
    [SerializeField] private bool includeInactiveUnitRootsInOutlineLookup = true;
    [SerializeField] private BattleManager battleManager;

    private readonly List<BattleCharactor> participants = new List<BattleCharactor>();
    private Queue<BattleCharactor> turnQueue = new Queue<BattleCharactor>();

    private readonly Dictionary<string, BattleCharactor> battleById = new Dictionary<string, BattleCharactor>();
    private readonly Dictionary<BattleCharactor, Outline> outlineByBattle = new Dictionary<BattleCharactor, Outline>();

    private Coroutine battleLoopRoutine;
    private int roundIndex = 0;

    private bool playerActionResolved;
    private bool IsBattleOver
    {
        get
        {
            List<BattleCharactor> aliveUnits = participants.Where(u => u != null && !u.IsDead).ToList();
            bool anyPlayerAlive = aliveUnits.Exists(u => u.IsPlayer);
            bool anyEnemyAlive = aliveUnits.Exists(u => !u.IsPlayer);
            return !anyEnemyAlive || !anyPlayerAlive;
        }
    }

    public BattleCharactor CurrentUnit { get; private set; }
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

        RebuildRuntimeLookup();
        CurrentUnit = null;
        roundIndex = 0;
        RefreshQueue();

        Log($"[BattleFlow] Initialize 완료. participants={participants.Count}, queue={turnQueue.Count}");

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
            .ToList();

        turnQueue = new Queue<BattleCharactor>(ordered);
        roundIndex++;
        Log($"[BattleFlow] Round {roundIndex} 시작. queue={turnQueue.Count}");
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

        List<BattleCharactor> aliveUnits = participants.Where(u => u != null && !u.IsDead).ToList();
        if (aliveUnits.Count == 0)
        {
            return null;
        }

        bool anyPlayerAlive = aliveUnits.Exists(u => u.IsPlayer);
        bool anyEnemyAlive = aliveUnits.Exists(u => !u.IsPlayer);
        if (!anyPlayerAlive || !anyEnemyAlive)
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


    private IEnumerator BattleLoop()
    {
        while (true)
        {
            BattleCharactor unit = GetNextUnit();
            if (unit == null)
            {
                if (TryEvaluateBattleResult(out BattleResult result))
                {
                    OnBattleEnded?.Invoke(result);
                }
                Log("[BattleFlow] 전투 종료(생존 진영 없음 또는 참가자 전멸). BattleLoop 종료.");
                battleLoopRoutine = null;
                yield break;
            }

            CurrentUnit = unit;
            OnTurnStarted?.Invoke(roundIndex, CurrentUnit);

            // 턴 전환 시 입력 상태(타겟팅/아웃라인)가 남지 않도록 항상 정리
            inputHandler?.ClearSelectionState();
            Log(FormatTurnStartLog(unit));
            SetOutline(unit, true);

            CurrentUnit.ProcessTurnStartStatusEffects();
            if (CurrentUnit == null || CurrentUnit.IsDead)
            {
                SetOutline(unit, false);
                CurrentUnit = null;
                EndTurnSelectionCleanup();
                yield return null;
                continue;
            }

            if (CurrentUnit.IsStunned)
            {
                Debug.Log($"[Stun] {CurrentUnit.UnitName}은(는) 기절 상태여서 턴을 건너뜁니다!");
                CurrentUnit.AdvanceStatusEffectDuration();
                SetOutline(unit, false);
                CurrentUnit = null;
                EndTurnSelectionCleanup();
                yield return null;
                continue;
            }

            if (unit.IsPlayer)
            {
                Log("[BattleFlow] 플레이어 턴: 적 선택 후 숫자키(1/2) 입력 대기");
                BeginPlayerTurnSelectionCleanup();
                playerActionResolved = false;
                yield return new WaitUntil(() =>
                    playerActionResolved
                    || CurrentUnit == null
                    || CurrentUnit.IsDead
                    || IsBattleOver);

                if (TryEndBattleImmediately())
                {
                    yield break;
                }
            }
            else
            {
                yield return RunEnemyTurn(CurrentUnit);
                if (TryEndBattleImmediately())
                {
                    yield break;
                }
            }

            SetOutline(unit, false);
            if (!unit.IsDead)
            {
                unit.AdvanceStatusEffectDuration();
            }

            CurrentUnit = null;
            EndTurnSelectionCleanup();
            yield return null;
        }
    }

    private bool TryEvaluateBattleResult(out BattleResult result)
    {
        result = BattleResult.Defeat;
        List<BattleCharactor> aliveUnits = participants.Where(u => u != null && !u.IsDead).ToList();
        bool anyPlayerAlive = aliveUnits.Exists(u => u.IsPlayer);
        bool anyEnemyAlive = aliveUnits.Exists(u => !u.IsPlayer);

        if (!anyEnemyAlive)
        {
            result = BattleResult.Victory;
            return true;
        }

        if (!anyPlayerAlive)
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
            OnBattleEnded?.Invoke(result);
        }

        Log("[BattleFlow] 전투 즉시 종료(IsBattleOver 감지). BattleLoop 종료.");
        battleLoopRoutine = null;
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
            Debug.LogWarning($"[BattleFlow] EnemyScript가 없어 기본 공격 fallback 실행: {GetUnitLabel(enemyUnit)}");
            BattleCharactor fallbackTarget = participants.FirstOrDefault(p => p != null && p.IsPlayer && !p.IsDead);
            if (fallbackTarget != null && battleManager != null)
            {
                yield return StartCoroutine(battleManager.ExecuteBasicAttack(enemyUnit, fallbackTarget));
            }
            else
            {
                yield return null;
            }
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

        var action = new BattleAction(
            CurrentUnit,
            autoTarget,
            BattleActionType.BasicAttack
        );

        battleManager.ExecuteAction(action);

        // TODO: 턴 종료 흐름 연결
        yield return null;
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

    private void OnPlayerSkillActionResolved(BattleCharactor actor, BattleCharactor target)
    {
        if (CurrentUnit == null || actor == null)
        {
            return;
        }

        if (actor != CurrentUnit || !CurrentUnit.IsPlayer)
        {
            return;
        }
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

        SetOutline(deadUnit, false);
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
    /// 참가자 ID 맵을 만든 뒤, 씬의 BattleCharactor 루트를 수집해 Outline을 캐싱한다.
    /// </summary>
    private void RebuildRuntimeLookup()
    {
        battleById.Clear();
        outlineByBattle.Clear();

        foreach (var battle in participants)
        {
            if (battle == null) continue;
            string key = battle.UnitId != null ? battle.UnitId.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!battleById.ContainsKey(key))
            {
                battleById.Add(key, battle);
            }
        }

        var inactiveMode = includeInactiveUnitRootsInOutlineLookup
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;

        foreach (var battle in FindObjectsByType<BattleCharactor>(inactiveMode, FindObjectsSortMode.None))
        {
            TryRegisterOutline(battle);
        }

        Log($"[BattleFlow] Lookup 재구성: id={battleById.Count}, outline={outlineByBattle.Count}");
    }

    /// <summary>
    /// 유닛 루트(데이터 스크립트) 기준 하향 탐색으로만 Outline을 찾아 등록한다(타 유닛 Outline 오참조 방지).
    /// </summary>
    private void TryRegisterOutline(IUnitIdentifier id)
    {
        if (id == null || string.IsNullOrWhiteSpace(id.UnitID)) return;
        if (!battleById.TryGetValue(id.UnitID, out var battle)) return;

        var comp = id as Component;
        if (comp == null) return;

        if (outlineByBattle.ContainsKey(battle)) return;

        var allOutlines = comp.GetComponentsInChildren<Outline>(true);
        if (allOutlines == null || allOutlines.Length == 0)
        {
            Debug.LogWarning($"[BattleFlow] UnitID={id.UnitID} 유닛 루트 하위에서 Outline을 찾지 못했습니다.");
            return;
        }

        if (allOutlines.Length > 1)
        {
            Debug.LogWarning(
                $"[BattleFlow] UnitID={id.UnitID}에 Outline이 {allOutlines.Length}개 있습니다. 첫 번째만 사용합니다.");
        }

        Outline outline = comp.GetComponent<Outline>() ?? allOutlines[0];
        if (outline == null)
        {
            return;
        }

        outlineByBattle.Add(battle, outline);
        outline.OutlineMode = Outline.Mode.OutlineHidden;
    }

    private void SetOutline(BattleCharactor battle, bool visible)
    {
        if (battle == null) return;
        if (!outlineByBattle.TryGetValue(battle, out var outline) || outline == null) return;
        outline.OutlineMode = visible ? Outline.Mode.OutlineVisible : Outline.Mode.OutlineHidden;
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
