// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnitGruothPerLevelData
{
    public int Level;
    public string Rank;
    public int NeedExpieriencePoint;
    public int AddIP;
    public int FighterStudySkill;
    public int BlasterStudySkill;
    public int StrikerStudySkill;
    public int SuppoterStudySkill;
}

[CreateAssetMenu(fileName="UnitGruothPerLevelDataTable", menuName="DataTable/UnitGruothPerLevel")]
public class UnitGruothPerLevelDataTable : ScriptableObject
{
    public List<UnitGruothPerLevelData> DataList = new List<UnitGruothPerLevelData>();
}
