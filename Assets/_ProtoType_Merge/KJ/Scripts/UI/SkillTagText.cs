using System.Collections.Generic;

/// <summary>전투 설명과 라이센스 툴팁에서 사용하는 스킬 태그 문구.</summary>
public static class SkillTagText
{
    public static string Get(int skillId, int effect, int range, int target, int slot)
    {
        return slot switch
        {
            0 => HeroSkillRules.FamilyId(skillId) == 4020 ? "공격/회복" : effect switch
            {
                0 => "공격", 1 => "회복", 2 => "부활", 3 => "버프", 4 => "디버프", _ => string.Empty
            },
            1 => range switch { 0 => "근거리", 1 => "원거리", _ => string.Empty },
            2 => target switch { 0 => "단일", 1 => "광역", 2 => "광역", _ => string.Empty },
            _ => string.Empty
        };
    }

    public static string Format(DHClassSkillTemplate skill)
    {
        if (skill == null) return string.Empty;
        var tags = new List<string>(3);
        for (int i = 0; i < 3; i++)
        {
            string tag = Get(skill.NumericSkillId, skill.Effect, skill.Range, skill.Target, i);
            if (!string.IsNullOrEmpty(tag)) tags.Add(tag);
        }
        return string.Join(" · ", tags);
    }
}
