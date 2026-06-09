// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnitGrowthExpData
{
    public int Level;
    public string Rank;
    public int NeedExpieriencePoint;
    public int AddMaxIP;
    public int GuardianStudySkill;
    public int BlasterStudySkill;
    public int StrikerStudySkill;
    public int SuppoterStudySkill;
    public int FighterStudySkill;
}

[CreateAssetMenu(fileName="UnitGrowthExpDataTable", menuName="DataTable/UnitGrowthExp")]
public class UnitGrowthExpDataTable : ScriptableObject
{
    public List<UnitGrowthExpData> DataList = new List<UnitGrowthExpData>();
}
