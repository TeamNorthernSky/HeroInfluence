using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [KJ 260714] 채팅 모달 컨트롤러 (ChatModal.prefab 루트). 프리팹 instantiate 방식 (DDOL 아님 — 대화는 원자적 세션).
/// Canvas(Overlay, order 500) + Modal(pausesGame) 로 씬 캔버스와 독립 (SaveRetryModal 패턴 승계).
/// 진행: ChatRunner가 데이터 순회, 컨트롤러는 말풍선/선택지 표시와 입력만 담당.
/// 입력: 화면 클릭 = 다음 대사 / 선택지 표시 중엔 버튼만.
/// </summary>
[DisallowMultipleComponent]
public class ChatModalController : MonoBehaviour
{
    private const string PrefabPath = "UI/ChatModal"; // Assets/Resources/UI/ChatModal.prefab

    [Header("레이아웃 참조")]
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("말풍선이 세로로 쌓이는 ScrollView 내부 Content.")]
    [SerializeField] private RectTransform contentRoot;
    [Tooltip("선택지 버튼이 쌓이는 영역.")]
    [SerializeField] private RectTransform choiceArea;

    [Header("하위 프리팹")]
    [SerializeField] private ChatBubbleView bubblePrefab;
    [SerializeField] private ChoiceButtonView choiceButtonPrefab;

    [Header("스킵 [KJ 260715]")]
    [SerializeField] private Button skipButton;
    [Tooltip("'스킵하시겠습니까?' 확인 팝업 루트. 평소 비활성.")]
    [SerializeField] private GameObject skipConfirmPopup;
    [SerializeField] private Button skipYesButton; // 예 → 채팅 패널 전체 닫기
    [SerializeField] private Button skipNoButton;  // 아니오 → 팝업만 닫기

    private static ChatModalController current;

    private ChatRunner runner;
    private readonly List<GameObject> activeChoices = new List<GameObject>();
    private bool choicesVisible;
    private int beginFrame; // 소환 당시 클릭이 첫 대사를 즉시 넘기는 것 방지

    /// <summary>대화 소환. 이미 떠 있으면 재활성 후 해당 대화로 재시작(중복 생성 방지).</summary>
    public static void Show(int startChatId)
    {
        if (current != null)
        {
            if (!current.gameObject.activeSelf) current.gameObject.SetActive(true);
            current.Begin(startChatId);
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[ChatModalController] 프리팹 없음: Resources/{PrefabPath}");
            return;
        }

        GameObject go = Instantiate(prefab);
        current = go.GetComponent<ChatModalController>();
        if (current != null) current.Begin(startChatId);
    }

    private void Awake()
    {
        if (skipButton != null) skipButton.onClick.AddListener(OpenSkipConfirm);
        if (skipYesButton != null) skipYesButton.onClick.AddListener(Close);            // 예: 대화 종료
        if (skipNoButton != null) skipNoButton.onClick.AddListener(CloseSkipConfirm);   // 아니오: 팝업만
        if (skipConfirmPopup != null) skipConfirmPopup.SetActive(false);
    }

    private void OpenSkipConfirm()
    {
        if (skipConfirmPopup != null) skipConfirmPopup.SetActive(true);
    }

    private void CloseSkipConfirm()
    {
        if (skipConfirmPopup != null) skipConfirmPopup.SetActive(false);
    }

    private void Begin(int startChatId)
    {
        ClearBubbles();
        ClearChoices();

        runner = new ChatRunner(ChatDatabase.Instance);
        beginFrame = Time.frameCount;

        if (!runner.Start(startChatId))
        {
            Debug.LogWarning($"[ChatModalController] 시작 Chat_ID {startChatId} 미존재 — 대화 취소");
            Close();
            return;
        }

        ShowCurrentNode();
    }

    /// <summary>현재 노드 표시: 말풍선 생성 → 분기 노드면 선택지(또는 자동 분기), 아니면 클릭 대기.</summary>
    private void ShowCurrentNode()
    {
        ChatNode node = runner.Current;
        if (node == null)
        {
            Close();
            return;
        }

        SpawnBubble(node);

        List<BranchOption> options = runner.ResolveOptions();
        if (options.Count == 0) return; // 분기 없음 — Update의 클릭 대기

        // 전부 빈 텍스트 = 자동 진행 분기(버튼 없이 조건이 경로 결정, 시트 관찰 기반 규칙)
        bool allEmpty = true;
        for (int i = 0; i < options.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(options[i].Text)) { allEmpty = false; break; }
        }

        if (allEmpty) OnChoice(options[0]);
        else ShowChoices(options);
    }

    private void Update()
    {
        if (runner == null || choicesVisible) return;
        if (skipConfirmPopup != null && skipConfirmPopup.activeSelf) return; // 스킵 확인 중엔 진행 정지
        if (!Input.GetMouseButtonDown(0)) return;
        if (Time.frameCount == beginFrame) return; // 트리거를 누른 그 클릭은 무시
        if (IsPointerOverButton()) return; // Skip 등 버튼 클릭은 대사 진행으로 취급하지 않음

        if (runner.AdvanceLinear()) ShowCurrentNode();
        else Close();
    }

    /// <summary>클릭 지점이 Button 위인지 — 버튼 클릭과 "화면 클릭=다음 대사"의 이중 반응 방지.</summary>
    private static bool IsPointerOverButton()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        var pointer = new PointerEventData(es) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        es.RaycastAll(pointer, results);
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject != null &&
                results[i].gameObject.GetComponentInParent<Button>() != null)
                return true;
        }
        return false;
    }

    private void ShowChoices(List<BranchOption> options)
    {
        choicesVisible = true;
        for (int i = 0; i < options.Count; i++)
        {
            BranchOption option = options[i];
            ChoiceButtonView view = Instantiate(choiceButtonPrefab, choiceArea);
            view.Bind(option.Text, () => OnChoice(option));
            activeChoices.Add(view.gameObject);
        }
    }

    private void OnChoice(BranchOption option)
    {
        ClearChoices();
        if (runner.Choose(option)) ShowCurrentNode();
        else Close();
    }

    private void SpawnBubble(ChatNode node)
    {
        if (bubblePrefab == null || contentRoot == null) return;
        ChatBubbleView bubble = Instantiate(bubblePrefab, contentRoot);
        bubble.Bind(node.CharName, node.Message, node.Type, node.CharProfile);
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null; // 레이아웃 계산 후 (timeScale=0에서도 프레임은 진행)
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void ClearBubbles()
    {
        if (contentRoot == null) return;
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);
    }

    private void ClearChoices()
    {
        choicesVisible = false;
        for (int i = 0; i < activeChoices.Count; i++)
        {
            if (activeChoices[i] != null) Destroy(activeChoices[i]);
        }
        activeChoices.Clear();
    }

    private void Close()
    {
        runner = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }
}
