// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|trigger_condition_type:Int:Comma|trigger_condition_target:Int:Comma|trigger_condition_stat_type:Int:Comma|trigger_condition_calculation_type:Int:Comma|trigger_condition_operator:Int:Comma|trigger_condition_value:Int:Comma|world_event_name:String:Comma|npc_type:Int:Comma|world_event_description:String:Comma|world_event_accept:String:Comma|world_event_cancel:String:Comma|world_event_proceed:String:Comma|world_event_decline:String:Comma|result_id:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConsumeCondition1Data
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
    public string world_event_cancel;
    public string world_event_proceed;
    public string world_event_decline;
    public string result_id;
    public string note;
}

[CreateAssetMenu(fileName="ConsumeCondition1DataTable", menuName="DataTable/ConsumeCondition1")]
public class ConsumeCondition1DataTable : ScriptableObject
{
    public List<ConsumeCondition1Data> DataList = new List<ConsumeCondition1Data>();
}
