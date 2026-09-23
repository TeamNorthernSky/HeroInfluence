// Auto Generated. Do not modify.
// Template Signature: dict=0|result_group_id:String:Comma|world_event_accept:String:Comma|target_scope:Int:Comma|effect_type:Int:Comma|effect_amount:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChoiceResult1Data
{
    public string result_group_id;
    public string world_event_accept;
    public int target_scope;
    public int effect_type;
    public int effect_amount;
    public string note;
}

[CreateAssetMenu(fileName="ChoiceResult1DataTable", menuName="DataTable/ChoiceResult1")]
public class ChoiceResult1DataTable : ScriptableObject
{
    public List<ChoiceResult1Data> DataList = new List<ChoiceResult1Data>();
}
