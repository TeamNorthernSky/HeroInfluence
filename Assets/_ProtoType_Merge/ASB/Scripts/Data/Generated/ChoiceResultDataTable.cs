// Auto Generated. Do not modify.
// Template Signature: dict=0|result_group_id:String:Comma|effect_order:Int:Comma|target_scope:Int:Comma|effect_type:Int:Comma|effect_amount:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChoiceResultData
{
    public string result_group_id;
    public int effect_order;
    public int target_scope;
    public int effect_type;
    public int effect_amount;
    public string note;
}

[CreateAssetMenu(fileName="ChoiceResultDataTable", menuName="DataTable/ChoiceResult")]
public class ChoiceResultDataTable : ScriptableObject
{
    public List<ChoiceResultData> DataList = new List<ChoiceResultData>();
}
