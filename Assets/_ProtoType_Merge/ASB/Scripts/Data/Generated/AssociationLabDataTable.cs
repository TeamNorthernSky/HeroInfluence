// Auto Generated. Do not modify.
// Template Signature: dict=0|required_lab_level:Int:Comma|upgradable_skill_group:String:Comma|to_skill_level:Int:Comma|cost_resource_1_type:Int:Comma|cost_resource_1_amount:Int:Comma|cost_resource_2_type:Int:Comma|cost_resource_2_amount:Int:Comma|skill_upgrade_condition:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AssociationLabData
{
    public int required_lab_level;
    public string upgradable_skill_group;
    public int to_skill_level;
    public int cost_resource_1_type;
    public int cost_resource_1_amount;
    public int cost_resource_2_type;
    public int cost_resource_2_amount;
    public string skill_upgrade_condition;
    public string note;
}

[CreateAssetMenu(fileName="AssociationLabDataTable", menuName="DataTable/AssociationLab")]
public class AssociationLabDataTable : ScriptableObject
{
    public List<AssociationLabData> DataList = new List<AssociationLabData>();
}
