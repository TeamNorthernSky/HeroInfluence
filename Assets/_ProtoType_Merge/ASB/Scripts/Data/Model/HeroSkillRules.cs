using UnityEngine;

/// <summary>V1.4 히어로 스킬 계열. 습득한 '+' 변형과 협회 강화(기본~+5)를 구분합니다.</summary>
public static class HeroSkillRules
{
    public static int FamilyId(int id)
    {
        int hero = id / 1000, slot = (id % 1000) / 10, variant = id % 10;
        return hero >= 1 && hero <= 4 && slot >= 1 && slot <= 4 && variant <= 1
            ? id - variant : id;
    }

    public static bool IsCurrentHeroSkill(int id)
    {
        int family = FamilyId(id);
        return family / 1000 >= 1 && family / 1000 <= 4 && family % 1000 >= 10 && family % 1000 <= 40 && family % 10 == 0;
    }

    public static string FamilyKey(string key)
    {
        return key != null && key.StartsWith("HS") && int.TryParse(key.Substring(2), out int id)
            ? "HS" + FamilyId(id) : key;
    }

    public static bool IsFamily(SkillData skill, int family) => skill != null && FamilyId(skill.skillIndex) == family;
    // 저장 호환: 기존 1을 기본, 2를 +1로 유지하고 6(+5)을 추가합니다.
    public static float RiskChance(int storedLevel) => Mathf.Max(0, 5 - Mathf.Clamp(storedLevel - 1, 0, 5)) * 0.07f;
}
