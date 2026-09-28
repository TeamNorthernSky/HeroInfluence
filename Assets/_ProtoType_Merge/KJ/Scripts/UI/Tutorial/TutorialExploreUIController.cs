using UnityEngine;

/// <summary>번호로 설명 패널을 열고, 클릭하면 다음 패널로 교체한다.</summary>
[DisallowMultipleComponent]
public sealed class TutorialExploreUIController : MonoBehaviour
{
    [Tooltip("설명 패널을 표시할 순서대로 등록합니다. 번호는 0부터 시작합니다.")]
    [SerializeField] private GameObject[] panels = System.Array.Empty<GameObject>();
    [SerializeField] private UnityEngine.UI.Button nextButton;

    public int CurrentPanelIndex { get; private set; } = -1;
    public bool IsShowing => CurrentPanelIndex >= 0;
    private int lastChangeFrame = -1;
    private bool initialized;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
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
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        panels[index].SetActive(true);
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
