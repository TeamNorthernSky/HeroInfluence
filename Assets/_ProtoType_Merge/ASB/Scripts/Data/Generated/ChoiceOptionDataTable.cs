// Auto Generated. Do not modify.
// Template Signature: dict=0|choice_id:String:Comma|world_event_id:String:Comma|choice_text:String:Comma|enable_condition_unit:Int:Comma|enable_condition_type:Int:Comma|enable_condition_target:Int:Comma|enable_condition_calculation_type:Int:Comma|enable_condition_operator:Int:Comma|enable_condition_value:Int:Comma|use_ip_success_rate_bonus:Int:Comma|base_success_rate:String:Comma|success_result_group_id:String:Comma|failure_result_group_id:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChoiceOptionData
{
    public string choice_id;
    public string world_event_id;
    public string choice_text;
    public int enable_condition_unit;
    public int enable_condition_type;
    public int enable_condition_target;
    public int enable_condition_calculation_type;
    public int enable_condition_operator;
    public int enable_condition_value;
    public int use_ip_success_rate_bonus;
    public string base_success_rate;
    public string success_result_group_id;
    public string failure_result_group_id;
    public string note;
}

[CreateAssetMenu(fileName="ChoiceOptionDataTable", menuName="DataTable/ChoiceOption")]
public class ChoiceOptionDataTable : ScriptableObject
{
    public List<ChoiceOptionData> DataList = new List<ChoiceOptionData>();
}
