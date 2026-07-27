// Auto Generated. Do not modify.
// Template Signature: dict=0|Field_02:String:Comma|Field_03:String:Comma|제목:String:Comma|소개:String:Comma|비고:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class 이벤트1구역Data
{
    public string Field_02;
    public string Field_03;
    public string 제목;
    public string 소개;
    public string 비고;
}

[CreateAssetMenu(fileName="이벤트1구역DataTable", menuName="DataTable/이벤트1구역")]
public class 이벤트1구역DataTable : ScriptableObject
{
    public List<이벤트1구역Data> DataList = new List<이벤트1구역Data>();
}
