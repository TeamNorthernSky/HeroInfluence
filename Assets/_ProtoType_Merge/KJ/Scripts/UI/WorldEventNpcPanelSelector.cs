using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
    private Button consumeProceed, consumeDecline, rewardProceed, choiceDecline;
    private Button[] choiceButtons;
    private string[] choiceIds = new string[2];
    private string cancelId;
    private bool buttonsBound;
    private int inputFrame = -1;

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

        ShowForEventType(eventId.Trim()[0], template);
    }

    public static bool MatchesEventType(char eventTypeID, DHWorldEventType eventType)
    {
        switch (char.ToUpperInvariant(eventTypeID))
        {
            case 'C': return eventType == DHWorldEventType.Consume;
            case 'R': return eventType == DHWorldEventType.Reward;
            case 'S': return eventType == DHWorldEventType.Choice;
            default: return false;
        }
    }

    // SpeechView에서 조회한 종류를 그대로 사용한다. 여기서 ID로 다시 조회하지 않는다.
    public void ShowForEventType(char eventTypeID, DHWorldEventTemplate template)
    {
        HideAll();
        if (template == null || template.SourceType != DHWorldEventSourceType.Npc ||
            !MatchesEventType(eventTypeID, template.EventType)) return;
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        GameObject target = null;
        switch (template.EventType)
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
    }

    private void OnPresentationChanged(DHWorldEventPresentationRequest request)
    {
        ShowForEventId(request != null ? request.WorldEventId : null);
        if (displayedEventId != null && request != null) Render(request);
    }

    private void OnPresentationClosed(DHWorldEventPresentationRequest request)
    {
        if (request == null || request.WorldEventId == displayedEventId)
            HideAll();
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
        buttonsBound = true;
    }

    private void Render(DHWorldEventPresentationRequest request)
    {
        current = request;
        bool live = manager != null && ReferenceEquals(manager.CurrentRequest, request);
        bool confirm = request.IsWaitingFinalConfirm;
        if (request.EventType == DHWorldEventType.Consume)
        {
            SetButton(consumeProceed, confirm ? "확인" : request.ProceedText, live && (confirm || request.CanProceed));
            consumeDecline.gameObject.SetActive(!confirm);
            SetButton(consumeDecline, request.DeclineText, live);
        }
        else if (request.EventType == DHWorldEventType.Reward)
            SetButton(rewardProceed, request.ProceedText, live && request.CanProceed);
        else if (request.EventType == DHWorldEventType.Choice)
        {
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
        return true;
    }

    private void OnProceed()
    {
        if (!CanSubmit(current != null && current.EventType == DHWorldEventType.Reward ? rewardProceed : consumeProceed)) return;
        if (current.IsWaitingFinalConfirm) manager.ConfirmCurrentMessage();
        else manager.SelectProceed();
    }

    private void OnDecline() { if (CanSubmit(consumeDecline)) manager.SelectDecline(); }
    private void OnChoiceCancel() { if (CanSubmit(choiceDecline)) manager.SelectChoice(cancelId); }
    private void OnChoiceOne() { if (CanSubmit(choiceButtons[0])) manager.SelectChoice(choiceIds[0]); }
    private void OnChoiceTwo() { if (CanSubmit(choiceButtons[1])) manager.SelectChoice(choiceIds[1]); }

    private void OnDestroy()
    {
        if (!buttonsBound) return;
        if (consumeProceed != null) consumeProceed.onClick.RemoveListener(OnProceed);
        if (consumeDecline != null) consumeDecline.onClick.RemoveListener(OnDecline);
        if (rewardProceed != null) rewardProceed.onClick.RemoveListener(OnProceed);
        if (choiceDecline != null) choiceDecline.onClick.RemoveListener(OnChoiceCancel);
        if (choiceButtons[0] != null) choiceButtons[0].onClick.RemoveListener(OnChoiceOne);
        if (choiceButtons[1] != null) choiceButtons[1].onClick.RemoveListener(OnChoiceTwo);
    }
}
