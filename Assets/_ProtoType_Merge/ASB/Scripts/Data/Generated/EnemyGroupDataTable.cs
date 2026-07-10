// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyGroupData
{
    public int EnemyIndex;
    public int Enemy1;
    public int Enemy1Slot;
    public int Enemy2;
    public int Enemy2Slot;
    public int Enemy3;
    public int Enemy3Slot;
    public int Enemy4;
    public int Enemy4Slot;
    public int Enemy5;
    public int Enemy5Slot;
    public string EnemyGroupName;
    public int MinLevel;
    public int MaxLevel;
}

[CreateAssetMenu(fileName="EnemyGroupDataTable", menuName="DataTable/EnemyGroup")]
public class EnemyGroupDataTable : ScriptableObject
{
    public List<EnemyGroupData> DataList = new List<EnemyGroupData>();
}
