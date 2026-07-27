// Auto Generated. Do not modify.
// Template Signature: dict=1|Chat_ID:String:Comma|Chat_Type:String:Comma|Char_Name:String:Comma|Char_Profile:String:Comma|Message_Text:String:Comma|IMG_Res:String:Comma|Next_Chat_ID:String:Comma|Branch_Group_ID:String:Comma

using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChatDB메인퀘스트Data
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

[CreateAssetMenu(fileName="ChatDB메인퀘스트DataTable", menuName="DataTable/ChatDB메인퀘스트")]
public class ChatDB메인퀘스트DataTable : ScriptableObject, ISerializationCallbackReceiver
{
    public List<ChatDB메인퀘스트Data> DataList = new List<ChatDB메인퀘스트Data>();

    [NonSerialized]
    public Dictionary<string, ChatDB메인퀘스트Data> DataMap = new Dictionary<string, ChatDB메인퀘스트Data>();

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        DataMap.Clear();
        foreach (var data in DataList)
        {
            if (!DataMap.ContainsKey(data.Chat_ID))
            {
                DataMap.Add(data.Chat_ID, data);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[ChatDB메인퀘스트DataTable] Duplicate key: {data.Chat_ID}");
            }
        }
    }
}
