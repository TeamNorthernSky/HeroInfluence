using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHAssociationEnforceTemplate
{
    private readonly List<string> weaponKeys;
    private readonly List<DHAssociationResourceCost> costs;

    public int RequiredWorkshopLevel { get; }
    public string EnchantableEquipmentGroup { get; }
    public IReadOnlyList<string> WeaponKeys => weaponKeys;
    public int TargetEnchantLevel { get; }
    public IReadOnlyList<DHAssociationResourceCost> Costs => costs;
    public string EnchantCondition { get; }
    public string Note { get; }

    public DHAssociationEnforceTemplate(
        int requiredWorkshopLevel,
        string enchantableEquipmentGroup,
        IEnumerable<string> weaponKeys,
        int targetEnchantLevel,
        IEnumerable<DHAssociationResourceCost> costs,
        string enchantCondition,
        string note)
    {
        RequiredWorkshopLevel = requiredWorkshopLevel;
        EnchantableEquipmentGroup = enchantableEquipmentGroup?.Trim() ?? string.Empty;
        this.weaponKeys = weaponKeys != null
            ? new List<string>(weaponKeys)
            : new List<string>();
        TargetEnchantLevel = targetEnchantLevel;
        this.costs = costs != null
            ? new List<DHAssociationResourceCost>(costs)
            : new List<DHAssociationResourceCost>();
        EnchantCondition = enchantCondition?.Trim() ?? string.Empty;
        Note = note?.Trim() ?? string.Empty;
    }
}
