using System.Collections.Generic;

/// <summary>AssociationResourceCatalog와 협회 UI의 비용 API 사이의 읽기 전용 어댑터.</summary>
public static class AssociationCosts
{
    public static bool TryBuild(HQDepartment department, int targetLevel, out DHAssociationBuildTemplate row)
    {
        row = null;
        var catalog = AssociationResourceCatalog.Instance;
        if (catalog == null || targetLevel < 1) return false;
        string name = HQStateManager.GetDepartmentKoreanName(department);
        int action = targetLevel == 1 ? 0 : 1;
        // V1.7 room_type에는 레벨까지 포함된다. 의무실 행은 공백이 없다.
        return catalog.TryGetBuildTemplate($"{name} Lv.{targetLevel}", targetLevel, action, out row)
            || catalog.TryGetBuildTemplate($"{name}Lv.{targetLevel}", targetLevel, action, out row)
            || catalog.TryGetBuildTemplate(name, targetLevel, action, out row);
    }

    public static bool TryConvert(IReadOnlyList<DHAssociationResourceCost> source,
        out Dictionary<ResourceType, int> costs)
    {
        costs = null;
        if (source == null) return false;
        var result = new Dictionary<ResourceType, int>
        {
            { ResourceType.Money, 0 }, { ResourceType.Chip, 0 },
            { ResourceType.Crystal, 0 }, { ResourceType.Supply, 0 }
        };
        foreach (var item in source)
        {
            if (item == null || item.Amount < 0 || !result.ContainsKey((ResourceType)item.ResourceType)) return false;
            var type = (ResourceType)item.ResourceType;
            long total = (long)result[type] + item.Amount;
            if (total > int.MaxValue) return false;
            result[type] = (int)total;
        }
        costs = result;
        return true;
    }

    // 기존 모달의 표시 슬롯에 없는 자원을 조용히 누락해 무료로 처리하지 않는다.
    public static bool TryPair(IReadOnlyList<DHAssociationResourceCost> source,
        ResourceType second, out int money, out int other)
    {
        money = other = -1;
        if (!TryConvert(source, out var costs)) return false;
        foreach (var pair in costs)
            if (pair.Key != ResourceType.Money && pair.Key != second && pair.Value != 0) return false;
        money = costs[ResourceType.Money];
        other = costs[second];
        return true;
    }

    public static bool TryMoney(DHAssociationResourceCost cost, out int money)
    {
        money = -1;
        if (cost == null || cost.ResourceType != (int)ResourceType.Money || cost.Amount < 0) return false;
        money = cost.Amount;
        return true;
    }

    public static bool TryLab(int targetLevel, out DHAssociationLabTemplate row)
    {
        row = null;
        var catalog = AssociationResourceCatalog.Instance;
        return catalog != null && catalog.TryGetLabTemplate("모든 보유 스킬", targetLevel, out row);
    }

    public static bool TryTraining(TrainingStat stat, int targetLevel, out DHAssociationTrainingTemplate row)
    {
        row = null;
        var catalog = AssociationResourceCatalog.Instance;
        if (catalog == null || targetLevel < 1) return false;
        int status = stat == TrainingStat.Attack ? 4 : stat == TrainingStat.Health ? 3 : -1;
        foreach (var candidate in catalog.GetTrainingTemplates(targetLevel))
        {
            if (candidate.StatusType != status) continue;
            if (row != null) { row = null; return false; } // 모호한 행은 실행하지 않는다.
            row = candidate;
        }
        return row != null;
    }

    public static bool TryPromotion(int level, out DHAssociationPromotionTemplate row)
    {
        row = null;
        var catalog = AssociationResourceCatalog.Instance;
        if (catalog == null || level < 1) return false;
        var rows = catalog.GetPromotionTemplates(level);
        if (rows.Count != 1) return false;
        row = rows[0];
        return row != null;
    }
}
