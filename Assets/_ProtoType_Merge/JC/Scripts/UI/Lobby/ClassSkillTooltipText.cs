/// <summary>
/// [JC 260619] 클래스 스킬 설명 본문 생성(연구소·HeroInfo 공용). 전투 UI와 동일하게
/// description의 수식을 선택 영웅과 스킬 레벨 기준 숫자로 표시한다.
/// (LabModalController.EffectText 로직 추출)
/// </summary>
public static class ClassSkillTooltipText
{
    // JC | 2026-11-06: 전투 IP 구간은 HUD와 동일하게 현재 IP의 소수점을 버려 판정합니다.
    // IP 0도 첫 구간에 포함하며, 계산용 IP나 전투 효과량은 변경하지 않습니다.
    public static int GetBattleIpBand(float currentInfluence)
    {
        int ip = (int)UnityEngine.Mathf.Max(0f, currentInfluence);
        return ip <= 50 ? 0 : ip <= 100 ? 1 : ip <= 150 ? 2 : 3;
    }

    // JC | 2026-11-06: PPT 1슬라이드와 JC가 확정한 네 구간의 전투 전용 표현입니다.
    public static string GetBattleIpDescription(float currentInfluence)
    {
        switch (GetBattleIpBand(currentInfluence))
        {
            case 0: return "적은 IP의 힘을 더해";
            case 1: return "보통 IP의 힘을 더해";
            case 2: return "많은 IP의 힘을 더해";
            default: return "매우 많은 IP의 힘을 더해";
        }
    }

    // JC | 2026-11-06: 확정된 전투 설명만 구성합니다. 기존 카탈로그의 ID/계열을 사용하며
    // 원본 Description·계수와 아래 BuildDesc/습득 안내는 보존하여 전투 밖 표시에 영향을 주지 않습니다.
    // 기본/+ 본문은 같고 이름은 호출처가 그대로 표시합니다. 튜토리얼/테스트도 이 경로를 사용합니다.
    public static string BuildBattleDesc(int skillIndex, bool weapon, float currentInfluence)
    {
        string description;
        if (weapon)
        {
            switch (skillIndex)
            {
                case 1: description = "적 1명에게 {IPdescription} 대미지를 준다."; break;
                case 2: description = "모든 적에게 {IPdescription} 대미지를 준다."; break;
                case 3: description = "히어로젬의 힘으로 보호막을 생성하여, 다음 자신의 차례 전까지 자신이 받는 대미지를 감소시킨다."; break;
                case 4: description = "히어로젬의 힘으로 아군 1명의 체력을 회복한다."; break;
                case 5: description = "히어로젬의 힘으로 모든 적의 방어력을 1턴 동안 감소시킨다."; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(skillIndex), skillIndex, "전투용 코어 스킬 설명이 확정되지 않았습니다.");
            }
        }
        else
        {
            switch (HeroSkillRules.FamilyId(skillIndex))
            {
                case 1010: description = "적 1명에게 {IPdescription} 대미지를 주고, 다음 자신의 차례 전까지 모든 적의 단일 공격 대상을 자신으로 변경한다."; break;
                case 1020: description = "적 1명에게 {IPdescription} 대미지를 주며, 대상이 전열에 있을 경우 추가 대미지를 준다."; break;
                // JC | 2026-11-06: 대쉬는 후열 추가 피해로 JC 확인을 받았으며 전열 보너스 스킬과 구분합니다.
                case 1030: description = "적을 관통해 해당 가로줄의 적들에게 {IPdescription} 대미지를 주며, 후열의 적에게는 추가 대미지를 준다."; break;
                case 1040: description = "모든 적에게 {IPdescription} 대미지를 주고, 다음 자신의 차례 전까지 모든 적의 단일 공격 대상을 자신으로 변경한다."; break;
                case 2010: description = "적 1명에게 {IPdescription} 대미지를 준다."; break;
                case 2020: description = "선택한 적 1명에게 {IPdescription} 대미지를 주고, 인접한 적 1명에게도 대미지를 준다."; break;
                case 2030: description = "선택한 세로줄에 있는 모든 적에게 {IPdescription} 대미지를 준다."; break;
                case 2040: description = "모든 적에게 {IPdescription} 대미지를 주며, 현재 체력이 최대 체력의 50% 미만인 대상에게는 추가 대미지를 준다."; break;
                case 3010: description = "적 1명에게 {IPdescription} 대미지를 준다."; break;
                case 3020: description = "적 1명에게 {IPdescription} 대미지를 주며, 대상이 전열에 있을 경우 추가 대미지를 준다."; break;
                case 3030: description = "적 1명을 {IPdescription} 2회 공격한다. 치명타와 피해 경감 판정은 각 공격마다 별도로 적용된다."; break;
                // JC | 2026-11-06: 마지막 확인에서 JC가 지정한 현재 체력 기준 정성 문구를 그대로 사용합니다.
                case 3040: description = "적 1명에게 {IPdescription} 대미지를 준다. 대상의 현재 체력이 낮을수록 더 큰 대미지를 준다."; break;
                case 4010: description = "아군 1명의 체력을 회복한다."; break;
                case 4020: description = "적 1명에게 {IPdescription} 대미지를 주고, 실제 입힌 대미지만큼 무작위 아군 1명의 체력을 회복한다."; break;
                case 4030: description = "모든 아군의 체력을 회복한다."; break;
                case 4040: description = "리타이어한 아군 1명의 체력을 일부 회복시켜 부활시키고, 모든 적에게 {IPdescription} 대미지를 준다. 부활 효과는 전투당 1회만 적용되며, 이후 사용 시에는 적 전체 공격 효과만 적용된다."; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(skillIndex), skillIndex, "전투용 히어로 스킬 설명이 확정되지 않았습니다.");
            }
        }
        // JC | 2026-11-06: IP가 직접 반영되는 공격 문장에만 토큰이 있고 회복/방어 문장에는 없습니다.
        return description.Replace("{IPdescription}", GetBattleIpDescription(currentInfluence));
    }

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
