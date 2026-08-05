using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHPlayerUnitTemplate
{
    private readonly List<int> classSkillIndices;
    private readonly List<int> weaponIndices;

    public string UnitKey { get; }
    public int ClassIndex { get; }
    public string UnitName { get; }
    public string ClassName { get; }
    public string ClassConcept { get; }
    public StatBlock BaseStats { get; }
    public StatBlock LevelupStats { get; }
    public IReadOnlyList<int> ClassSkillIndices => classSkillIndices;
    public IReadOnlyList<int> WeaponIndices => weaponIndices;

    public DHPlayerUnitTemplate(
        string unitKey,
        int classIndex,
        string unitName,
        string className,
        string classConcept,
        StatBlock baseStats,
        StatBlock levelupStats,
        IEnumerable<int> classSkillIndices,
        IEnumerable<int> weaponIndices)
    {
        UnitKey = string.IsNullOrWhiteSpace(unitKey) ? string.Empty : unitKey.Trim();
        ClassIndex = Math.Max(0, classIndex);
        UnitName = string.IsNullOrWhiteSpace(unitName) ? string.Empty : unitName.Trim();
        ClassName = string.IsNullOrWhiteSpace(className) ? string.Empty : className.Trim();
        ClassConcept = string.IsNullOrWhiteSpace(classConcept) ? string.Empty : classConcept.Trim();
        BaseStats = baseStats;
        LevelupStats = levelupStats;
        this.classSkillIndices = classSkillIndices != null ? new List<int>(classSkillIndices) : new List<int>();
        this.weaponIndices = weaponIndices != null ? new List<int>(weaponIndices) : new List<int>();
    }
}
