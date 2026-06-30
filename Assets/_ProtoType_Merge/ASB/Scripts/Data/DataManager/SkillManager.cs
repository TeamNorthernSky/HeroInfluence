using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SkillManager] 중복 인스턴스가 감지되었습니다.");
        }

        Instance = this;
    }

    public List<SkillData> GetSkillsForCharacter(string characterName)
    {
        if (DHCsvTemplateCatalog.Instance == null || string.IsNullOrWhiteSpace(characterName))
        {
            return new List<SkillData>();
        }

        string normalized = characterName.Trim();
        List<SkillData> skills = DHCsvTemplateCatalog.Instance.GetSkillsByClass(normalized);
        if (skills.Count > 0)
        {
            return skills;
        }

        if (DHCsvTemplateCatalog.Instance.TryGetPlayerTemplate(normalized, out UnitData unitTemplate) &&
            unitTemplate != null &&
            !string.IsNullOrWhiteSpace(unitTemplate.UnitType))
        {
            return DHCsvTemplateCatalog.Instance.GetSkillsByClass(unitTemplate.UnitType.Trim());
        }

        return skills;
    }

    public List<SkillData> GetSkillsForCharacter(int classIndex)
    {
        if (DHCsvTemplateCatalog.Instance == null || classIndex <= 0)
        {
            return new List<SkillData>();
        }

        return DHCsvTemplateCatalog.Instance.GetSkillsByClassIndex(classIndex);
    }

    public List<SkillData> GetAvailableSkillsForCharacter(string characterName, int currentLevel)
    {
        var classSkills = GetSkillsForCharacter(characterName);
        if (classSkills.Count == 0) return classSkills;

        int safeLevel = Mathf.Max(1, currentLevel);
        return classSkills
            .Where(x => x != null && x.acquireLevel <= safeLevel)
            .OrderBy(x => x.acquireLevel)
            .ThenBy(x => x.skillIndex)
            .ToList();
    }

    public List<SkillData> GetAvailableSkillsForCharacter(int classIndex, int currentLevel)
    {
        if (DHCsvTemplateCatalog.Instance == null || classIndex <= 0)
        {
            return new List<SkillData>();
        }

        return DHCsvTemplateCatalog.Instance.GetAvailableSkillsByClassIndex(classIndex, currentLevel);
    }
}
