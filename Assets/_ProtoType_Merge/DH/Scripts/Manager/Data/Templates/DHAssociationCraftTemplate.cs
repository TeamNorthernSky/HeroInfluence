using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHAssociationCraftTemplate
{
    private readonly List<string> weaponKeys;
    private readonly List<DHAssociationResourceCost> costs;

    public int RequiredWorkshopLevel { get; }
    public string ProducibleEquipmentGroup { get; }
    public IReadOnlyList<string> WeaponKeys => weaponKeys;
    public IReadOnlyList<DHAssociationResourceCost> Costs => costs;
    public string Note { get; }

    public DHAssociationCraftTemplate(
        int requiredWorkshopLevel,
        string producibleEquipmentGroup,
        IEnumerable<string> weaponKeys,
        IEnumerable<DHAssociationResourceCost> costs,
        string note)
    {
        RequiredWorkshopLevel = requiredWorkshopLevel;
        ProducibleEquipmentGroup = producibleEquipmentGroup?.Trim() ?? string.Empty;
        this.weaponKeys = weaponKeys != null
            ? new List<string>(weaponKeys)
            : new List<string>();
        this.costs = costs != null
            ? new List<DHAssociationResourceCost>(costs)
            : new List<DHAssociationResourceCost>();
        Note = note?.Trim() ?? string.Empty;
    }
}
