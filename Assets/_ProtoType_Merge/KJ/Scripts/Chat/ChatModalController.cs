using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrimeTween;

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
    [SerializeField] private ChatRewardResultView rewardResultPrefab;

    [Header("스킵")]
    [SerializeField] private Button skipButton;
    [Tooltip("'스킵하시겠습니까?' 확인 팝업 루트. 평소 비활성.")]
    [SerializeField] private GameObject skipConfirmPopup;
    [SerializeField] private Button skipYesButton; // 예 → 선택지/마지막 대사까지 자동 진행 [KJ 260723]
    [SerializeField] private Button skipNoButton;  // 아니오 → 팝업만 닫기

    private static ChatModalController current;

    // 대사가 끝난 뒤 보상 브리핑을 보고 있는 시간도 포함한다.
    public static bool IsOpen => current != null && current.gameObject.activeInHierarchy;

    private ChatManager manager;
    private Action onClosed;
    private readonly List<GameObject> activeChoices = new List<GameObject>();
    private bool choicesVisible;
    private int beginFrame; // 소환 당시 클릭이 첫 대사를 즉시 넘기는 것 방지
    private int skipConfirmClosedFrame = -1; // 팝업을 닫은 입력의 같은 프레임 대사 진행 차단
    private int choiceSelectedFrame = -1;
    private bool waitForSkipInputRelease; // 스킵 팝업을 닫은 키/클릭이 아직 눌려 있는 동안 대사 진행 차단
    private DHEventEffectRuntimeManager effectManager;
    private bool dispatchingChatEffect;
    private bool hasRewardResult;
    private bool waitingForRewardClose;
    private ChoiceButtonView briefingEndButton;
    private int briefingEndShownFrame = -1;
    private RectTransform entranceRoot;
    private CanvasGroup entranceGroup;
    private Tween entranceTween;
    private ChatBubbleView previousSpeakerBubble;
    private Coroutine skipRoutine;
    private bool skipping;

    /// <summary>대화 소환. 이미 떠 있으면 재활성 후 해당 대화로 재시작(중복 생성 방지).</summary>
    private bool isClosing;
    private bool closeFinished;

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
            if (current.isClosing)
            {
                current.onClosed += () => Show(zoneId, startChatId, onClosed);
                return;
            }
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
        PrepareEntrance();
    }

    private void PrepareEntrance()
    {
        if (entranceRoot != null) return;
        var children = new List<Transform>();
        foreach (Transform child in transform)
            if (child.name != "Dim Panel" && (skipConfirmPopup == null || child.gameObject != skipConfirmPopup))
                children.Add(child);

        var host = new GameObject("ChatEntrance", typeof(RectTransform), typeof(CanvasGroup));
        host.layer = gameObject.layer;
        entranceRoot = (RectTransform)host.transform;
        entranceRoot.SetParent(transform, false);
        entranceRoot.anchorMin = Vector2.zero;
        entranceRoot.anchorMax = Vector2.one;
        entranceRoot.offsetMin = entranceRoot.offsetMax = Vector2.zero;
        foreach (var child in children) child.SetParent(entranceRoot, false);
        entranceGroup = host.GetComponent<CanvasGroup>();
        if (skipConfirmPopup != null) skipConfirmPopup.transform.SetAsLastSibling();
    }

    public bool IsEntrancePlaying => entranceTween.isAlive;

    private void PlayEntrance()
    {
        PrepareEntrance();
        if (entranceTween.isAlive) entranceTween.Stop();
        Canvas.ForceUpdateCanvases();
        float distance = Mathf.Max(((RectTransform)transform).rect.height, entranceRoot.rect.height) + 100f;
        entranceRoot.anchoredPosition = Vector2.down * distance;
        entranceGroup.interactable = false;
        entranceTween = Tween.Custom(this, distance, 0f, .3f,
            static (view, offset) => view.entranceRoot.anchoredPosition = Vector2.down * offset,
            Ease.OutCubic, useUnscaledTime: true);
        entranceTween.OnComplete(this, static view =>
        {
            view.entranceRoot.anchoredPosition = Vector2.zero;
            view.entranceGroup.interactable = true;
            view.beginFrame = Time.frameCount;
        });
    }

    private void OnDisable()
    {
        if (entranceTween.isAlive) entranceTween.Stop();
        if (entranceRoot != null) entranceRoot.anchoredPosition = Vector2.zero;
        if (entranceGroup != null) entranceGroup.interactable = true;
        if (isClosing && !closeFinished) FinishClose();
    }

    /// <summary>
    /// ESC 처리 — 스킵 확인 팝업이 떠 있으면 팝업만 닫는다.
    /// 마지막 대사의 ESC 진행은 Update에서 스페이스바와 동일하게 처리하고, 진행 중에는 스킵 확인 팝업을 연다.
    /// 처리했으면 true — 상위 ESC 로직(ModalManager.CloseTop / 시스템 메뉴)을 막는다.
    /// </summary>
    public static bool HandleEscape()
    {
        if (current == null || !current.gameObject.activeInHierarchy) return false;
        if (ModalManager.Top != current.gameObject) return false; // 채팅 위에 다른 모달이 있으면 그쪽이 우선
        if (current.isClosing || current.entranceTween.isAlive) return true;

        if (current.skipConfirmPopup != null && current.skipConfirmPopup.activeSelf)
        {
            current.CloseSkipConfirm();
            return true;
        }

        // 마지막 대사 진행/브리핑 종료는 Update의 공통 입력 가드를 사용한다.
        if (current.waitingForRewardClose || (current.manager != null && current.manager.IsAtFinalChat))
            return true;

        // 선택지 표시 중(스킵 불가)이면 팝업을 열지 않되, ESC는 소비한다.
        if (current.skipButton == null || current.skipButton.interactable)
            current.OpenSkipConfirm();
        return true;
    }

    private void OpenSkipConfirm()
    {
        if (isClosing || skipping) return;
        if (skipConfirmPopup != null) skipConfirmPopup.SetActive(true);
    }

    private void CloseSkipConfirm()
    {
        if (skipConfirmPopup == null || !skipConfirmPopup.activeSelf) return;
        skipConfirmClosedFrame = Time.frameCount;
        waitForSkipInputRelease = true;
        skipConfirmPopup.SetActive(false);
    }

    /// <summary>
    /// [KJ 260723] 스킵 확정: 창을 닫지 않고 선택지 또는 마지막 대사까지 자동 진행.
    /// 지나간 대사는 말풍선으로 모두 쌓이고(OnChatShown 경유), 선택지 도달 시 선택 대기.
    /// </summary>
    private void SkipChat()
    {
        CloseSkipConfirm();
        if (skipping || manager == null || !manager.IsRunning || isClosing) return;
        skipping = true;
        RefreshSkipButtonState();
        skipRoutine = StartCoroutine(PlaySkippedChatInOrder());
    }

    private IEnumerator PlaySkippedChatInOrder()
    {
        // Let the current bubble finish before advancing, even while the modal pauses the game.
        for (int step = 0; step < 1000; step++)
        {
            yield return new WaitForSecondsRealtime(.01f);
            if (isClosing || manager == null || !manager.IsRunning ||
                choicesVisible || manager.IsAtFinalChat) break;
            var previous = manager.CurrentChat;
            RunChatAction(() => manager.Advance());
            if (manager == null || !manager.IsRunning || choicesVisible ||
                manager.IsAtFinalChat || manager.CurrentChat == previous) break;
        }
        skipping = false;
        skipRoutine = null;
        RefreshSkipButtonState();
    }

    private void StopSkipping()
    {
        if (skipRoutine != null) StopCoroutine(skipRoutine);
        skipRoutine = null;
        skipping = false;
    }

    /// <summary>[KJ 260723] 마지막 대사이거나 선택지 표시 중이면 스킵 버튼 비활성화 (매 노드 표시 후 호출).</summary>
    private void RefreshSkipButtonState()
    {
        if (skipButton == null) return;
        bool blocked = skipping || choicesVisible || (manager != null && manager.IsAtFinalChat);
        skipButton.interactable = !blocked;
    }

    private void Begin(int zoneId, int startChatId, Action closedCallback)
    {
        StopSkipping();
        ClearBubbles();
        ClearChoices();
        CloseSkipConfirm();
        if (choiceLayout != null) choiceLayout.ApplyHiddenImmediate();
        UnsubscribeFromManager();

        // 이전 대화의 종료 이벤트가 새 모달의 Close/조우 취소 콜백을 실행하지 않도록
        // 새 콜백과 구독을 등록하기 전에 이전 세션을 끝냅니다.
        ChatManager nextManager = ChatManager.Instance;
        if (nextManager != null && nextManager.IsRunning)
            nextManager.EndChat();

        onClosed = closedCallback;
        isClosing = false;
        closeFinished = false;
        hasRewardResult = false;
        waitingForRewardClose = false;
        briefingEndButton = null;
        briefingEndShownFrame = -1;
        beginFrame = Time.frameCount;
        manager = nextManager;

        if (manager == null)
        {
            Debug.LogWarning($"[ChatModalController] 시작 Chat_ID {startChatId} 미존재 — 대화 취소");
            Close();
            return;
        }

        manager.OnChatShown += HandleChatShown;
        manager.OnBranchShown += HandleBranchShown;
        manager.OnChatEnded += HandleChatEnded;
        effectManager = DHEventEffectRuntimeManager.EnsureInstance();
        DHEventRewardRuntimeManager.EnsureInstance();
        effectManager.EffectDispatched += HandleRewardDispatched;
        PlayEntrance();
        RunChatAction(() => manager.StartChat(zoneId, startChatId));
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
        if (SceneFadeController.Instance != null && SceneFadeController.Instance.IsTransitioning) return;
        if (skipping) return;
        if (entranceTween.isAlive || Time.frameCount == beginFrame) return;
        if (isClosing || (!waitingForRewardClose && (manager == null || !manager.IsRunning))) return;
        if (skipConfirmPopup != null && skipConfirmPopup.activeSelf) return; // 스킵 확인 중엔 진행 정지
        // 팝업을 닫은 키/클릭을 뗄 때까지는 대사 진행 입력으로 받지 않는다 (실행 순서와 무관하게 차단)
        if (waitForSkipInputRelease)
        {
            if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)) return;
            waitForSkipInputRelease = false;
        }
        if (Time.frameCount == skipConfirmClosedFrame) return; // 확인/취소 입력은 채팅 진행에 재사용하지 않음
        if (Time.frameCount == choiceSelectedFrame) return;
        if (waitingForRewardClose)
        {
            if (Time.frameCount == briefingEndShownFrame || ModalManager.Top != gameObject) return;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                briefingEndButton?.ClickFromShortcut();
            return;
        }
        if (choicesVisible)
        {
            if (Time.frameCount == beginFrame || ModalManager.Top != gameObject) return;
            HandleChoiceShortcut();
            return;
        }
        if (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space) &&
            !(Input.GetKeyDown(KeyCode.Escape) && manager.IsAtFinalChat && ModalManager.Top == gameObject)) return;
        if (Time.frameCount == beginFrame) return; // 트리거를 누른 그 클릭은 무시
        if (IsPointerOverControl()) return; // 버튼·스크롤바 조작은 대사 진행으로 취급하지 않음

        RunChatAction(() => manager.Advance());
    }

    private void HandleChoiceShortcut()
    {
        // 표시된 순서대로 1~9에 대응한다. 비활성 선택지도 번호를 유지한다.
        int count = Mathf.Min(activeChoices.Count, 9);
        for (int i = 0; i < count; i++)
        {
            if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) &&
                !Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i))) continue;

            if (activeChoices[i] != null)
                activeChoices[i].GetComponent<ChoiceButtonView>()?.ClickFromShortcut();
            return; // 콜백이 다음 선택지를 만들어도 같은 프레임에는 한 번만 선택한다.
        }
    }

    /// <summary>클릭 지점이 Selectable 위인지 — 버튼·스크롤바(손잡이 포함)의 클릭과 대사 진행의 이중 반응 방지.</summary>
    private static bool IsPointerOverControl()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        var pointer = new PointerEventData(es) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        es.RaycastAll(pointer, results);
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject != null &&
                results[i].gameObject.GetComponentInParent<Selectable>() != null)
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
            view.Bind(option.SelectionText, () => SelectChoice(option, view), state.IsInteractable);
            view.SetIndex(activeChoices.Count + 1);
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
        if (isClosing || waitingForRewardClose) return;
        if (hasRewardResult && choiceLayout != null && choiceLayout.SpawnParent != null && choiceButtonPrefab != null)
        {
            ClearChoices();
            waitingForRewardClose = true;
            briefingEndShownFrame = Time.frameCount;
            briefingEndButton = Instantiate(choiceButtonPrefab, choiceLayout.SpawnParent);
            briefingEndButton.Bind("대화 종료", Close, true);
            activeChoices.Add(briefingEndButton.gameObject);
            choiceLayout.PlayShow();
            if (skipButton != null) skipButton.interactable = false;
            StartCoroutine(ScrollToBottomNextFrame());
            return;
        }
        Close();
    }

    private void SelectChoice(DHEventBranchTemplate option, ChoiceButtonView view)
    {
        if (!choicesVisible || isClosing || manager == null || !manager.IsRunning ||
            view == null || !activeChoices.Contains(view.gameObject) ||
            choiceSelectedFrame == Time.frameCount) return;

        choiceSelectedFrame = Time.frameCount;
        // Select가 다음 대사를 동기적으로 추가하므로 답변 기록을 먼저 넣는다.
        SpawnChoiceHistory(view.Text);
        ClearChoices();
        if (choiceLayout != null) choiceLayout.PlayHide();
        RunChatAction(() => manager.Select(option));
    }

    private ChatRewardResultView.ValueSnapshot rewardBefore;

    private void RunChatAction(Action action)
    {
        bool wasDispatching = dispatchingChatEffect;
        var previousSnapshot = rewardBefore;
        rewardBefore = ChatRewardResultView.ValueSnapshot.Capture();
        dispatchingChatEffect = true;
        try { action(); }
        finally { dispatchingChatEffect = wasDispatching; rewardBefore = previousSnapshot; }
    }

    private void HandleRewardDispatched(DHEventEffectToken token)
    {
        if (!dispatchingChatEffect || rewardResultPrefab == null || contentRoot == null) return;
        if (token == null || token.kind != DHEventEffectKind.Reward) return;
        if (DHEventRewardRuntimeManager.Instance == null || !DHEventRewardRuntimeManager.Instance.isActiveAndEnabled) return;
        if (!int.TryParse(token.payload, out int rewardId) || rewardId <= 0) return;
        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null || !catalog.TryGetEventRewardTemplate(rewardId, out DHEventRewardTemplate reward)) return;
        ChatRewardResultView result = Instantiate(rewardResultPrefab, contentRoot);
        previousSpeakerBubble = null;
        result.SetBeforeValues(rewardBefore);
        result.Bind(reward);
        hasRewardResult = true;
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private void SpawnChoiceHistory(string text)
    {
        previousSpeakerBubble = null;
        if (contentRoot == null || choiceButtonPrefab == null) return;

        Transform header = choiceLayout != null && choiceLayout.SpawnParent != null
            ? choiceLayout.SpawnParent.Find("AnswerHeader") : null;
        if (header != null)
        {
            GameObject historyHeader = Instantiate(header.gameObject, contentRoot, false);
            historyHeader.SetActive(true);
            foreach (Graphic graphic in historyHeader.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        ChoiceButtonView answer = Instantiate(choiceButtonPrefab, contentRoot);
        answer.BindHistory(text);
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private void SpawnBubble(DHEventChatTemplate chat)
    {
        if (bubblePrefab == null || contentRoot == null) return;
        ChatBubbleView bubble = Instantiate(bubblePrefab, contentRoot);
        bubble.Bind(chat.CharacterName, chat.MessageText, chat.ChatType, chat.CharacterProfile);
        if (bubble.HasSameSpeaker(previousSpeakerBubble)) bubble.JoinPreviousSpeaker(previousSpeakerBubble);
        previousSpeakerBubble = bubble;
        if (chat.ChatType != 2) StartCoroutine(PlayBubbleSideEntrance(bubble, chat.ChatType == 1));
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator PlayBubbleSideEntrance(ChatBubbleView bubble, bool fromRight)
    {
        var rect = bubble.EntranceRoot;
        if (rect == null) yield break;
        var group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        yield return null;
        if (bubble == null || rect == null || contentRoot == null) yield break;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)bubble.transform);
        // Reserve only the message box's space in the row. Portrait and name
        // stay visible while the box moves independently inside its slot.
        var slot = new GameObject("ChatBubbleSlot", typeof(RectTransform), typeof(LayoutElement));
        slot.layer = bubble.gameObject.layer;
        var slotRect = (RectTransform)slot.transform;
        slotRect.SetParent(rect.parent, false);
        slotRect.SetSiblingIndex(rect.GetSiblingIndex());
        var layout = slot.GetComponent<LayoutElement>();
        var boxLayout = rect.GetComponent<LayoutElement>();
        layout.minWidth = LayoutUtility.GetMinWidth(rect);
        layout.preferredWidth = LayoutUtility.GetPreferredWidth(rect);
        layout.flexibleWidth = LayoutUtility.GetFlexibleWidth(rect);
        layout.minHeight = layout.preferredHeight = Mathf.Max(rect.rect.height, LayoutUtility.GetPreferredHeight(rect));
        if (boxLayout != null) layout.layoutPriority = boxLayout.layoutPriority;
        rect.SetParent(slotRect, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        var effect = rect.GetComponent<BubbleRiseEffect>();
        if (effect == null) effect = rect.gameObject.AddComponent<BubbleRiseEffect>();
        effect.Configure(fromRight ? BubbleRiseEffect.EntranceStyle.RightPop : BubbleRiseEffect.EntranceStyle.LeftPop,
            .36f, 110f);
        effect.Play(Vector2.zero);
        LayoutRebuilder.MarkLayoutForRebuild(contentRoot);
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
        previousSpeakerBubble = null;
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
        StopSkipping();

        if (manager != null && manager.IsRunning)
        {
            manager.EndChat();
        }

        UnsubscribeFromManager();
        manager = null;
        if (!isActiveAndEnabled || entranceRoot == null)
        {
            FinishClose();
            return;
        }
        if (entranceTween.isAlive) entranceTween.Stop();
        entranceGroup.interactable = false;
        if (skipConfirmPopup != null) skipConfirmPopup.SetActive(false);
        float startY = entranceRoot.anchoredPosition.y;
        entranceTween = Tween.Custom(this, startY, 14f, .08f,
            static (view, y) => view.entranceRoot.anchoredPosition = Vector2.up * y,
            Ease.OutQuad, useUnscaledTime: true);
        entranceTween.OnComplete(this, static view => view.PlayExitDrop());
    }

    private void PlayExitDrop()
    {
        float distance = Mathf.Max(((RectTransform)transform).rect.height, entranceRoot.rect.height) + 100f;
        entranceTween = Tween.Custom(this, entranceRoot.anchoredPosition.y, -distance, .24f,
            static (view, y) => view.entranceRoot.anchoredPosition = Vector2.up * y,
            Ease.InCubic, useUnscaledTime: true);
        entranceTween.OnComplete(this, static view => view.FinishClose());
    }

    private void FinishClose()
    {
        if (closeFinished) return;
        closeFinished = true;
        Action callback = onClosed;
        onClosed = null;
        if (current == this) current = null;
        Destroy(gameObject);
        callback?.Invoke();
    }

    private void UnsubscribeFromManager()
    {
        if (effectManager != null)
            effectManager.EffectDispatched -= HandleRewardDispatched;
        effectManager = null;
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
