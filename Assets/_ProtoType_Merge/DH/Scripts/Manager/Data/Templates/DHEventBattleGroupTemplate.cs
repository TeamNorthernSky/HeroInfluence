using System;
using System.Collections.Generic;

[Serializable]
public sealed class DHEventBattleGroupTemplate
{
    private readonly List<DHEventBattleGroupMember> members;

    public string BattleKey { get; }
    public string GroupName { get; }
    public int MinLevel { get; }
    public int MaxLevel { get; }
    public IReadOnlyList<DHEventBattleGroupMember> Members => members;

    public DHEventBattleGroupTemplate(
        string battleKey,
        string groupName,
        int minLevel,
        int maxLevel,
        IEnumerable<DHEventBattleGroupMember> members)
    {
        BattleKey = string.IsNullOrWhiteSpace(battleKey) ? string.Empty : battleKey.Trim();
        GroupName = string.IsNullOrWhiteSpace(groupName) ? string.Empty : groupName.Trim();
        MinLevel = minLevel;
        MaxLevel = maxLevel;
        this.members = members != null
            ? new List<DHEventBattleGroupMember>(members)
            : new List<DHEventBattleGroupMember>();
    }
}

[Serializable]
public readonly struct DHEventBattleGroupMember
{
    public string UnitKey { get; }
    public int CombatSlot { get; }

    public DHEventBattleGroupMember(string unitKey, int combatSlot)
    {
        UnitKey = string.IsNullOrWhiteSpace(unitKey) ? string.Empty : unitKey.Trim();
        CombatSlot = combatSlot;
    }
}
