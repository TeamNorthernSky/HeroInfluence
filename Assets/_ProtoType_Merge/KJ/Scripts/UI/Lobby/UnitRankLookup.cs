using System.Collections.Generic;

/// <summary>
/// [KJ 260910] 영웅 레벨 → 랭크 문자열(F, E, … SSS).
/// 성장 테이블에서 Level ≤ 입력 레벨인 마지막 행의 Rank를 쓴다. 테이블은 레벨 오름차순 전제
/// (SkillSelectionPanel에 있던 기존 로직을 공용으로 옮긴 것).
/// </summary>
public static class UnitRankLookup
{
    public const string Unknown = "-";

    public static string GetRank(int level)
    {
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null) return Unknown;
        return ResolveRank(catalog.GetUnitGrowthTemplates(), level);
    }

    public static string ResolveRank(IReadOnlyList<DHUnitGrowthTemplate> table, int level)
    {
        if (table == null) return Unknown;

        DHUnitGrowthTemplate match = null;
        foreach (var row in table)
        {
            if (row == null) continue;
            if (row.Level <= level) match = row;
            else break;
        }
        return match != null && !string.IsNullOrEmpty(match.Rank) ? match.Rank : Unknown;
    }
}
