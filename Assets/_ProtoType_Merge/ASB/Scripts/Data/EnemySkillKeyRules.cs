using System.Globalization;

/// <summary>
/// 적 스킬 키 단일 규격 (구현지시서 §5-11).
/// enemyKey + 슬롯(1/2) 조합으로 문자열 키를 만든다. 예: Compose("FV20001", 1) => "FV20001_1".
/// 기존에 흩어져 있던 (enemyIndex * 10) + slot 산술을 이 헬퍼 하나로 대체한다.
/// 대소문자·앞뒤 공백은 Trim만 적용(원본 보존), 구분자는 '_'.
/// </summary>
public static class EnemySkillKeyRules
{
    public const char SlotSeparator = '_';

    /// <summary>enemyKey와 슬롯을 조합해 적 스킬 키를 만든다. enemyKey가 비면 빈 문자열.</summary>
    public static string Compose(string enemyKey, int slot)
    {
        if (string.IsNullOrWhiteSpace(enemyKey))
        {
            return string.Empty;
        }
        return enemyKey.Trim() + SlotSeparator + slot.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>적 스킬 키에서 enemyKey와 슬롯을 분리한다. 규격에 안 맞으면 false.</summary>
    public static bool TryParse(string skillKey, out string enemyKey, out int slot)
    {
        enemyKey = string.Empty;
        slot = 0;
        if (string.IsNullOrWhiteSpace(skillKey))
        {
            return false;
        }

        int sep = skillKey.LastIndexOf(SlotSeparator);
        if (sep <= 0 || sep >= skillKey.Length - 1)
        {
            return false;
        }

        string slotPart = skillKey.Substring(sep + 1);
        if (!int.TryParse(slotPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot))
        {
            return false;
        }

        enemyKey = skillKey.Substring(0, sep);
        return true;
    }
}
