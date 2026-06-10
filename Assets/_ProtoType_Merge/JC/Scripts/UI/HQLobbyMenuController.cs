using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HQLobbyScene(신 협회 로비) 경량 메뉴 컨트롤러. HQLobbyCanvas에 부착.
/// 이번 라운드 범위: 자원 4종 HUD + 턴/주/일 표시(DHScene_3 동일 사양) + 나가기 + 턴종료.
/// - 나가기(BTN_HQLobby_Exit) → 탐사씬 복귀
/// - 턴종료(BTN_HQLobby_NextTurn) → (확인 모달) → "나가기 + 탐사 턴종료" 위임(GameManager.RequestEndTurnViaExploration)
/// 옵션/출전/툴팁은 다음 패스. 기존 LobbyMenuController(BTN_Lobby_ 프리픽스·13모달)와 분리된 새 디스패처.
/// SerializedField를 비우면 이름으로 자동 탐색(공식 UI 개편 시 재바인딩 용이).
/// </summary>
[DisallowMultipleComponent]
public class HQLobbyMenuController : MonoBehaviour
{
    [Header("자원 텍스트 (비우면 이름 자동 탐색)")]
    [SerializeField] private TextMeshProUGUI moneyText;   // Text_Money   → ResourceType.Money
    [SerializeField] private TextMeshProUGUI medalText;   // Text_Medal   → ResourceType.Chip (메달)
    [SerializeField] private TextMeshProUGUI crystalText; // Text_Crystal → ResourceType.Crystal
    [SerializeField] private TextMeshProUGUI supplyText;  // Text_Supply  → ResourceType.Supply

    [Header("턴/주/일")]
    [SerializeField] private TextMeshProUGUI turnWeekDayText; // Text_TurnWeekDay

    [Header("버튼 (비우면 이름 자동 탐색)")]
    [SerializeField] private Button exitButton;     // BTN_HQLobby_Exit
    [SerializeField] private Button nextTurnButton; // BTN_HQLobby_NextTurn

    [Header("턴종료 확인 모달 (편의 기능 — 추후 제거 가능. 비우면 즉시 종료)")]
    [Tooltip("씬 시작 시 active 상태여야 이름 탐색이 됨. Awake에서 자동으로 비활성화함.")]
    [SerializeField] private GameObject endTurnConfirmModal;
    [SerializeField] private string endTurnConfirmModalName = "Modal_HQLobby_EndTurnConfirm";
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    private const int DaysPerWeek = 7;
    private EconomyManager subscribedEco;

    private string ExplorationScene => GameSceneManager.Instance != null
        ? GameSceneManager.Instance.ExplorationScene
        : "DHScene_3";

    private void Awake()
    {
        ResolveMissingReferences();

        if (exitButton != null) exitButton.onClick.AddListener(OnClickExit);
        if (nextTurnButton != null) nextTurnButton.onClick.AddListener(OnClickNextTurn);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnClickConfirmYes);
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnClickConfirmNo);

        if (endTurnConfirmModal != null) endTurnConfirmModal.SetActive(false);
    }

    private void OnEnable() { TrySubscribe(); Refresh(); }
    private void OnDisable() { Unsubscribe(); }
    private void Update() { if (subscribedEco == null) TrySubscribe(); }

    // ─── 참조 해석 ──────────────────────────────────────────
    private void ResolveMissingReferences()
    {
        if (moneyText == null) moneyText = FindText("Text_Money");
        if (medalText == null) medalText = FindText("Text_Medal");
        if (crystalText == null) crystalText = FindText("Text_Crystal");
        if (supplyText == null) supplyText = FindText("Text_Supply");
        if (turnWeekDayText == null) turnWeekDayText = FindText("Text_TurnWeekDay");

        if (exitButton == null) exitButton = FindButton("BTN_HQLobby_Exit");
        if (nextTurnButton == null) nextTurnButton = FindButton("BTN_HQLobby_NextTurn");

        if (endTurnConfirmModal == null && !string.IsNullOrEmpty(endTurnConfirmModalName))
            endTurnConfirmModal = GameObject.Find(endTurnConfirmModalName);
        if (endTurnConfirmModal != null)
        {
            if (confirmYesButton == null) confirmYesButton = FindChildButton(endTurnConfirmModal, "Yes");
            if (confirmNoButton == null) confirmNoButton = FindChildButton(endTurnConfirmModal, "No");
        }
    }

    private static TextMeshProUGUI FindText(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponent<TextMeshProUGUI>() : null;
    }

    private static Button FindButton(string goName)
    {
        var go = GameObject.Find(goName);
        return go != null ? go.GetComponentInChildren<Button>(true) : null;
    }

    private static Button FindChildButton(GameObject root, string nameContains)
    {
        var buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            if (buttons[i].gameObject.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return buttons[i];
        return null;
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
        Refresh();
    }

    private void Unsubscribe()
    {
        if (subscribedEco != null) { subscribedEco.OnResourceChanged -= OnResourceChanged; subscribedEco = null; }
    }

    private void OnResourceChanged(ResourceType _, int __) => RefreshResources();

    // ─── 버튼 동작 ──────────────────────────────────────────
    private void OnClickExit() => LoadExploration();

    private void OnClickNextTurn()
    {
        if (endTurnConfirmModal != null) { endTurnConfirmModal.SetActive(true); return; }
        ConfirmEndTurn();
    }

    public void OnClickConfirmYes()
    {
        if (endTurnConfirmModal != null) endTurnConfirmModal.SetActive(false);
        ConfirmEndTurn();
    }

    public void OnClickConfirmNo()
    {
        if (endTurnConfirmModal != null) endTurnConfirmModal.SetActive(false);
    }

    private void ConfirmEndTurn()
    {
        // 턴종료 = "나가기 + 탐사 턴종료" 위임. CurrentDay 직접 증가 없음(적 턴 정상 진행 보장).
        var gm = GameManager.Instance;
        if (gm != null) gm.RequestEndTurnViaExploration();
        LoadExploration();
    }

    private void LoadExploration()
    {
        if (GameSceneManager.Instance != null) GameSceneManager.Instance.LoadScene(ExplorationScene);
        else SceneManager.LoadScene(ExplorationScene);
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
        int day = GameManager.Instance != null ? Mathf.Max(1, GameManager.Instance.CurrentDay) : 1;
        int week = (day - 1) / DaysPerWeek + 1;
        int dayInWeek = (day - 1) % DaysPerWeek + 1;
        turnWeekDayText.text = $"{day}턴  {week}주  {dayInWeek}일";
    }
}
