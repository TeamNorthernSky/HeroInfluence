// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|trigger_condition_type:Int:Comma|trigger_condition_target:Int:Comma|trigger_condition_stat_type:Int:Comma|trigger_condition_calculation_type:Int:Comma|trigger_condition_operator:Int:Comma|trigger_condition_value:Int:Comma|world_event_name:String:Comma|npc_type:Int:Comma|world_event_description:String:Comma|world_event_cancel:String:Comma|choice_1_id:String:Comma|choice_2_id:String:Comma|world_event_decline:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChoiceCondition1Data
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
    public string world_event_cancel;
    public string choice_1_id;
    public string choice_2_id;
    public string world_event_decline;
    public string note;
}

[CreateAssetMenu(fileName="ChoiceCondition1DataTable", menuName="DataTable/ChoiceCondition1")]
public class ChoiceCondition1DataTable : ScriptableObject
{
    public List<ChoiceCondition1Data> DataList = new List<ChoiceCondition1Data>();
}
