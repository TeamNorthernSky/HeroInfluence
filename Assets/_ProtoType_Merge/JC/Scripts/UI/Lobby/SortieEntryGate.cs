using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260616] 출전 버튼(BTN_HQLobby_Go) 진입 게이트. (기획서 협회(본부,출전) ④)
///
/// 기존 ModalToggleButton(단순 토글)을 대체. 상태별 분기:
///  - 출전 가능 영웅 0명(상주 파티 멤버 + 무소속 모두 없음) → 버튼 비활성 + 회색 오버레이(hover·클릭 차단).
///  - 상주(방문중) 파티 존재 → 진형 편집 모달 오픈.
///  - 상주 파티 없음 + 무소속 영웅 존재(유일 파티가 본부를 떠난 상태) → 새 파티 생성에 해당
///    → "현재 버전에서는 1개 파티만 생성할 수 있습니다" 메시지 모달.
///  - 모달 열린 상태에서 재클릭 → SortieController.TryClose(멤버 0명이면 닫기 차단).
///
/// 회색 오버레이·메시지 모달은 런타임 생성(씬 의존 최소화). 상주/카운트 판정은 SortieController 정적 메서드 단일 출처.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class SortieEntryGate : MonoBehaviour
{
    [Header("참조 (비우면 자동 해석)")]
    [SerializeField] private Button button;
    [SerializeField] private SortieController controller;
    [SerializeField] private GameObject modalRoot;

    [Header("0명 회색 오버레이")]
    [SerializeField] private Color disabledOverlayColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);

    [Header("1파티 제한 메시지")]
    [SerializeField] private string singlePartyMessage = "현재 버전에서는 1개 파티만 생성할 수 있습니다.";

    [Header("한글 기본 폰트 (메시지 모달용. 비우면 캔버스에서 탐색)")]
    [SerializeField] private TMP_FontAsset hangulFont; // = NotoSansKR-Regular SDF

    private Image greyOverlay;
    private GameObject msgModal;
    private TMP_Text msgText;
    private TMP_FontAsset cachedFont;
    private float nextPoll;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (controller == null) controller = FindObjectOfType<SortieController>(true);
        if (modalRoot == null && controller != null) modalRoot = controller.gameObject;

        // 충돌 방지: 기존 단순 토글 컴포넌트가 남아 있으면 제거(중복 토글 차단)
        var toggle = GetComponent<ModalToggleButton>();
        if (toggle != null) Destroy(toggle);

        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.2f;
        RefreshGate();
    }

    private void RefreshGate()
    {
        // [JC 260629] 크로스번들 — controller를 사용 시점에 레지스트리에서 해석.
        if (controller == null) controller = LobbyUIRegistry.Sortie;
        if (modalRoot == null && controller != null) modalRoot = controller.gameObject;

        // [JC 260629] 모달이 열려 있으면 출전 버튼은 '편성완료(닫기)' 역할을 겸하므로 항상 활성이어야 한다.
        // (0명 비활성 게이트는 모달 진입 전에만 적용 — 열린 상태에서 비활성화되면 닫을 수 없게 됨.)
        bool modalOpen = modalRoot != null && modalRoot.activeSelf;
        bool active = modalOpen || SortieController.CountDeployableHeroes() > 0;
        if (button != null) button.interactable = active;
        EnsureGreyOverlay();
        if (greyOverlay != null) greyOverlay.gameObject.SetActive(!active);
    }

    private void OnClick()
    {
        // [JC 260629] 크로스번들 — controller를 클릭 시점에 레지스트리에서 해석.
        if (controller == null) controller = LobbyUIRegistry.Sortie;
        if (modalRoot == null && controller != null) modalRoot = controller.gameObject;

        bool open = modalRoot != null && modalRoot.activeSelf;
        if (open)
        {
            // [JC 260616] 새 파티(무-상주) 모드: 진형에 1명 이상이면 추가생성 불가 팝업(닫지 않음).
            // 진형이 비었으면 현재 버전 한정 임시로 그냥 닫기(파티 생성 안 함). 상태 안내는 진입 시부터 상시 표시.
            if (controller != null && !controller.HasResidentParty)
            {
                if (controller.FilledCount > 0)
                {
                    ShowMessage(singlePartyMessage);
                    return;
                }
                controller.TryClose(); // 0명 → 닫기 허용(무-상주라 저장 no-op)
                return;
            }
            if (controller != null) controller.TryClose();
            else if (modalRoot != null) modalRoot.SetActive(false);
            return;
        }

        if (SortieController.CountDeployableHeroes() <= 0) return; // 비활성 상태여야 정상

        // [JC 260616] 진입 자체는 차단하지 않는다. 상주 파티가 없어도 새 파티 편성 화면으로 열되,
        // 출전(확정) 시 "1개 파티만" 안내로 막는다. 저장도 no-op이라 새 파티는 생기지 않음.
        if (modalRoot != null) modalRoot.SetActive(true);
    }

    // ─── 회색 오버레이 ───────────────────────────────────────
    private void EnsureGreyOverlay()
    {
        if (greyOverlay != null || button == null) return;
        var go = new GameObject("SortieDisabledOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(button.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        greyOverlay = go.GetComponent<Image>();
        greyOverlay.color = disabledOverlayColor;
        greyOverlay.raycastTarget = true; // hover·클릭 동시 차단
        rt.SetAsLastSibling();
        go.SetActive(false);
    }

    // ─── 메시지 모달 (런타임) ────────────────────────────────
    private void ShowMessage(string msg)
    {
        EnsureMessageModal();
        if (msgModal == null) return;
        if (msgText != null) msgText.text = msg;
        msgModal.transform.SetAsLastSibling();
        msgModal.SetActive(true);
    }

    private void EnsureMessageModal()
    {
        if (msgModal != null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas == null) return;
        ResolveFont();

        // 루트(전체 화면 dim + 외부 클릭 닫기)
        msgModal = new GameObject("Modal_SortieNotice", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rootRt = (RectTransform)msgModal.transform;
        rootRt.SetParent(canvas.transform, false);
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        msgModal.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        msgModal.GetComponent<Button>().onClick.AddListener(CloseMessage); // 외부 클릭 닫기

        // 패널
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var panelRt = (RectTransform)panel.transform;
        panelRt.SetParent(rootRt, false);
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(560f, 240f);
        panel.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f, 0.98f);

        // 메시지 텍스트
        var txtGo = new GameObject("Message", typeof(RectTransform));
        var txtRt = (RectTransform)txtGo.transform;
        txtRt.SetParent(panelRt, false);
        txtRt.anchorMin = new Vector2(0f, 0.35f);
        txtRt.anchorMax = new Vector2(1f, 1f);
        txtRt.offsetMin = new Vector2(24f, 0f);
        txtRt.offsetMax = new Vector2(-24f, -16f);
        msgText = txtGo.AddComponent<TextMeshProUGUI>();
        msgText.alignment = TextAlignmentOptions.Center;
        msgText.fontSize = 24f;
        msgText.color = Color.white;
        msgText.raycastTarget = false;
        if (cachedFont != null) msgText.font = cachedFont;

        // 확인 버튼
        var okGo = new GameObject("BTN_OK", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var okRt = (RectTransform)okGo.transform;
        okRt.SetParent(panelRt, false);
        okRt.anchorMin = new Vector2(0.5f, 0f);
        okRt.anchorMax = new Vector2(0.5f, 0f);
        okRt.pivot = new Vector2(0.5f, 0f);
        okRt.anchoredPosition = new Vector2(0f, 24f);
        okRt.sizeDelta = new Vector2(160f, 52f);
        okGo.GetComponent<Image>().color = new Color(0.30f, 0.55f, 0.95f, 1f);
        okGo.GetComponent<Button>().onClick.AddListener(CloseMessage);

        var okTxtGo = new GameObject("Label", typeof(RectTransform));
        var okTxtRt = (RectTransform)okTxtGo.transform;
        okTxtRt.SetParent(okRt, false);
        okTxtRt.anchorMin = Vector2.zero;
        okTxtRt.anchorMax = Vector2.one;
        okTxtRt.offsetMin = Vector2.zero;
        okTxtRt.offsetMax = Vector2.zero;
        var okLabel = okTxtGo.AddComponent<TextMeshProUGUI>();
        okLabel.text = "확인";
        okLabel.alignment = TextAlignmentOptions.Center;
        okLabel.fontSize = 22f;
        okLabel.color = Color.white;
        okLabel.raycastTarget = false;
        if (cachedFont != null) okLabel.font = cachedFont;

        msgModal.SetActive(false);
    }

    private void CloseMessage()
    {
        if (msgModal != null) msgModal.SetActive(false);
    }

    private void ResolveFont()
    {
        if (cachedFont != null) return;
        if (hangulFont != null) { cachedFont = hangulFont; return; } // 한글 기본 명시 우선
        var anyText = GetComponentInParent<Canvas>()?.GetComponentInChildren<TMP_Text>(true);
        if (anyText != null && anyText.font != null) cachedFont = anyText.font;
    }
}
