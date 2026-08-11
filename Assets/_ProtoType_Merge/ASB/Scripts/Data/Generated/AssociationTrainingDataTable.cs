// Auto Generated. Do not modify.
// Template Signature: dict=0|required_training_room_level:Int:Comma|training_name:String:Comma|cost_resource_type:Int:Comma|cost_resource_amount:Int:Comma|status_type:Int:Comma|status_amount_per_training:Int:Comma|training_count_per_unit:Int:Comma|training_condition:String:Comma|note:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AssociationTrainingData
{
    public int required_training_room_level;
    public string training_name;
    public int cost_resource_type;
    public int cost_resource_amount;
    public int status_type;
    public int status_amount_per_training;
    public int training_count_per_unit;
    public string training_condition;
    public string note;
}

[CreateAssetMenu(fileName="AssociationTrainingDataTable", menuName="DataTable/AssociationTraining")]
public class AssociationTrainingDataTable : ScriptableObject
{
    public List<AssociationTrainingData> DataList = new List<AssociationTrainingData>();
}
