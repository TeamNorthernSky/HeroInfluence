using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

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
                    DHWorldEventChoiceResolver.BuildPresentationOptions(template, null, template.DeclineText, DHWorldEventCatalog.Instance) : null));
    }

    public void HideForEventId(string eventId)
    {
        if (!string.IsNullOrEmpty(eventId) && displayedEventId == eventId) HideAll();
    }

    public void HideAll()
    {
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
        consumeProceed = consumePanel.transform.Find("ProceedButton").GetComponent<Button>();
        consumeDecline = consumePanel.transform.Find("DeclineButton").GetComponent<Button>();
        rewardProceed = rewardPanel.transform.Find("ProceedButton").GetComponent<Button>();
        choiceDecline = choicePanel.transform.Find("DeclineButton").GetComponent<Button>();
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
        bool waitForClick = confirm && (request.EventType == DHWorldEventType.Consume ||
            request.EventType == DHWorldEventType.Choice);
        if (waitForClick && !ReferenceEquals(lastConfirmRequest, request))
        {
            lastConfirmRequest = request;
            resultShownFrame = Time.frameCount;
        }
        resultClickArea.gameObject.SetActive(waitForClick);
        resultClickArea.interactable = live;
        if (waitForClick) resultClickArea.transform.SetAsLastSibling();
        if (request.EventType == DHWorldEventType.Consume)
        {
            consumeProceed.gameObject.SetActive(!confirm);
            SetButton(consumeProceed, request.ProceedText, live && request.CanProceed);
            consumeDecline.gameObject.SetActive(!confirm);
            SetButton(consumeDecline, request.DeclineText, live);
        }
        else if (request.EventType == DHWorldEventType.Reward)
            SetButton(rewardProceed, request.ProceedText, live && request.CanProceed);
        else if (request.EventType == DHWorldEventType.Choice)
        {
            if (confirm)
            {
                foreach (Button button in choiceButtons) button.gameObject.SetActive(false);
                choiceDecline.gameObject.SetActive(false);
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
            }
            for (; index < choiceButtons.Length; index++)
            {
                choiceIds[index] = null;
                choiceButtons[index].gameObject.SetActive(false);
            }
            choiceDecline.gameObject.SetActive(cancelId != null);
        }
    }

    private static void SetButton(Button button, string text, bool enabled)
    {
        var label = button.transform.Find("Label").GetComponent<TMP_Text>();
        label.text = string.IsNullOrWhiteSpace(text) ? "확인" : text;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14;
        label.fontSizeMax = 24;
        button.interactable = enabled;
    }

    private bool CanSubmit(Button button)
    {
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

    private void OnDecline() { if (CanSubmit(consumeDecline)) manager.SelectDecline(); }
    private void OnChoiceCancel() { if (CanSubmit(choiceDecline)) manager.SelectDecline(); }
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
