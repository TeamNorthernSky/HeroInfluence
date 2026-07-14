// Auto Generated. Do not modify.
// Template Signature: dict=0|Branch_ID:Int:Comma|Selection_Index:String:Comma|Trigger_Type:String:Comma|Trigger_Value:ListString:Backslash|Selection_Text:String:Comma|Target_Talk_ID:Int:Comma|Trigger_Effect:String:Comma|비고:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BranchDBEventData
{
    public int Branch_ID;
    public string Selection_Index;
    public string Trigger_Type;
    public List<string> Trigger_Value;
    public string Selection_Text;
    public int Target_Talk_ID;
    public string Trigger_Effect;
    public string 비고;
}

[CreateAssetMenu(fileName="BranchDBEventDataTable", menuName="DataTable/BranchDBEvent")]
public class BranchDBEventDataTable : ScriptableObject
{
    public List<BranchDBEventData> DataList = new List<BranchDBEventData>();
}
