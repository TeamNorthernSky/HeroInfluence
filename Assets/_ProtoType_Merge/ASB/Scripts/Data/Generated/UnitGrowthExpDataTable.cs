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
    public string Character1StudySkill;
    public string Character2StudySkill;
    public string Character3StudySkill;
    public string Character4StudySkill;
}

[CreateAssetMenu(fileName="UnitGrowthExpDataTable", menuName="DataTable/UnitGrowthExp")]
public class UnitGrowthExpDataTable : ScriptableObject
{
    public List<UnitGrowthExpData> DataList = new List<UnitGrowthExpData>();
}
