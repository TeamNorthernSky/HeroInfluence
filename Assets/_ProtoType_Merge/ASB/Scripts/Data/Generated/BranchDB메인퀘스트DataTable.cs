// Auto Generated. Do not modify.
// Template Signature: dict=0|Branch_ID:String:Comma|Selection_Index:String:Comma|Trigger_Type:String:Comma|Trigger_Value:String:Comma|Selection_Text:String:Comma|Target_Talk_ID:String:Comma|Trigger_Effect:String:Comma|비고:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BranchDB메인퀘스트Data
{
    public string Branch_ID;
    public string Selection_Index;
    public string Trigger_Type;
    public string Trigger_Value;
    public string Selection_Text;
    public string Target_Talk_ID;
    public string Trigger_Effect;
    public string 비고;
}

[CreateAssetMenu(fileName="BranchDB메인퀘스트DataTable", menuName="DataTable/BranchDB메인퀘스트")]
public class BranchDB메인퀘스트DataTable : ScriptableObject
{
    public List<BranchDB메인퀘스트Data> DataList = new List<BranchDB메인퀘스트Data>();
}
