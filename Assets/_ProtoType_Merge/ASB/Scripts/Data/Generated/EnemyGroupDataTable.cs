// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyGroupData
{
    public string EnemyIndex;
    public string EnemyGroupName;
    public string Enemy1;
    public int Enemy1Slot;
    public string Enemy2;
    public int Enemy2Slot;
    public string Enemy3;
    public int Enemy3Slot;
    public string Enemy4;
    public int Enemy4Slot;
    public string Enemy5;
    public int Enemy5Slot;
    public string Enemy6;
    public int Enemy6Slot;
    public int MinLevel;
    public int MaxLevel;
}

[CreateAssetMenu(fileName="EnemyGroupDataTable", menuName="DataTable/EnemyGroup")]
public class EnemyGroupDataTable : ScriptableObject
{
    public List<EnemyGroupData> DataList = new List<EnemyGroupData>();
}
