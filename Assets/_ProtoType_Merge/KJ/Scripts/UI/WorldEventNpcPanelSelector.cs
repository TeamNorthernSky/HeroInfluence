using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

/// <summary>이벤트 ID의 카탈로그 종류에 따라 NPC 선택지 패널 하나만 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class WorldEventNpcPanelSelector : MonoBehaviour
{
    [SerializeField] private GameObject consumePanel;
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private GameObject choicePanel;

    private DHWorldEventRuntimeManager manager;
    private string displayedEventId;
    private DHWorldEventPresentationRequest current;
    [SerializeField] private Button consumeProceed, consumeDecline, rewardProceed, choiceDecline;
    
    private Button[] choiceButtons;
    private string[] choiceIds = new string[2];
    private string cancelId;
    private bool buttonsBound;
    private int inputFrame = -1;
    private Button resultClickArea;
    private DHWorldEventPresentationRequest lastConfirmRequest;
    private int resultShownFrame = -1;
    private int choicesShownFrame = -1;
    private DHWorldEventPresentationRequest lastShortcutRequest;
    private readonly List<Button> shortcutButtons = new List<Button>();
    private GameObject questList;
    private bool questListHidden;
    private string questEventId;
    private bool choiceSubmitted;
    private static int resultConfirmedFrame = -1;
    private readonly Dictionary<Button, NpcPreview> previews = new Dictionary<Button, NpcPreview>();
    private Sprite choiceDeclineSprite;
    private Sprite consumeDeclineSprite;
    private Coroutine choiceEntrance;
    private readonly List<ChoiceEntranceItem> entranceItems = new List<ChoiceEntranceItem>();
    private sealed class ChoiceEntranceItem
    {
        public RectTransform rect;
        public Vector2 home;
        public CanvasGroup group;
        public float alpha;
        public float distance;
        public bool interactable, blocksRaycasts;
    }

    private void StopChoiceEntrance()
    {
        if (choiceEntrance != null) StopCoroutine(choiceEntrance);
        choiceEntrance = null;
        foreach (var item in entranceItems)
        {
            if (item.rect == null) continue;
            item.rect.anchoredPosition = item.home;
            item.group.alpha = item.alpha;
            item.group.interactable = item.interactable;
            item.group.blocksRaycasts = item.blocksRaycasts;
        }
        entranceItems.Clear();
    }

    private IEnumerator SlideChoices()
    {
        // SpeechView updates the shared bubble in LateUpdate, after the request callback.
        yield return null;
        var camera = WorldEventNpcSpeechView.ResolveEventCamera();
        while (camera != null && camera.IsEventFocusMoving)
            yield return null;
        // Give SpeechView a LateUpdate to activate the bubble after camera completion.
        yield return null;
        var bubble = transform.Find("NpcDialogue");
        var pop = bubble != null ? bubble.GetComponent<DialogueBubblePopEffect>() : null;
        var typewriter = bubble != null ? bubble.GetComponent<DialogueTypewriterEffect>() : null;
        while ((pop != null && pop.isActiveAndEnabled && pop.IsPlaying) ||
               (typewriter != null && typewriter.isActiveAndEnabled && typewriter.IsPlaying))
            yield return null;
        float elapsed = 0f;
        const float duration = .28f, stagger = .1f;
        while (elapsed < duration + Mathf.Max(0, entranceItems.Count - 1) * stagger)
        {
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < entranceItems.Count; i++)
            {
                var item = entranceItems[i];
                if (item.rect == null) continue;
                float t = Mathf.Clamp01((elapsed - i * stagger) / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                item.rect.anchoredPosition = item.home + Vector2.right * item.distance * (1f - eased);
                item.group.alpha = item.alpha * t;
            }
            yield return null;
        }
        choiceEntrance = null;
        StopChoiceEntrance();
    }

    private static float SlideDistance(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRect == null) return rect.rect.width + 40f;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, rect);
        var world = canvasRect.TransformVector(Vector3.right * Mathf.Max(0, canvasRect.rect.xMax - bounds.min.x + 40f));
        return Mathf.Abs(rect.parent.InverseTransformVector(world).x);
    }

    private void BeginChoiceEntrance(params Button[] buttons)
    {
        foreach (var button in buttons)
        {
            if (!button.gameObject.activeInHierarchy) continue;
            var rect = (RectTransform)button.transform;
            var group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            entranceItems.Add(new ChoiceEntranceItem { rect = rect, home = rect.anchoredPosition,
                group = group, alpha = group.alpha, distance = SlideDistance(rect), interactable = group.interactable,
                blocksRaycasts = group.blocksRaycasts });
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
        }
        if (entranceItems.Count > 0) choiceEntrance = StartCoroutine(SlideChoices());
    }

    // 종료로 요청이 사라진 뒤에도 같은 프레임의 ESC는 시스템 메뉴에서 사용하지 않는다.
    public static bool BlocksSystemMenuEscape
    {
        get
        {
            if (resultConfirmedFrame == Time.frameCount) return true;
            var request = DHWorldEventRuntimeManager.Instance != null
                ? DHWorldEventRuntimeManager.Instance.CurrentRequest : null;
            return request != null && request.SourceType == DHWorldEventSourceType.Npc &&
                request.IsWaitingFinalConfirm &&
                (request.EventType == DHWorldEventType.Consume || request.EventType == DHWorldEventType.Choice);
        }
    }

    private void SetQuestListHidden(bool hidden)
    {
        if (questList == null && hidden)
        {
            // UIModulePlacer가 재배치한 이후에도 같은 탐사 Canvas 안에서 찾는다.
            for (Transform scope = transform.parent; scope != null && questList == null; scope = scope.parent)
                foreach (Transform child in scope.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Quest") { questList = child.gameObject; break; }
        }
        if (questList == null) return;
        if (hidden || questListHidden) questList.SetActive(!hidden);
        questListHidden = hidden;
    }

    private void Update()
    {
        if (current != null && (current.EventType == DHWorldEventType.Choice || current.EventType == DHWorldEventType.Consume) &&
            current.IsWaitingFinalConfirm &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)))
        {
            if (current.EventType == DHWorldEventType.Consume) OnDecline();
            else OnChoiceCancel();
            return;
        }
        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) && resultClickArea != null &&
            resultClickArea.isActiveAndEnabled && resultClickArea.IsInteractable())
        {
            resultClickArea.onClick.Invoke();
            return;
        }

        for (int number = 1; number <= 9; number++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + number - 1)) ||
                Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + number - 1)))
            {
                ClickNumberedChoice(number);
                return;
            }
        }
    }

    private void ClickNumberedChoice(int number)
    {
        if (number < 1 || number > 9 || current == null || current.IsWaitingFinalConfirm ||
            manager == null || !ReferenceEquals(manager.CurrentRequest, current) ||
            Time.frameCount == choicesShownFrame || Time.frameCount == inputFrame) return;

        GameObject panel = current.EventType == DHWorldEventType.Consume ? consumePanel :
            current.EventType == DHWorldEventType.Reward ? rewardPanel : choicePanel;
        if (panel == null || !panel.activeInHierarchy) return;
        shortcutButtons.Clear();
        panel.GetComponentsInChildren(false, shortcutButtons);
        // 회색 선택지도 번호를 유지한다. 표시 위치 기준 위→아래, 같은 높이는 왼쪽→오른쪽.
        shortcutButtons.Sort((a, b) =>
        {
            int y = b.transform.position.y.CompareTo(a.transform.position.y);
            return y != 0 ? y : a.transform.position.x.CompareTo(b.transform.position.x);
        });
        if (number > shortcutButtons.Count) return;
        Button button = shortcutButtons[number - 1];
        if (button.isActiveAndEnabled && button.IsInteractable()) button.onClick.Invoke();
    }

    private void OnEnable()
    {
        HideAll();
        if (!Application.isPlaying) return;
        if (GetComponent<NpcDialogueHudSlide>() == null) gameObject.AddComponent<NpcDialogueHudSlide>();
        BindButtons();

        manager = DHWorldEventRuntimeManager.EnsureInstance();
        manager.PresentationChanged += OnPresentationChanged;
        manager.PresentationClosed += OnPresentationClosed;
        OnPresentationChanged(manager.CurrentRequest);
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.PresentationChanged -= OnPresentationChanged;
            manager.PresentationClosed -= OnPresentationClosed;
        }
        manager = null;
        questEventId = null;
        choiceSubmitted = false;
        HideAll();
    }

    // Inspector의 UnityEvent 또는 NPC 상호작용 코드에서도 호출 가능.
    public void ShowForObject(WorldEventObject source)
    {
        ShowForEventId(source != null ? source.WorldEventId : null);
    }

    public void ShowForEventId(string eventId)
    {
        HideAll();
        DHWorldEventCatalog catalog = DHWorldEventCatalog.Instance;
        if (catalog == null || string.IsNullOrWhiteSpace(eventId) ||
            !catalog.TryGetEvent(eventId, out DHWorldEventTemplate template) ||
            template == null || template.SourceType != DHWorldEventSourceType.Npc)
            return;

        ShowForEventType(template.EventType, template);
    }

    // SpeechView에서 조회한 종류를 그대로 사용한다. 여기서 ID로 다시 조회하지 않는다.
    public void ShowForEventType(DHWorldEventType eventTypeID, DHWorldEventTemplate template)
    {
        HideAll();
        if (template == null || template.SourceType != DHWorldEventSourceType.Npc ||
            eventTypeID != template.EventType) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        GameObject target = null;
        switch (eventTypeID)
        {
            case DHWorldEventType.Consume: target = consumePanel; break;
            case DHWorldEventType.Reward: target = rewardPanel; break;
            case DHWorldEventType.Choice: target = choicePanel; break;
        }
        if (target == null) return;
        displayedEventId = template.WorldEventId;
        target.SetActive(true);
        BindButtons();
        var live = manager != null ? manager.CurrentRequest : null;
        Render(live != null && live.WorldEventId == template.WorldEventId ? live :
            new DHWorldEventPresentationRequest(template, DHWorldEventPresentationStep.Description,
                template.Description, false, string.Empty,
                template.EventType == DHWorldEventType.Choice ?
                    DHWorldEventChoiceResolver.BuildPresentationOptions(template, null, template.DeclineText, DHWorldEventCatalog.Instance) : null,
                DHWorldEventPreviewBuilder.Build(template)));
    }

    public void HideForEventId(string eventId)
    {
        if (!string.IsNullOrEmpty(eventId) && displayedEventId == eventId) HideAll();
    }

    public void HideAll()
    {
        StopChoiceEntrance();
        displayedEventId = null;
        current = null;
        if (consumePanel != null) consumePanel.SetActive(false);
        if (rewardPanel != null) rewardPanel.SetActive(false);
        if (choicePanel != null) choicePanel.SetActive(false);
        if (resultClickArea != null) resultClickArea.gameObject.SetActive(false);
        SetQuestListHidden(false);
    }

    private void OnPresentationChanged(DHWorldEventPresentationRequest request)
    {
        ShowForEventId(request != null ? request.WorldEventId : null);
        if (displayedEventId != null && request != null) Render(request);
    }

    private void OnPresentationClosed(DHWorldEventPresentationRequest request)
    {
        if (request == null || request.WorldEventId == displayedEventId)
        {
            questEventId = null;
            choiceSubmitted = false;
            HideAll();
        }
    }

    private void BindButtons()
    {
        if (buttonsBound) return;
        // Keep the positions authored on the UI_Explore prefab instance.
        foreach (var panel in new[] { consumePanel, rewardPanel, choicePanel })
        {
            var layout = panel.GetComponent<LayoutGroup>();
            if (layout != null) layout.enabled = false;
        }
        consumeProceed = consumePanel.transform.Find("ProceedButton").GetComponent<Button>();
        consumeDecline = consumePanel.transform.Find("DeclineButton").GetComponent<Button>();
        rewardProceed = rewardPanel.transform.Find("ProceedButton").GetComponent<Button>();
        choiceDecline = choicePanel.transform.Find("DeclineButton").GetComponent<Button>();
        choiceDeclineSprite = ((Image)choiceDecline.targetGraphic).sprite;
        consumeDeclineSprite = ((Image)consumeDecline.targetGraphic).sprite;
        choiceButtons = new[] {
            choicePanel.transform.Find("Choice1Button").GetComponent<Button>(),
            choicePanel.transform.Find("Choice2Button").GetComponent<Button>() };
        consumeProceed.onClick.AddListener(OnProceed);
        consumeDecline.onClick.AddListener(OnDecline);
        rewardProceed.onClick.AddListener(OnProceed);
        choiceDecline.onClick.AddListener(OnChoiceCancel);
        choiceButtons[0].onClick.AddListener(OnChoiceOne);
        choiceButtons[1].onClick.AddListener(OnChoiceTwo);
        // 결과를 닫는 클릭이 뒤의 맵/다른 버튼으로 전달되지 않도록 화면 전체에서 받는다.
        var clickRoot = new GameObject("ResultClickArea", typeof(RectTransform), typeof(Image), typeof(Button));
        clickRoot.layer = gameObject.layer;
        clickRoot.transform.SetParent(transform, false);
        var rect = (RectTransform)clickRoot.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = clickRoot.GetComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = true;
        resultClickArea = clickRoot.GetComponent<Button>();
        resultClickArea.targetGraphic = image;
        resultClickArea.transition = Selectable.Transition.None;
        resultClickArea.onClick.AddListener(OnResultClick);
        clickRoot.SetActive(false);
        buttonsBound = true;
    }

    private void Render(DHWorldEventPresentationRequest request)
    {
        StopChoiceEntrance();
        RenderContent(request);
        if (request.EventType == DHWorldEventType.Consume)
            LayoutVisibleChoices(consumePanel, consumeProceed, consumeDecline);
        else if (request.EventType == DHWorldEventType.Reward)
            LayoutVisibleChoices(rewardPanel, rewardProceed);
        else if (request.EventType == DHWorldEventType.Choice)
            LayoutVisibleChoices(choicePanel, choiceButtons[0], choiceButtons[1], choiceDecline);
        if (!Application.isPlaying || !isActiveAndEnabled || request.IsWaitingFinalConfirm) return;
        if (request.EventType == DHWorldEventType.Consume) BeginChoiceEntrance(consumeProceed, consumeDecline);
        else if (request.EventType == DHWorldEventType.Reward) BeginChoiceEntrance(rewardProceed);
        else if (request.EventType == DHWorldEventType.Choice)
            BeginChoiceEntrance(choiceButtons[0], choiceButtons[1], choiceDecline);
    }

    private static void LayoutVisibleChoices(GameObject panel, params Button[] buttons)
    {
        const float gap = 12f;
        float top = 0f;
        int visible = 0;
        foreach (var button in buttons)
        {
            if (button == null || !button.gameObject.activeSelf) continue;
            var rect = (RectTransform)button.transform;
            if (visible++ > 0) top += gap;
            float height = rect.rect.height;
            rect.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, top, height);
            top += height;
        }
        if (visible > 0)
            ((RectTransform)panel.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, top);
    }

    private void RenderContent(DHWorldEventPresentationRequest request)
    {
        if (!ReferenceEquals(lastShortcutRequest, request))
        {
            lastShortcutRequest = request;
            choicesShownFrame = Time.frameCount;
        }
        current = request;
        if (questEventId != request.WorldEventId)
        {
            questEventId = request.WorldEventId;
            choiceSubmitted = false;
        }
        SetQuestListHidden(!request.IsWaitingFinalConfirm && !choiceSubmitted);
        bool live = manager != null && ReferenceEquals(manager.CurrentRequest, request);
        bool confirm = request.IsWaitingFinalConfirm;
        bool waitForClick = confirm && request.EventType == DHWorldEventType.Choice;
        bool blockConsumeBackground = confirm && request.EventType == DHWorldEventType.Consume;
        if (confirm && !ReferenceEquals(lastConfirmRequest, request))
        {
            lastConfirmRequest = request;
            resultShownFrame = Time.frameCount;
        }
        resultClickArea.gameObject.SetActive(waitForClick || blockConsumeBackground);
        resultClickArea.interactable = live && waitForClick;
        if (waitForClick || blockConsumeBackground)
        {
            // Keep the full-screen confirmation receiver above the choice panel too.
            // Its background otherwise consumes clicks without confirming the result.
            resultClickArea.transform.SetAsLastSibling();
            if (blockConsumeBackground) consumePanel.transform.SetAsLastSibling();
        }
        if (request.EventType == DHWorldEventType.Consume)
        {
            var declineImage = consumeDecline.targetGraphic as Image;
            var proceedImage = consumeProceed.targetGraphic as Image;
            if (declineImage != null && proceedImage != null)
            {
                declineImage.sprite = confirm ? proceedImage.sprite : consumeDeclineSprite;
                declineImage.overrideSprite = null;
            }
            consumeProceed.gameObject.SetActive(!confirm);
            SetButton(consumeProceed, request.ProceedText, live && request.CanProceed);
            consumeDecline.gameObject.SetActive(true);
            SetButton(consumeDecline, confirm ? "확인" : request.DeclineText, live);
            ShowPreview(consumeProceed, request.PreviewEntries);
        }
        else if (request.EventType == DHWorldEventType.Reward)
        {
            SetButton(rewardProceed, request.ProceedText, live && request.CanProceed);
            ShowPreview(rewardProceed, request.PreviewEntries);
        }
        else if (request.EventType == DHWorldEventType.Choice)
        {
            SetChoiceConfirmationStyle(confirm);
            if (confirm)
            {
                foreach (Button button in choiceButtons) button.gameObject.SetActive(false);
                choiceDecline.gameObject.SetActive(true);
                SetButton(choiceDecline, "확인", live);
                return;
            }
            cancelId = null;
            int index = 0;
            foreach (var option in request.ChoiceOptions)
            {
                if (option.IsCancel)
                {
                    cancelId = option.ChoiceId;
                    SetButton(choiceDecline, string.IsNullOrWhiteSpace(request.DeclineText) ? option.ChoiceText : request.DeclineText, live && option.IsEnabled);
                    continue;
                }
                if (index >= choiceButtons.Length) break;
                var button = choiceButtons[index];
                choiceIds[index++] = option.ChoiceId;
                button.gameObject.SetActive(true);
                SetButton(button, option.ChoiceText, live && option.IsEnabled);
                var chance = button.transform.Find("SuccessChance").GetComponent<TMP_Text>();
                chance.text = option.SuccessRateText;
                var preview = GetPreview(button);
                preview.Begin(true);
                preview.Add(option.SuccessPreviewEntries, "성공 시");
                preview.Add(option.FailurePreviewEntries, "실패 시");
                preview.End();
            }
            for (; index < choiceButtons.Length; index++)
            {
                choiceIds[index] = null;
                choiceButtons[index].gameObject.SetActive(false);
            }
            choiceDecline.gameObject.SetActive(cancelId != null);
        }
    }

    private void SetChoiceConfirmationStyle(bool confirm)
    {
        var image = choiceDecline.targetGraphic as Image;
        var proceedImage = consumeProceed.targetGraphic as Image;
        if (image == null || proceedImage == null) return;
        image.sprite = confirm ? proceedImage.sprite : choiceDeclineSprite;
        image.overrideSprite = null;
    }

    private NpcPreview GetPreview(Button button)
    {
        if (!previews.TryGetValue(button, out var preview))
        {
            preview = new NpcPreview(button);
            previews.Add(button, preview);
        }
        return preview;
    }

    private void ShowPreview(Button button, IReadOnlyList<DHWorldEventPreviewEntry> entries)
    {
        var preview = GetPreview(button);
        preview.Begin(false);
        preview.Add(entries, null);
        preview.End();
    }

    private static float LayoutButtonHeader(Button button, bool includeChance)
    {
        float width = Mathf.Max(1f, ((RectTransform)button.transform).rect.width - 42f);
        var label = button.transform.Find("Label").GetComponent<TMP_Text>();
        float height = MeasureText(label, width, 24f, 44f);
        PlaceButtonText(label.rectTransform, 21f, 10f, width, height);
        float bottom = 10f + height + 8f;
        var chanceTransform = button.transform.Find("SuccessChance");
        var chance = chanceTransform != null ? chanceTransform.GetComponent<TMP_Text>() : null;
        if (chance != null)
        {
            bool show = includeChance && !string.IsNullOrWhiteSpace(chance.text);
            chance.gameObject.SetActive(show);
            if (show)
            {
                float chanceHeight = MeasureText(chance, width, 20f, 32f);
                PlaceButtonText(chance.rectTransform, 21f, bottom, width, chanceHeight);
                bottom += chanceHeight + 8f;
            }
        }
        return bottom;
    }

    private static float MeasureText(TMP_Text text, float width, float size, float minimum)
    {
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        return Mathf.Max(minimum, Mathf.Ceil(text.GetPreferredValues(text.text, width, Mathf.Infinity).y));
    }

    private static void PlaceButtonText(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // Display-only rows reuse the button's font. They never receive pointer input.
    private sealed class NpcPreview
    {
        private readonly Button button;
        private readonly TMP_Text label;
        private readonly List<Image> icons = new List<Image>();
        private readonly List<TMP_Text> texts = new List<TMP_Text>();
        private int count;
        private float rowTop;
        private float rowLeft;
        private float lineHeight;
        private int lineStart;
        private bool showChance;
        private static Sprite licenseIpIcon;

        public NpcPreview(Button button)
        {
            this.button = button;
            label = button.transform.Find("Label").GetComponent<TMP_Text>();
        }

        public void Begin(bool choice)
        {
            count = 0;
            rowLeft = 21f;
            lineHeight = 0f;
            lineStart = 0;
            showChance = choice;
            rowTop = LayoutButtonHeader(button, false);
        }

        public void Add(IReadOnlyList<DHWorldEventPreviewEntry> entries, string outcome)
        {
            bool firstInOutcome = true;
            foreach (var entry in entries)
            {
                if (entry == null || entry.Amount == 0) continue;
                if (count == texts.Count)
                {
                    var iconObject = new GameObject("PreviewIcon_" + count, typeof(RectTransform), typeof(Image));
                    iconObject.layer = button.gameObject.layer;
                    iconObject.transform.SetParent(button.transform, false);
                    var icon = iconObject.GetComponent<Image>();
                    icon.raycastTarget = false;
                    icon.preserveAspect = true;
                    icons.Add(icon);
                    var text = Instantiate(label, button.transform);
                    text.name = "PreviewText_" + count;
                    text.raycastTarget = false;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 14;
                    text.fontSizeMax = 19;
                    text.alignment = TextAlignmentOptions.MidlineLeft;
                    texts.Add(text);
                }
                bool cost = entry.Group == DHWorldEventPreviewGroup.Cost;
                var iconView = icons[count];
                var textView = texts[count];
                iconView.sprite = Icon(entry);
                iconView.gameObject.SetActive(iconView.sprite != null);
                textView.gameObject.SetActive(true);
                int amount = cost ? -Mathf.Abs(entry.Amount) : entry.Amount;
                textView.text = amount > 0 ? "+" + amount : amount.ToString();
                if (firstInOutcome && !string.IsNullOrWhiteSpace(outcome))
                {
                    if (rowLeft > 21f)
                    {
                        CenterCurrentLine();
                        rowTop += lineHeight + 4f;
                        rowLeft = 21f;
                        lineHeight = 0f;
                    }
                    textView.text = outcome + " " + textView.text;
                }
                firstInOutcome = false;
                textView.color = cost || entry.Amount < 0 ? new Color32(185, 35, 45, 255) : new Color32(20, 70, 151, 255);
                textView.fontSize = textView.fontSizeMax;
                float buttonWidth = ((RectTransform)button.transform).rect.width;
                float iconWidth = iconView.sprite != null ? 34 : 0;
                float textWidth = Mathf.Min(textView.GetPreferredValues(textView.text).x + 2f,
                    Mathf.Max(1, buttonWidth - 42f - iconWidth));
                float rowHeight = MeasureText(textView, textWidth, 19f, 28f);
                float itemWidth = iconWidth + textWidth;
                if (rowLeft > 21f && rowLeft + itemWidth > buttonWidth - 21f)
                {
                    CenterCurrentLine();
                    rowTop += lineHeight + 4f;
                    rowLeft = 21f;
                    lineHeight = 0f;
                }
                PlaceButtonText(iconView.rectTransform, rowLeft, rowTop + (rowHeight - 26f) * .5f, 26, 26);
                PlaceButtonText(textView.rectTransform, rowLeft + iconWidth, rowTop, textWidth, rowHeight);
                rowLeft += itemWidth + 16f;
                lineHeight = Mathf.Max(lineHeight, rowHeight);
                count++;
            }
        }

        public void End()
        {
            CenterCurrentLine();
            for (int i = count; i < texts.Count; i++)
            {
                icons[i].gameObject.SetActive(false);
                texts[i].gameObject.SetActive(false);
            }
            float bottom = rowTop + lineHeight;
            var chance = button.transform.Find("SuccessChance")?.GetComponent<TMP_Text>();
            if (chance != null && showChance && !string.IsNullOrWhiteSpace(chance.text))
            {
                chance.gameObject.SetActive(true);
                float width = Mathf.Max(1f, ((RectTransform)button.transform).rect.width - 42f);
                float chanceHeight = MeasureText(chance, width, 20f, 32f);
                PlaceButtonText(chance.rectTransform, 21f, bottom + 4f, width, chanceHeight);
                bottom += chanceHeight + 4f;
            }
            float height = Mathf.Max(110f, bottom + 10f);
            ((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void CenterCurrentLine()
        {
            if (lineStart >= count) return;
            float width = rowLeft - 21f - 16f;
            float shift = (((RectTransform)button.transform).rect.width - width) * .5f - 21f;
            for (int i = lineStart; i < count; i++)
            {
                icons[i].rectTransform.anchoredPosition += Vector2.right * shift;
                texts[i].rectTransform.anchoredPosition += Vector2.right * shift;
            }
            lineStart = count;
        }

        private static Sprite Icon(DHWorldEventPreviewEntry entry)
        {
            if (entry.Group == DHWorldEventPreviewGroup.Reward &&
                DHWorldEventCodeMap.TryGetRewardType(entry.TypeCode, out DHWorldEventRewardType reward) &&
                DHWorldEventCodeMap.TryGetRewardResourceType(reward, out ResourceType resource))
                return Sprites.UI.Resource(resource);
            switch (entry.Label)
            {
                case "Money": return Sprites.UI.Resource(ResourceType.Money);
                case "Chip": case "Medal": return Sprites.UI.Resource(ResourceType.Chip);
                case "Crystal": return Sprites.UI.Resource(ResourceType.Crystal);
                case "Supply": return Sprites.UI.Resource(ResourceType.Supply);
            }
            switch (entry.Label)
            {
                case "CurrentIP": case "MaxIP":
                    if (licenseIpIcon == null) licenseIpIcon = Sprites.Load("NpcPreview/UI_icon_IP");
                    return licenseIpIcon;
                case "CurrentHP": case "MaxHP": return Sprites.UI.Status(UIStatusIconType.HP);
                case "Atk": return Sprites.UI.Status(UIStatusIconType.ATK);
                default: return null;
            }
        }

        private static string Name(string value)
        {
            switch (value)
            {
                case "Money": return "자금";
                case "Chip": case "Medal": return "칩";
                case "Crystal": return "크리스탈";
                case "Supply": return "보급품";
                case "CurrentIP": return "IP";
                case "MaxIP": return "최대 IP";
                case "CurrentHP": return "체력";
                case "MaxHP": return "최대 체력";
                case "Atk": return "공격력";
                default: return value;
            }
        }
    }

    private static void SetButton(Button button, string text, bool enabled)
    {
        var label = button.transform.Find("Label").GetComponent<TMP_Text>();
        label.text = string.IsNullOrWhiteSpace(text) ? "확인" : text;
        button.interactable = enabled;
        float bottom = LayoutButtonHeader(button, false);
        ((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(110f, bottom + 10f));
        if (button.transform.name == "DeclineButton")
        {
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(21f, 10f);
            rect.offsetMax = new Vector2(-21f, -10f);
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    private bool CanSubmit(Button button)
    {
        if (choiceEntrance != null) return false;
        if (button == null || !button.isActiveAndEnabled || !button.interactable ||
            current == null || manager == null || !ReferenceEquals(manager.CurrentRequest, current) ||
            Time.frameCount == inputFrame) return false;
        inputFrame = Time.frameCount;
        if (!current.IsWaitingFinalConfirm)
        {
            choiceSubmitted = true;
            SetQuestListHidden(false);
        }
        return true;
    }

    private void OnProceed()
    {
        if (!CanSubmit(current != null && current.EventType == DHWorldEventType.Reward ? rewardProceed : consumeProceed)) return;
        manager.SelectProceed();
    }

    private void OnResultClick()
    {
        if (current == null || !current.IsWaitingFinalConfirm ||
            Time.frameCount == resultShownFrame || !CanSubmit(resultClickArea)) return;
        resultConfirmedFrame = Time.frameCount;
        manager.ConfirmCurrentMessage();
    }

    private void OnDecline()
    {
        if (current != null && current.IsWaitingFinalConfirm)
        {
            if (Time.frameCount == resultShownFrame || !CanSubmit(consumeDecline)) return;
            resultConfirmedFrame = Time.frameCount;
            manager.ConfirmCurrentMessage();
            return;
        }
        if (CanSubmit(consumeDecline)) manager.SelectDecline();
    }
    private void OnChoiceCancel()
    {
        if (current != null && current.IsWaitingFinalConfirm)
        {
            if (Time.frameCount == resultShownFrame || !CanSubmit(choiceDecline)) return;
            resultConfirmedFrame = Time.frameCount;
            manager.ConfirmCurrentMessage();
            return;
        }
        if (CanSubmit(choiceDecline)) manager.SelectDecline();
    }
    private void OnChoiceOne() { if (CanSubmit(choiceButtons[0])) manager.SelectChoice(choiceIds[0]); }
    private void OnChoiceTwo() { if (CanSubmit(choiceButtons[1])) manager.SelectChoice(choiceIds[1]); }

    private void OnDestroy()
    {
        if (!buttonsBound) return;
        if (resultClickArea != null) resultClickArea.onClick.RemoveListener(OnResultClick);
        if (consumeProceed != null) consumeProceed.onClick.RemoveListener(OnProceed);
        if (consumeDecline != null) consumeDecline.onClick.RemoveListener(OnDecline);
        if (rewardProceed != null) rewardProceed.onClick.RemoveListener(OnProceed);
        if (choiceDecline != null) choiceDecline.onClick.RemoveListener(OnChoiceCancel);
        if (choiceButtons[0] != null) choiceButtons[0].onClick.RemoveListener(OnChoiceOne);
        if (choiceButtons[1] != null) choiceButtons[1].onClick.RemoveListener(OnChoiceTwo);
    }
}
