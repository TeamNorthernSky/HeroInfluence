// Auto Generated. Do not modify.
// Template Signature: dict=0|room_type:String:Comma|action_type:Int:Comma|to_room_level:Int:Comma|cost_resource_1_type:Int:Comma|cost_resource_1_amount:Int:Comma|cost_resource_2_type:Int:Comma|cost_resource_2_amount:Int:Comma|room_action_condition:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AssociationBuildData
{
    public string room_type;
    public int action_type;
    public int to_room_level;
    public int cost_resource_1_type;
    public int cost_resource_1_amount;
    public int cost_resource_2_type;
    public int cost_resource_2_amount;
    public string room_action_condition;
    public string note;
}

[CreateAssetMenu(fileName="AssociationBuildDataTable", menuName="DataTable/AssociationBuild")]
public class AssociationBuildDataTable : ScriptableObject
{
    public List<AssociationBuildData> DataList = new List<AssociationBuildData>();
}
