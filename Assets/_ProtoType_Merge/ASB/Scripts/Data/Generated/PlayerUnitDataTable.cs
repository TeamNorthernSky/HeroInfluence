// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerUnitData
{
    public int ClassIndex;
    public string UnitName;
    public string ClassName;
    public string ClassConcept;
    public int UnitMaxHP;
    public int UnitATK;
    public int UnitDEF;
    public float CriticalRate;
    public float CounterRate;
    public float ReduceRate;
    public int Speed;
    public List<string> ClassSkillIndexList;
    public List<string> WeaponIndexList;
    public int LevelGrowthMaxHP;
    public int LevelGrowthMaxAtk;
    public int LevelGrowthMaxDef;
}

[CreateAssetMenu(fileName="PlayerUnitDataTable", menuName="DataTable/PlayerUnit")]
public class PlayerUnitDataTable : ScriptableObject
{
    public List<PlayerUnitData> DataList = new List<PlayerUnitData>();
}
