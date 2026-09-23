// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|zone_no:Int:Comma|npc_type:Int:Comma|world_event_description:String:Comma|world_event_proceed:String:Comma|world_event_accept:String:Comma|reward_type_1:Int:Comma|reward_amount_1:Int:Comma|reward_type_2:Int:Comma|reward_amount_2:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RewardNPC1Data
{
    public string world_event_id;
    public int zone_no;
    public int npc_type;
    public string world_event_description;
    public string world_event_proceed;
    public string world_event_accept;
    public int reward_type_1;
    public int reward_amount_1;
    public int reward_type_2;
    public int reward_amount_2;
    public string note;
}

[CreateAssetMenu(fileName="RewardNPC1DataTable", menuName="DataTable/RewardNPC1")]
public class RewardNPC1DataTable : ScriptableObject
{
    public List<RewardNPC1Data> DataList = new List<RewardNPC1Data>();
}
