using System;

[Serializable]
public sealed class DHAssociationPromotionTemplate
{
    public int PublicityLevel { get; }
    public DHAssociationResourceCost Cost { get; }
    public int StatusType { get; }
    public int InfluenceAmountPerCharge { get; }
    public int InfluenceChargeLimit { get; }
    public int InfluenceChargeCycle { get; }
    public string Note { get; }

    public DHAssociationPromotionTemplate(
        int publicityLevel,
        DHAssociationResourceCost cost,
        int statusType,
        int influenceAmountPerCharge,
        int influenceChargeLimit,
        int influenceChargeCycle,
        string note)
    {
        PublicityLevel = publicityLevel;
        Cost = cost;
        StatusType = statusType;
        InfluenceAmountPerCharge = influenceAmountPerCharge;
        InfluenceChargeLimit = influenceChargeLimit;
        InfluenceChargeCycle = influenceChargeCycle;
        Note = note?.Trim() ?? string.Empty;
    }
}
