// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|zone_no:Int:Comma|trigger_condition_type:Int:Comma|trigger_condition_target:Int:Comma|trigger_condition_stat_type:Int:Comma|trigger_condition_calculation_type:Int:Comma|trigger_condition_operator:Int:Comma|trigger_condition_value:Int:Comma|world_event_name:String:Comma|npc_type:Int:Comma|world_event_description:String:Comma|choice_1_id:String:Comma|choice_2_id:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChoiceConditionData
{
    public string world_event_id;
    public int zone_no;
    public int trigger_condition_type;
    public int trigger_condition_target;
    public int trigger_condition_stat_type;
    public int trigger_condition_calculation_type;
    public int trigger_condition_operator;
    public int trigger_condition_value;
    public string world_event_name;
    public int npc_type;
    public string world_event_description;
    public string choice_1_id;
    public string choice_2_id;
    public string note;
}

[CreateAssetMenu(fileName="ChoiceConditionDataTable", menuName="DataTable/ChoiceCondition")]
public class ChoiceConditionDataTable : ScriptableObject
{
    public List<ChoiceConditionData> DataList = new List<ChoiceConditionData>();
}
