// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|result_id:String:Comma|cost_resource_type_1:Int:Comma|cost_resource_amount_1:Int:Comma|cost_resource_type_2:Int:Comma|cost_resource_amount_2:Int:Comma|status_type_1:Int:Comma|status_amount_per_event_1:Int:Comma|status_type_2:Int:Comma|status_amount_per_event_2:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConsumeResultData
{
    public string world_event_id;
    public string result_id;
    public int cost_resource_type_1;
    public int cost_resource_amount_1;
    public int cost_resource_type_2;
    public int cost_resource_amount_2;
    public int status_type_1;
    public int status_amount_per_event_1;
    public int status_type_2;
    public int status_amount_per_event_2;
    public string note;
}

[CreateAssetMenu(fileName="ConsumeResultDataTable", menuName="DataTable/ConsumeResult")]
public class ConsumeResultDataTable : ScriptableObject
{
    public List<ConsumeResultData> DataList = new List<ConsumeResultData>();
}
