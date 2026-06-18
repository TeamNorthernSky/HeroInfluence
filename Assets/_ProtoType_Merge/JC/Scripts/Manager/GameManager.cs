using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public EconomyManager Economy { get; private set; }
    // [JC 폐기 260512] SceneLoader 폐기. UnityEngine.SceneManagement.SceneManager 래퍼 GameSceneManager(정적)로 대체
    public UIPrefabRegistry UIPrefabRegistry { get; private set; }
    public DebugManager Debug { get; private set; }
    public HQStateManager HQ { get; private set; }
    public PublicityManager Publicity { get; private set; }
    public TrainingManager Training { get; private set; }
    public LabManager Lab { get; private set; }
    public WorkshopManager Workshop { get; private set; }
    public TurnIncomeModalController TurnIncomeModal { get; private set; }
    public HeroInfoModal HeroInfoModal { get; private set; }
    public HeroInfoModal HeroStatusModal { get; private set; } // 탐사 멤버 클릭용(스킬 우측 레이아웃, Modal_HeroStatus)

    [Header("매턴 income 모달 트리거 씬 (기본: PlayScene = DH씬)")]
    [SerializeField] private string turnIncomeTriggerScene = "DHScene_3";

    public GridManager Grid { get; private set; }
    public TurnManager Turn { get; private set; }
    public CombatEncounterManager Combat { get; private set; }
    public FogGridManager FogGrid { get; private set; }
    public FogRenderManager FogRender { get; private set; }
    public LevelLoader Level { get; private set; }

    // [JC 추가 260511] 영속 매니저 참조 (각자 Singleton)
    public PersistentUnitRepository UnitRepo => PersistentUnitRepository.Instance;
    public PersistentEnemyRepository EnemyRepo => PersistentEnemyRepository.Instance;
    public HQVisitState HQVisit => HQVisitState.Instance;

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
                if (HQ != null) HQ.OnTurnAdvanced();
                int income = 0;
                if (HQ != null && Economy != null)
                {
                    income = HQ.GetTurnIncome(HQDepartment.Headquarters);
                    if (income > 0) Economy.Add(ResourceType.Money, income);
                }
                if (Publicity != null) Publicity.OnTurnAdvanced(currentDay);
                // [JC 260610] income 모달은 영속 모달을 직접 표시(씬 로드 의존 제거).
                // 턴 진행은 항상 탐사씬 TurnManager.AdvanceDay에서 일어나므로 이 시점은 탐사씬 안이다.
                if (TurnIncomeModal != null) TurnIncomeModal.Show(currentDay, income);
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
        currentDay = 1;
        pendingEndTurnOnExploration = false;

        if (Economy != null) Economy.ResetAll();
        if (HQ != null) HQ.Reset();
        if (Publicity != null) Publicity.Reset();
        if (Training != null) Training.Reset();
        if (Lab != null) Lab.Reset();
        if (Workshop != null) Workshop.Reset();

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

    private void Update()
    {
        if (Debug != null) Debug.Tick();
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

        UIPrefabRegistry = GetComponentInChildren<UIPrefabRegistry>(true);

        Debug = GetComponentInChildren<DebugManager>(true);
        if (Debug != null) Debug.Initialize();

        TurnIncomeModal = GetComponentInChildren<TurnIncomeModalController>(true);
        foreach (var m in GetComponentsInChildren<HeroInfoModal>(true))
        {
            if (m.gameObject.name.Contains("Status")) HeroStatusModal = m;
            else HeroInfoModal = m;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneManagers();
        // [JC 260619] 유닛 시딩(PartyUnitBootstrap.Start 등) 완료 후 기본 클래스 스킬 자동장착(멱등).
        // 무기는 유닛 생성 시 자동장착되지만 스킬은 미장착이라, 시작 시 보장한다.
        if (Lab != null) StartCoroutine(EnsureDefaultSkillsNextFrame());
    }

    private System.Collections.IEnumerator EnsureDefaultSkillsNextFrame()
    {
        yield return null; // 씬 오브젝트 Start 완료(유닛 시딩) 이후로 1프레임 지연
        if (Lab != null) Lab.EnsureAllDefaultEquipped();
    }

    private void RefreshSceneManagers()
    {
        Grid = FindFirstObjectByType<GridManager>();
        Turn = FindFirstObjectByType<TurnManager>();
        Combat = FindFirstObjectByType<CombatEncounterManager>();
        FogGrid = FindFirstObjectByType<FogGridManager>();
        FogRender = FindFirstObjectByType<FogRenderManager>();
        Level = FindFirstObjectByType<LevelLoader>();
    }
}
