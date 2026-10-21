using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Serialization;

public class TurnManager : MonoBehaviour
{
    public event System.Action<int> DayAdvanced;
    public event System.Action<bool> EnemyTurnStateChanged;

    [SerializeField] private int day = 1;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private EnemyTurnController enemyTurnController;
    [SerializeField] private TMP_Text turnStateText;
    [FormerlySerializedAs("mineRegistry")]
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private GateThreatController gateThreatController;

    [Header("Automatic Turn End")]
    [Tooltip("End the player turn after movement uses all move points, once interactions and combat follow-up finish.")]
    [SerializeField] private bool autoEndTurnWhenMovePointsDepleted;
    public bool AutoTurnEnd
    { 
        get => autoEndTurnWhenMovePointsDepleted; 
        set => autoEndTurnWhenMovePointsDepleted = value; 
    }

    private bool enemyTurnRunning;
    private Coroutine pendingEnemyTurnStartCoroutine;
    private PartyGridMover autoEndTurnParty;
    private PartyIdentity autoEndTurnPartyIdentity;
    private PartyRuntime autoEndTurnPartyRuntime;
    private CombatEncounterManager combatEncounterManager;
    private CombatPromptService combatPromptService;
    private bool playerMovementStarted;
    private int automaticEndTurnReadyFrames;

    public bool IsEnemyTurnRunning => enemyTurnRunning;
    public bool IsPlayerTurn => !enemyTurnRunning;

    private void Awake()
    {
        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        combatEncounterManager = FindFirstObjectByType<CombatEncounterManager>();
        combatPromptService = FindFirstObjectByType<CombatPromptService>();

        if (enemyTurnController == null)
            enemyTurnController = FindFirstObjectByType<EnemyTurnController>();

        if (gateThreatController == null)
            gateThreatController = FindFirstObjectByType<GateThreatController>();

        // [JC 수정 260511] GameManager.CurrentDay에서 day 복원 (DHScene 재로드 시 Day 리셋 방지)
        if (GameManager.Instance != null)
            day = GameManager.Instance.CurrentDay;

        UpdateTurnStateText("Player Turn");
    }

    private void Start()
    {
        // [JC 260610] 로비에서 위임된 턴종료 요청 처리.
        // 로비 턴종료 = "나가기 + 탐사 턴종료" 이므로, 탐사 진입 직후 정상 EndPlayerTurn(적 턴 진행)을 실행한다.
        bool pendingEndTurn = GameManager.Instance != null && GameManager.Instance.ConsumePendingEndTurn();
        if (pendingEndTurn)
        {
            EndPlayerTurn();
            return;
        }

        EnemyTurnSessionRepository sessionRepository = EnemyTurnSessionRepository.Instance;
        if (sessionRepository != null && sessionRepository.ShouldResumeAfterCombat)
        {
            StartCoroutine(ResumeEnemyTurnAfterSceneReady());
            return;
        }

        StartCoroutine(CaptureInitialTurnStartSnapshotAfterSceneReady());
    }

    public void EndPlayerTurn()
    {
        if (DHGameEndState.IsEnding || enemyTurnRunning || pendingEnemyTurnStartCoroutine != null)
            return;

        ClearAutomaticEndTurn();

        // [JC 260615] 턴 전환 시작 → 월드 입력 차단(다음 턴 income 모달 확인 시 해제).
        // 적 턴 진행 중은 물론, 씬 전환 직후·모달 표시 전의 틈에도 탐사 오브젝트 인터랙션(본부 더블클릭 등)을 막는다.
        WorldInputGate.IsTurnResolving = true;

        pendingEnemyTurnStartCoroutine = StartCoroutine(StartEnemyTurnAfterPendingThreats());
    }

