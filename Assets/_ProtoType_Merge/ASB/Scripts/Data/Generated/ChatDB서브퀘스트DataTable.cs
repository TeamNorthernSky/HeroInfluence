// Auto Generated. Do not modify.
// Template Signature: dict=0|Chat_ID:String:Comma|Chat_Type:String:Comma|Char_Name:String:Comma|Char_Profile:String:Comma|Message_Text:String:Comma|IMG_Res:String:Comma|Next_Chat_ID:String:Comma|Branch_Group_ID:String:Comma

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChatDB서브퀘스트Data
{
    public string Chat_ID;
    public string Chat_Type;
    public string Char_Name;
    public string Char_Profile;
    public string Message_Text;
    public string IMG_Res;
    public string Next_Chat_ID;
    public string Branch_Group_ID;
}

[CreateAssetMenu(fileName="ChatDB서브퀘스트DataTable", menuName="DataTable/ChatDB서브퀘스트")]
public class ChatDB서브퀘스트DataTable : ScriptableObject
{
    public List<ChatDB서브퀘스트Data> DataList = new List<ChatDB서브퀘스트Data>();
}
