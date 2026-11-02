using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    private const int DaysPerWeek = 7;
    private TurnManager subscribedTM;
    private bool listenerHooked;
    private Canvas canvas;
    private GraphicRaycaster graphicRaycaster;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        graphicRaycaster = GetComponent<GraphicRaycaster>();
        ResolveMissing();
        HookButton();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        UpdateVisibility();
        TrySubscribe();
        RefreshTurn();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Unsubscribe();
    }

    private void OnActiveSceneChanged(Scene a, Scene b) => UpdateVisibility();

    private void UpdateVisibility()
    {
        if (canvas == null) return;
        canvas.enabled = gameObject.scene == SceneManager.GetActiveScene();
    }

    private void Update()
    {
        if (subscribedTM == null) TrySubscribe();
        if (Input.GetKeyDown(KeyCode.R) && CanUseRestShortcut())
            nextTurnButton.onClick.Invoke();
    }

    private bool CanUseRestShortcut()
    {
        if (gameObject.scene != SceneManager.GetActiveScene() ||
            nextTurnButton == null || !nextTurnButton.isActiveAndEnabled || !nextTurnButton.IsInteractable() ||
            turnManager == null || !turnManager.IsPlayerTurn || turnManager.IsEnemyTurnRunning ||
            DHGameEndState.IsEnding || DHGameStateRestoreService.IsRestoring ||
            ModalManager.HasAny || WorldInputGate.IsTurnResolving || GateCameraFocusController.IsBusy ||
            JC.Environment.JcDayNightController.IsTransitioning ||
            ChatModalController.IsOpen || (ChatManager.Instance != null && ChatManager.Instance.IsRunning) ||
            (DHWorldEventRuntimeManager.Instance != null && DHWorldEventRuntimeManager.Instance.IsRunning) ||
            (DHWorldEventConditionRuntimeManager.Instance != null && DHWorldEventConditionRuntimeManager.Instance.IsCheckPending) ||
            (SceneFadeController.Instance != null && SceneFadeController.Instance.IsTransitioning))
            return false;

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return false;

        EventSystem events = EventSystem.current;
        if (events == null) return false;
        GameObject selected = events.currentSelectedGameObject;
        if (selected != null)
        {
            TMP_InputField input = selected.GetComponentInParent<TMP_InputField>();
            InputField legacyInput = selected.GetComponentInParent<InputField>();
            if ((input != null && input.isFocused) || (legacyInput != null && legacyInput.isFocused)) return false;
        }

        // These panels can be shown without registering a Modal.
        if (FindFirstObjectByType<CombatPromptPanelController>() != null ||
            FindFirstObjectByType<BattleResultPanel>() != null ||
            FindFirstObjectByType<SkillSelectionPanel>() != null) return false;

        foreach (Canvas parentCanvas in nextTurnButton.GetComponentsInParent<Canvas>())
            if (!parentCanvas.isActiveAndEnabled) return false;

        // Respect UI shields and hidden/non-raycastable buttons just like a pointer click.
        Canvas buttonCanvas = nextTurnButton.GetComponentInParent<Canvas>();
        if (buttonCanvas == null) return false;
        buttonCanvas = buttonCanvas.rootCanvas;
        Camera eventCamera = buttonCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : buttonCanvas.worldCamera;
        var pointer = new PointerEventData(events)
        {
            position = RectTransformUtility.WorldToScreenPoint(eventCamera,
                nextTurnButton.transform.TransformPoint(((RectTransform)nextTurnButton.transform).rect.center))
        };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        events.RaycastAll(pointer, hits);
        return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(nextTurnButton.transform);
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
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
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
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
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
        if (nextTurnButton != null) nextTurnButton.interactable = playerTurn;
        if (graphicRaycaster != null) graphicRaycaster.enabled = playerTurn;
    }

    private void RefreshNextTurnInteractable()
        => ApplyEnemyTurnUIState(turnManager != null && turnManager.IsEnemyTurnRunning);

    private void OnClickNextTurn()
    {
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        if (turnManager == null) return;
        if (!turnManager.IsPlayerTurn) return;
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
