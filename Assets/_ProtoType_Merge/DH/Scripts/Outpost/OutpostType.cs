public enum OutpostType
{
    Bank,
    Composite
}

public static class OutpostTypeUtility
{
    public static OutpostType Normalize(OutpostType outpostType)
    {
        return outpostType == OutpostType.Bank
            ? OutpostType.Bank
            : OutpostType.Composite;
    }

    public static OutpostType FromLegacyResourceType(ResourceType resourceType)
    {
        return resourceType == ResourceType.Money
            ? OutpostType.Bank
            : OutpostType.Composite;
    }
}
