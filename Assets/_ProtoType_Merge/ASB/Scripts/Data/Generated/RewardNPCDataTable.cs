// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|zone_no:Int:Comma|world_event_name:String:Comma|npc_type:Int:Comma|world_event_description:String:Comma|world_event_proceed:String:Comma|reward_type_1:Int:Comma|reward_amount_1:Int:Comma|reward_type_2:Int:Comma|reward_amount_2:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RewardNPCData
{
    public string world_event_id;
    public int zone_no;
    public string world_event_name;
    public int npc_type;
    public string world_event_description;
    public string world_event_proceed;
    public int reward_type_1;
    public int reward_amount_1;
    public int reward_type_2;
    public int reward_amount_2;
    public string note;
}

[CreateAssetMenu(fileName="RewardNPCDataTable", menuName="DataTable/RewardNPC")]
public class RewardNPCDataTable : ScriptableObject
{
    public List<RewardNPCData> DataList = new List<RewardNPCData>();
}
