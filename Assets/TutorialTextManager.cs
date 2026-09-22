using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class TutorialTextManager : MonoBehaviour
{
    [Tooltip("설명 패널 루트를 표시할 순서대로 등록합니다. 패널들은 같은 부모 아래에 배치하세요.")]
    [FormerlySerializedAs("textObjects")]
    [SerializeField] private GameObject[] panels = System.Array.Empty<GameObject>();

    [Tooltip("모든 기능을 활성화할 설명의 Panels 인덱스입니다. 0부터 시작하며 -1은 사용 안 함입니다.")]
    [Min(-1)]
    [SerializeField] private int activateAllFeaturesIndex = -1;
    [SerializeField] private TutorialLobbyUIActivator tutorialLobbyActivator;

    private readonly Queue<(int index, GameObject panel)> panelQueue = new Queue<(int, GameObject)>();
    private GameObject currentPanel;
    private int lastAdvanceFrame = -1;
    public int CurrentIndex { get; private set; } = -1;

    private void Start()
    {
        for (int index = 0; index < panels.Length; index++)
        {
            GameObject panel = panels[index];
            if (panel == null)
                continue;

            // 매니저 자신이나 부모를 끄면 다음 클릭을 받을 수 없다.
            if (transform.IsChildOf(panel.transform))
            {
                Debug.LogWarning("TutorialTextManager는 설명 패널 바깥에 배치해주세요.", this);
                continue;
            }

            panel.SetActive(false);
            panelQueue.Enqueue((index, panel));
        }

        ShowNextPanel();
    }

    // 기존 Button/UnityEvent 연결과 코드 호출을 유지한다.
    public void ShowNextText() => ShowNextPanel();

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            ShowNextPanel();
    }

    /// <summary>다음 패널을 표시한다. 다음 패널이 없으면 마지막 패널을 유지한다.</summary>
    public void ShowNextPanel()
    {
        // 동일 클릭에 Update와 UI 버튼 콜백이 함께 실행돼도 한 단계만 진행한다.
        if (lastAdvanceFrame == Time.frameCount)
            return;

        // 대기 중 파괴된 패널은 건너뛰고, 다음 패널이 없으면 현재 표시를 유지한다.
        while (panelQueue.Count > 0 && panelQueue.Peek().panel == null)
            panelQueue.Dequeue();
        if (panelQueue.Count == 0)
            return;

        lastAdvanceFrame = Time.frameCount;
        // 외부에서 켜진 패널도 함께 꺼서 현재 순서의 패널만 표시한다.
        foreach (var panel in panels)
            if (panel != null && !transform.IsChildOf(panel.transform))
                panel.SetActive(false);

        currentPanel = null;
        CurrentIndex = -1;
        while (panelQueue.Count > 0 && currentPanel == null)
        {
            var next = panelQueue.Dequeue();
            currentPanel = next.panel;
            if (currentPanel != null)
                CurrentIndex = next.index;
        }

        if (currentPanel != null)
        {
            currentPanel.SetActive(true);
            if (CurrentIndex == activateAllFeaturesIndex)
            {
                if (tutorialLobbyActivator != null)
                    tutorialLobbyActivator.ActivateAllFeatures();
                else
                    Debug.LogWarning("Tutorial Lobby Activator를 연결해주세요.", this);
            }
        }
    }
}
