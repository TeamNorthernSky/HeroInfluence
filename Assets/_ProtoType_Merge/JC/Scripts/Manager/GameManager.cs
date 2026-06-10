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
    public BroadcastManager Broadcast { get; private set; }
    public TrainingManager Training { get; private set; }
    public TurnIncomeModalController TurnIncomeModal { get; private set; }

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
                if (Broadcast != null) Broadcast.OnTurnAdvanced(currentDay);
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

        Broadcast = GetComponentInChildren<BroadcastManager>(true);
        if (Broadcast != null)
        {
            Broadcast.Initialize();
            Broadcast.SubscribeHQ(HQ);
        }

        Training = GetComponentInChildren<TrainingManager>(true);
        if (Training != null) Training.Initialize();

        UIPrefabRegistry = GetComponentInChildren<UIPrefabRegistry>(true);

        Debug = GetComponentInChildren<DebugManager>(true);
        if (Debug != null) Debug.Initialize();

        TurnIncomeModal = GetComponentInChildren<TurnIncomeModalController>(true);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneManagers();
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
