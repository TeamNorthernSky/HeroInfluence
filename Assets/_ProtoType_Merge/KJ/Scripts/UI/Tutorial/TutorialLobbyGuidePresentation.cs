using JC.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>협회 안내의 기존 순서와 시설 해금을 유지하며 탐사 안내 표현을 연결한다.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(1000)]
public sealed class TutorialLobbyGuidePresentation : MonoBehaviour
{
    [SerializeField] private TutorialTextManager progression;
    [SerializeField] private JcTutorialGuideView view;
    [SerializeField] private GameObject inputShield;
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject[] panels;
    [SerializeField] private TMP_Text[] sourceMessages;
    [SerializeField] private GameObject[] legacyMessageRoots;
    [SerializeField] private RectTransform[] focusTargets;
    [SerializeField] private Vector2[] messagePositions;
    [SerializeField, Tooltip("시설 기능 개요 안내창의 수평 위치 보정입니다. Canvas 기준 단위입니다.")]
    private float overviewOffsetX = -20;
    private JcTutorialGraphic cardBackground;
    private const int FacilitiesOverviewIndex = 1;

    private int shownIndex = -1;
    private int openedFrame;
    private bool wasProgressionEnabled;
    private bool initialized;
    private Button shieldButton;
    private bool[] originalBackgroundEnabled;
    private Canvas exitCanvas;
    private GraphicRaycaster exitRaycaster;
    private bool addedExitCanvas, addedExitRaycaster;
    private bool exitOriginalOverride;
    private bool exitOriginalCanvasEnabled, exitOriginalRaycasterEnabled, exitSortingActive;
    private int exitOriginalOrder, exitOriginalLayer;

    private void Start()
    {
        if (progression == null || view == null || view.panel == null ||
            panels == null || sourceMessages == null || panels.Length == 0 ||
            panels.Length != sourceMessages.Length || System.Array.Exists(sourceMessages, text => text == null))
        {
            Debug.LogError("협회 안내 연결이 누락되었습니다.", this);
            enabled = false;
            return;
        }

        // 기존 Start가 첫 안내를 선택한 뒤 화면 클릭만 이 연결부에서 담당한다.
        wasProgressionEnabled = progression.enabled;
        shieldButton = inputShield != null ? inputShield.GetComponent<Button>() : null;
        originalBackgroundEnabled = new bool[panels.Length];
        for (int i = 0; i < panels.Length; i++)
        {
            var background = panels[i] != null ? panels[i].GetComponent<Image>() : null;
            originalBackgroundEnabled[i] = background != null && background.enabled;
        }
        initialized = true;
        cardBackground = view.panel.GetComponentInChildren<JcTutorialGraphic>(true);

        TakeOwnership();
    }

    private void Update()
    {
        RefreshPresentation();
    }

    private void OnEnable()
    {
        if (initialized) TakeOwnership();
    }

    private void TakeOwnership()
    {
        progression.enabled = false;
        if (continueButton != null) continueButton.onClick.AddListener(Advance);
        if (shieldButton != null) shieldButton.onClick.AddListener(Advance);
        shownIndex = -1;
        RefreshPresentation();
    }

    public void Advance()
    {
        if (!initialized || Time.frameCount <= openedFrame || shownIndex >= panels.Length - 1) return;
        progression.ShowNextPanel();
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        int index = progression.CurrentIndex;
        if (index == shownIndex || index < 0 || index >= panels.Length) return;
        shownIndex = index;
        openedFrame = Time.frameCount;
        if (legacyMessageRoots != null)
            foreach (var root in legacyMessageRoots)
                if (root != null) root.SetActive(false);
        // 시설별 작은 설명과 마지막 나가기 버튼은 기존 패널에 남겨 둔다.
        foreach (var panel in panels)
        {
            if (panel == null) continue;
            var background = panel.GetComponent<Image>();
            if (background != null) background.enabled = false;
        }
        view.SetSideLayout(null);
        bool overview = index == FacilitiesOverviewIndex;
        if (cardBackground != null) cardBackground.SetOpaqueFill(true);
        view.SetContent(JcTutorialText.Get("lobbyguidepresentation.001", "협회 안내"), JcTutorialText.Read(sourceMessages[index]));
        var card = (RectTransform)view.panel.transform;
        if (messagePositions != null && index < messagePositions.Length)
            card.anchoredPosition = messagePositions[index] + (overview ? new Vector2(overviewOffsetX, 0) : Vector2.zero);
        view.panel.alpha = 0;
        view.FitMessageLayout();
        var focus = focusTargets != null && index < focusTargets.Length ? focusTargets[index] : null;
        if (focus != null && card.anchoredPosition.x > 0) view.SetSideLayout(focus, null, true, overview ? new Vector2(overviewOffsetX, 0) : Vector2.zero);
        view.SetVisible(true, focus, JcTutorialGuideView.FocusPresentation.DimmedExplanation);
        bool final = index == panels.Length - 1;
        if (continueButton != null) continueButton.gameObject.SetActive(!final);
        if (inputShield != null) inputShield.SetActive(true);
        if (final) RaiseExitAboveShield();
    }

