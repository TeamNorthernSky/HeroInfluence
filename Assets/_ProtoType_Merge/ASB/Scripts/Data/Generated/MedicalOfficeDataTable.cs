// Auto Generated. Do not modify.
// Template Signature: dict=0|infirmary_level:Int:Comma|cost_resource_type:Int:Comma|cost_resource_amount:Int:Comma|hp_recovery_amount:Percent:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MedicalOfficeData
{
    public int infirmary_level;
    public int cost_resource_type;
    public int cost_resource_amount;
    public float hp_recovery_amount;
    public string note;
}

[CreateAssetMenu(fileName="MedicalOfficeDataTable", menuName="DataTable/MedicalOffice")]
public class MedicalOfficeDataTable : ScriptableObject
{
    public List<MedicalOfficeData> DataList = new List<MedicalOfficeData>();
}
