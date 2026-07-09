// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ClassSkillData
{
    public string ClassSkillIndex;
    public string Class;
    public int ClassSkill_AcquireRank;
    public string ClassSkillName;
    public string ClassSkillDescription;
    public int IPCost;
    public int ClassSkillEffect;
    public int ClassSkillRange;
    public int ClassSkillRangeLine;
    public int ClassSkillTarget;
    public List<int> ClassSkillMultiTarget;
    public int ClassSkill_MultiTargetType;
    public int ClassSkill_MultiTargetCount;
    public float ClassSkillValueLv1;
    public float ClassSkillSubValueLv1;
    public float ClassSkillValueLv2;
    public float ClassSkillSubValueLv2;
    public float ClassSkillValueLv3;
    public float ClassSkillSubValueLv3;
    public float ClassSkillValueLv4;
    public float ClassSkillSubValueLv4;
    public float ClassSkillValueLv5;
    public float ClassSkillSubValueLv5;
}

[CreateAssetMenu(fileName="ClassSkillDataTable", menuName="DataTable/ClassSkill")]
public class ClassSkillDataTable : ScriptableObject
{
    public List<ClassSkillData> DataList = new List<ClassSkillData>();
}
