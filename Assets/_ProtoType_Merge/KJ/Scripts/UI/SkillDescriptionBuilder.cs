public static class SkillDescriptionBuilder
{
    // 설명문의 {토큰}을 SkillData 수치로 치환합니다. (대소문자 무시)
    // 지원 토큰:
    //   {skillValue}      → skillValue × 100 (%)
    //   {skillSubValue}   → skillSubValue × 100 (%)
    //   {ipCost}          → ipCost
    //   {multiTargetCount}→ multiTargetCount + 1 (실제 타깃 수)
    //   {weaponSkillValue}, {weaponSkillSubValue}, {weaponIPCost}, {weaponTargetCount}
    public static string Resolve(SkillData skill)
    {
        if (skill == null || string.IsNullOrEmpty(skill.description))
            return string.Empty;

        var tokens = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            //{ "SkillValue",          ((int)(skill.skillValue * 100)).ToString() },
            //{ "SkillSubValue",       ((int)(skill.skillSubValue * 100)).ToString() },
            { "IPCost",              skill.ipCost.ToString() },
            { "MultiTargetCount",    (skill.multiTargetCount + 1).ToString() },
            { "ClassSkillValue",     ((int)(skill.skillValue * 100)).ToString() },
            { "ClassSkillSubValue",  ((int)(skill.skillSubValue * 100)).ToString() },
            { "ClassIPCost",         skill.ipCost.ToString() },
            { "ClassTargetCount",    (skill.multiTargetCount + 1).ToString() },
            { "WeaponSkillValue",    ((int)(skill.skillValue * 100)).ToString() },
            { "WeaponSkillSubValue", ((int)(skill.skillSubValue * 100)).ToString() },
            { "WeaponIPCost",        skill.ipCost.ToString() },
            { "WeaponTargetCount",   (skill.multiTargetCount + 1).ToString() },
        };

        return System.Text.RegularExpressions.Regex.Replace(
            skill.description,
            @"\{(\w+)\}",
            m => tokens.TryGetValue(m.Groups[1].Value, out var val) ? val : m.Value
        );
    }

    // classSkillEffect: 0=공격, 1=힐, 2=부활
    // classSkillTarget: 0=단일, 1=복수, 2=전체
    // classSkillRange:  0=근거리, 1=원거리
    // classSkillRangeLine: 0=전열 우선, 1=선택, 2=후열 우선

    public static string Build(SkillData skill)
    {
        if (skill == null) return string.Empty;

        return BuildText(
            skill.classSkillEffect,
            skill.classSkillTarget,
            skill.classSkillRange,
            skill.classSkillRangeLine,
            skill.multiTargetCount,
            skill.skillValue,
            skill.skillSubValue,
            skill.ipCost,
            skill.description);
    }

    // [KJ 260805] DH 템플릿 경유 오버로드. 수치는 Lv1 기준 — SkillData.skillValue가
    // 카탈로그에서 ValueLv1로 채워지므로 표시 결과는 기존과 동일하다.
    public static string Build(DHClassSkillTemplate skill)
    {
        if (skill == null) return string.Empty;

        return BuildText(
            skill.Effect,
            skill.Target,
            skill.Range,
            skill.RangeLine,
            skill.MultiTargetCount,
            skill.ValueLv1,
            skill.SubValueLv1,
            skill.IpCost,
            skill.Description);
    }

    private static string BuildText(
        int effect,
        int skillTarget,
        int skillRange,
        int skillRangeLine,
        int multiTargetCount,
        float value,
        float subValue,
        int ipCost,
        string description)
    {
        var sb = new System.Text.StringBuilder();

        // 대상
        string target = skillTarget switch
        {
            0 => "적 1명",
            1 => $"적 {multiTargetCount + 1}명",
            2 => "적 전체",
            _ => "대상"
        };

        // 효과
        switch (effect)
        {
            case 0: // 공격
                int dmgPercent = (int)(value * 100);
                sb.Append($"{target}에게 {dmgPercent}% 대미지");
                if (subValue > 0)
                    sb.Append($" + {(int)(subValue * 100)}% 추가 대미지");
                break;

            case 1: // 힐
                int healPercent = (int)(value * 100);
                string healTarget = skillTarget switch
                {
                    0 => "아군 1명",
                    1 => $"아군 {multiTargetCount + 1}명",
                    2 => "아군 전체",
                    _ => "대상"
                };
                sb.Append($"{healTarget}의 HP를 {healPercent}% 회복");
                break;

            case 2: // 부활
                int revivePercent = (int)(value * 100);
                sb.Append($"전사한 아군 1명을 HP {revivePercent}%로 부활");
                break;

            default:
                sb.Append(description);
                break;
        }

        // 사정거리
        string range = skillRange switch
        {
            0 => "근거리",
            1 => "원거리",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(range))
            sb.Append($" ({range}");

        // 열 우선
        if (skillRange >= 0)
        {
            string line = skillRangeLine switch
            {
                0 => ", 전열 우선",
                1 => ", 열 선택",
                2 => ", 후열 우선",
                _ => string.Empty
            };
            sb.Append(line);
            sb.Append(")");
        }

        // IP 코스트
        if (ipCost > 0)
            sb.Append($"\nIP 소모: {ipCost}");

        return sb.ToString();
    }
}
