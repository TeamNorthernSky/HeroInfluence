// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UnitGrowthExpData
{
    public int Level;
    public string Rank;
    public int NeedExpieriencePoint;
    public int AddIP;
    public int Character1StudySkill;
    public int Character2StudySkill;
    public int Character3StudySkill;
    public int Character4StudySkill;
}

[CreateAssetMenu(fileName="UnitGrowthExpDataTable", menuName="DataTable/UnitGrowthExp")]
public class UnitGrowthExpDataTable : ScriptableObject
{
    public List<UnitGrowthExpData> DataList = new List<UnitGrowthExpData>();
}
