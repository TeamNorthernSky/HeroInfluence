// Auto Generated. Do not modify.
// Template Signature: dict=0|publicity_level:Int:Comma|cost_resource_type:Int:Comma|cost_resource_amount:Int:Comma|status_type:Int:Comma|ip_amount_per_charge:Int:Comma|ip_charge_limit:Int:Comma|ip_charge_cycle:Int:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AssociationPromotionData
{
    public int publicity_level;
    public int cost_resource_type;
    public int cost_resource_amount;
    public int status_type;
    public int ip_amount_per_charge;
    public int ip_charge_limit;
    public int ip_charge_cycle;
    public string note;
}

[CreateAssetMenu(fileName="AssociationPromotionDataTable", menuName="DataTable/AssociationPromotion")]
public class AssociationPromotionDataTable : ScriptableObject
{
    public List<AssociationPromotionData> DataList = new List<AssociationPromotionData>();
}
