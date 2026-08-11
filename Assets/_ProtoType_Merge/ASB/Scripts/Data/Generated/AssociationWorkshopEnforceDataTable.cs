// Auto Generated. Do not modify.
// Template Signature: dict=0|required_workshop_level:Int:Comma|enchantable_equipment_group:String:Comma|weapon_index_list:ListString:Comma|to_enchant_level:Int:Comma|cost_resource_1_type:Int:Comma|cost_resource_1_amount:Int:Comma|cost_resource_2_type:Int:Comma|cost_resource_2_amount:Int:Comma|enchant_condition:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AssociationWorkshopEnforceData
{
    public int required_workshop_level;
    public string enchantable_equipment_group;
    public List<string> weapon_index_list;
    public int to_enchant_level;
    public int cost_resource_1_type;
    public int cost_resource_1_amount;
    public int cost_resource_2_type;
    public int cost_resource_2_amount;
    public string enchant_condition;
    public string note;
}

[CreateAssetMenu(fileName="AssociationWorkshopEnforceDataTable", menuName="DataTable/AssociationWorkshopEnforce")]
public class AssociationWorkshopEnforceDataTable : ScriptableObject
{
    public List<AssociationWorkshopEnforceData> DataList = new List<AssociationWorkshopEnforceData>();
}
