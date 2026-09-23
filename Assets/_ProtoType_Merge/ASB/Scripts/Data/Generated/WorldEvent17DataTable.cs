// Auto Generated. Do not modify.
// Template Signature: dict=0|world_event_id:String:Comma|world_event_type:Int:Comma|world_event_target:Int:Comma|world_event_target_count:Int:Comma|world_event_target_select:Int:Comma|cost_resource_type:Int:Comma|cost_resource_amount:Int:Comma|cost_resource_type_2:Int:Comma|cost_resource_amount_2:Int:Comma|status_type:Int:Comma|status_amount_per_event:Int:Comma|status_type_2:Int:Comma|status_amount_per_event_2:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WorldEvent17Data
{
    public string world_event_id;
    public int world_event_type;
    public int world_event_target;
    public int world_event_target_count;
    public int world_event_target_select;
    public int cost_resource_type;
    public int cost_resource_amount;
    public int cost_resource_type_2;
    public int cost_resource_amount_2;
    public int status_type;
    public int status_amount_per_event;
    public int status_type_2;
    public int status_amount_per_event_2;
    public string note;
}

[CreateAssetMenu(fileName="WorldEvent17DataTable", menuName="DataTable/WorldEvent17")]
public class WorldEvent17DataTable : ScriptableObject
{
    public List<WorldEvent17Data> DataList = new List<WorldEvent17Data>();
}
