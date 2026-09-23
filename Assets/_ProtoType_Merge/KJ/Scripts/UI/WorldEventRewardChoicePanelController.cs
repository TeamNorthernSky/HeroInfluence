using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>카탈로그에서 DH 런타임을 거쳐 전달된 보상형/선택형 이벤트를 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class WorldEventRewardChoicePanelController : MonoBehaviour
{
    private static readonly Color Blue = new Color32(0, 55, 155, 255);
    private static readonly Color Red = new Color32(175, 0, 0, 255);
    private GameObject rewardPanel;
    private GameObject choicePanel;
    private TMP_Text rewardTitle, rewardMessage, rewardProceedText;
    private TMP_Text choiceTitle, choiceMessage;
    private Button rewardProceed, choiceCancel;
    private TMP_Text cancelText;
    private readonly List<PreviewRow> rewardRows = new List<PreviewRow>();
    private readonly List<ChoiceCard> cards = new List<ChoiceCard>();
    private DHWorldEventRuntimeManager manager;
    private DHWorldEventPresentationRequest current;
    private string cancelId;
    private int inputFrame = -1;

    private void Awake()
    {
        ResolveView();
        BindButtons();
        manager = DHWorldEventRuntimeManager.EnsureInstance();
        manager.PresentationChanged += OnPresentationChanged;
        manager.PresentationClosed += OnPresentationClosed;
        rewardPanel.SetActive(false);
        choicePanel.SetActive(false);
        if (manager.CurrentRequest != null)
            OnPresentationChanged(manager.CurrentRequest);
    }

    private void OnDestroy()
    {
        if (manager == null) return;
        manager.PresentationChanged -= OnPresentationChanged;
        manager.PresentationClosed -= OnPresentationClosed;
    }

    private void Update()
    {
        if (current == null) return;
        GameObject panel = current.Style == DHWorldEventPresentationStyle.ConditionReward ? rewardPanel : choicePanel;
        if (!panel.activeSelf) panel.SetActive(true);
    }

    private void ResolveView()
    {
        rewardPanel = Find<Transform>(transform, "WorldEventRewardPanel").gameObject;
        choicePanel = Find<Transform>(transform, "WorldEventChoicePanel").gameObject;
        rewardTitle = Find<TMP_Text>(rewardPanel.transform, "world_event_name", "RewardEventTitle");
        rewardMessage = Find<TMP_Text>(rewardPanel.transform, "world_event_description", "MessageText");
        rewardProceed = Find<Button>(rewardPanel.transform, "world_event_proceed", "ProceedButton");
        rewardProceedText = rewardProceed.GetComponentInChildren<TMP_Text>(true);
        FitText(rewardTitle, 24);
        FitText(rewardMessage, 22);
        FitText(rewardProceedText, 18);
        rewardRows.Add(new PreviewRow(null,
            Find<Image>(rewardPanel.transform, "RewardIcon"),
            Find<TMP_Text>(rewardPanel.transform, "RewardAmountText")));
        choiceTitle = Find<TMP_Text>(choicePanel.transform, "world_event_name", "ChoiceEventTitle");
        choiceMessage = Find<TMP_Text>(choicePanel.transform, "world_event_description", "MessageText");
        FitText(choiceTitle, 24);
        FitText(choiceMessage, 22);
        cards.Add(new ChoiceCard(Find<Button>(choicePanel.transform, "ChoiceLeftButton")));
        cards.Add(new ChoiceCard(Find<Button>(choicePanel.transform, "ChoiceRightButton")));
        choiceCancel = Find<Button>(choicePanel.transform, "ChoiceCancelButton");
        cancelText = choiceCancel.GetComponentInChildren<TMP_Text>(true);
    }

    private void BindButtons()
    {
        rewardProceed.onClick.AddListener(OnProceed);
        choiceCancel.onClick.AddListener(OnCancel);
        foreach (ChoiceCard card in cards)
        {
            ChoiceCard target = card;
            target.Button.onClick.AddListener(() => OnChoice(target));
        }
    }

    private void OnPresentationChanged(DHWorldEventPresentationRequest request)
    {
        if (!IsConditionRewardOrChoiceRequest(request))
        {
            current = null;
            rewardPanel.SetActive(false);
            choicePanel.SetActive(false);
            return;
        }
        current = request;
        if (request.Style == DHWorldEventPresentationStyle.ConditionReward)
        {
            RenderReward(request);
            choicePanel.SetActive(false);
            rewardPanel.transform.SetAsLastSibling();
            rewardPanel.SetActive(true);
        }
        else
        {
            RenderChoice(request);
            rewardPanel.SetActive(false);
            choicePanel.transform.SetAsLastSibling();
            choicePanel.SetActive(true);
        }
    }

    private void OnPresentationClosed(DHWorldEventPresentationRequest request)
    {
        if (current == null || (request != null && request.WorldEventId != current.WorldEventId)) return;
        current = null;
        rewardPanel.SetActive(false);
        choicePanel.SetActive(false);
    }

    private static bool IsConditionRewardOrChoiceRequest(DHWorldEventPresentationRequest request)
    {
        return request != null &&
               (request.Style == DHWorldEventPresentationStyle.ConditionReward ||
                request.Style == DHWorldEventPresentationStyle.ConditionChoice);
    }

    private static string Message(DHWorldEventPresentationRequest request)
    {
        return string.IsNullOrWhiteSpace(request.DisabledReason)
            ? request.DescriptionText
            : request.DescriptionText + "\n" + UnavailableMessage(request.DisabledReason);
    }

    private static string UnavailableMessage(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return string.Empty;
        if (reason.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0)
            return "자원 조건을 충족하지 못했습니다.";
        if (reason.IndexOf("Target unit", StringComparison.OrdinalIgnoreCase) >= 0)
            return "대상 파티를 확인할 수 없습니다.";
        if (reason.IndexOf("status", StringComparison.OrdinalIgnoreCase) >= 0)
            return "능력치 조건을 충족하지 못했습니다.";
        return "현재 이 선택을 진행할 수 없습니다.";
    }

    private void RenderReward(DHWorldEventPresentationRequest request)
    {
        rewardTitle.text = string.IsNullOrWhiteSpace(request.WorldEventName) ? "보상형 이벤트" : request.WorldEventName;
        rewardMessage.text = Message(request);
        rewardProceedText.text = string.IsNullOrWhiteSpace(request.ProceedText) ? "확인" : request.ProceedText;
        rewardProceed.interactable = request.CanProceed;
        for (int i = 0; i < request.PreviewEntries.Count; i++)
        {
            if (i == rewardRows.Count) rewardRows.Add(rewardRows[0].Clone("Reward_" + i));
            PreviewRow row = rewardRows[i];
            row.Show(request.PreviewEntries[i]);
            float spacing = 100f / Mathf.Max(1, request.PreviewEntries.Count - 1);
            row.Icon.rectTransform.anchoredPosition = rewardRows[0].IconOrigin + Vector2.down * spacing * i;
            row.Amount.rectTransform.anchoredPosition = rewardRows[0].AmountOrigin + Vector2.down * spacing * i;
        }
        for (int i = request.PreviewEntries.Count; i < rewardRows.Count; i++) rewardRows[i].Hide();
    }

    private void RenderChoice(DHWorldEventPresentationRequest request)
    {
        choiceTitle.text = string.IsNullOrWhiteSpace(request.WorldEventName) ? "선택형 이벤트" : request.WorldEventName;
        choiceMessage.text = Message(request);
        cancelId = null;
        int index = 0;
        DHWorldEventTemplate template = null;
        if (DHWorldEventCatalog.Instance != null)
            DHWorldEventCatalog.Instance.TryGetEvent(request.WorldEventId, out template);
        foreach (DHWorldEventChoicePresentationOption option in request.ChoiceOptions)
        {
            if (option == null) continue;
            if (option.IsCancel)
            {
                cancelId = option.ChoiceId;
                cancelText.text = option.ChoiceText;
                choiceCancel.interactable = option.IsEnabled;
                continue;
            }
            // 카탈로그는 choice_1_id/choice_2_id 두 선택지를 제공한다.
            if (index >= cards.Count) break;
            bool usesIp = false;
            if (template != null)
                foreach (DHWorldEventChoiceTemplate choice in template.Choices)
                    if (choice.ChoiceId == option.ChoiceId) usesIp = choice.UseIpSuccessRateBonus == 1;
            cards[index++].Render(option, usesIp);
        }
        for (; index < cards.Count; index++) cards[index].Button.gameObject.SetActive(false);
        choiceCancel.gameObject.SetActive(!string.IsNullOrEmpty(cancelId));
    }

    private bool CanSubmit(DHWorldEventType type)
    {
        // 동일 프레임 중복 입력과 이미 닫힌 이벤트의 콜백을 차단한다.
        if (current == null || current.EventType != type || manager == null ||
            manager.CurrentRequest != current || Time.frameCount == inputFrame) return false;
        inputFrame = Time.frameCount;
        return true;
    }

    private void OnProceed()
    {
        if (rewardProceed.interactable && CanSubmit(DHWorldEventType.Reward)) manager.SelectProceed();
    }

    private void OnChoice(ChoiceCard card)
    {
        if (card.Button.interactable && !string.IsNullOrEmpty(card.ChoiceId) && CanSubmit(DHWorldEventType.Choice))
            manager.SelectChoice(card.ChoiceId);
    }

    private void OnCancel()
    {
        if (choiceCancel.interactable && !string.IsNullOrEmpty(cancelId) && CanSubmit(DHWorldEventType.Choice))
            manager.SelectChoice(cancelId);
    }

    private static T Find<T>(Transform root, params string[] names) where T : Component
    {
        foreach (string name in names)
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                {
                    T component = child.GetComponent<T>() ?? child.GetComponentInChildren<T>(true);
                    if (component != null) return component;
                }
        throw new InvalidOperationException(root.name + ": missing " + string.Join("/", names));
    }

    private static void FitText(TMP_Text text, float minimumSize)
    {
        text.enableWordWrapping = true;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static Sprite IconFor(DHWorldEventPreviewEntry entry)
    {
        if (entry.Group == DHWorldEventPreviewGroup.Reward &&
            DHWorldEventCodeMap.TryGetRewardType(entry.TypeCode, out DHWorldEventRewardType reward))
        {
            if (reward == DHWorldEventRewardType.CurrentIP) return Sprites.UI.Status(UIStatusIconType.IP);
            if (DHWorldEventCodeMap.TryGetRewardResourceType(reward, out ResourceType resource)) return Sprites.UI.Resource(resource);
        }
        // 선택 결과의 TypeCode는 자원/스탯이 겹친다. PreviewBuilder가 제공하는 enum 이름으로 구분한다.
        switch (entry.Label)
        {
            case "Money": return Sprites.UI.Resource(ResourceType.Money);
            case "Chip": return Sprites.UI.Resource(ResourceType.Chip);
            case "Crystal": return Sprites.UI.Resource(ResourceType.Crystal);
            case "Supply": return Sprites.UI.Resource(ResourceType.Supply);
            case "CurrentIP": case "MaxIP": return Sprites.UI.Status(UIStatusIconType.IP);
            case "CurrentHP": case "MaxHP": return Sprites.UI.Status(UIStatusIconType.HP);
            case "Atk": return Sprites.UI.Status(UIStatusIconType.ATK);
            default: return null;
        }
    }

    private sealed class PreviewRow
    {
        public readonly TMP_Text Label, Amount;
        public readonly Image Icon;
        public readonly Vector2 IconOrigin, AmountOrigin;
        public PreviewRow(TMP_Text label, Image icon, TMP_Text amount)
        {
            Label = label; Icon = icon; Amount = amount;
            IconOrigin = icon.rectTransform.anchoredPosition;
            AmountOrigin = amount.rectTransform.anchoredPosition;
        }
        public PreviewRow Clone(string suffix)
        {
            TMP_Text label = Label == null ? null : Instantiate(Label, Label.transform.parent);
            Image icon = Instantiate(Icon, Icon.transform.parent);
            TMP_Text amount = Instantiate(Amount, Amount.transform.parent);
            if (label != null) label.name = suffix + "Label";
            icon.name = suffix + "Icon"; amount.name = suffix + "Amount";
            return new PreviewRow(label, icon, amount);
        }
        public void Show(DHWorldEventPreviewEntry entry)
        {
            Icon.gameObject.SetActive(true); Amount.gameObject.SetActive(true);
            if (Label != null) Label.gameObject.SetActive(true);
            Icon.sprite = IconFor(entry); Icon.enabled = Icon.sprite != null;
            Icon.preserveAspect = true;
            Amount.text = Icon.sprite != null ? entry.Amount.ToString() : entry.Label + " " + entry.Amount;
            Amount.color = entry.Amount < 0 ? Red : Blue;
        }
        public void Hide()
        {
            if (Label != null) Label.gameObject.SetActive(false);
            Icon.gameObject.SetActive(false); Amount.gameObject.SetActive(false);
        }
    }

    private sealed class ChoiceCard
    {
        public readonly Button Button;
        public string ChoiceId;
        private readonly TMP_Text title, chance, bonus;
        private readonly List<PreviewRow> rows = new List<PreviewRow>();
        public ChoiceCard(Button button)
        {
            Button = button;
            title = Find<TMP_Text>(button.transform, "ChoiceTitle");
            chance = Find<TMP_Text>(button.transform, "SuccessChance");
            bonus = Find<TMP_Text>(button.transform, "ChanceBonus");
            FitText(title, 20);
            FitText(bonus, 18);
            rows.Add(new PreviewRow(Find<TMP_Text>(button.transform, "SuccessLabel"),
                Find<Image>(button.transform, "SuccessRewardIcon"), Find<TMP_Text>(button.transform, "SuccessRewardAmount")));
            // 기존 오른쪽 카드의 실패 행도 풀에 포함해, 실패 결과가 없는 다음 이벤트에서 숨긴다.
            foreach (Transform child in button.GetComponentsInChildren<Transform>(true))
                if (child.name == "FailureLabel")
                    rows.Add(new PreviewRow(child.GetComponent<TMP_Text>(), Find<Image>(button.transform, "FailurePenaltyIcon"),
                        Find<TMP_Text>(button.transform, "FailurePenaltyAmount")));
        }
        public void Render(DHWorldEventChoicePresentationOption option, bool usesIp)
        {
            Button.gameObject.SetActive(true);
            ChoiceId = option.ChoiceId;
            Button.interactable = option.IsEnabled;
            title.text = option.ChoiceText;
            chance.text = "성공 확률 " + option.SuccessRateText;
            bonus.text = !option.IsEnabled ? UnavailableMessage(option.DisabledReason) : usesIp ? "IP 보너스 반영" : string.Empty;
            bonus.gameObject.SetActive(!string.IsNullOrEmpty(bonus.text));
            int count = option.SuccessPreviewEntries.Count + option.FailurePreviewEntries.Count;
            float height = Mathf.Min(81f, 180f / Mathf.Max(1, count));
            int index = 0;
            foreach (DHWorldEventPreviewEntry entry in option.SuccessPreviewEntries) RenderRow(index++, entry, "성공 시", height);
            foreach (DHWorldEventPreviewEntry entry in option.FailurePreviewEntries) RenderRow(index++, entry, "실패 시", height);
            for (; index < rows.Count; index++) rows[index].Hide();
        }
        private void RenderRow(int index, DHWorldEventPreviewEntry entry, string label, float height)
        {
            if (index == rows.Count) rows.Add(rows[0].Clone("Outcome_" + index));
            PreviewRow row = rows[index];
            row.Show(entry); row.Label.text = label;
            float top = 207 + index * height;
            Place(row.Label.rectTransform, 42, top, 145, height);
            Place(row.Icon.rectTransform, 178, top, 75, Mathf.Min(64, height));
            Place(row.Amount.rectTransform, 260, top, 190, height);
            row.Label.enableAutoSizing = row.Amount.enableAutoSizing = true;
            row.Label.fontSizeMin = row.Amount.fontSizeMin = Mathf.Min(20, height * .55f);
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
