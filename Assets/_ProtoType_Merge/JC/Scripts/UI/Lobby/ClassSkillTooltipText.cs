/// <summary>
/// [JC 260619] 클래스 스킬 설명 본문 생성(연구소·HeroInfo 공용). 전투 UI와 동일하게
/// description의 {ClassSkillValue}/{ClassSkillSubValue}를 레벨별 계수로 치환한다.
/// (LabModalController.EffectText 로직 추출)
/// </summary>
public static class ClassSkillTooltipText
{
    public static string BuildDesc(SkillData s, int level)
    {
        if (s == null) return string.Empty;
        string d = s.description ?? string.Empty;
        var cat = DHCsvTemplateCatalog.Instance;
        float v = cat != null ? cat.GetClassSkillValueAtLevel(s.skillIndex, level) : s.skillValue;
        float sub = cat != null ? cat.GetClassSkillSubValueAtLevel(s.skillIndex, level) : s.skillSubValue;
        bool atk = s.classSkillEffect == 0;
        string vStr = atk ? $"기본 피해량 × {v:0.##}" : $"{v:0.##}";
        string subStr = atk ? $"기본 피해량 × {sub:0.##}" : $"{sub:0.##}";
        return d.Replace("{ClassSkillValue}", vStr).Replace("{ClassSkillSubValue}", subStr);
    }
}
