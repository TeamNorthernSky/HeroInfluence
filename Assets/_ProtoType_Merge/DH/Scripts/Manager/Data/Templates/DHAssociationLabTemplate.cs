using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHAssociationLabTemplate
{
    private readonly List<DHAssociationResourceCost> costs;

    public int RequiredLabLevel { get; }
    public string UpgradableSkillGroup { get; }
    public int TargetSkillLevel { get; }
    public IReadOnlyList<DHAssociationResourceCost> Costs => costs;
    public string SkillUpgradeCondition { get; }
    public string Note { get; }

    public DHAssociationLabTemplate(
        int requiredLabLevel,
        string upgradableSkillGroup,
        int targetSkillLevel,
        IEnumerable<DHAssociationResourceCost> costs,
        string skillUpgradeCondition,
        string note)
    {
        RequiredLabLevel = requiredLabLevel;
        UpgradableSkillGroup = upgradableSkillGroup?.Trim() ?? string.Empty;
        TargetSkillLevel = targetSkillLevel;
        this.costs = costs != null
            ? new List<DHAssociationResourceCost>(costs)
            : new List<DHAssociationResourceCost>();
        SkillUpgradeCondition = skillUpgradeCondition?.Trim() ?? string.Empty;
        Note = note?.Trim() ?? string.Empty;
    }
}
