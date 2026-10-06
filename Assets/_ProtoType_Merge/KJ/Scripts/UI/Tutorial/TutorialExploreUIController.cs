using UnityEngine;

/// <summary>번호로 설명 패널을 열고, 클릭하면 다음 패널로 교체한다.</summary>
[DisallowMultipleComponent]
public sealed class TutorialExploreUIController : MonoBehaviour
{
    [Tooltip("설명 패널을 표시할 순서대로 등록합니다. 번호는 0부터 시작합니다.")]
    [SerializeField] private GameObject[] panels = System.Array.Empty<GameObject>();
    [SerializeField] private UnityEngine.UI.Button nextButton;
    [Header("첫 전투 후 홍보 도입")]
    [Tooltip("기존 탐사 메시지박스·강조·입력 차단을 재사용할 안내입니다. 비어 있으면 기존 패널 순서를 사용합니다.")]
    [SerializeField] private JC.Tutorial.JcTutorialExploreGuide publicityEntryGuide;
    [Tooltip("우측 하단 HUD에 있는 실제 홍보 버튼입니다. 안내 패널 안의 예시 버튼 대신 연결하며 기존 클릭 이벤트를 유지합니다.")]
    [SerializeField] private UnityEngine.UI.Button publicityEntryButton;
    [TextArea, Tooltip("첫 전투 승리 후 표시할 홍보 기능 설명입니다. 화면을 클릭하면 버튼 안내로 넘어갑니다.")]
    [SerializeField] private string publicityIntroMessage = "홍보 기능에 대해 알아봅시다.\n홍보는 자금을 사용해 영웅의 영향력(IP)을 높이는 기능입니다.";
    [Range(0, 1), Tooltip("홍보 도입 두 단계의 배경 디밍 농도입니다. 0이면 투명, 1이면 완전히 가리며 안내 종료 후 기존 농도로 복원합니다.")]
    [SerializeField] private float publicityDimAlpha = .55f;
    private TutorialPublicityExplanationView publicityExplanation;

    private enum EntryStage { None, Introduction, Button, Opened }
    private EntryStage entryStage;
    private bool entryAwaitingRelease, originalEntryButtonInteractable, originalPanelRaycasts;
    private RectTransform originalPointerTarget;
    private UnityEngine.UI.Image entryDimImage;
    private Color originalDimColor;
    private int entryOpenedFrame;

    public int CurrentPanelIndex { get; private set; } = -1;
    public bool IsShowing => CurrentPanelIndex >= 0 || entryStage != EntryStage.None;
    public static bool BlocksSystemMenuEscape
    {
        get
        {
            foreach (var tutorial in FindObjectsByType<TutorialExploreUIController>(FindObjectsSortMode.None))
                if (tutorial.IsShowing) return true;
            return false;
        }
    }
    private int lastChangeFrame = -1;
    private bool initialized;
    private const string PublicityIntroSeenKey = "KJ.Tutorial.Publicity.AfterFirstVictory";
    private CombatContext victoryReturnContext;

    private void Awake()
    {
        Initialize();
        // 결과 처리기가 복귀 후 컨텍스트를 비우기 전에 이번 전투의 승리를 기억한다.
        var context = CombatContext.Instance;
        if (gameObject.scene.name == "TutorialExploreScene" && context != null &&
            context.IsTutorial && context.Result == CombatResult.Victory &&
            context.ReturnSceneName == gameObject.scene.name)
            victoryReturnContext = context;
    }

