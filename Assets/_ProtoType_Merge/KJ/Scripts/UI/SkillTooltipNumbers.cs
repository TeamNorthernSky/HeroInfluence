using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Evaluates authored tooltip expressions without changing combat data or rolling damage.</summary>
public static class SkillTooltipNumbers
{
    public static string Build(string description, string prefix, float value, float subValue,
        int unitIndex = 0, BattleCharactor caster = null, bool attackEffect = false)
    {
        float? attack = null, health = null;
        if (caster != null)
        {
            attack = caster.FinalStats.Atk;
            health = caster.MaxHp;
        }
        else if (unitIndex > 0 && PersistentUnitRepository.Instance != null &&
                 PersistentUnitRepository.Instance.TryGetUnit(unitIndex, out var unit) && unit != null)
        {
            attack = unit.IngameStats.Atk;
            health = unit.IngameStats.HP;
        }
        return Format(description, prefix, value, subValue, attack, health, attackEffect);
    }

    public static string Format(string description, string prefix, float value, float subValue,
        float? attack, float? maxHealth, bool attackEffect = false)
    {
        string text = description ?? string.Empty;
        string valueToken = "{" + prefix + "Value}";
        string subToken = "{" + prefix + "SubValue}";
        // These refer to a future target, not the hero whose tooltip is open.
        text = text.Replace("UnitMaxHP의" + subToken, "최대 체력의 " + Percent(subValue));
        text = Regex.Replace(text, @"((?:해당|선택한) 아군 최대 체력의\s*)" + Regex.Escape(subToken),
            match => match.Groups[1].Value + Percent(subValue));
        text = Regex.Replace(text, @"(?:네코밍|자신|사용한 히어로의?)\s*최대 체력의\s*(" +
            Regex.Escape(valueToken) + "|" + Regex.Escape(subToken) + ")",
            match => "{UnitMaxHP}*" + match.Groups[1].Value);
        text = text.Replace("방어력의 " + valueToken, "방어력의 " + Percent(value));
        text = text.Replace(valueToken + "%", Percent(value)).Replace(subToken + "%", Percent(subValue));
        string token = @"\{(?:UnitATK|UnitMaxHP|" + Regex.Escape(prefix) + @"(?:Sub)?Value)\}";
        string operand = @"(?:" + token + @"|\d+(?:\.\d+)?)";
        string expression = operand + @"(?:\s*[*×+]\s*" + operand + @")*";
        return Regex.Replace(text, expression, match =>
        {
            string source = match.Value;
            if (!source.Contains("{")) return source;
            bool implicitAttack = attackEffect && !source.Contains("{UnitATK}") && !source.Contains("{UnitMaxHP}");
            bool usesAttack = source.Contains("{UnitATK}") || implicitAttack;
            bool usesHealth = source.Contains("{UnitMaxHP}");
            if ((usesAttack && !attack.HasValue) || (usesHealth && !maxHealth.HasValue))
                return "영웅 선택 필요";
            string resolved = source.Replace("{UnitATK}", Number(attack ?? 0))
                .Replace("{UnitMaxHP}", Number(maxHealth ?? 0))
                .Replace(valueToken, Number(value)).Replace(subToken, Number(subValue));
            double sum = 0;
            foreach (string term in resolved.Split('+'))
            {
                double product = 1;
                foreach (string factor in term.Split('*', '×'))
                    product *= double.Parse(factor.Trim(), CultureInfo.InvariantCulture);
                sum += product;
            }
            if (implicitAttack) sum *= attack.Value;
            if (usesAttack) sum = System.Math.Floor(sum + .5);
            return sum.ToString("0.##", CultureInfo.InvariantCulture);
        });
    }

    /// <summary>백분율 피해량의 조사만 통일하며 회복량·횟수 등은 유지한다.</summary>
    public static string FormatPercentageDamage(string description)
    {
        return Regex.Replace(description ?? string.Empty,
            @"([0-9]+(?:\.[0-9]+)?[%％])\s*(?:의\s*)?대미지", "$1의 대미지");
    }

    /// <summary>표시 화면에서 요청한 경우에만 IP 설명 자리표시자를 생략한다.</summary>
    public static string OmitIPDescription(string description)
    {
        return Regex.Replace(description ?? string.Empty,
            @"\{IPDescription\}[ \t]*", string.Empty, RegexOptions.IgnoreCase).Trim();
    }

    private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Percent(float value) => (value * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
