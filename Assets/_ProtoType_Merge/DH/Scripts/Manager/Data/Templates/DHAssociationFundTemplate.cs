using System;

[Serializable]
public sealed class DHAssociationFundTemplate
{
    public int MainBaseLevel { get; }
    public int ResourceType { get; }
    public int ResourceAmount { get; }
    public int ResourceCycle { get; }
    public string Note { get; }

    public DHAssociationFundTemplate(
        int mainBaseLevel,
        int resourceType,
        int resourceAmount,
        int resourceCycle,
        string note)
    {
        MainBaseLevel = mainBaseLevel;
        ResourceType = resourceType;
        ResourceAmount = resourceAmount;
        ResourceCycle = resourceCycle;
        Note = note?.Trim() ?? string.Empty;
    }
}