    private System.Collections.IEnumerator Start()
    {
        if (victoryReturnContext == null) yield break;
        var repository = TutorialProgressRepository.EnsureInstance();
        if (repository == null || repository.IsMessageSeen(PublicityIntroSeenKey)) yield break;

        // DH 결과 처리기의 상태 복원 및 ClearTutorial 완료를 기다린다.
        while (victoryReturnContext != null && victoryReturnContext.IsTutorial)
            yield return null;
        yield return null;

        if (repository == null || repository.LastCombatResult != CombatResult.Victory ||
            repository.IsMessageSeen(PublicityIntroSeenKey)) yield break;
        if (!IsShowing) BeginPublicityIntroduction();
        victoryReturnContext = null;
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 디버그 재실행: 홍보 안내의 시작 상태를 준비하고 첫 안내부터 표시한다.
        if (Input.GetKeyDown(KeyCode.F6)) BeginPublicityIntroduction();
#endif
        RefreshPublicityEntry(Input.GetMouseButton(0), Input.GetMouseButtonDown(0));
    }

    private void RefreshPublicityEntry(bool pointerHeld, bool clicked)
    {
        if (entryStage == EntryStage.None || Time.frameCount == entryOpenedFrame) return;
        if (entryAwaitingRelease)
        {
            if (pointerHeld) return;
            entryAwaitingRelease = false;
            if (entryStage == EntryStage.Button) publicityEntryButton.interactable = true;
            return;
        }
        if (entryStage == EntryStage.Introduction &&
            ModalManager.Top == publicityEntryGuide.explanationShield &&
            JcPointerInput.CanControl && JcPointerInput.Inside && clicked)
        {
            entryStage = EntryStage.Button;
            entryAwaitingRelease = true;
            publicityExplanation.ShowEntryTarget(publicityEntryButton.gameObject, publicityEntryGuide.explanationShield);
            ShowEntryMessage("홍보 버튼을 클릭하세요.", publicityEntryButton.transform as RectTransform);
        }
        else if (entryStage == EntryStage.Opened && !pointerHeld)
        {
            // 버튼의 기존 이벤트가 실제 홍보 창을 연 뒤에만 기존 설명으로 넘긴다.
            if (!publicityExplanation.IsPublicityModalOpen) return;
            ShowPanel(1); // 기존 0번은 예시 홍보 버튼 안내이므로 실제 버튼 안내로 대체한다.
        }
    }

    public void BeginPublicityIntroduction()
    {
        Initialize();
        if (publicityEntryGuide == null || publicityEntryButton == null || publicityExplanation == null ||
            publicityEntryGuide.view == null || publicityEntryGuide.explanationShield == null)
        {
            ShowPanel(0);
            return;
        }
        HideTutorial();
        publicityEntryGuide.SetExternalPresentation(true);
        originalPointerTarget = publicityEntryGuide.view.centeredPointerTarget;
        originalPanelRaycasts = publicityEntryGuide.view.blockPanelRaycasts;
        originalEntryButtonInteractable = publicityEntryButton.interactable;
        entryDimImage = publicityEntryGuide.explanationShield.GetComponent<UnityEngine.UI.Image>();
        if (entryDimImage != null)
        {
            originalDimColor = entryDimImage.color;
            var color = originalDimColor; color.a = publicityDimAlpha; entryDimImage.color = color;
        }
        publicityEntryButton.interactable = false;
        publicityEntryButton.onClick.AddListener(OnPublicityEntryClicked);
        entryStage = EntryStage.Introduction;
        entryOpenedFrame = Time.frameCount;
        entryAwaitingRelease = true;
        ShowEntryMessage(publicityIntroMessage, null);
    }

    private void ShowEntryMessage(string message, RectTransform target)
    {
        var guide = publicityEntryGuide;
        guide.explanationShield.SetActive(true);
        if (guide.continueButton != null) guide.continueButton.gameObject.SetActive(false);
        guide.view.blockPanelRaycasts = false;
        guide.view.centeredPointerTarget = target;
        guide.view.SetContent("탐사 · 홍보", message);
        // 도입용 디밍과 대상 승격은 기존 경로를 유지하고 큰 분절 화살표만 제외한다.
        guide.view.SetVisible(true, target, JC.Tutorial.JcTutorialGuideView.FocusPresentation.BorderOnly);
    }

