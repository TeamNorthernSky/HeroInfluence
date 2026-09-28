/// <summary>교환소 레벨별 지불·수령량을 AssociationResourceCatalog에서 조회한다.</summary>
public static class ExchangeData
{
    private static int CurrentLevel => GameManager.Instance != null && GameManager.Instance.HQ != null
        ? GameManager.Instance.HQ.GetLevel(HQDepartment.Exchange) : 0;

    public static bool TryGetExchange(ResourceType give, ResourceType receive, int level,
        out DHAssociationExchangeTemplate row)
    {
        row = null;
        var catalog = AssociationResourceCatalog.Instance;
        if (catalog == null || level < 1 || give == receive) return false;
        foreach (var candidate in catalog.GetExchangeTemplates(level))
        {
            if (candidate.Cost == null || candidate.Reward == null
                || candidate.Cost.ResourceType != (int)give || candidate.Reward.ResourceType != (int)receive) continue;
            if (candidate.Cost.Amount <= 0 || candidate.Reward.Amount <= 0 || row != null) return false;
            row = candidate;
        }
        return row != null;
    }

    public static int GetBatchUnit(ResourceType give) => GetBatchUnit(give, CurrentLevel);

    // 수령 대상을 선택하기 전에도 지불량을 선택하는 UI: 모든 유효 환율의 공통 배수.
    public static int GetBatchUnit(ResourceType give, int level)
    {
        int unit = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!TryGetExchange(give, (ResourceType)i, level, out var row)) continue;
            int amount = row.Cost.Amount;
            if (unit == 0) { unit = amount; continue; }
            int a = unit, b = amount;
            while (b != 0) { int remainder = a % b; a = b; b = remainder; }
            long common = (long)(unit / a) * amount;
            if (common > int.MaxValue) return 0;
            unit = (int)common;
        }
        return unit;
    }

    public static bool CanExchange(ResourceType give, ResourceType receive)
        => GetReceivePerBatch(give, receive, CurrentLevel) > 0;

    public static int GetBaseReceive(ResourceType give, ResourceType receive)
        => GetReceivePerBatch(give, receive, 1);

    public static int GetReceivePerBatch(ResourceType give, ResourceType receive, int exchangeLevel)
    {
        if (!TryGetExchange(give, receive, exchangeLevel, out var row)) return 0;
        int unit = GetBatchUnit(give, exchangeLevel);
        long received = (long)(unit / row.Cost.Amount) * row.Reward.Amount;
        return received <= int.MaxValue ? (int)received : 0;
    }
}
