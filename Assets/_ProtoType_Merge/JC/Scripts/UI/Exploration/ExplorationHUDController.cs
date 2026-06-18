using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 탐사씬(DHScene_3) HUD 컨트롤러. Canvas에 부착.
/// 자원 4종(자금/메달/수정/자재) 텍스트를 EconomyManager와 실시간 연동하고,
/// 턴/주/일 텍스트를 TurnManager.DayAdvanced + GameManager.CurrentDay와 연동한다.
/// BTN_Explor_NextTurn → TurnManager.EndPlayerTurn(기존 TurnButton 대체). 적 턴 동안은 버튼 interactable=false.
/// SerializedField를 비워두면 이름으로 자동 탐색(공식 UI 개편 시 재바인딩 용이).
/// 가시성 게이팅: 이 Canvas는 DHScene_3이 active 씬일 때만 렌더(canvas.enabled). GameLoadGate가
/// DHScene_3을 Additive 로드 후 로비 진입 판정을 내리는 동안(GameLoadScene active)에는 숨겨져,
/// 판정 전 탐사 HUD가 깜빡이지 않는다. 잔류 분기(SetActiveScene) 또는 일반 Single 진입 시 표시.
/// </summary>
[DisallowMultipleComponent]
public class ExplorationHUDController : MonoBehaviour
{
    [Header("자원 텍스트 (비우면 이름으로 자동 탐색)")]
    [SerializeField] private TextMeshProUGUI moneyText;   // Text_Money   → ResourceType.Money
    [SerializeField] private TextMeshProUGUI medalText;   // Text_Medal   → ResourceType.Chip (메달)
    [SerializeField] private TextMeshProUGUI crystalText; // Text_Crystal → ResourceType.Crystal
    [SerializeField] private TextMeshProUGUI supplyText;  // Text_Supply  → ResourceType.Supply

    [Header("턴/주/일 텍스트")]
    [SerializeField] private TextMeshProUGUI turnWeekDayText; // Text_TurnWeekDay

    [Header("턴 종료 버튼 / TurnManager (비우면 자동 탐색)")]
    [SerializeField] private Button nextTurnButton;       // BTN_Explor_NextTurn
    [SerializeField] private TurnManager turnManager;

    [Header("옵션(시스템 메뉴) 버튼 (비우면 이름으로 자동 탐색)")]
    [SerializeField] private Button optionButton;         // BTN_Explor_Option → 영속 SystemMenuModal

    private const int DaysPerWeek = 7;

