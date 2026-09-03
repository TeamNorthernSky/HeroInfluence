using System;
using System.Collections.Generic;

public enum DHWorldEventType
{
    None = 0,
    Consume = 1,
    Reward = 2,
    Choice = 3
}

public enum DHWorldEventSourceType
{
    None = 0,
    Npc = 1,
    Condition = 2
}

public enum DHWorldEventResultKind
{
    Unknown = 0,
    ResourceCost = 1,
    StatusEffect = 2,
    Reward = 3,
    ChoiceEffect = 4
}

[Serializable]
public sealed class DHWorldEventTemplate
{
    private readonly List<DHWorldEventConditionTemplate> triggerConditions;
    private readonly List<DHWorldEventChoiceTemplate> choices;
    private readonly List<DHWorldEventResultTemplate> results;
    private readonly List<DHWorldEventRewardEntry> rewards;

    public string WorldEventId { get; }
    public int ZoneNo { get; }
    public DHWorldEventType EventType { get; }
    public DHWorldEventSourceType SourceType { get; }
    public string EventName { get; }
    public int NpcType { get; }
    public string Description { get; }
    public string AcceptText { get; }
    public string CancelText { get; }
    public string ProceedText { get; }
    public string DeclineText { get; }
    public string ResultId { get; }
    public string Note { get; }

    public IReadOnlyList<DHWorldEventConditionTemplate> TriggerConditions => triggerConditions;
    public IReadOnlyList<DHWorldEventChoiceTemplate> Choices => choices;
    public IReadOnlyList<DHWorldEventResultTemplate> Results => results;
    public IReadOnlyList<DHWorldEventRewardEntry> Rewards => rewards;

    public DHWorldEventTemplate(
        string worldEventId,
        int zoneNo,
        DHWorldEventType eventType,
        DHWorldEventSourceType sourceType,
        string eventName,
        int npcType,
        string description,
        string acceptText,
        string cancelText,
        string proceedText,
        string declineText,
        string resultId,
        string note,
        IEnumerable<DHWorldEventConditionTemplate> triggerConditions = null,
        IEnumerable<DHWorldEventChoiceTemplate> choices = null,
        IEnumerable<DHWorldEventResultTemplate> results = null,
        IEnumerable<DHWorldEventRewardEntry> rewards = null)
    {
        WorldEventId = Normalize(worldEventId);
        ZoneNo = zoneNo;
        EventType = eventType;
        SourceType = sourceType;
        EventName = Normalize(eventName);
        NpcType = npcType;
        Description = NormalizeMultiline(description);
        AcceptText = NormalizeMultiline(acceptText);
        CancelText = NormalizeMultiline(cancelText);
        ProceedText = NormalizeMultiline(proceedText);
        DeclineText = NormalizeMultiline(declineText);
        ResultId = Normalize(resultId);
        Note = NormalizeMultiline(note);

        this.triggerConditions = triggerConditions != null
            ? new List<DHWorldEventConditionTemplate>(triggerConditions)
            : new List<DHWorldEventConditionTemplate>();
        this.choices = choices != null
            ? new List<DHWorldEventChoiceTemplate>(choices)
            : new List<DHWorldEventChoiceTemplate>();
        this.results = results != null
            ? new List<DHWorldEventResultTemplate>(results)
            : new List<DHWorldEventResultTemplate>();
        this.rewards = rewards != null
            ? new List<DHWorldEventRewardEntry>(rewards)
            : new List<DHWorldEventRewardEntry>();
    }

    public DHWorldEventTemplate WithChoices(IEnumerable<DHWorldEventChoiceTemplate> value)
    {
        return new DHWorldEventTemplate(
            WorldEventId,
            ZoneNo,
            EventType,
            SourceType,
            EventName,
            NpcType,
            Description,
            AcceptText,
            CancelText,
            ProceedText,
            DeclineText,
            ResultId,
            Note,
            triggerConditions,
            value,
            results,
            rewards);
    }

