using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// DHWorldEventCatalog에서 런타임 요청으로 전달된 소비형 이벤트를 표시한다.
/// NPC형과 조건형 모두 같은 소비형 패널을 사용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldEventConsumePanelController : MonoBehaviour
{
    private static readonly Color NormalButtonColor = Color.white;
    private static readonly Color NormalTextColor = new Color32(20, 70, 151, 255);
    private static readonly Color SelectedButtonColor = new Color32(17, 72, 174, 255);
    private static readonly Color SelectedTextColor = Color.white;
    private static readonly Color NegativeAmountColor = new Color32(190, 29, 44, 255);
    private static readonly Color PositiveAmountColor = new Color32(22, 68, 151, 255);

    private TMP_Text titleText;
    private GameObject panelRoot;
    private TMP_Text messageText;
    private TMP_Text costAmountText;
    private TMP_Text resultAmountText;
    private Image costIcon;
    private Image resultIcon;
    private Button proceedButton;
    private Button declineButton;
    private TMP_Text proceedText;
    private TMP_Text declineText;

    private Image secondResultIcon;
    private TMP_Text secondResultAmountText;
    private Vector2 resultIconPosition;
    private Vector2 resultAmountPosition;
    private DHWorldEventRuntimeManager manager;
    private DHWorldEventPresentationRequest currentRequest;
    private int selectionFrame = -1;

    private void Awake()
    {
        ResolveView();
        proceedButton.onClick.AddListener(OnProceed);
        declineButton.onClick.AddListener(OnDecline);
        AddPanelClickHandler();
        manager = DHWorldEventRuntimeManager.EnsureInstance();
        manager.PresentationChanged += OnPresentationChanged;
        manager.PresentationClosed += OnPresentationClosed;

        if (manager.CurrentRequest != null)
            OnPresentationChanged(manager.CurrentRequest);
        else
            panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (manager == null)
            return;

        manager.PresentationChanged -= OnPresentationChanged;
        manager.PresentationClosed -= OnPresentationClosed;
    }

    private void Update()
    {
        // ESC나 외부 모달 닫기 경로가 런타임 이벤트를 중간에 숨기지 못하게 한다.
        if (currentRequest != null && panelRoot != null && !panelRoot.activeSelf)
            panelRoot.SetActive(true);
    }

    private void OnPanelPointerClick(BaseEventData eventData)
    {
        if (currentRequest == null || !currentRequest.IsWaitingFinalConfirm || Time.frameCount == selectionFrame)
            return;

        PointerEventData pointer = eventData as PointerEventData;
        if (pointer != null && pointer.pointerPress != null && pointer.pointerPress.GetComponentInParent<Button>() != null)
            return;

        manager?.ConfirmCurrentMessage();
    }

    private void OnPresentationChanged(DHWorldEventPresentationRequest request)
    {
        if (!IsConditionConsumeRequest(request))
            return;

        currentRequest = request;
        Render(request);
        panelRoot.transform.SetAsLastSibling();
        panelRoot.SetActive(true);
    }

    private void OnPresentationClosed(DHWorldEventPresentationRequest request)
    {
        if (currentRequest == null || (request != null && request.WorldEventId != currentRequest.WorldEventId))
            return;

        currentRequest = null;
        panelRoot.SetActive(false);
    }

    private static bool IsConditionConsumeRequest(DHWorldEventPresentationRequest request)
    {
        return request != null &&
               request.Style == DHWorldEventPresentationStyle.ConditionConsume;
    }

    private void Render(DHWorldEventPresentationRequest request)
    {
        // 배경 이미지에 "월드 이벤트:"가 포함되어 있으므로 이벤트명만 표시한다.
        // ConsumeNPC 테이블에는 이름 열이 없고, ConsumeCondition에는 이름이 있다.
        titleText.text = string.IsNullOrWhiteSpace(request.WorldEventName)
            ? "소비형 이벤트"
            : request.WorldEventName;
        messageText.text = request.IsWaitingFinalConfirm ? request.MessageText : request.DescriptionText;
        proceedText.text = request.ProceedText;
        declineText.text = request.DeclineText;

        bool waiting = request.IsWaitingFinalConfirm;
        proceedButton.interactable = waiting || request.CanProceed;
        declineButton.interactable = true;

        bool accepted = request.Step == DHWorldEventPresentationStep.Accept;
        bool declined = request.Step == DHWorldEventPresentationStep.Cancel;
        ApplyButtonState(proceedButton, proceedText, accepted);
        ApplyButtonState(declineButton, declineText, declined);
        RenderPreview(request.PreviewEntries);
    }

    private void RenderPreview(IReadOnlyList<DHWorldEventPreviewEntry> entries)
    {
        DHWorldEventPreviewEntry cost = null;
        var results = new List<DHWorldEventPreviewEntry>(2);

        for (int i = 0; i < entries.Count; i++)
        {
            DHWorldEventPreviewEntry entry = entries[i];
            if (entry.Group == DHWorldEventPreviewGroup.Cost && cost == null)
                cost = entry;
            else if (entry.Group != DHWorldEventPreviewGroup.Cost && results.Count < 2)
                results.Add(entry);
        }

        ApplyPreview(costIcon, costAmountText, cost, false);
        ApplyPreview(resultIcon, resultAmountText, results.Count > 0 ? results[0] : null, true);

        EnsureSecondResultView();
        ApplyPreview(secondResultIcon, secondResultAmountText, results.Count > 1 ? results[1] : null, true);

        bool hasSecondResult = results.Count > 1;
        float y = hasSecondResult ? 35f : 0f;
        resultIcon.rectTransform.anchoredPosition = resultIconPosition + Vector2.up * y;
        resultAmountText.rectTransform.anchoredPosition = resultAmountPosition + Vector2.up * y;
        secondResultIcon.rectTransform.anchoredPosition = resultIconPosition + Vector2.down * 35f;
        secondResultAmountText.rectTransform.anchoredPosition = resultAmountPosition + Vector2.down * 35f;
    }

    private static void ApplyPreview(Image icon, TMP_Text amountText, DHWorldEventPreviewEntry entry, bool signed)
    {
        bool visible = entry != null;
        icon.gameObject.SetActive(visible);
        amountText.gameObject.SetActive(visible);
        if (!visible)
            return;

        icon.sprite = ResolveIcon(entry);
        icon.enabled = icon.sprite != null;
        amountText.text = signed && entry.Amount > 0 ? entry.Amount.ToString() : entry.Amount.ToString();
        amountText.color = signed && entry.Amount < 0 ? NegativeAmountColor : PositiveAmountColor;
    }

    private static Sprite ResolveIcon(DHWorldEventPreviewEntry entry)
    {
        if (entry.Group == DHWorldEventPreviewGroup.Cost &&
            DHWorldEventCodeMap.TryGetResourceType(entry.TypeCode, out ResourceType costType))
            return Sprites.UI.Resource(costType);

        if (entry.Group == DHWorldEventPreviewGroup.Reward &&
            DHWorldEventCodeMap.TryGetRewardType(entry.TypeCode, out DHWorldEventRewardType rewardType))
        {
            if (rewardType == DHWorldEventRewardType.CurrentIP)
                return Sprites.UI.Status(UIStatusIconType.IP);
            if (DHWorldEventCodeMap.TryGetRewardResourceType(rewardType, out ResourceType rewardResource))
                return Sprites.UI.Resource(rewardResource);
        }

        if (DHWorldEventCodeMap.TryGetStatusType(entry.TypeCode, out DHWorldEventStatusType statusType))
        {
            switch (statusType)
            {
                case DHWorldEventStatusType.CurrentIP:
                case DHWorldEventStatusType.MaxIP:
                    return Sprites.UI.Status(UIStatusIconType.IP);
                case DHWorldEventStatusType.CurrentHP:
                case DHWorldEventStatusType.MaxHP:
                    return Sprites.UI.Status(UIStatusIconType.HP);
                case DHWorldEventStatusType.Atk:
                    return Sprites.UI.Status(UIStatusIconType.ATK);
            }
        }

        return null;
    }

    private void OnProceed()
    {
        if (currentRequest == null || currentRequest.IsWaitingFinalConfirm)
            return;

        selectionFrame = Time.frameCount;
        manager?.SelectProceed();
    }

    private void OnDecline()
    {
        if (currentRequest == null || currentRequest.IsWaitingFinalConfirm)
            return;

        selectionFrame = Time.frameCount;
        manager?.SelectDecline();
    }

    private static void ApplyButtonState(Button button, TMP_Text label, bool selected)
    {
        button.image.color = selected ? SelectedButtonColor : NormalButtonColor;
        label.color = selected ? SelectedTextColor : NormalTextColor;
    }

    private void ResolveView()
    {
        Transform panel = FindTransform("WorldEventConsumePanel");
        panelRoot = panel.gameObject;
        titleText = FindDirect<TMP_Text>("world_event_name") ?? FindDirect<TMP_Text>("ConsumeEventTitle");
        proceedButton = FindDirect<Button>("world_event_proceed") ?? FindDirect<Button>("YesButton");
        declineButton = FindDirect<Button>("world_event_decline") ?? FindDirect<Button>("NoButton");
        proceedText = proceedButton.GetComponentInChildren<TMP_Text>(true);
        declineText = declineButton.GetComponentInChildren<TMP_Text>(true);
        costIcon = FindDirect<Image>("ResourceIcon");
        resultIcon = FindDirect<Image>("RewardIcon") ?? FindDirect<Image>("EffectIcon");
        messageText = FindDirect<TMP_Text>("world_event_description");
        costAmountText = FindDirect<TMP_Text>("Consumed Resources");
        resultAmountText = FindDirect<TMP_Text>("Rewards");

        // 구 프리팹의 직속 텍스트만 위치로 보완한다. 새 그룹의 로컬 좌표는 사용하지 않는다.
        TMP_Text[] labels = panelRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == titleText || label == proceedText || label == declineText || label.transform.parent != panel)
                continue;

            float x = label.rectTransform.anchoredPosition.x;
            float y = label.rectTransform.anchoredPosition.y;
            if (y > -300f && messageText == null)
                messageText = label;
            else if (y <= -300f && x < 600f && costAmountText == null)
                costAmountText = label;
            else if (y <= -300f && x >= 600f && resultAmountText == null)
                resultAmountText = label;
        }

        resultIconPosition = resultIcon.rectTransform.anchoredPosition;
        resultAmountPosition = resultAmountText.rectTransform.anchoredPosition;
    }

    private void EnsureSecondResultView()
    {
        if (secondResultIcon != null)
            return;

        secondResultIcon = Instantiate(resultIcon, resultIcon.transform.parent);
        secondResultIcon.name = "EffectIcon_2";
        secondResultAmountText = Instantiate(resultAmountText, resultAmountText.transform.parent);
        secondResultAmountText.name = "EffectAmountText_2";
    }

    private T FindDirect<T>(string objectName) where T : Component
    {
        Transform[] children = panelRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
                return children[i].GetComponent<T>() ?? children[i].GetComponentInChildren<T>(true);
        }

        return null;
    }

    private Transform FindTransform(string objectName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
                return children[i];
        }

        return null;
    }

    private void AddPanelClickHandler()
    {
        EventTrigger trigger = panelRoot.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = panelRoot.AddComponent<EventTrigger>();
        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener(OnPanelPointerClick);
        trigger.triggers.Add(entry);
    }

}
