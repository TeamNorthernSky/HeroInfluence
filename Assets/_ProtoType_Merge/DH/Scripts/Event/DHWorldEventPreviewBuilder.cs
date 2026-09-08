using System.Collections.Generic;

public static class DHWorldEventPreviewBuilder
{
    private const int ChoiceResultScopeNone = 0;
    private const int ChoiceResultScopeResource = 1;
    private const int ChoiceResultScopeParty = 2;

    public static IReadOnlyList<DHWorldEventPreviewEntry> Build(DHWorldEventTemplate template)
    {
        var entries = new List<DHWorldEventPreviewEntry>();
        if (template == null)
            return entries;

        IReadOnlyList<DHWorldEventResultTemplate> results = template.Results;
        for (int i = 0; i < results.Count; i++)
        {
            DHWorldEventResultTemplate result = results[i];
            if (result.EffectAmount == 0)
                continue;

            switch (result.ResultKind)
            {
                case DHWorldEventResultKind.ResourceCost:
                    entries.Add(new DHWorldEventPreviewEntry(
                        DHWorldEventPreviewGroup.Cost,
                        result.EffectType,
                        System.Math.Abs(result.EffectAmount),
                        ResolveResourceLabel(result.EffectType)));
                    break;
                case DHWorldEventResultKind.StatusEffect:
                    entries.Add(new DHWorldEventPreviewEntry(
                        DHWorldEventPreviewGroup.Result,
                        result.EffectType,
                        result.EffectAmount,
                        ResolveStatusLabel(result.EffectType)));
                    break;
            }
        }

        IReadOnlyList<DHWorldEventRewardEntry> rewards = template.Rewards;
        for (int i = 0; i < rewards.Count; i++)
        {
            DHWorldEventRewardEntry reward = rewards[i];
            if (reward.RewardAmount == 0)
                continue;

            entries.Add(new DHWorldEventPreviewEntry(
                DHWorldEventPreviewGroup.Reward,
                reward.RewardType,
                reward.RewardAmount,
                ResolveRewardLabel(reward.RewardType)));
        }

        return entries;
    }

    public static IReadOnlyList<DHWorldEventPreviewEntry> BuildChoiceResult(
        IReadOnlyList<DHWorldEventResultTemplate> results)
    {
        var entries = new List<DHWorldEventPreviewEntry>();
        if (results == null)
            return entries;

        for (int i = 0; i < results.Count; i++)
        {
            DHWorldEventResultTemplate result = results[i];
            if (result.ResultKind != DHWorldEventResultKind.ChoiceEffect ||
                result.EffectAmount == 0 ||
                result.TargetScope == ChoiceResultScopeNone)
            {
                continue;
            }

            if (result.TargetScope == ChoiceResultScopeResource)
            {
                entries.Add(new DHWorldEventPreviewEntry(
                    DHWorldEventPreviewGroup.Result,
                    result.EffectType,
                    result.EffectAmount,
                    ResolveResourceLabel(result.EffectType)));
            }
            else if (result.TargetScope == ChoiceResultScopeParty)
            {
                entries.Add(new DHWorldEventPreviewEntry(
                    DHWorldEventPreviewGroup.Result,
                    result.EffectType,
                    result.EffectAmount,
                    ResolveStatusLabel(result.EffectType)));
            }
        }

        return entries;
    }

    private static string ResolveResourceLabel(int code)
    {
        return DHWorldEventCodeMap.TryGetResourceType(code, out ResourceType resourceType)
            ? resourceType.ToString()
            : code.ToString();
    }

    private static string ResolveStatusLabel(int code)
    {
        return DHWorldEventCodeMap.TryGetStatusType(code, out DHWorldEventStatusType statusType)
            ? statusType.ToString()
            : code.ToString();
    }

    private static string ResolveRewardLabel(int code)
    {
        if (!DHWorldEventCodeMap.TryGetRewardType(code, out DHWorldEventRewardType rewardType))
            return code.ToString();

        return rewardType == DHWorldEventRewardType.CurrentIP
            ? DHWorldEventStatusType.CurrentIP.ToString()
            : rewardType.ToString();
    }
}
