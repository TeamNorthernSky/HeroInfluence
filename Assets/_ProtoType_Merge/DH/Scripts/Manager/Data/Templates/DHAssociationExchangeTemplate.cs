using System;

[Serializable]
public sealed class DHAssociationExchangeTemplate
{
    public int ExchangeRoomLevel { get; }
    public DHAssociationResourceCost Cost { get; }
    public DHAssociationResourceCost Reward { get; }
    public string Note { get; }

    public DHAssociationExchangeTemplate(
        int exchangeRoomLevel,
        DHAssociationResourceCost cost,
        DHAssociationResourceCost reward,
        string note)
    {
        ExchangeRoomLevel = exchangeRoomLevel;
        Cost = cost;
        Reward = reward;
        Note = note?.Trim() ?? string.Empty;
    }
}
