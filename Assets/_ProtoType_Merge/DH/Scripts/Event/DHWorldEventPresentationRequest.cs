using System.Collections.Generic;

public enum DHWorldEventPresentationStep
{
    None = 0,
    Description = 1,
    Accept = 2,
    Cancel = 3
}

public enum DHWorldEventPresentationStyle
{
    None = 0,
    NpcConsume = 1,
    NpcReward = 2,
    NpcChoice = 3,
    ConditionConsume = 4,
    ConditionReward = 5,
    ConditionChoice = 6
}

public enum DHWorldEventPreviewGroup
{
    Cost = 0,
    Result = 1,
    Reward = 2
}

public sealed class DHWorldEventPreviewEntry
{
    public DHWorldEventPreviewGroup Group { get; }
    public int TypeCode { get; }
    public int Amount { get; }
    public string Label { get; }

    public DHWorldEventPreviewEntry(
        DHWorldEventPreviewGroup group,
        int typeCode,
        int amount,
        string label)
    {
        Group = group;
        TypeCode = typeCode;
        Amount = amount;
        Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
    }
}

public sealed class DHWorldEventChoicePresentationOption
{
    private readonly List<DHWorldEventPreviewEntry> successPreviewEntries;
    private readonly List<DHWorldEventPreviewEntry> failurePreviewEntries;

    public string ChoiceId { get; }
    public string ChoiceText { get; }
    public bool IsCancel { get; }
    public bool IsEnabled { get; }
    public string DisabledReason { get; }
    public float SuccessRatePercent { get; }
    public string SuccessRateText { get; }
    public string SuccessResultGroupId { get; }
    public string FailureResultGroupId { get; }
    public IReadOnlyList<DHWorldEventPreviewEntry> SuccessPreviewEntries => successPreviewEntries;
    public IReadOnlyList<DHWorldEventPreviewEntry> FailurePreviewEntries => failurePreviewEntries;
    public bool HasSuccessPreviewEntries => successPreviewEntries.Count > 0;
    public bool HasFailurePreviewEntries => failurePreviewEntries.Count > 0;

    public DHWorldEventChoicePresentationOption(
        string choiceId,
        string choiceText,
        bool isCancel,
        bool isEnabled,
        string disabledReason,
        float successRatePercent,
        string successRateText,
        string successResultGroupId,
        string failureResultGroupId,
        IEnumerable<DHWorldEventPreviewEntry> successPreviewEntries = null,
        IEnumerable<DHWorldEventPreviewEntry> failurePreviewEntries = null)
    {
        ChoiceId = Normalize(choiceId);
        ChoiceText = Normalize(choiceText);
        IsCancel = isCancel;
        IsEnabled = isEnabled;
        DisabledReason = Normalize(disabledReason);
        SuccessRatePercent = successRatePercent;
        SuccessRateText = Normalize(successRateText);
        SuccessResultGroupId = Normalize(successResultGroupId);
        FailureResultGroupId = Normalize(failureResultGroupId);
        this.successPreviewEntries = successPreviewEntries != null
            ? new List<DHWorldEventPreviewEntry>(successPreviewEntries)
            : new List<DHWorldEventPreviewEntry>();
        this.failurePreviewEntries = failurePreviewEntries != null
            ? new List<DHWorldEventPreviewEntry>(failurePreviewEntries)
            : new List<DHWorldEventPreviewEntry>();
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class DHWorldEventPresentationRequest
{
    private readonly List<DHWorldEventChoicePresentationOption> choiceOptions;
    private readonly List<DHWorldEventPreviewEntry> previewEntries;

    public string WorldEventId { get; }
    public int ZoneNo { get; }
    public int NpcType { get; }
    public DHWorldEventType EventType { get; }
    public DHWorldEventSourceType SourceType { get; }
    public DHWorldEventPresentationStyle Style { get; }
    public DHWorldEventPresentationStep Step { get; }
    public string WorldEventName { get; }
    public string MessageText { get; }
    public string DescriptionText { get; }
    public string ProceedText { get; }
    public string DeclineText { get; }
    public string AcceptText { get; }
    public string CancelText { get; }
    public bool CanProceed { get; }
    public string DisabledReason { get; }
    public IReadOnlyList<DHWorldEventChoicePresentationOption> ChoiceOptions => choiceOptions;
    public IReadOnlyList<DHWorldEventPreviewEntry> PreviewEntries => previewEntries;
    public bool HasChoiceOptions => choiceOptions.Count > 0;
    public bool HasPreviewEntries => previewEntries.Count > 0;
    public bool IsWaitingFinalConfirm => Step == DHWorldEventPresentationStep.Accept || Step == DHWorldEventPresentationStep.Cancel;

    public DHWorldEventPresentationRequest(
        DHWorldEventTemplate template,
        DHWorldEventPresentationStep step,
        string messageText,
        bool canProceed,
        string disabledReason,
        IEnumerable<DHWorldEventChoicePresentationOption> choiceOptions = null,
        IEnumerable<DHWorldEventPreviewEntry> previewEntries = null)
    {
        WorldEventId = template != null ? template.WorldEventId : string.Empty;
        ZoneNo = template != null ? template.ZoneNo : 0;
        NpcType = template != null ? template.NpcType : 0;
        EventType = template != null ? template.EventType : DHWorldEventType.None;
        SourceType = template != null ? template.SourceType : DHWorldEventSourceType.None;
        Style = ResolveStyle(EventType, SourceType);
        Step = step;
        WorldEventName = template != null ? template.EventName : string.Empty;
        MessageText = Normalize(messageText);
        DescriptionText = template != null ? template.Description : string.Empty;
        ProceedText = template != null ? template.ProceedText : string.Empty;
        DeclineText = template != null ? template.DeclineText : string.Empty;
        AcceptText = template != null ? template.AcceptText : string.Empty;
        CancelText = template != null ? template.CancelText : string.Empty;
        CanProceed = canProceed;
        DisabledReason = Normalize(disabledReason);
        this.choiceOptions = choiceOptions != null
            ? new List<DHWorldEventChoicePresentationOption>(choiceOptions)
            : new List<DHWorldEventChoicePresentationOption>();
        this.previewEntries = previewEntries != null
            ? new List<DHWorldEventPreviewEntry>(previewEntries)
            : new List<DHWorldEventPreviewEntry>();
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static DHWorldEventPresentationStyle ResolveStyle(
        DHWorldEventType eventType,
        DHWorldEventSourceType sourceType)
    {
        if (sourceType == DHWorldEventSourceType.Npc)
        {
            return eventType switch
            {
                DHWorldEventType.Consume => DHWorldEventPresentationStyle.NpcConsume,
                DHWorldEventType.Reward => DHWorldEventPresentationStyle.NpcReward,
                DHWorldEventType.Choice => DHWorldEventPresentationStyle.NpcChoice,
                _ => DHWorldEventPresentationStyle.None
            };
        }

        if (sourceType == DHWorldEventSourceType.Condition)
        {
            return eventType switch
            {
                DHWorldEventType.Consume => DHWorldEventPresentationStyle.ConditionConsume,
                DHWorldEventType.Reward => DHWorldEventPresentationStyle.ConditionReward,
                DHWorldEventType.Choice => DHWorldEventPresentationStyle.ConditionChoice,
                _ => DHWorldEventPresentationStyle.None
            };
        }

        return DHWorldEventPresentationStyle.None;
    }
}
