using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public EconomyManager Economy { get; private set; }
    // [JC 폐기 260512] SceneLoader 폐기. UnityEngine.SceneManagement.SceneManager 래퍼 GameSceneManager(정적)로 대체
    public HQStateManager HQ { get; private set; }
    public PublicityManager Publicity { get; private set; }
    public TrainingManager Training { get; private set; }
    public LabManager Lab { get; private set; }
    public WorkshopManager Workshop { get; private set; }
    public InfirmaryManager Infirmary { get; private set; }
    // [JC 260629] 영속 UI 모달은 CommonUIManager로 분리. 파사드 유지로 기존 호출부(HeroProfileButton/ExplorationHeroBox) 무변경.
    public HeroInfoModal HeroInfoModal => CommonUIManager.Instance != null ? CommonUIManager.Instance.HeroInfoModal : null;

    // [JC 260703] 씬 로케이터는 Grid만 유지(Game.Grid 소비처用). 타 씬 매니저·영속 레포는 각자 .Instance 직접 접근.
    public GridManager Grid { get; private set; }

    // [JC 추가 260511] 영속 상태 - 매니저 통합 (씬 종속 매니저들의 데이터 영속화)
    [Header("Persistent State")]
    [SerializeField] private int currentDay = 1;
    public int CurrentDay
    {
        get => currentDay;
        set
        {
            int newDay = Mathf.Max(1, value);
            bool advanced = newDay > currentDay;
            currentDay = newDay;
            if (advanced)
            {
                // [KJ 260811] HQ.OnTurnAdvanced() 제거 — 턴당 1회 건설/업그레이드 제한 폐지.
                //  아래 턴 수입 가산은 무관한 로직이므로 그대로 유지한다.
                int income = 0;
                if (HQ != null && Economy != null)
                {
                    income = HQ.GetTurnIncome(HQDepartment.Headquarters);
                    if (income > 0) Economy.Add(ResourceType.Money, income);
                }
                if (Publicity != null) Publicity.OnTurnAdvanced(currentDay);
                if (Infirmary != null) Infirmary.OnTurnAdvanced(); // [KJ 260701] 턴당 회복 제한 리셋
                // [JC 260629] 턴 전환 시퀀스 종료 → 월드 입력 차단 해제. (구 TurnIncomeModal.Show 대체 — 인컴 모달 제거, 자금 가산은 위에서 처리됨)
                WorldInputGate.IsTurnResolving = false;
            }
        }
    }

    // [JC 260610] 로비 턴종료 → "나가기 + 탐사 턴종료" 위임 플래그.
    // 로비에서 CurrentDay를 직접 올리면 적 턴(EndPlayerTurn)을 건너뛰는 버그가 있어,
    // 탐사 진입 후 TurnManager.Start가 정상 EndPlayerTurn 하도록 위임한다.
    private bool pendingEndTurnOnExploration;
    public void RequestEndTurnViaExploration() => pendingEndTurnOnExploration = true;
    public bool ConsumePendingEndTurn()
    {
        if (!pendingEndTurnOnExploration) return false;
        pendingEndTurnOnExploration = false;
        return true;
    }

    /// <summary>
    /// [JC 260617] 새 게임 전체 초기화 — 첫 실행과 동일 상태로.
    /// 호출 시점: 새 게임 시작(GameLoadGate) + 타이틀 복귀(시스템메뉴/엔딩). 두 경로 모두에서 안전하게 멱등 호출.
    /// JC 영속(턴/자원/본부/홍보/훈련/연구소/공방) + DH 영속(유닛/적/파티/무기/맵진행/방문/전투컨텍스트)을 모두 리셋.
    /// </summary>
    public void ResetForNewGame()
    {
        explorationEntryVersion++;
        explorationEntryPending = false;
        explorationEntryFailed = false;
        initialAutoSaveRequired = true;
        initialAutoSaveDone = false;
        currentDay = 1;
        pendingEndTurnOnExploration = false;

        if (Economy != null) Economy.ResetAll();
        if (HQ != null) HQ.Reset();
        if (Publicity != null) Publicity.Reset();
        if (Training != null) Training.Reset();
        if (Lab != null) Lab.Reset();
        if (Workshop != null) Workshop.Reset();

        // Static battle settings must not carry over into a new game session.
        BattleRuntimeSettings.Reset();

        DHGameProgressResetService.ResetDHProgress(); // DH 레포 + HQVisit + CombatContext + MapProgress + DHGameEndState

        UnityEngine.Debug.Log("[GameManager] ResetForNewGame — 전체 영속 상태를 첫 실행값으로 초기화");
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeManagers();

        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshSceneManagers();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void InitializeManagers()
    {
        Economy = GetComponentInChildren<EconomyManager>();
        Economy.Initialize();

        HQ = GetComponentInChildren<HQStateManager>(true);
        if (HQ != null) HQ.Initialize();

        Publicity = GetComponentInChildren<PublicityManager>(true);
        if (Publicity != null)
        {
            Publicity.Initialize();
            Publicity.SubscribeHQ(HQ);
        }

        Training = GetComponentInChildren<TrainingManager>(true);
        if (Training != null) Training.Initialize();

        Lab = GetComponentInChildren<LabManager>(true);
        if (Lab != null) Lab.Initialize();

        Workshop = GetComponentInChildren<WorkshopManager>(true);
        if (Workshop != null) Workshop.Initialize();

        // [KJ 260701] 의무실 — HQ 수집 이후여야 SubscribeHQ 정상. Initialize() 없음(무상태).
        Infirmary = GetComponentInChildren<InfirmaryManager>(true);
        if (Infirmary != null) Infirmary.SubscribeHQ(HQ);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneManagers();
        // [JC 260619] 유닛 시딩(PartyUnitBootstrap.Start 등) 완료 후 기본 클래스 스킬 자동장착(멱등).
        // 무기는 유닛 생성 시 자동장착되지만 스킬은 미장착이라, 시작 시 보장한다.
        if (Lab != null) StartCoroutine(EnsureDefaultSkillsNextFrame());

        // 최초 저장은 sceneLoaded의 프레임 지연 대신 TurnManager의 최초 캡처 성공 통지에서 실행한다.
    }

    // [KJ 260707] 세션당 1회. 타이틀 복귀(ResetForNewGame) 시 리셋되어 다음 새 게임에서도 첫 스냅샷 보장.
    private bool initialAutoSaveDone;

    private bool initialAutoSaveRequired = true;
    private bool explorationEntryPending;
    private bool explorationEntryFailed;
    private int explorationEntryVersion;
    public bool IsExplorationEntryPending => explorationEntryPending;
    public int ExplorationEntryVersion => explorationEntryVersion;

    public bool IsExplorationScene(Scene scene)
    {
        string expected = GameSceneManager.Instance != null ? GameSceneManager.Instance.ExplorationScene : "DHScene";
        return scene.IsValid() && scene.isLoaded && scene.name == expected;
    }

    /// <summary>씬 시딩/복원/방문 상태 반영이 끝나기 전 최초 캡처와 저장을 보류한다.</summary>
    public int BeginExplorationEntry(bool isContinue)
    {
        explorationEntryVersion++;
        explorationEntryPending = true;
        explorationEntryFailed = false;
        initialAutoSaveRequired = !isContinue;
        initialAutoSaveDone = false;
        return explorationEntryVersion;
    }

    public void CompleteExplorationEntry(int version)
    {
        if (version == explorationEntryVersion) explorationEntryPending = false;
    }

    public void CancelExplorationEntry(int version)
    {
        if (version != explorationEntryVersion) return;
        explorationEntryPending = false;
        explorationEntryFailed = true;
    }

    /// <summary>현재 진입의 준비 완료 후, 첫 스냅샷에 포함할 홍보 초기 지급을 확정한다.</summary>
    public bool TryPrepareInitialExplorationState(int version)
    {
        if (version != explorationEntryVersion || explorationEntryPending || explorationEntryFailed ||
            DHGameStateRestoreService.IsRestoring || DHTurnStartSnapshotStore.IsCaptureSuppressed)
            return false;
        return Publicity != null && Publicity.TryCompleteInitialGrant();
    }

    public void NotifyInitialTurnSnapshotCaptured(Scene scene, int version)
    {
        if (!IsExplorationScene(scene) || !TryPrepareInitialExplorationState(version)) return;
        var snapshot = DHTurnStartSnapshotStore.Instance;
        if (snapshot == null || !snapshot.HasSnapshot || snapshot.CapturedDay != CurrentDay) return;
        TryCommitInitialAutoSave(SaveSlotRepository.CurrentSlot, GameSaveService.SaveToSlot);
    }

    // 저장 I/O의 성공 여부만 완료 상태에 반영한다. 실패는 완료로 기록하지 않으며 매 프레임 재시도하지 않는다.
    private bool TryCommitInitialAutoSave(int slot, System.Func<int, bool> save)
    {
        if (!initialAutoSaveRequired || initialAutoSaveDone) return false;
        if (!save(slot)) return false;
        initialAutoSaveDone = true;
        return true;
    }

    private System.Collections.IEnumerator EnsureDefaultSkillsNextFrame()
    {
        yield return null; // 씬 오브젝트 Start 완료(유닛 시딩) 이후로 1프레임 지연
        if (Lab != null) Lab.EnsureAllDefaultEquipped();
    }

    private void RefreshSceneManagers()
    {
        Grid = FindFirstObjectByType<GridManager>();
    }
}
