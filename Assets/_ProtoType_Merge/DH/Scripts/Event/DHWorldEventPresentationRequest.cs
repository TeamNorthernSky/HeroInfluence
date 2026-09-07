using System.Collections.Generic;

public enum DHWorldEventPresentationStep
{
    None = 0,
    Description = 1,
    Accept = 2,
    Cancel = 3
}

public sealed class DHWorldEventChoicePresentationOption
{
    public string ChoiceId { get; }
    public string ChoiceText { get; }
    public bool IsCancel { get; }
    public bool IsEnabled { get; }
    public string DisabledReason { get; }
    public float SuccessRatePercent { get; }
    public string SuccessRateText { get; }
    public string SuccessResultGroupId { get; }
    public string FailureResultGroupId { get; }

    public DHWorldEventChoicePresentationOption(
        string choiceId,
        string choiceText,
        bool isCancel,
        bool isEnabled,
        string disabledReason,
        float successRatePercent,
        string successRateText,
        string successResultGroupId,
        string failureResultGroupId)
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
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class DHWorldEventPresentationRequest
{
    private readonly List<DHWorldEventChoicePresentationOption> choiceOptions;

    public string WorldEventId { get; }
    public int ZoneNo { get; }
    public int NpcType { get; }
    public DHWorldEventType EventType { get; }
    public DHWorldEventSourceType SourceType { get; }
    public DHWorldEventPresentationStep Step { get; }
    public string MessageText { get; }
    public string DescriptionText { get; }
    public string ProceedText { get; }
    public string DeclineText { get; }
    public string AcceptText { get; }
    public string CancelText { get; }
    public bool CanProceed { get; }
    public string DisabledReason { get; }
    public IReadOnlyList<DHWorldEventChoicePresentationOption> ChoiceOptions => choiceOptions;
    public bool HasChoiceOptions => choiceOptions.Count > 0;
    public bool IsWaitingFinalConfirm => Step == DHWorldEventPresentationStep.Accept || Step == DHWorldEventPresentationStep.Cancel;

    public DHWorldEventPresentationRequest(
        DHWorldEventTemplate template,
        DHWorldEventPresentationStep step,
        string messageText,
        bool canProceed,
        string disabledReason,
        IEnumerable<DHWorldEventChoicePresentationOption> choiceOptions = null)
    {
        WorldEventId = template != null ? template.WorldEventId : string.Empty;
        ZoneNo = template != null ? template.ZoneNo : 0;
        NpcType = template != null ? template.NpcType : 0;
        EventType = template != null ? template.EventType : DHWorldEventType.None;
        SourceType = template != null ? template.SourceType : DHWorldEventSourceType.None;
        Step = step;
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
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
