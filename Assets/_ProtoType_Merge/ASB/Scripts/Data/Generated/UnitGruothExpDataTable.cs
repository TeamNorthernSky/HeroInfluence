// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnitGruothExpData
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

[CreateAssetMenu(fileName="UnitGruothExpDataTable", menuName="DataTable/UnitGruothExp")]
public class UnitGruothExpDataTable : ScriptableObject
{
    public List<UnitGruothExpData> DataList = new List<UnitGruothExpData>();
}