    private void RaiseExitAboveShield()
    {
        if (exitSortingActive || inputShield == null) return;
        var exit = System.Array.Find(panels[panels.Length - 1].GetComponentsInChildren<Button>(true),
            button => button.name == "BTN_HQLobby_Exit");
        var shieldCanvas = inputShield.GetComponentInParent<Canvas>();
        if (exit == null || shieldCanvas == null)
        {
            Debug.LogError("마지막 협회 안내의 나가기 버튼 또는 입력 차단 Canvas가 누락되었습니다.", this);
            return;
        }
        if (exitCanvas == null)
        {
            exitCanvas = exit.GetComponent<Canvas>();
            addedExitCanvas = exitCanvas == null;
            if (addedExitCanvas) exitCanvas = exit.gameObject.AddComponent<Canvas>();
            exitOriginalOverride = exitCanvas.overrideSorting;
            exitOriginalOrder = exitCanvas.sortingOrder;
            exitOriginalLayer = exitCanvas.sortingLayerID;
            exitOriginalCanvasEnabled = !addedExitCanvas && exitCanvas.enabled;
            exitRaycaster = exit.GetComponent<GraphicRaycaster>();
            addedExitRaycaster = exitRaycaster == null;
            if (addedExitRaycaster) exitRaycaster = exit.gameObject.AddComponent<GraphicRaycaster>();
            exitOriginalRaycasterEnabled = !addedExitRaycaster && exitRaycaster.enabled;
        }
        exitCanvas.enabled = true;
        exitCanvas.overrideSorting = true;
        exitCanvas.sortingLayerID = shieldCanvas.sortingLayerID;
        exitCanvas.sortingOrder = shieldCanvas.sortingOrder + 1;
        exitRaycaster.enabled = true;
        exitSortingActive = true;
    }

    private void RestoreExitSorting()
    {
        if (!exitSortingActive) return;
        // Retain temporary components so a same-frame re-enable cannot reuse objects pending destruction.
        if (exitRaycaster != null) exitRaycaster.enabled = exitOriginalRaycasterEnabled;
        if (exitCanvas != null)
        {
            exitCanvas.overrideSorting = exitOriginalOverride;
            exitCanvas.sortingOrder = exitOriginalOrder;
            exitCanvas.sortingLayerID = exitOriginalLayer;
            exitCanvas.enabled = exitOriginalCanvasEnabled;
        }
        exitSortingActive = false;
    }

    private void OnDisable()
    {
        RestoreExitSorting();
        if (continueButton != null) continueButton.onClick.RemoveListener(Advance);
        if (shieldButton != null) shieldButton.onClick.RemoveListener(Advance);
        if (!initialized) return;
        if (cardBackground != null) cardBackground.SetOpaqueFill(false);
        if (view != null) { view.SetSideLayout(null); view.SetVisible(false); }
        if (inputShield != null) inputShield.SetActive(false);
        if (progression != null) progression.enabled = wasProgressionEnabled;
        if (legacyMessageRoots != null)
            foreach (var root in legacyMessageRoots)
                if (root != null) root.SetActive(true);
        for (int i = 0; i < panels.Length; i++)
        {
            var background = panels[i] != null ? panels[i].GetComponent<Image>() : null;
            if (background != null) background.enabled = originalBackgroundEnabled[i];
        }
    }
}
