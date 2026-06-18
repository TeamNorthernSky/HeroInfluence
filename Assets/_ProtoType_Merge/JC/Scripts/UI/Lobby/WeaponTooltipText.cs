using System.Text;

/// <summary>
/// [JC 260619] 무기/무기스킬 툴팁 본문 생성(공방·HeroInfo 공용). 무기 시트 V6.0 레벨 계수 치환.
/// WorkshopModalController.BuildLevelDesc 로직을 추출해 공유한다.
/// </summary>
public static class WeaponTooltipText
{
    /// <summary>무기 레벨 설명 = 무기스킬 효과(레벨 계수 치환) + 스탯 보너스. 공방 무기 호버와 동일.</summary>
    public static string BuildWeaponLevelDesc(WeaponData wd, int weaponIndex, int level)
    {
        if (wd == null) return string.Empty;
        var catalog = DHCsvTemplateCatalog.Instance;
        var sb = new StringBuilder();

        sb.AppendLine($"<b>[스킬] {wd.WeaponSkillName}</b>  (IP {wd.IPCost})");
        sb.Append(BuildSkillEffect(wd, weaponIndex, level, catalog));

        if (catalog != null && catalog.TryGetWeaponBonusAtLevel(weaponIndex, level, out var st))
        {
            var parts = new System.Collections.Generic.List<string>();
            if (st.Atk != 0f) parts.Add($"공격 +{st.Atk:0.##}");
            if (st.HP != 0f) parts.Add($"체력 +{st.HP:0.##}");
            if (st.DEF != 0f) parts.Add($"방어 +{st.DEF:0.##}");
            if (st.CriticalRate != 0f) parts.Add($"치명 +{st.CriticalRate * 100f:0.#}%");
            if (st.CounterRate != 0f) parts.Add($"반격 +{st.CounterRate * 100f:0.#}%");
            if (st.AvoidRate != 0f) parts.Add($"경감 +{st.AvoidRate * 100f:0.#}%");
            if (parts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append("<b>[스탯]</b> " + string.Join("  ", parts));
            }
        }
        return sb.ToString();
    }

    /// <summary>무기스킬만(현재 레벨 효과). HeroInfo 3번째 아이콘 B타입 툴팁용 — 이름은 ShowInfo가 별도 표시.</summary>
    public static string BuildWeaponSkillDesc(WeaponData wd, int weaponIndex, int level)
    {
        if (wd == null) return string.Empty;
        var catalog = DHCsvTemplateCatalog.Instance;
        var sb = new StringBuilder();
        sb.AppendLine($"(IP {wd.IPCost})");
        sb.Append(BuildSkillEffect(wd, weaponIndex, level, catalog));
        return sb.ToString();
    }

    private static string BuildSkillEffect(WeaponData wd, int weaponIndex, int level, DHCsvTemplateCatalog catalog)
    {
        float v = catalog != null ? catalog.GetWeaponSkillValueAtLevel(weaponIndex, level) : wd.WeaponSkillValue;
        float sv = catalog != null ? catalog.GetWeaponSkillSubValueAtLevel(weaponIndex, level) : wd.WeaponSkillSubValue;
        string valStr = wd.WeaponSkillEffect == 0 ? $"×{v:0.##}" : $"{v:0.##}";
        string subStr = wd.WeaponSkillEffect == 0 ? $"×{sv:0.##}" : $"{sv:0.##}";
        return (wd.WeaponSkillDescription ?? string.Empty)
            .Replace("{WeaponSkillValue}", valStr)
            .Replace("{WeaponSkillSubValue}", subStr);
    }
}
