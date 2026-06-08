// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnitGrowthPerLevelData
{
    public int Level;
    public string Rank;
    public int NeedExpieriencePoint;
    public int AddIP;
    public int GuardianStudySkill;
    public int BlasterStudySkill;
    public int StrikerStudySkill;
    public int SuppoterStudySkill;
    public int FighterStudySkill;
}

[CreateAssetMenu(fileName="UnitGrowthPerLevelDataTable", menuName="DataTable/UnitGrowthPerLevel")]
public class UnitGrowthPerLevelDataTable : ScriptableObject
{
    public List<UnitGrowthPerLevelData> DataList = new List<UnitGrowthPerLevelData>();
}
