/// <summary>
/// [JC 260619] 클래스 스킬 설명 본문 생성(연구소·HeroInfo 공용). 전투 UI와 동일하게
/// description의 {ClassSkillValue}/{ClassSkillSubValue}를 레벨별 계수로 치환한다.
/// (LabModalController.EffectText 로직 추출)
/// </summary>
public static class ClassSkillTooltipText
{
    public static string BuildDesc(DHClassSkillTemplate s, int level, bool percentageValues = false)
    {
        if (s == null) return string.Empty;
        string d = s.Description ?? string.Empty;
        var cat = DHCsvTemplateCatalog.Instance;
        float v = cat != null ? cat.GetClassSkillValueAtLevel(s.NumericSkillId, level) : s.ValueLv1;
        float sub = cat != null ? cat.GetClassSkillSubValueAtLevel(s.NumericSkillId, level) : s.SubValueLv1;
        bool atk = s.Effect == 0;
        string vStr = atk ? $"기본 피해량 × {v:0.##}" : $"{v:0.##}";
        string subStr = atk ? $"기본 피해량 × {sub:0.##}" : $"{sub:0.##}";
        return (percentageValues
            ? ReplaceBattleCoefficients(d, "ClassSkill", v, sub)
            : d.Replace("{ClassSkillValue}", vStr).Replace("{ClassSkillSubValue}", subStr))
            + $"\n협회 강화 {(level <= 1 ? "기본" : "+" + (level - 1))} · 리스크 확률 {HeroSkillRules.RiskChance(level) * 100f:0}%";
    }

    /// <summary>[임시 브리지 260805] ASB BattleCharactor 가 아직 SkillData 를 노출(KJ SkillButtonTooltip 소비).
    /// ASB 측 템플릿 전환이 끝나면 이 오버로드는 제거한다.</summary>
    public static string BuildDesc(SkillData s, int level, bool percentageValues = false)
    {
        if (s == null) return string.Empty;
        var cat = DHCsvTemplateCatalog.Instance;
        if (cat != null && cat.TryGetClassSkillTemplate(s.skillIndex, out var template) && template != null)
            return BuildDesc(template, level, percentageValues);

        // 카탈로그 미로드 폴백 — 행 자체 값으로 1레벨 표시
        string d = s.description ?? string.Empty;
        bool atk = s.classSkillEffect == 0;
        string vStr = atk ? $"기본 피해량 × {s.skillValue:0.##}" : $"{s.skillValue:0.##}";
        string subStr = atk ? $"기본 피해량 × {s.skillSubValue:0.##}" : $"{s.skillSubValue:0.##}";
        return (percentageValues
            ? ReplaceBattleCoefficients(d, "ClassSkill", s.skillValue, s.skillSubValue)
            : d.Replace("{ClassSkillValue}", vStr).Replace("{ClassSkillSubValue}", subStr))
            + $"\n협회 강화 {(level <= 1 ? "기본" : "+" + (level - 1))} · 리스크 확률 {HeroSkillRules.RiskChance(level) * 100f:0}%";
    }
    /// <summary>전투 표시용 계수 토큰만 백분율로 바꿉니다. IP·횟수·기존 숫자는 변경하지 않습니다.</summary>
    internal static string ReplaceBattleCoefficients(string description, string prefix, float value, float subValue)
    {
        string result = description ?? string.Empty;
        result = ReplacePercentage(result, "{" + prefix + "Value}", value);
        return ReplacePercentage(result, "{" + prefix + "SubValue}", subValue);
    }

    private static string ReplacePercentage(string text, string token, float value)
    {
        string pattern = System.Text.RegularExpressions.Regex.Escape(token) + @"(?:[ \t]*[%％])?";
        string formatted = (value * 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
        return System.Text.RegularExpressions.Regex.Replace(text, pattern, formatted);
    }
}