    private void LateUpdate()
    {
        if (DHGameStateRestoreService.IsRestoring)
        {
            automaticEndTurnReadyFrames = 0;
            return;
        }

        // Tutorial has its own turn flow, including the end-turn button lesson.
        if (gameObject.scene.name.IndexOf("Tutorial", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        BindAutomaticEndTurnParty();

        MapProgressRepository repository = MapProgressRepository.Instance;
        if (!autoEndTurnWhenMovePointsDepleted)
        {
            ClearAutomaticEndTurn();
            return;
        }

        if (repository == null || !repository.HasPendingAutomaticEndTurn)
        {
            automaticEndTurnReadyFrames = 0;
            return;
        }

        if (repository.AutomaticEndTurnDay != day)
        {
            ClearAutomaticEndTurn();
            return;
        }

        if (!CanExecuteAutomaticEndTurn())
        {
            automaticEndTurnReadyFrames = 0;
            return;
        }

        // Allow deferred chat effects and condition-event checks to open their UI first.
        if (++automaticEndTurnReadyFrames >= 2)
            EndPlayerTurn();
    }

    private void OnDisable()
    {
        UnbindAutomaticEndTurnParty();
        // Keep the repository reservation across combat scene changes.
        automaticEndTurnReadyFrames = 0;
    }

    private void BindAutomaticEndTurnParty()
    {
        PartyGridMover nextParty = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (autoEndTurnParty == nextParty)
            return;

        UnbindAutomaticEndTurnParty();
        autoEndTurnParty = nextParty;
        if (autoEndTurnParty == null)
            return;

        autoEndTurnPartyIdentity = autoEndTurnParty.GetComponent<PartyIdentity>();
        autoEndTurnPartyRuntime = autoEndTurnParty.GetComponent<PartyRuntime>();
        autoEndTurnParty.MovementStateChanged += HandlePlayerMovementStateChanged;
        autoEndTurnParty.MoveCompleted += HandlePlayerMoveCompleted;
    }

    private void UnbindAutomaticEndTurnParty()
    {
        if (autoEndTurnParty != null)
        {
            autoEndTurnParty.MovementStateChanged -= HandlePlayerMovementStateChanged;
            autoEndTurnParty.MoveCompleted -= HandlePlayerMoveCompleted;
        }

        autoEndTurnParty = null;
        autoEndTurnPartyIdentity = null;
        autoEndTurnPartyRuntime = null;
        playerMovementStarted = false;
    }

    private void HandlePlayerMovementStateChanged(bool moving)
    {
        if (moving)
        {
            EnemyTurnSessionRepository session = EnemyTurnSessionRepository.Instance;
            playerMovementStarted = autoEndTurnWhenMovePointsDepleted && !enemyTurnRunning &&
                pendingEnemyTurnStartCoroutine == null && !WorldInputGate.IsTurnResolving &&
                (session == null || !session.ShouldResumeAfterCombat);
        }
    }

    private void HandlePlayerMoveCompleted()
    {
        bool completedPlayerMovement = playerMovementStarted;
        playerMovementStarted = false;
        if (!completedPlayerMovement || !autoEndTurnWhenMovePointsDepleted ||
            autoEndTurnParty == null || autoEndTurnParty.RemainingMovePoints > 0 ||
            autoEndTurnPartyIdentity == null || enemyTurnRunning || pendingEnemyTurnStartCoroutine != null)
            return;

        MapProgressRepository.Instance?.ReserveAutomaticEndTurn(day, autoEndTurnPartyIdentity.PartyId);
        automaticEndTurnReadyFrames = 0;
    }

    private bool CanExecuteAutomaticEndTurn()
    {
        if (DHGameEndState.IsEnding || enemyTurnRunning || pendingEnemyTurnStartCoroutine != null ||
            WorldInputGate.IsTurnResolving ||
            gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene() ||
            autoEndTurnParty == null || autoEndTurnPartyIdentity == null || autoEndTurnParty.IsMoving ||
            autoEndTurnPartyIdentity.PartyId != MapProgressRepository.Instance.AutomaticEndTurnPartyId)
            return false;

        EnemyTurnSessionRepository session = EnemyTurnSessionRepository.Instance;
        if (session != null && session.ShouldResumeAfterCombat)
            return false;

        CombatContext context = CombatContext.Instance;
        if (context != null && (context.Result != CombatResult.None || context.HasEventBattle ||
            context.CombatParty != null || context.CombatEnemy != null))
            return false;

        if ((combatEncounterManager != null && combatEncounterManager.IsCombatActive) ||
            (combatPromptService != null && combatPromptService.IsOpen) ||
            (autoEndTurnPartyRuntime != null && autoEndTurnPartyRuntime.IsInputLocked) ||
            (ChatManager.Instance != null && ChatManager.Instance.IsRunning) ||
            ChatModalController.IsOpen || ModalManager.HasAny ||
            (DHWorldEventRuntimeManager.Instance != null && DHWorldEventRuntimeManager.Instance.IsRunning) ||
            (DHWorldEventConditionRuntimeManager.Instance != null && DHWorldEventConditionRuntimeManager.Instance.IsCheckPending) ||
            (SceneFadeController.Instance != null && SceneFadeController.Instance.IsTransitioning))
            return false;

        return true;
    }

    private void ClearAutomaticEndTurn()
    {
        MapProgressRepository.Instance?.ClearAutomaticEndTurn();
        playerMovementStarted = false;
        automaticEndTurnReadyFrames = 0;
    }

    private IEnumerator StartEnemyTurnAfterPendingThreats()
    {
        if (gateThreatController == null)
            gateThreatController = FindFirstObjectByType<GateThreatController>();

        if (gateThreatController != null)
            yield return gateThreatController.ResolvePendingThreatsBeforeEnemyTurn();

        pendingEnemyTurnStartCoroutine = null;
        StartEnemyTurn();
    }

    private void StartEnemyTurn()
    {
        if (DHGameEndState.IsEnding)
            return;

        if (enemyTurnRunning)
            return;

        enemyTurnRunning = true;
        EnemyTurnStateChanged?.Invoke(true);
        UpdateTurnStateText("Enemy Turn");

        if (enemyTurnController == null)
        {
            enemyTurnRunning = false;
            EndEnemyTurn();
            return;
        }

        StartCoroutine(RunEnemyTurn());
    }

    private void EndEnemyTurn()
    {
        if (DHGameEndState.IsEnding)
        {
            enemyTurnRunning = false;
            EnemyTurnStateChanged?.Invoke(false);
            WorldInputGate.IsTurnResolving = false; // [JC 260615] 게임 종료 시 게이트 해제(누수 방지)
            return;
        }

        AdvanceDay();
        ProduceClaimedOutposts();
        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        if (DHGameEndState.IsEnding)
            return;

        ClearAutomaticEndTurn();
        UpdateTurnStateText("Player Turn");

        if (partyRegistry != null)
        {
            PartyGridMover partyMover = partyRegistry.PlayerParty;
            if (partyMover != null)
                partyMover.ResetMovePointsToMax();
        }

        // [KJ 260706] 플레이어 턴 시작마다 자동 저장 — AdvanceDay·거점 생산·이동력 리셋 반영 후 시점.
        DHTurnStartSnapshotStore.CaptureTurnStartSnapshot();
    }

    private void AdvanceDay()
    {
        day++;
        // [JC 수정 260511] GameManager에 동기화
        if (GameManager.Instance != null)
            GameManager.Instance.CurrentDay = day;
        DayAdvanced?.Invoke(day);
    }

    private void ProduceClaimedOutposts()
    {
        if (outpostRegistry == null)
            return;

        IReadOnlyList<Outpost> outposts = outpostRegistry.Outposts;

        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost outpost = outposts[i];
            if (outpost == null)
                continue;

            outpost.ProduceForTurn();
        }
    }

    public int GetDay()
    {
        return day;
    }

    private IEnumerator RunEnemyTurn()
    {
        yield return enemyTurnController.ExecuteEnemyTurn();

        if (DHGameEndState.IsEnding)
        {
            enemyTurnRunning = false;
            EnemyTurnStateChanged?.Invoke(false);
            WorldInputGate.IsTurnResolving = false; // [JC 260615] 게임 종료 시 게이트 해제(누수 방지)
            yield break;
        }

        EnemyTurnSessionRepository sessionRepository = EnemyTurnSessionRepository.Instance;
        if (sessionRepository != null && sessionRepository.ShouldResumeAfterCombat)
        {
            // Combat interrupted the enemy turn. Keep exploration UI in enemy-turn state while the battle scene loads;
            // sending EnemyTurnStateChanged(false) here makes move-range UI refresh as if a player turn had started.
            enemyTurnRunning = false;
            yield break;
        }

        enemyTurnRunning = false;
        EnemyTurnStateChanged?.Invoke(false);
        EndEnemyTurn();
    }

    private IEnumerator ResumeEnemyTurnAfterSceneReady()
    {
        yield return null;

        while (CombatContext.Instance != null && CombatContext.Instance.Result != CombatResult.None)
            yield return null;

        // Chat logic can finish before reward confirmation and the modal's exit animation.
        while ((ChatManager.Instance != null && ChatManager.Instance.IsRunning) || ChatModalController.IsOpen)
            yield return null;

        if (DHGameEndState.IsEnding || enemyTurnRunning)
            yield break;

        if (enemyTurnController == null)
            enemyTurnController = FindFirstObjectByType<EnemyTurnController>();

        if (enemyTurnController == null)
            yield break;

        EnemyTurnSessionRepository sessionRepository = EnemyTurnSessionRepository.Instance;
        if (sessionRepository == null || !sessionRepository.ShouldResumeAfterCombat)
            yield break;

        enemyTurnRunning = true;
        EnemyTurnStateChanged?.Invoke(true);
        UpdateTurnStateText("Enemy Turn");
        StartCoroutine(RunEnemyTurn());
    }

    private IEnumerator CaptureInitialTurnStartSnapshotAfterSceneReady()
    {
        GameManager gameManager = GameManager.Instance;
        int entryVersion = gameManager != null ? gameManager.ExplorationEntryVersion : 0;
        yield return null;
        yield return null;

        if (gameManager != null && gameManager.IsExplorationScene(gameObject.scene))
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (true)
            {
                if (gameManager == null || gameManager.ExplorationEntryVersion != entryVersion ||
                    DHGameEndState.IsEnding || enemyTurnRunning)
                    yield break;
                if (gameManager.TryPrepareInitialExplorationState(entryVersion))
                    break;
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning("[TurnManager] 본게임 초기화가 완료되지 않아 최초 스냅샷·자동 저장을 보류합니다. 씬 준비, 복원 상태 및 협회 홍보 카탈로그를 확인하세요.", this);
                    yield break;
                }
                yield return null;
            }
        }

        if (DHGameEndState.IsEnding || enemyTurnRunning)
            yield break;

        if (gameManager != null && gameManager.ExplorationEntryVersion != entryVersion)
            yield break;
        if (DHTurnStartSnapshotStore.CaptureTurnStartSnapshot() && gameManager != null)
            gameManager.NotifyInitialTurnSnapshotCaptured(gameObject.scene, entryVersion);
    }

    private void UpdateTurnStateText(string nextText)
    {
        if (turnStateText == null)
            return;

        turnStateText.text = nextText;
    }

    private void OnValidate()
    {
        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (gateThreatController == null)
            gateThreatController = FindFirstObjectByType<GateThreatController>();
    }
}
