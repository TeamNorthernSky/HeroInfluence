public static class SkillDescriptionBuilder
{
    // classSkillEffect: 0=공격, 1=힐, 2=부활
    // classSkillTarget: 0=단일, 1=복수, 2=전체
    // classSkillRange:  0=근거리, 1=원거리
    // classSkillRangeLine: 0=전열 우선, 1=선택, 2=후열 우선

    public static string Build(SkillData skill)
    {
        if (skill == null) return string.Empty;

        var sb = new System.Text.StringBuilder();

        // 대상
        string target = skill.classSkillTarget switch
        {
            0 => "적 1명",
            1 => $"적 {skill.multiTargetCount + 1}명",
            2 => "적 전체",
            _ => "대상"
        };

        // 효과
        switch (skill.classSkillEffect)
        {
            case 0: // 공격
                int dmgPercent = (int)(skill.skillValue * 100);
                sb.Append($"{target}에게 {dmgPercent}% 대미지");
                if (skill.skillSubValue > 0)
                    sb.Append($" + {(int)(skill.skillSubValue * 100)}% 추가 대미지");
                break;

            case 1: // 힐
                int healPercent = (int)(skill.skillValue * 100);
                string healTarget = skill.classSkillTarget switch
                {
                    0 => "아군 1명",
                    1 => $"아군 {skill.multiTargetCount + 1}명",
                    2 => "아군 전체",
                    _ => "대상"
                };
                sb.Append($"{healTarget}의 HP를 {healPercent}% 회복");
                break;

            case 2: // 부활
                int revivePercent = (int)(skill.skillValue * 100);
                sb.Append($"전사한 아군 1명을 HP {revivePercent}%로 부활");
                break;

            default:
                sb.Append(skill.description);
                break;
        }

        // 사정거리
        string range = skill.classSkillRange switch
        {
            0 => "근거리",
            1 => "원거리",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(range))
            sb.Append($" ({range}");

        // 열 우선
        if (skill.classSkillRange >= 0)
        {
            string line = skill.classSkillRangeLine switch
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
        if (skill.ipCost > 0)
            sb.Append($"\nIP 소모: {skill.ipCost}");

        return sb.ToString();
    }
}
