// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|zone_no:Int:Comma|npc_type:Int:Comma|world_event_description:String:Comma|world_event_accept:String:Comma|world_event_cancel:String:Comma|world_event_proceed:String:Comma|world_event_decline:String:Comma|result_id:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConsumeNPCData
{
    public string world_event_id;
    public int zone_no;
    public int npc_type;
    public string world_event_description;
    public string world_event_accept;
    public string world_event_cancel;
    public string world_event_proceed;
    public string world_event_decline;
    public string result_id;
    public string note;
}

[CreateAssetMenu(fileName="ConsumeNPCDataTable", menuName="DataTable/ConsumeNPC")]
public class ConsumeNPCDataTable : ScriptableObject
{
    public List<ConsumeNPCData> DataList = new List<ConsumeNPCData>();
}
