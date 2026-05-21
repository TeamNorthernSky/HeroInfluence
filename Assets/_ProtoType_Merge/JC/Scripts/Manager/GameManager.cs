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
                if (HQ != null && Economy != null)
                {
                    int income = HQ.GetTurnIncome(HQDepartment.Headquarters);
                    if (income > 0) Economy.Add(ResourceType.Money, income);
                }
            }
        }
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

        UIPrefabRegistry = GetComponentInChildren<UIPrefabRegistry>(true);

        Debug = GetComponentInChildren<DebugManager>(true);
        if (Debug != null) Debug.Initialize();
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
