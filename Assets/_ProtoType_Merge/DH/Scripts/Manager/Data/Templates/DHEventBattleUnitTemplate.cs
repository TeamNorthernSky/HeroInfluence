using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHEventBattleUnitTemplate
{
    private readonly List<DHEventBattleSkillTemplate> skills;

    public string UnitKey { get; }
    public string EnemyName { get; }
    public string EnemyConcept { get; }
    public int BaseMaxHp { get; }
    public int BaseAtk { get; }
    public int BaseDef { get; }
    public float CriticalRate { get; }
    public float CounterRate { get; }
    public float ReduceRate { get; }
    public int Speed { get; }
    public string UnitAI { get; }
    public int ExperiencePoint { get; }
    public int LevelGrowthExperiencePoint { get; }
    public int LevelGrowthMaxHp { get; }
    public int LevelGrowthAtk { get; }
    public int LevelGrowthDef { get; }
    public IReadOnlyList<DHEventBattleSkillTemplate> Skills => skills;

    public DHEventBattleUnitTemplate(
        string unitKey,
        string enemyName,
        string enemyConcept,
        int baseMaxHp,
        int baseAtk,
        int baseDef,
        float criticalRate,
        float counterRate,
        float reduceRate,
        int speed,
        string unitAI,
        int experiencePoint,
        int levelGrowthExperiencePoint,
        int levelGrowthMaxHp,
        int levelGrowthAtk,
        int levelGrowthDef,
        IEnumerable<DHEventBattleSkillTemplate> skills)
    {
        UnitKey = string.IsNullOrWhiteSpace(unitKey) ? string.Empty : unitKey.Trim();
        EnemyName = string.IsNullOrWhiteSpace(enemyName) ? string.Empty : enemyName.Trim();
        EnemyConcept = string.IsNullOrWhiteSpace(enemyConcept) ? string.Empty : enemyConcept.Trim();
        BaseMaxHp = baseMaxHp;
        BaseAtk = baseAtk;
        BaseDef = baseDef;
        CriticalRate = criticalRate;
        CounterRate = counterRate;
        ReduceRate = reduceRate;
        Speed = speed;
        UnitAI = string.IsNullOrWhiteSpace(unitAI) ? string.Empty : unitAI.Trim();
        ExperiencePoint = experiencePoint;
        LevelGrowthExperiencePoint = levelGrowthExperiencePoint;
        LevelGrowthMaxHp = levelGrowthMaxHp;
        LevelGrowthAtk = levelGrowthAtk;
        LevelGrowthDef = levelGrowthDef;
        this.skills = skills != null
            ? new List<DHEventBattleSkillTemplate>(skills)
            : new List<DHEventBattleSkillTemplate>();
    }
}

[Serializable]
public sealed class DHEventBattleSkillTemplate
{
    private readonly List<int> boundary;

    public int Slot { get; }
    public string SkillName { get; }
    public string Description { get; }
    public int Effect { get; }
    public int Range { get; }
    public int RangeLine { get; }
    public int Target { get; }
    public IReadOnlyList<int> Boundary => boundary;
    public int MultiTargetType { get; }
    public int MultiTargetCount { get; }
    public float Value { get; }
    public float SubValue { get; }

    public DHEventBattleSkillTemplate(
        int slot,
        string skillName,
        string description,
        int effect,
        int range,
        int rangeLine,
        int target,
        IEnumerable<int> boundary,
        int multiTargetType,
        int multiTargetCount,
        float value,
        float subValue)
    {
        Slot = slot;
        SkillName = string.IsNullOrWhiteSpace(skillName) ? string.Empty : skillName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        Effect = effect;
        Range = range;
        RangeLine = rangeLine;
        Target = target;
        this.boundary = boundary != null ? new List<int>(boundary) : new List<int>();
        MultiTargetType = multiTargetType;
        MultiTargetCount = multiTargetCount;
        Value = value;
        SubValue = subValue;
    }
}
