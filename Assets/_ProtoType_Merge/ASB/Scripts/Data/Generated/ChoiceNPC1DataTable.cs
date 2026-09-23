// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|zone_no:Int:Comma|npc_type:Int:Comma|world_event_description:String:Comma|world_event_cancel:String:Comma|choice_1_id:String:Comma|choice_2_id:String:Comma|world_event_decline:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChoiceNPC1Data
{
    public string world_event_id;
    public int zone_no;
    public int npc_type;
    public string world_event_description;
    public string world_event_cancel;
    public string choice_1_id;
    public string choice_2_id;
    public string world_event_decline;
    public string note;
}

[CreateAssetMenu(fileName="ChoiceNPC1DataTable", menuName="DataTable/ChoiceNPC1")]
public class ChoiceNPC1DataTable : ScriptableObject
{
    public List<ChoiceNPC1Data> DataList = new List<ChoiceNPC1Data>();
}
