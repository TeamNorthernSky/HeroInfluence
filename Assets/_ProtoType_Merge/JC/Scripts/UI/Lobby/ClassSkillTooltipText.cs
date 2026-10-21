/// <summary>
/// [JC 260619] 클래스 스킬 설명 본문 생성(연구소·HeroInfo 공용). 전투 UI와 동일하게
/// description의 수식을 선택 영웅과 스킬 레벨 기준 숫자로 표시한다.
/// (LabModalController.EffectText 로직 추출)
/// </summary>
public static class ClassSkillTooltipText
{
    public static string BuildDesc(DHClassSkillTemplate s, int level, bool percentageValues = false, int unitIndex = 0, BattleCharactor caster = null)
    {
        if (s == null) return string.Empty;
        string d = s.Description ?? string.Empty;
        var cat = DHCsvTemplateCatalog.Instance;
        float v = cat != null ? cat.GetClassSkillValueAtLevel(s.NumericSkillId, level) : s.ValueLv1;
        float sub = cat != null ? cat.GetClassSkillSubValueAtLevel(s.NumericSkillId, level) : s.SubValueLv1;
        return percentageValues ? ReplaceBattleCoefficients(d, "ClassSkill", v, sub)
            : SkillTooltipNumbers.Build(d, "ClassSkill", v, sub, unitIndex, caster, s.Effect == 0);
    }

    /// <summary>[임시 브리지 260805] ASB BattleCharactor 가 아직 SkillData 를 노출(KJ SkillButtonTooltip 소비).
    /// ASB 측 템플릿 전환이 끝나면 이 오버로드는 제거한다.</summary>
    public static string BuildDesc(SkillData s, int level, bool percentageValues = false, int unitIndex = 0, BattleCharactor caster = null)
    {
        if (s == null) return string.Empty;
        var cat = DHCsvTemplateCatalog.Instance;
        if (cat != null && cat.TryGetClassSkillTemplate(s.skillIndex, out var template) && template != null)
            return BuildDesc(template, level, percentageValues, unitIndex, caster);

        // 카탈로그 미로드 폴백 — 행 자체 값으로 1레벨 표시
        string d = s.description ?? string.Empty;
        return percentageValues ? ReplaceBattleCoefficients(d, "ClassSkill", s.skillValue, s.skillSubValue)
            : SkillTooltipNumbers.Build(d, "ClassSkill", s.skillValue, s.skillSubValue, unitIndex, caster, s.classSkillEffect == 0);
    }
    /// <summary>전투 표시용 계수 토큰만 백분율로 바꿉니다. IP·횟수·기존 숫자는 변경하지 않습니다.</summary>
    internal static string ReplaceBattleCoefficients(string description, string prefix, float value, float subValue)
    {
        string result = (description ?? string.Empty).Replace("{SubClassSkillSubValue}", "{ClassSkillSubValue}");
        result = ReplacePercentage(result, "{" + prefix + "Value}", value);
        return FormatBattleDescription(ReplacePercentage(result, "{" + prefix + "SubValue}", subValue));
    }

    /// <summary>습득 안내 전용 문장 변환입니다. 다른 툴팁의 표시 방식은 유지합니다.</summary>
    internal static string BuildAcquisitionDescription(string description, float value, float subValue)
    {
        return ReplaceBattleCoefficients(description, "ClassSkill", value, subValue);
    }

    /// <summary>전투 화면에서만 사용합니다. 수치 계산 없이 표시용 능력치 수식을 문장으로 바꿉니다.</summary>
    internal static string FormatBattleDescription(string description)
    {
        string result = description ?? string.Empty;
        result = FormatStatFormula(result, "UnitATK", "기본 공격력");
        result = FormatStatFormula(result, "UnitMaxHP", "최대 체력");
        result = result.Replace("{UnitATK}", "기본 공격력").Replace("{UnitMaxHP}", "최대 체력");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"(?<![A-Za-z0-9_])UnitMaxHP(?![A-Za-z0-9_])", "최대 체력");
        result = result.Replace("{TotalHostageAggro}", "전체 인질의 어그로 합계")
            .Replace("{HostageAggro}", "인질 어그로").Replace("{EnergyStack}", "에너지 중첩");
        return System.Text.RegularExpressions.Regex.Replace(result, @"(의)(?=최대 체력|[0-9]+(?:\.[0-9]+)?%)", "$1 ");
    }

    private static string FormatStatFormula(string text, string token, string label)
    {
        string stat = @"(?:기본\s*)?\{" + token + @"\}\s*[*×]\s*";
        const string percentage = @"(?<percent>[0-9]+(?:\.[0-9]+)?)\s*[%％]";
        string pattern = stat + percentage + @"(?:\s*\+\s*" + stat + percentage + @")*";
        return System.Text.RegularExpressions.Regex.Replace(text, pattern, match =>
        {
            float total = 0f;
            foreach (System.Text.RegularExpressions.Capture part in match.Groups["percent"].Captures)
                total += float.Parse(part.Value, System.Globalization.CultureInfo.InvariantCulture);
            return label + "의 " + total.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
        });
    }

    private static string ReplacePercentage(string text, string token, float value)
    {
        string pattern = System.Text.RegularExpressions.Regex.Escape(token) + @"(?:[ \t]*[%％])?";
        string formatted = (value * 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
        return System.Text.RegularExpressions.Regex.Replace(text, pattern, formatted);
    }
}