    private void OnPublicityEntryClicked()
    {
        if (entryStage == EntryStage.Button && !entryAwaitingRelease)
            entryStage = EntryStage.Opened;
    }

    private void EndPublicityEntry()
    {
        if (entryStage == EntryStage.None) return;
        entryStage = EntryStage.None;
        entryAwaitingRelease = false;
        if (publicityEntryButton != null)
        {
            publicityEntryButton.onClick.RemoveListener(OnPublicityEntryClicked);
            publicityEntryButton.interactable = originalEntryButtonInteractable;
        }
        if (publicityExplanation != null) publicityExplanation.Hide();
        if (entryDimImage != null) entryDimImage.color = originalDimColor;
        entryDimImage = null;
        if (publicityEntryGuide != null)
        {
            if (publicityEntryGuide.view != null)
            {
                publicityEntryGuide.view.centeredPointerTarget = originalPointerTarget;
                publicityEntryGuide.view.blockPanelRaycasts = originalPanelRaycasts;
            }
            publicityEntryGuide.SetExternalPresentation(false);
        }
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        publicityExplanation = GetComponent<TutorialPublicityExplanationView>();
        HideTutorial();
        if (nextButton != null) nextButton.onClick.AddListener(ShowNextPanel);
    }

    private void OnDestroy()
    {
        EndPublicityEntry();
        if (nextButton != null) nextButton.onClick.RemoveListener(ShowNextPanel);
    }

    private void OnDisable() => HideTutorial();

    /// <summary>외부 이벤트나 Button.onClick에서 원하는 패널 번호를 전달한다.</summary>
    public void ShowPanel(int index)
    {
        Initialize();
        if (index < 0 || index >= panels.Length || !IsValidPanel(panels[index]))
        {
            Debug.LogWarning($"설명 패널 번호 또는 연결을 확인해주세요: {index}", this);
            return;
        }
        HidePanels();
        CurrentPanelIndex = index;
        // 설명 패널이 버튼의 자식일 수 있으므로 오브젝트는 유지하고 클릭만 막는다.
        bool requiresAction = publicityExplanation != null && publicityExplanation.RequiresAction(index);
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(true);
            nextButton.interactable = !requiresAction;
        }
        panels[index].SetActive(true);
        if (publicityExplanation != null) publicityExplanation.ShowStep(index, panels[index]);
    }

    /// <summary>다음 설명을 표시한다. 마지막 패널 다음에는 닫는다.</summary>
    public void ShowNextPanel()
    {
        if (entryStage != EntryStage.None) return;
        if (!IsShowing || lastChangeFrame == Time.frameCount) return;
        if (publicityExplanation != null && CurrentPanelIndex > 0 && !publicityExplanation.CanAdvance) return;
        int next = CurrentPanelIndex + 1;
        while (next < panels.Length && !IsValidPanel(panels[next])) next++;
        if (next < panels.Length) ShowPanel(next);
        else
        {
            TutorialProgressRepository.EnsureInstance()?.MarkMessageSeen(PublicityIntroSeenKey);
            HideTutorial();
        }
    }

    public void HideTutorial()
    {
        HidePanels();
        if (publicityExplanation != null) publicityExplanation.EndPractice();
    }

    private void HidePanels()
    {
        EndPublicityEntry();
        if (publicityExplanation != null) publicityExplanation.Hide();
        foreach (GameObject panel in panels)
            if (IsValidPanel(panel)) panel.SetActive(false);
        if (nextButton != null && !transform.IsChildOf(nextButton.transform))
            nextButton.gameObject.SetActive(false);
        CurrentPanelIndex = -1;
        lastChangeFrame = Time.frameCount;
    }

    private bool IsValidPanel(GameObject panel) => panel != null &&
        !transform.IsChildOf(panel.transform) &&
        (nextButton == null || panel != nextButton.gameObject);
}
