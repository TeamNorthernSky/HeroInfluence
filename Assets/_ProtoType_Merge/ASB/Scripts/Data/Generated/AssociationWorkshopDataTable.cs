// Auto Generated. Do not modify.
// Template Signature: dict=0|required_workshop_level:Int:Comma|producible_equipment_group:String:Comma|weapon_index_list:ListString:Comma|cost_resource_1_type:Int:Comma|cost_resource_1_amount:Int:Comma|cost_resource_2_type:Int:Comma|cost_resource_2_amount:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AssociationWorkshopData
{
    public int required_workshop_level;
    public string producible_equipment_group;
    public List<string> weapon_index_list;
    public int cost_resource_1_type;
    public int cost_resource_1_amount;
    public int cost_resource_2_type;
    public int cost_resource_2_amount;
    public string note;
}

[CreateAssetMenu(fileName="AssociationWorkshopDataTable", menuName="DataTable/AssociationWorkshop")]
public class AssociationWorkshopDataTable : ScriptableObject
{
    public List<AssociationWorkshopData> DataList = new List<AssociationWorkshopData>();
}
