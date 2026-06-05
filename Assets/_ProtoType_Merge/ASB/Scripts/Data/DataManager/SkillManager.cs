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

        return DHCsvTemplateCatalog.Instance.GetSkillsByClass(characterName);
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
}
