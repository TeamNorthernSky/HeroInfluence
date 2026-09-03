// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|zone_no:Int:Comma|world_event_name:String:Comma|npc_type:Int:Comma|world_event_description:String:Comma|choice_1_id:String:Comma|choice_2_id:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChoiceNPCData
{
    public string world_event_id;
    public int zone_no;
    public string world_event_name;
    public int npc_type;
    public string world_event_description;
    public string choice_1_id;
    public string choice_2_id;
    public string note;
}

[CreateAssetMenu(fileName="ChoiceNPCDataTable", menuName="DataTable/ChoiceNPC")]
public class ChoiceNPCDataTable : ScriptableObject
{
    public List<ChoiceNPCData> DataList = new List<ChoiceNPCData>();
}
