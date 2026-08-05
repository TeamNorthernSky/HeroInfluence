using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHUnitGrowthTemplate
{
    private readonly Dictionary<int, int> studySkillByClassIndex;

    public int Level { get; }
    public string Rank { get; }
    public int RequiredExperience { get; }
    public int AddInfluence { get; }
    public IReadOnlyDictionary<int, int> StudySkillByClassIndex => studySkillByClassIndex;

    public DHUnitGrowthTemplate(
        int level,
        string rank,
        int requiredExperience,
        int addInfluence,
        IReadOnlyDictionary<int, int> studySkillByClassIndex)
    {
        Level = level;
        Rank = string.IsNullOrWhiteSpace(rank) ? string.Empty : rank.Trim();
        RequiredExperience = requiredExperience;
        AddInfluence = addInfluence;
        this.studySkillByClassIndex = studySkillByClassIndex != null
            ? new Dictionary<int, int>(studySkillByClassIndex)
            : new Dictionary<int, int>();
    }
}
