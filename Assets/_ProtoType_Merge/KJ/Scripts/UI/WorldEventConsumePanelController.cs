using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DHWorldEventCatalog에서 런타임 요청으로 전달된 소비형 이벤트를 표시한다.
/// NPC형과 조건형 모두 같은 소비형 패널을 사용한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldEventConsumePanelController : MonoBehaviour
{
    [SerializeField] private Sprite influenceIcon;
    [SerializeField] private Sprite healthIcon;
    [SerializeField] private Sprite attackIcon;
    private static readonly Color NormalButtonColor = Color.white;
    private static readonly Color NormalTextColor = Color.white;
    private static readonly Color SelectedButtonColor = new Color32(17, 72, 174, 255);
    private static readonly Color SelectedTextColor = Color.white;
    private static readonly Color NegativeAmountColor = new Color32(190, 29, 44, 255);
    private static readonly Color PositiveAmountColor = new Color32(22, 68, 151, 255);

    private TMP_Text titleText;
    private GameObject panelRoot;
    private TMP_Text messageText;
    private readonly List<PreviewSlot> costSlots = new List<PreviewSlot>();
    private readonly List<PreviewSlot> rewardSlots = new List<PreviewSlot>();
    private Button proceedButton;
    private Button declineButton;
    private TMP_Text proceedText;
    private TMP_Text declineText;

    private DHWorldEventRuntimeManager manager;
    private DHWorldEventPresentationRequest currentRequest;
    private int selectionFrame = -1;
    private Vector2 proceedPosition;
    private Vector2 confirmPosition;
    private Image resultInputBlocker;

    private void Awake()
    {
        ResolveView();
        proceedButton.onClick.AddListener(OnProceed);
        declineButton.onClick.AddListener(OnDecline);
        AddResultInputBlocker();
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
        if (resultInputBlocker != null) Destroy(resultInputBlocker.gameObject);
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
        UpdateResultInputBlocker();
    }

    private void OnDisable()
    {
        if (resultInputBlocker != null) resultInputBlocker.gameObject.SetActive(false);
    }

    private void OnResultClick()
    {
        if (currentRequest == null || !currentRequest.IsWaitingFinalConfirm || Time.frameCount == selectionFrame)
            return;

        if (manager == null || !ReferenceEquals(manager.CurrentRequest, currentRequest))
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
        UpdateResultInputBlocker();
    }

    private void OnPresentationClosed(DHWorldEventPresentationRequest request)
    {
        if (currentRequest == null || (request != null && request.WorldEventId != currentRequest.WorldEventId))
            return;

        currentRequest = null;
        panelRoot.SetActive(false);
        UpdateResultInputBlocker();
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
        proceedText.text = request.IsWaitingFinalConfirm ? "확인" : request.ProceedText;
        declineText.text = request.DeclineText;

        bool waiting = request.IsWaitingFinalConfirm;
        declineButton.gameObject.SetActive(!waiting);
        ((RectTransform)proceedButton.transform).anchoredPosition = waiting ? confirmPosition : proceedPosition;
        proceedButton.interactable = waiting || request.CanProceed;
        declineButton.interactable = true;

        bool accepted = request.Step == DHWorldEventPresentationStep.Accept;
        bool declined = request.Step == DHWorldEventPresentationStep.Cancel;
        ApplyButtonState(proceedButton, proceedText, !waiting && accepted);
        ApplyButtonState(declineButton, declineText, declined);
        RenderPreview(request.PreviewEntries);
    }

    private sealed class PreviewSlot
    {
        public GameObject Root;
        public Image Icon;
        public TMP_Text Amount;
    }

    private void RenderPreview(IReadOnlyList<DHWorldEventPreviewEntry> entries)
    {
        int costIndex = 0;
        int rewardIndex = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            DHWorldEventPreviewEntry entry = entries[i];
            bool isCost = entry.Group == DHWorldEventPreviewGroup.Cost;
            List<PreviewSlot> slots = isCost ? costSlots : rewardSlots;
            int index = isCost ? costIndex++ : rewardIndex++;
            if (index < slots.Count)
                ApplyPreview(slots[index], entry, !isCost);
        }

        for (int i = costIndex; i < costSlots.Count; i++)
            costSlots[i].Root.SetActive(false);
        for (int i = rewardIndex; i < rewardSlots.Count; i++)
            rewardSlots[i].Root.SetActive(false);
    }

    private void ApplyPreview(PreviewSlot slot, DHWorldEventPreviewEntry entry, bool signed)
    {
        slot.Root.SetActive(true);
        slot.Icon.sprite = ResolveIcon(entry);
        slot.Icon.enabled = slot.Icon.sprite != null;
        slot.Amount.text = signed
            ? entry.Amount.ToString("+0;-0;+0")
            : "-" + System.Math.Abs((long)entry.Amount).ToString();
        slot.Amount.color = signed && entry.Amount < 0 ? NegativeAmountColor : PositiveAmountColor;
    }

    private Sprite ResolveIcon(DHWorldEventPreviewEntry entry)
    {
        if (entry.Group == DHWorldEventPreviewGroup.Cost &&
            DHWorldEventCodeMap.TryGetResourceType(entry.TypeCode, out ResourceType costType))
            return Sprites.UI.Resource(costType);

        if (entry.Group == DHWorldEventPreviewGroup.Reward &&
            DHWorldEventCodeMap.TryGetRewardType(entry.TypeCode, out DHWorldEventRewardType rewardType))
        {
            if (rewardType == DHWorldEventRewardType.CurrentIP)
                return influenceIcon;
            if (rewardType == DHWorldEventRewardType.CurrentHP)
                return healthIcon;
            if (DHWorldEventCodeMap.TryGetRewardResourceType(rewardType, out ResourceType rewardResource))
                return Sprites.UI.Resource(rewardResource);
        }

        if (DHWorldEventCodeMap.TryGetStatusType(entry.TypeCode, out DHWorldEventStatusType statusType))
        {
            switch (statusType)
            {
                case DHWorldEventStatusType.CurrentIP:
                case DHWorldEventStatusType.MaxIP:
                    return influenceIcon;
                case DHWorldEventStatusType.CurrentHP:
                case DHWorldEventStatusType.MaxHP:
                    return healthIcon;
                case DHWorldEventStatusType.Atk:
                    return attackIcon;
            }
        }

        return null;
    }

    private void OnProceed()
    {
        if (currentRequest == null)
            return;

        if (currentRequest.IsWaitingFinalConfirm)
        {
            OnResultClick();
            return;
        }

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
        proceedPosition = ((RectTransform)proceedButton.transform).anchoredPosition;
        confirmPosition = (proceedPosition + ((RectTransform)declineButton.transform).anchoredPosition) * 0.5f;
        messageText = FindDirect<TMP_Text>("world_event_description");
        ResolvePreviewSlots(panel);
    }

    private void ResolvePreviewSlots(Transform panel)
    {
        costSlots.Clear();
        rewardSlots.Clear();
        // 계층의 순서대로 사용하며 프리팹에 배치한 위치와 크기를 유지한다.
        foreach (Transform child in panel)
        {
            string slotName = child.name.Replace(" ", string.Empty);
            bool isCost = slotName.StartsWith("ConsumedResources", System.StringComparison.Ordinal);
            bool isReward = slotName.StartsWith("Rewards", System.StringComparison.Ordinal);
            if (!isCost && !isReward) continue;

            var slot = new PreviewSlot
            {
                Root = child.gameObject,
                Icon = child.GetComponentInChildren<Image>(true),
                Amount = child.GetComponentInChildren<TMP_Text>(true)
            };
            if (slot.Icon == null || slot.Amount == null)
            {
                Debug.LogError($"[{nameof(WorldEventConsumePanelController)}] {child.name} requires an icon and amount text.", child);
                continue;
            }
            (isCost ? costSlots : rewardSlots).Add(slot);
        }
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

    private void AddResultInputBlocker()
    {
        var blocker = new GameObject("ConsumeResultInputBlocker", typeof(RectTransform), typeof(Image));
        blocker.layer = panelRoot.layer;
        blocker.transform.SetParent(panelRoot.transform.parent, false);
        var rect = (RectTransform)blocker.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        resultInputBlocker = blocker.GetComponent<Image>();
        resultInputBlocker.color = Color.clear;
        resultInputBlocker.raycastTarget = true;
        blocker.SetActive(false);
    }

    private void UpdateResultInputBlocker()
    {
        if (resultInputBlocker == null) return;
        bool waiting = currentRequest != null && currentRequest.IsWaitingFinalConfirm && panelRoot.activeInHierarchy;
        resultInputBlocker.gameObject.SetActive(waiting);
        if (!waiting) return;
        // UIComposer can reparent the panel after Awake.
        if (resultInputBlocker.transform.parent != panelRoot.transform.parent)
            resultInputBlocker.transform.SetParent(panelRoot.transform.parent, false);
        int panelIndex = panelRoot.transform.GetSiblingIndex();
        int blockerIndex = resultInputBlocker.transform.GetSiblingIndex();
        int desiredIndex = panelIndex - (blockerIndex < panelIndex ? 1 : 0);
        if (blockerIndex != desiredIndex) resultInputBlocker.transform.SetSiblingIndex(desiredIndex);
    }
}
