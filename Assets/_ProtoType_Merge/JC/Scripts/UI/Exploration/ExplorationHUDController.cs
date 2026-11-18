using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// [JC 260608 / 정리 260630] 탐사씬 턴 HUD. 자원 HUD·옵션·툴팁은 공유 TopBar(ResourceHUDController)로 이관됨.
/// 본 컨트롤러는 턴/주/일 표시 + NextTurn(EndPlayerTurn) + 적턴 입력 게이팅 + 가시성 게이팅 전담.
/// </summary>
[DisallowMultipleComponent]
public class ExplorationHUDController : MonoBehaviour
{
    [Header("턴/주/일 텍스트 (비우면 이름 자동 탐색)")]
    [SerializeField] private TextMeshProUGUI turnWeekDayText; // Text_TurnWeekDay
    [Tooltip("표시할 최대 턴 수. 0 이하이면 35를 사용합니다.")]
    public int maxTurn;

    [Header("턴 종료 버튼 / TurnManager (비우면 자동 탐색)")]
    [SerializeField] private Button nextTurnButton;           // BTN_Explor_NextTurn
    [SerializeField] private TurnManager turnManager;

    [Header("턴 종료 시 숨길 패널")]
    public GameObject pressNextTurnPanel;

    private const int DaysPerWeek = 7;
    private TurnManager subscribedTM;
    private bool listenerHooked;
    // JC | 2026-11-18 | 튜토리얼에 없는 일반 턴 관리자를 매 프레임 찾지 않습니다.
    // 늦게 생성되는 시스템은 1초 이내 재연결하며 씬/활성 변경 시 즉시 다시 확인합니다.
    private float nextTurnSearch, nextCameraSearch, nextPublicitySearch;
    private void ResolveTurnManager()
    {
        if (turnManager != null || Time.unscaledTime < nextTurnSearch) return;
        nextTurnSearch = Time.unscaledTime + 1f;
        turnManager = FindFirstObjectByType<TurnManager>();
    }
    private Canvas canvas;
    private GraphicRaycaster graphicRaycaster;
    private QuarterViewCameraFollower cameraFollower;
    private Button publicityButton;
    private bool publicityCameraBlocked;
    private ExplorationDefeatBlockPanelController defeatBlockPanelController;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        graphicRaycaster = GetComponent<GraphicRaycaster>();
        defeatBlockPanelController = GetComponent<ExplorationDefeatBlockPanelController>();
        ResolveMissing();
        HookButton();
    }

    private void OnEnable()
    {
        nextTurnSearch = nextCameraSearch = nextPublicitySearch = 0;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        UpdateVisibility();
        TrySubscribe();
        RefreshTurn();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Unsubscribe();
        RefreshPublicityCameraState(false);
    }

    // JC | 2026-11-18 | 씬 전환에서는 재시도 대기를 해제해 기존 구독 시점을 유지합니다.
    private void OnActiveSceneChanged(Scene a, Scene b)
    { nextTurnSearch = nextCameraSearch = nextPublicitySearch = 0; UpdateVisibility(); }

    private void UpdateVisibility()
    {
        if (canvas == null) return;
        canvas.enabled = gameObject.scene == SceneManager.GetActiveScene();
    }

    private void Update()
    {
        if (subscribedTM == null) TrySubscribe();
        bool cameraBlocked = IsCameraPresentationBlocking();
        RefreshPublicityCameraState(cameraBlocked);
        RefreshNextTurnInteractable();
    }

    public bool CanUseExplorationActions()
    {
        ResolveTurnManager();
        return turnManager != null && turnManager.IsPlayerTurn && !turnManager.IsEnemyTurnRunning &&
            !IsCameraPresentationBlocking();
    }

    private void RefreshPublicityCameraState(bool blocked)
    {
        if (publicityButton == null && Time.unscaledTime >= nextPublicitySearch)
        {
            // JC | 2026-11-18 | 홍보 버튼이 없는 화면의 이름 검색도 초당 한 번으로 제한합니다.
            nextPublicitySearch = Time.unscaledTime + 1f;
            publicityCameraBlocked = false;
            var target = GameObject.Find("Publicity Button");
            if (target != null && target.scene == gameObject.scene)
                publicityButton = target.GetComponent<Button>();
        }
        if (publicityButton == null) return;
        if (blocked)
        {
            publicityCameraBlocked = true;
            publicityButton.interactable = false;
        }
        else if (publicityCameraBlocked)
        {
            publicityCameraBlocked = false;
            publicityButton.interactable = true;
        }
    }

    private bool IsCameraPresentationBlocking()
    {
        if (GateCameraFocusController.IsBusy) return true;
        if (cameraFollower == null && Time.unscaledTime >= nextCameraSearch)
        {
            // JC | 2026-11-18 | 카메라 연출 잠금 판정은 매 프레임 유지하고 누락 참조 탐색만 제한합니다.
            nextCameraSearch = Time.unscaledTime + 1f;
            Camera mainCamera = Camera.main;
            cameraFollower = mainCamera != null ? mainCamera.GetComponent<QuarterViewCameraFollower>() : null;
            if (cameraFollower == null) cameraFollower = FindFirstObjectByType<QuarterViewCameraFollower>();
        }
        return cameraFollower != null && cameraFollower.isActiveAndEnabled &&
            (cameraFollower.IsExternalInputLocked || cameraFollower.IsEventFocusMoving || cameraFollower.IsTemporaryFocusActive);
    }

    private void ResolveMissing()
    {
        if (turnWeekDayText == null)
        {
            var go = GameObject.Find("Text_TurnWeekDay");
            if (go != null) turnWeekDayText = go.GetComponent<TextMeshProUGUI>();
        }
        if (nextTurnButton == null)
        {
            var go = GameObject.Find("BTN_Explor_NextTurn");
            if (go != null) nextTurnButton = go.GetComponentInChildren<Button>(true);
        }
        ResolveTurnManager();
    }

    private void HookButton()
    {
        if (!listenerHooked && nextTurnButton != null)
        {
            nextTurnButton.onClick.AddListener(OnClickNextTurn);
            listenerHooked = true;
        }
    }

    private void TrySubscribe()
    {
        if (subscribedTM != null) return;
        ResolveTurnManager();
        if (turnManager == null) return;
        subscribedTM = turnManager;
        subscribedTM.DayAdvanced += OnDayAdvanced;
        subscribedTM.EnemyTurnStateChanged += OnEnemyTurnStateChanged;
        HookButton();
        RefreshNextTurnInteractable();
        RefreshTurn();
    }

    private void Unsubscribe()
    {
        if (subscribedTM == null) return;
        subscribedTM.DayAdvanced -= OnDayAdvanced;
        subscribedTM.EnemyTurnStateChanged -= OnEnemyTurnStateChanged;
        subscribedTM = null;
    }

    private void OnDayAdvanced(int _) => RefreshTurn();
    private void OnEnemyTurnStateChanged(bool enemyRunning) => ApplyEnemyTurnUIState(enemyRunning);

    private void ApplyEnemyTurnUIState(bool enemyRunning)
    {
        bool playerTurn = !enemyRunning && (turnManager == null || turnManager.IsPlayerTurn);
        if (nextTurnButton != null) nextTurnButton.interactable = CanUseExplorationActions();
        if (graphicRaycaster != null) graphicRaycaster.enabled = playerTurn;
    }

    private void RefreshNextTurnInteractable()
        => ApplyEnemyTurnUIState(turnManager != null && turnManager.IsEnemyTurnRunning);

    private void OnClickNextTurn()
    {
        ResolveTurnManager();
        if (turnManager == null) return;
        if (!CanUseExplorationActions()) return;
        if (defeatBlockPanelController != null) defeatBlockPanelController.HideForNextTurn();
        if (pressNextTurnPanel != null) pressNextTurnPanel.SetActive(false);
        if (nextTurnButton != null) nextTurnButton.interactable = false;
        turnManager.EndPlayerTurn();
    }

    private void RefreshTurn()
    {
        if (turnWeekDayText == null) return;
        int day = ResolveCurrentDay();
        if (turnWeekDayText.transform.parent != null &&
            turnWeekDayText.transform.parent.name == "Current Turn Info")
        {
            int displayMaxTurn = maxTurn > 0 ? maxTurn : 35;
            turnWeekDayText.text = $"{day}/{displayMaxTurn}";
            return;
        }
        int week = (day - 1) / DaysPerWeek + 1;
        int dayInWeek = (day - 1) % DaysPerWeek + 1;
        turnWeekDayText.text = $"{day}턴  {week}주  {dayInWeek}일";
    }

    private int ResolveCurrentDay()
    {
        var gm = GameManager.Instance;
        if (gm != null) return Mathf.Max(1, gm.CurrentDay);
        if (turnManager != null) return Mathf.Max(1, turnManager.GetDay());
        return 1;
    }
}
