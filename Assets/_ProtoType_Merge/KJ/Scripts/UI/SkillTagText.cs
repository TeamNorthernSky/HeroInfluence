using System.Collections.Generic;

/// <summary>전투 설명과 라이센스 툴팁에서 사용하는 스킬 태그 문구.</summary>
public static class SkillTagText
{
    // JC | 2026-11-12 | 기존 KJ 공용 태그를 재사용하되 원거리는 출력 목록에 넣지 않습니다.
    // 사거리/대상 판정 데이터는 변경하지 않고 실제 표시할 항목만 순서대로 반환합니다.
    public static List<string> Build(int skillId, int effect, int range, int target)
    {
        var tags = new List<string>(3);
        string effectText = HeroSkillRules.FamilyId(skillId) == 4020 ? "공격/회복" : effect switch
        {
            0 => "공격", 1 => "회복", 2 => "부활", 3 => "버프", 4 => "디버프",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(effectText)) tags.Add(effectText);
        if (range == 0) tags.Add("전열 우선");
        string targetText = target switch { 0 => "단일", 1 => "광역", 2 => "광역", _ => string.Empty };
        if (!string.IsNullOrEmpty(targetText)) tags.Add(targetText);
        return tags;
    }

    public static string Format(DHClassSkillTemplate skill)
    {
        if (skill == null) return string.Empty;
        return string.Join(" · ", Build(skill.NumericSkillId, skill.Effect, skill.Range, skill.Target));
    }
}
