// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|trigger_condition_type:Int:Comma|trigger_condition_target:Int:Comma|trigger_condition_stat_type:Int:Comma|trigger_condition_calculation_type:Int:Comma|trigger_condition_operator:Int:Comma|trigger_condition_value:Int:Comma|world_event_name:String:Comma|npc_type:Int:Comma|world_event_description:String:Comma|world_event_accept:String:Comma|world_event_proceed:String:Comma|reward_type_1:Int:Comma|reward_amount_1:Int:Comma|reward_type_2:Int:Comma|reward_amount_2:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RewardCondition1Data
{
    public string world_event_id;
    public int trigger_condition_type;
    public int trigger_condition_target;
    public int trigger_condition_stat_type;
    public int trigger_condition_calculation_type;
    public int trigger_condition_operator;
    public int trigger_condition_value;
    public string world_event_name;
    public int npc_type;
    public string world_event_description;
    public string world_event_accept;
    public string world_event_proceed;
    public int reward_type_1;
    public int reward_amount_1;
    public int reward_type_2;
    public int reward_amount_2;
    public string note;
}

[CreateAssetMenu(fileName="RewardCondition1DataTable", menuName="DataTable/RewardCondition1")]
public class RewardCondition1DataTable : ScriptableObject
{
    public List<RewardCondition1Data> DataList = new List<RewardCondition1Data>();
}
