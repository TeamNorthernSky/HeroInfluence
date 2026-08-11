// Auto Generated. Do not modify.
// Template Signature: dict=0|exchange_room_level:Int:Comma|cost_resource_type:Int:Comma|cost_resource_amount:Int:Comma|get_resource_type:Int:Comma|get_resource_amount:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AssociationExchangeSheetData
{
    public int exchange_room_level;
    public int cost_resource_type;
    public int cost_resource_amount;
    public int get_resource_type;
    public int get_resource_amount;
    public string note;
}

[CreateAssetMenu(fileName="AssociationExchangeSheetDataTable", menuName="DataTable/AssociationExchangeSheet")]
public class AssociationExchangeSheetDataTable : ScriptableObject
{
    public List<AssociationExchangeSheetData> DataList = new List<AssociationExchangeSheetData>();
}
