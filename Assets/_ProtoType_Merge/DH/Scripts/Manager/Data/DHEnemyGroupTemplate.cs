using System;
using System.Collections.Generic;

[Serializable]
public class DHEnemyGroupTemplate
{
    public string GroupKey;
    public string GroupName;
    public int MinLevel;
    public int MaxLevel;
    public List<DHEnemyGroupMember> Members = new List<DHEnemyGroupMember>();
}

[Serializable]
public struct DHEnemyGroupMember
{
    public DHEnemyGroupMember(int enemyUnitIndex, int combatSlot)
    {
        EnemyUnitIndex = enemyUnitIndex;
        CombatSlot = combatSlot;
    }

    public int EnemyUnitIndex;
    public int CombatSlot;
}
