using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHClassSkillTemplate
{
    private readonly List<int> boundary;

    public string SkillKey { get; }
    public int NumericSkillId { get; }
    public string ClassName { get; }
    public int AcquireLevel { get; }
    public string SkillName { get; }
    public string Description { get; }
    public int IpCost { get; }
    public int Effect { get; }
    public int Range { get; }
    public int RangeLine { get; }
    public int Target { get; }
    public IReadOnlyList<int> Boundary => boundary;
    public int MultiTargetType { get; }
    public int MultiTargetCount { get; }
    public float ValueLv1 { get; }
    public float SubValueLv1 { get; }
    public float ValueLv2 { get; }
    public float SubValueLv2 { get; }
    public float ValueLv3 { get; }
    public float SubValueLv3 { get; }
    public float ValueLv4 { get; }
    public float SubValueLv4 { get; }
    public float ValueLv5 { get; }
    public float SubValueLv5 { get; }
    public string ReplaceSkillKey { get; }
    public string SkillRiskKey { get; }
    public string AnimationTrigger { get; }

    public DHClassSkillTemplate(
        string skillKey,
        int numericSkillId,
        string className,
        int acquireLevel,
        string skillName,
        string description,
        int ipCost,
        int effect,
        int range,
        int rangeLine,
        int target,
        IEnumerable<int> boundary,
        int multiTargetType,
        int multiTargetCount,
        float valueLv1,
        float subValueLv1,
        float valueLv2,
        float subValueLv2,
        float valueLv3,
        float subValueLv3,
        float valueLv4,
        float subValueLv4,
        float valueLv5,
        float subValueLv5,
        string replaceSkillKey,
        string skillRiskKey,
        string animationTrigger)
    {
        SkillKey = string.IsNullOrWhiteSpace(skillKey) ? string.Empty : skillKey.Trim();
        NumericSkillId = Math.Max(0, numericSkillId);
        ClassName = string.IsNullOrWhiteSpace(className) ? string.Empty : className.Trim();
        AcquireLevel = Math.Max(0, acquireLevel);
        SkillName = string.IsNullOrWhiteSpace(skillName) ? string.Empty : skillName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        IpCost = Math.Max(0, ipCost);
        Effect = effect;
        Range = range;
        RangeLine = rangeLine;
        Target = target;
        this.boundary = boundary != null ? new List<int>(boundary) : new List<int>();
        MultiTargetType = multiTargetType;
        MultiTargetCount = multiTargetCount;
        ValueLv1 = valueLv1;
        SubValueLv1 = subValueLv1;
        ValueLv2 = valueLv2;
        SubValueLv2 = subValueLv2;
        ValueLv3 = valueLv3;
        SubValueLv3 = subValueLv3;
        ValueLv4 = valueLv4;
        SubValueLv4 = subValueLv4;
        ValueLv5 = valueLv5;
        SubValueLv5 = subValueLv5;
        ReplaceSkillKey = string.IsNullOrWhiteSpace(replaceSkillKey) ? string.Empty : replaceSkillKey.Trim();
        SkillRiskKey = string.IsNullOrWhiteSpace(skillRiskKey) ? string.Empty : skillRiskKey.Trim();
        AnimationTrigger = string.IsNullOrWhiteSpace(animationTrigger) ? "Attack" : animationTrigger.Trim();
    }
}
