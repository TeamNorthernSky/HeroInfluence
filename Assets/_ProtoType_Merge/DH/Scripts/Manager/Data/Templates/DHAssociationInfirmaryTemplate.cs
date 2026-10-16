using System;

[Serializable]
public sealed class DHAssociationInfirmaryTemplate
{
    public int InfirmaryLevel { get; }
    public DHAssociationResourceCost Cost { get; }
    public float HpRecoveryFraction { get; }
    public string Note { get; }

    public DHAssociationInfirmaryTemplate(
        int infirmaryLevel,
        DHAssociationResourceCost cost,
        float hpRecoveryFraction,
        string note)
    {
        InfirmaryLevel = infirmaryLevel;
        Cost = cost;
        HpRecoveryFraction = hpRecoveryFraction;
        Note = note?.Trim() ?? string.Empty;
    }
}
