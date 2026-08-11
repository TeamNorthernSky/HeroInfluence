using System;

[Serializable]
public sealed class DHAssociationResourceCost
{
    public int ResourceType { get; }
    public int Amount { get; }

    public DHAssociationResourceCost(int resourceType, int amount)
    {
        ResourceType = resourceType;
        Amount = amount;
    }
}
