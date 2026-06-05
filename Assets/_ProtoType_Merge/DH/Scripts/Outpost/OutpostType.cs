public enum OutpostType
{
    Bank,
    Composite,
    Library,
    JewelryShop,
    BlockStore
}

public static class OutpostTypeUtility
{
    public static OutpostType Normalize(OutpostType outpostType)
    {
        return outpostType switch
        {
            OutpostType.Bank => OutpostType.Bank,
            OutpostType.Composite => OutpostType.Composite,
            OutpostType.Library => OutpostType.Library,
            OutpostType.JewelryShop => OutpostType.JewelryShop,
            OutpostType.BlockStore => OutpostType.BlockStore,
            _ => OutpostType.Composite
        };
    }

    public static OutpostType FromLegacyResourceType(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Money => OutpostType.Bank,
            ResourceType.Chip => OutpostType.Library,
            ResourceType.Crystal => OutpostType.JewelryShop,
            ResourceType.Supply => OutpostType.BlockStore,
            _ => OutpostType.Composite
        };
    }
}
