using UnityEngine;

/// <summary>번호로 설명 패널을 열고, 클릭하면 다음 패널로 교체한다.</summary>
[DisallowMultipleComponent]
public sealed class TutorialExploreUIController : MonoBehaviour
{
    [Tooltip("설명 패널을 표시할 순서대로 등록합니다. 번호는 0부터 시작합니다.")]
    [SerializeField] private GameObject[] panels = System.Array.Empty<GameObject>();
    [SerializeField] private UnityEngine.UI.Button nextButton;
    private TutorialPublicityExplanationView publicityExplanation;

    public int CurrentPanelIndex { get; private set; } = -1;
    public bool IsShowing => CurrentPanelIndex >= 0;
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
        if (!IsShowing) ShowPanel(0);
        if (IsShowing) repository.MarkMessageSeen(PublicityIntroSeenKey);
        victoryReturnContext = null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        // 디버그 재실행: 홍보 안내의 시작 상태를 준비하고 첫 안내부터 표시한다.
        if (Input.GetKeyDown(KeyCode.F6)) ShowPanel(0);
    }
#endif

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
        if (nextButton != null) nextButton.onClick.RemoveListener(ShowNextPanel);
    }

    /// <summary>외부 이벤트나 Button.onClick에서 원하는 패널 번호를 전달한다.</summary>
    public void ShowPanel(int index)
    {
        Initialize();
        if (index < 0 || index >= panels.Length || !IsValidPanel(panels[index]))
        {
            Debug.LogWarning($"설명 패널 번호 또는 연결을 확인해주세요: {index}", this);
            return;
        }
        HideTutorial();
        CurrentPanelIndex = index;
        // 설명 패널이 버튼의 자식일 수 있으므로 오브젝트는 유지하고 클릭만 막는다.
        bool requiresAction = publicityExplanation != null && publicityExplanation.RequiresAction(index);
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(true);
            nextButton.interactable = !requiresAction;
        }
        panels[index].SetActive(true);
        if (publicityExplanation != null) publicityExplanation.ShowStep(index);
    }

    /// <summary>다음 설명을 표시한다. 마지막 패널 다음에는 닫는다.</summary>
    public void ShowNextPanel()
    {
        if (!IsShowing || lastChangeFrame == Time.frameCount) return;
        int next = CurrentPanelIndex + 1;
        while (next < panels.Length && !IsValidPanel(panels[next])) next++;
        if (next < panels.Length) ShowPanel(next);
        else HideTutorial();
    }

    public void HideTutorial()
    {
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
