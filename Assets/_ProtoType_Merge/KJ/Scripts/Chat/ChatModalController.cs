using System;
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
    private const string PrefabPath = "UI_Prefab/Chatting_System/ChatModal"; // Assets/Resources/UI_Prefab/Chatting_System/ChatModal.prefab

    [Header("레이아웃 참조")]
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("말풍선이 세로로 쌓이는 ScrollView 내부 Content.")]
    [SerializeField] private RectTransform contentRoot;
    [Tooltip("선택지 영역의 동적 높이 + 말풍선 밀어내기 애니메이션 담당.")]
    [SerializeField] private ChatChoicePanelLayout choiceLayout;

    [Header("하위 프리팹")]
    [SerializeField] private ChatBubbleView bubblePrefab;
    [SerializeField] private ChoiceButtonView choiceButtonPrefab;

    [Header("스킵")]
    [SerializeField] private Button skipButton;
    [Tooltip("'스킵하시겠습니까?' 확인 팝업 루트. 평소 비활성.")]
    [SerializeField] private GameObject skipConfirmPopup;
    [SerializeField] private Button skipYesButton; // 예 → 선택지/마지막 대사까지 자동 진행 [KJ 260723]
    [SerializeField] private Button skipNoButton;  // 아니오 → 팝업만 닫기

    private static ChatModalController current;

    private ChatManager manager;
    private Action onClosed;
    private readonly List<GameObject> activeChoices = new List<GameObject>();
    private bool choicesVisible;
    private int beginFrame; // 소환 당시 클릭이 첫 대사를 즉시 넘기는 것 방지

    /// <summary>대화 소환. 이미 떠 있으면 재활성 후 해당 대화로 재시작(중복 생성 방지).</summary>
    private bool isClosing;

    public static void Show(int startChatId)
    {
        Show(ResolveDefaultZoneId(startChatId), startChatId, null);
    }

    public static void Show(int zoneId, int startChatId)
    {
        Show(zoneId, startChatId, null);
    }

    public static void Show(int zoneId, int startChatId, Action onClosed)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ChatModalController] Chat modal can only be opened in Play Mode.");
            return;
        }

        if (current == null)
        {
            current = FindFirstObjectByType<ChatModalController>();
        }

        if (current != null)
        {
            if (!current.gameObject.activeSelf) current.gameObject.SetActive(true);
            current.Begin(zoneId, startChatId, onClosed);
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
        if (current != null)
        {
            current.Begin(zoneId, startChatId, onClosed);
        }
        else
        {
            Debug.LogWarning("[ChatModalController] ChatModal prefab has no ChatModalController component.");
            Destroy(go);
        }
    }

    private void Awake()
    {
        if (current != null && current != this)
        {
            Destroy(gameObject);
            return;
        }

        current = this;

        if (skipButton != null) skipButton.onClick.AddListener(OpenSkipConfirm);
        if (skipYesButton != null) skipYesButton.onClick.AddListener(SkipChat);         // 예: 선택지/끝까지 자동 진행 [KJ 260723]
        if (skipNoButton != null) skipNoButton.onClick.AddListener(CloseSkipConfirm);   // 아니오: 팝업만
        if (skipConfirmPopup != null) skipConfirmPopup.SetActive(false);
    }

    /// <summary>
    /// [KJ 260729] ESC 처리 — ChatModal이 열려 있는 동안 ESC로는 대화가 닫히지 않는다.
    /// 스킵 확인 팝업이 떠 있으면 팝업만 닫고(타이틀 종료팝업과 동일), 아니면 스킵 확인 팝업을 연다.
    /// 처리했으면 true — 상위 ESC 로직(ModalManager.CloseTop / 시스템 메뉴)을 막는다.
    /// </summary>
    public static bool HandleEscape()
    {
        if (current == null || !current.gameObject.activeInHierarchy) return false;
        if (ModalManager.Top != current.gameObject) return false; // 채팅 위에 다른 모달이 있으면 그쪽이 우선

        if (current.skipConfirmPopup != null && current.skipConfirmPopup.activeSelf)
        {
            current.CloseSkipConfirm();
            return true;
        }

        // 마지막 대사/선택지 표시 중(스킵 불가)이면 팝업을 열지 않되, ESC는 소비해 대화가 닫히지 않게 한다.
        if (current.skipButton == null || current.skipButton.interactable)
            current.OpenSkipConfirm();
        return true;
    }

    private void OpenSkipConfirm()
    {
        if (skipConfirmPopup != null) skipConfirmPopup.SetActive(true);
    }

    private void CloseSkipConfirm()
    {
        if (skipConfirmPopup != null) skipConfirmPopup.SetActive(false);
    }

    /// <summary>
    /// [KJ 260723] 스킵 확정: 창을 닫지 않고 선택지 또는 마지막 대사까지 자동 진행.
    /// 지나간 대사는 말풍선으로 모두 쌓이고(OnChatShown 경유), 선택지 도달 시 선택 대기.
    /// </summary>
    private void SkipChat()
    {
        CloseSkipConfirm();
        manager?.SkipToEnd();
    }

    /// <summary>[KJ 260723] 마지막 대사이거나 선택지 표시 중이면 스킵 버튼 비활성화 (매 노드 표시 후 호출).</summary>
    private void RefreshSkipButtonState()
    {
        if (skipButton == null) return;
        bool blocked = choicesVisible || (manager != null && manager.IsAtFinalChat);
        skipButton.interactable = !blocked;
    }

    private void Begin(int zoneId, int startChatId, Action closedCallback)
    {
        ClearBubbles();
        ClearChoices();
        CloseSkipConfirm();
        if (choiceLayout != null) choiceLayout.ApplyHiddenImmediate();
        UnsubscribeFromManager();

        onClosed = closedCallback;
        isClosing = false;
        beginFrame = Time.frameCount;
        manager = ChatManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning($"[ChatModalController] 시작 Chat_ID {startChatId} 미존재 — 대화 취소");
            Close();
            return;
        }

        manager.OnChatShown += HandleChatShown;
        manager.OnBranchShown += HandleBranchShown;
        manager.OnChatEnded += HandleChatEnded;
        manager.StartChat(zoneId, startChatId);
    }

    private static int ResolveDefaultZoneId(int startChatId)
    {
        return startChatId >= 100000 ? startChatId / 100000 : 1;
    }

    /// <summary>현재 노드 표시: 말풍선 생성 → 분기 노드면 선택지(또는 자동 분기), 아니면 클릭 대기.</summary>
    private void HandleChatShown(DHEventChatTemplate chat)
    {
        if (chat == null)
        {
            Close();
            return;
        }

        SpawnBubble(chat);
        // 전부 빈 텍스트 = 자동 진행 분기(버튼 없이 조건이 경로 결정, 시트 관찰 기반 규칙)
    }

    private void Update()
    {
        if (manager == null || !manager.IsRunning || choicesVisible) return;
        if (skipConfirmPopup != null && skipConfirmPopup.activeSelf) return; // 스킵 확인 중엔 진행 정지
        if (!Input.GetMouseButtonDown(0)) return;
        if (Time.frameCount == beginFrame) return; // 트리거를 누른 그 클릭은 무시
        if (IsPointerOverButton()) return; // Skip 등 버튼 클릭은 대사 진행으로 취급하지 않음

        manager.Advance();
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

    private void HandleBranchShown(IReadOnlyList<ChatBranchOptionState> options)
    {
        ClearChoices();

        if (options == null || options.Count == 0)
        {
            if (choiceLayout != null) choiceLayout.PlayHide();
            RefreshSkipButtonState(); // 마지막 대사 도달 시 스킵 비활성 [KJ 260723]
            return;
        }

        choicesVisible = true;
        for (int i = 0; i < options.Count; i++)
        {
            ChatBranchOptionState state = options[i];
            DHEventBranchTemplate option = state?.Option;
            if (option == null || string.IsNullOrWhiteSpace(option.SelectionText))
            {
                continue;
            }

            ChoiceButtonView view = Instantiate(choiceButtonPrefab, choiceLayout.SpawnParent);
            view.Bind(option.SelectionText, () => manager?.Select(option), state.IsInteractable);
            activeChoices.Add(view.gameObject);
        }

        if (activeChoices.Count == 0)
        {
            choicesVisible = false;
        }

        // 스폰이 끝난 뒤에 호출해야 자연 높이가 정확히 측정된다. [KJ 260729]
        if (choiceLayout != null)
        {
            if (activeChoices.Count > 0) choiceLayout.PlayShow();
            else choiceLayout.PlayHide();
        }

        RefreshSkipButtonState(); // 선택지 표시 중에도 스킵 비활성 [KJ 260723]
    }

    private void HandleChatEnded()
    {
        Close();
    }

    private void SpawnBubble(DHEventChatTemplate chat)
    {
        if (bubblePrefab == null || contentRoot == null) return;
        ChatBubbleView bubble = Instantiate(bubblePrefab, contentRoot);
        bubble.Bind(chat.CharacterName, chat.MessageText, chat.ChatType, chat.CharacterProfile);
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
            if (activeChoices[i] == null) continue;

            // Destroy는 프레임 끝에 처리된다. 같은 프레임에 새 버튼을 스폰하고
            // ForceRebuildLayoutImmediate로 측정하면 파괴 예정인 옛 버튼까지 세게 되므로
            // 계층에서 먼저 떼어낸다. [KJ 260729]
            activeChoices[i].transform.SetParent(null, false);
            Destroy(activeChoices[i]);
        }
        activeChoices.Clear();
    }

    private void Close()
    {
        if (isClosing)
        {
            return;
        }

        isClosing = true;

        if (manager != null && manager.IsRunning)
        {
            manager.EndChat();
        }

        Action callback = onClosed;
        onClosed = null;

        UnsubscribeFromManager();
        manager = null;
        callback?.Invoke();
        Destroy(gameObject);
    }

    private void UnsubscribeFromManager()
    {
        if (manager == null)
        {
            return;
        }

        manager.OnChatShown -= HandleChatShown;
        manager.OnBranchShown -= HandleBranchShown;
        manager.OnChatEnded -= HandleChatEnded;
    }

    private void OnDestroy()
    {
        UnsubscribeFromManager();
        if (current == this) current = null;
    }
}
