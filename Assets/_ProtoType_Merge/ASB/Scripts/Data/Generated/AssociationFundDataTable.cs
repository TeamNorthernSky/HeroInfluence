// Auto Generated. Do not modify.
// Template Signature: dict=0|main_base_level:Int:Comma|resource_type:Int:Comma|resource_amount:Int:Comma|resource_cycle:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AssociationFundData
{
    public int main_base_level;
    public int resource_type;
    public int resource_amount;
    public int resource_cycle;
    public string note;
}

[CreateAssetMenu(fileName="AssociationFundDataTable", menuName="DataTable/AssociationFund")]
public class AssociationFundDataTable : ScriptableObject
{
    public List<AssociationFundData> DataList = new List<AssociationFundData>();
}
