using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨업 구간에서 해금되는 스킬 후보를 계산합니다. Repository나 저장 데이터를 변경하지 않습니다.
/// </summary>
public static class SkillUnlockResolver
{
    /// <summary>전투와 동일한 클래스 스킬 표의 습득 레벨로 신규·강화를 한 건씩 반환합니다.</summary>
    public static List<int> GetUnlockCandidates(int classIndex, int oldLevel, int newLevel)
    {
        var result = new List<int>();
        if (newLevel <= oldLevel || DHCsvTemplateCatalog.Instance == null) return result;
        var skills = DHCsvTemplateCatalog.Instance.GetSkillsByClassIndex(classIndex);
        if (skills == null) return result;
        skills = new List<SkillData>(skills);
        skills.Sort((a, b) => a == null ? (b == null ? 0 : 1) : b == null ? -1 :
            a.acquireLevel != b.acquireLevel ? a.acquireLevel.CompareTo(b.acquireLevel) : a.skillIndex.CompareTo(b.skillIndex));
        foreach (var skill in skills)
            if (skill != null && skill.acquireLevel > oldLevel && skill.acquireLevel <= newLevel && !result.Contains(skill.skillIndex))
                result.Add(skill.skillIndex);
        return result;
    }

    /// <summary>
    /// oldLevel → newLevel 구간에서 선택 가능한 스킬 후보 skillIndex 목록을 반환합니다.
    /// LevelUpData.skill == true 인 레벨이 구간에 없으면 빈 목록을 반환합니다.
    /// </summary>
    /// <param name="unitClassName">DHCsvTemplateCatalog.GetSkillsByClass에 쓸 클래스 이름</param>
    /// <param name="oldLevel">레벨업 전 레벨</param>
    /// <param name="newLevel">레벨업 후 레벨</param>
    /// <param name="currentSkillIndex">이미 장착 중인 스킬 — 후보에서 제외</param>
    public static List<int> GetUnlockCandidates(
        string unitClassName,
        int oldLevel,
        int newLevel,
        int currentSkillIndex)
    {
        var result = new List<int>();

        if (newLevel <= oldLevel) return result;

        DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null) return result;

        // 구간 내 LevelUpData.skill == true 인 레벨이 있는지 확인
        IReadOnlyList<LevelUpData> levelUpTable = catalog.GetLevelUpTemplates();
        if (levelUpTable == null || levelUpTable.Count == 0) return result;

        bool hasSkillUnlockInRange = false;
        for (int i = 0; i < levelUpTable.Count; i++)
        {
            LevelUpData row = levelUpTable[i];
            if (row == null) continue;

            int rowLevel = Mathf.RoundToInt(row.level);
            if (rowLevel > oldLevel && rowLevel <= newLevel && row.skill)
            {
                hasSkillUnlockInRange = true;
                break;
            }
        }

        if (!hasSkillUnlockInRange) return result;

        // 해당 클래스에서 acquireLevel이 구간 안에 들어오는 스킬 수집
        List<SkillData> classSkills = catalog.GetSkillsByClass(unitClassName);
        if (classSkills == null) return result;

        for (int i = 0; i < classSkills.Count; i++)
        {
            SkillData skill = classSkills[i];
            if (skill == null) continue;
            if (skill.acquireLevel <= oldLevel || skill.acquireLevel > newLevel) continue;
            if (skill.skillIndex == currentSkillIndex) continue;

            result.Add(skill.skillIndex);
        }

        return result;
    }
}