    public DHWorldEventTemplate WithResults(IEnumerable<DHWorldEventResultTemplate> value)
    {
        return new DHWorldEventTemplate(
            WorldEventId,
            ZoneNo,
            EventType,
            SourceType,
            EventName,
            NpcType,
            Description,
            AcceptText,
            CancelText,
            ProceedText,
            DeclineText,
            ResultId,
            Note,
            triggerConditions,
            choices,
            value,
            rewards);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeMultiline(string value)
    {
        return Normalize(value).Replace("\\n", "\n");
    }
}

[Serializable]
public readonly struct DHWorldEventConditionTemplate
{
    public int ConditionType { get; }
    public int Target { get; }
    public int StatType { get; }
    public int CalculationType { get; }
    public string Operator { get; }
    public int Value { get; }

    public DHWorldEventConditionTemplate(
        int conditionType,
        int target,
        int statType,
        int calculationType,
        string operatorText,
        int value)
    {
        ConditionType = conditionType;
        Target = target;
        StatType = statType;
        CalculationType = calculationType;
        Operator = string.IsNullOrWhiteSpace(operatorText) ? string.Empty : operatorText.Trim();
        Value = value;
    }
}

[Serializable]
public sealed class DHWorldEventChoiceTemplate
{
    public string ChoiceId { get; }
    public string ChoiceText { get; }
    public DHWorldEventConditionTemplate EnableCondition { get; }
    public int UseIpSuccessRateBonus { get; }
    public string BaseSuccessRateRaw { get; }
    public string SuccessResultGroupId { get; }
    public string FailureResultGroupId { get; }
    public string Note { get; }

    public DHWorldEventChoiceTemplate(
        string choiceId,
        string choiceText,
        DHWorldEventConditionTemplate enableCondition,
        int useIpSuccessRateBonus,
        string baseSuccessRateRaw,
        string successResultGroupId,
        string failureResultGroupId,
        string note)
    {
        ChoiceId = Normalize(choiceId);
        ChoiceText = NormalizeMultiline(choiceText);
        EnableCondition = enableCondition;
        UseIpSuccessRateBonus = useIpSuccessRateBonus;
        BaseSuccessRateRaw = Normalize(baseSuccessRateRaw);
        SuccessResultGroupId = Normalize(successResultGroupId);
        FailureResultGroupId = Normalize(failureResultGroupId);
        Note = NormalizeMultiline(note);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeMultiline(string value)
    {
        return Normalize(value).Replace("\\n", "\n");
    }
}

[Serializable]
public readonly struct DHWorldEventResultTemplate
{
    public DHWorldEventResultKind ResultKind { get; }
    public string ResultGroupId { get; }
    public int Order { get; }
    public int TargetScope { get; }
    public int EffectType { get; }
    public int EffectAmount { get; }
    public string Note { get; }

    public DHWorldEventResultTemplate(
        string resultGroupId,
        int order,
        int targetScope,
        int effectType,
        int effectAmount,
        string note)
        : this(DHWorldEventResultKind.ChoiceEffect, resultGroupId, order, targetScope, effectType, effectAmount, note)
    {
    }

    public DHWorldEventResultTemplate(
        DHWorldEventResultKind resultKind,
        string resultGroupId,
        int order,
        int targetScope,
        int effectType,
        int effectAmount,
        string note)
    {
        ResultKind = resultKind;
        ResultGroupId = string.IsNullOrWhiteSpace(resultGroupId) ? string.Empty : resultGroupId.Trim();
        Order = order;
        TargetScope = targetScope;
        EffectType = effectType;
        EffectAmount = effectAmount;
        Note = string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim().Replace("\\n", "\n");
    }
}

[Serializable]
public readonly struct DHWorldEventRewardEntry
{
    public int RewardType { get; }
    public int RewardAmount { get; }

    public DHWorldEventRewardEntry(int rewardType, int rewardAmount)
    {
        RewardType = rewardType;
        RewardAmount = rewardAmount;
    }
}