    private EconomyManager subscribedEco;
    private TurnManager subscribedTM;
    private bool listenerHooked;
    private bool optionHooked;
    private Canvas canvas;
    private GraphicRaycaster graphicRaycaster;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        graphicRaycaster = GetComponent<GraphicRaycaster>();
        ResolveMissingReferences();
        HookButton();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        UpdateVisibility();
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Unsubscribe();
    }

    // ─── 가시성 게이팅 (로비 진입 판정 전 탐사 UI 숨김) ──────────
    private void OnActiveSceneChanged(Scene previous, Scene next) => UpdateVisibility();

    private void UpdateVisibility()
    {
        if (canvas == null) return;
        // 이 Canvas가 속한 씬이 active 씬일 때만 렌더. GameLoadGate의 Additive 로드·판정 동안은
        // GameLoadScene이 active이므로 숨겨진다.
        canvas.enabled = gameObject.scene == SceneManager.GetActiveScene();
    }

    private void Update()
    {
        // GameManager/TurnManager가 늦게 준비될 수 있어 구독 완료 전까지 재시도.
        if (subscribedEco == null || subscribedTM == null) TrySubscribe();
    }

    // ─── 참조 해석 ──────────────────────────────────────────
    private void ResolveMissingReferences()
    {
        if (moneyText == null) moneyText = FindText("Text_Money");
        if (medalText == null) medalText = FindText("Text_Medal");
        if (crystalText == null) crystalText = FindText("Text_Crystal");
        if (supplyText == null) supplyText = FindText("Text_Supply");
        if (turnWeekDayText == null) turnWeekDayText = FindText("Text_TurnWeekDay");

        if (nextTurnButton == null)
        {
            var go = GameObject.Find("BTN_Explor_NextTurn");
            if (go != null) nextTurnButton = go.GetComponentInChildren<Button>(true);
        }
        if (optionButton == null)
        {
            var go = GameObject.Find("BTN_Explor_Option");
            if (go != null) optionButton = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
        }
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
    }

    private static TextMeshProUGUI FindText(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponent<TextMeshProUGUI>() : null;
    }

    private void HookButton()
    {
        if (!listenerHooked && nextTurnButton != null)
        {
            nextTurnButton.onClick.AddListener(OnClickNextTurn);
            listenerHooked = true;
        }
        if (!optionHooked && optionButton != null)
        {
            optionButton.onClick.AddListener(OnClickOption);
            optionHooked = true;
        }
    }

    // [JC 260619] 탐사 옵션 버튼 → 영속 SystemMenuModal 열기(로비 HQLobbyMenuController.OnClickOption과 동일 패턴).
    private void OnClickOption()
    {
        var sys = FindObjectOfType<SystemMenuController>(true);
        if (sys != null) sys.OpenMenu();
        else Debug.LogWarning("[ExplorationHUD] SystemMenuController 없음 — 시스템 메뉴를 열 수 없음");
    }

    // ─── 구독 ───────────────────────────────────────────────
    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm != null && subscribedEco == null && gm.Economy != null)
        {
            subscribedEco = gm.Economy;
            subscribedEco.OnResourceChanged += OnResourceChanged;
        }

        if (subscribedTM == null)
        {
            if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
            if (turnManager != null)
            {
                subscribedTM = turnManager;
                subscribedTM.DayAdvanced += OnDayAdvanced;
                subscribedTM.EnemyTurnStateChanged += OnEnemyTurnStateChanged;
                HookButton(); // 버튼이 늦게 해석된 경우 보강
                RefreshNextTurnInteractable();
            }
        }

        Refresh();
    }

    private void Unsubscribe()
    {
        if (subscribedEco != null) { subscribedEco.OnResourceChanged -= OnResourceChanged; subscribedEco = null; }
        if (subscribedTM != null)
        {
            subscribedTM.DayAdvanced -= OnDayAdvanced;
            subscribedTM.EnemyTurnStateChanged -= OnEnemyTurnStateChanged;
            subscribedTM = null;
        }
    }

    private void OnResourceChanged(ResourceType _, int __) => RefreshResources();
    private void OnDayAdvanced(int _) => RefreshTurn();

    private void OnEnemyTurnStateChanged(bool enemyTurnRunning) => ApplyEnemyTurnUIState(enemyTurnRunning);

    /// <summary>
    /// 적 턴 동안: NextTurn 버튼 interactable=false + 탐사 Canvas의 GraphicRaycaster 비활성
    /// (모든 버튼 hover·클릭 무반응, 외형은 정상색 유지). 플레이어 턴에 복구.
    /// </summary>
    private void ApplyEnemyTurnUIState(bool enemyTurnRunning)
    {
        bool playerTurn = !enemyTurnRunning;
        if (nextTurnButton != null) nextTurnButton.interactable = playerTurn;
        if (graphicRaycaster != null) graphicRaycaster.enabled = playerTurn;
    }

    private void RefreshNextTurnInteractable()
    {
        ApplyEnemyTurnUIState(turnManager != null && turnManager.IsEnemyTurnRunning);
    }

    private void OnClickNextTurn()
    {
        if (turnManager == null) turnManager = FindFirstObjectByType<TurnManager>();
        if (turnManager != null) turnManager.EndPlayerTurn();
    }

    // ─── 갱신 ───────────────────────────────────────────────
    private void Refresh()
    {
        RefreshResources();
        RefreshTurn();
    }

    private void RefreshResources()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Economy == null) return;
        if (moneyText != null) moneyText.text = gm.Economy.Get(ResourceType.Money).ToString("N0");
        if (medalText != null) medalText.text = gm.Economy.Get(ResourceType.Chip).ToString("N0");
        if (crystalText != null) crystalText.text = gm.Economy.Get(ResourceType.Crystal).ToString("N0");
        if (supplyText != null) supplyText.text = gm.Economy.Get(ResourceType.Supply).ToString("N0");
    }

    private void RefreshTurn()
    {
        if (turnWeekDayText == null) return;
        int day = ResolveCurrentDay();
        int turn = day;                                   // 1턴 = 1일 (누적 일수)
        int week = (day - 1) / DaysPerWeek + 1;
        int dayInWeek = (day - 1) % DaysPerWeek + 1;
        turnWeekDayText.text = $"{turn}턴  {week}주  {dayInWeek}일";
    }

    private int ResolveCurrentDay()
    {
        var gm = GameManager.Instance;
        if (gm != null) return Mathf.Max(1, gm.CurrentDay);
        if (turnManager != null) return Mathf.Max(1, turnManager.GetDay());
        return 1;
    }
}
