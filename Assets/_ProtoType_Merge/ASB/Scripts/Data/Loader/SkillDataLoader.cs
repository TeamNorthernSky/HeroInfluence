using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ClassSkillSheet에서 로드한 스킬을 skillIndex로 조회합니다.
/// </summary>
public class SkillDataLoader : MonoBehaviour
{
    private readonly Dictionary<int, SkillData> skillsByIndex = new Dictionary<int, SkillData>();

    public void ReplaceAll(IEnumerable<SkillData> skills)
    {
        skillsByIndex.Clear();
        if (skills == null)
        {
            return;
        }

        foreach (var s in skills)
        {
            if (s == null)
            {
                continue;
            }

            skillsByIndex[s.skillIndex] = s;
        }
    }

    public bool TryGetSkill(int skillIndex, out SkillData data)
    {
        return skillsByIndex.TryGetValue(skillIndex, out data);
    }

    public List<SkillData> GetAllSkills()
    {
        var result = new List<SkillData>(skillsByIndex.Values);
        result.Sort((a, b) =>
        {
            int left = a != null ? a.skillIndex : 0;
            int right = b != null ? b.skillIndex : 0;
            return left.CompareTo(right);
        });
        return result;
    }

    public List<SkillData> GetSkillsByClass(string className)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            return new List<SkillData>();
        }

        string normalized = className.Trim();
        var result = skillsByIndex.Values
            .Where(x =>
                x != null &&
                !string.IsNullOrWhiteSpace(x.skillClass) &&
                string.Equals(x.skillClass.Trim(), normalized, System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.acquireLevel)
            .ThenBy(x => x.skillIndex)
            .ToList();

        return result;
    }

    public int Count => skillsByIndex.Count;
}
