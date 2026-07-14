// Auto Generated. Do not modify.
// Template Signature: dict=1|Chat_ID:Int:Comma|Chat_Type:Int:Comma|Char_Name:String:Comma|Char_Profile:String:Comma|Message_Text:String:Comma|IMG_Res:String:Comma|Next_Chat_ID:Int:Comma|Branch_Group_ID:Int:Comma

using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ChatDBEventData
{
    public int Chat_ID;
    public int Chat_Type;
    public string Char_Name;
    public string Char_Profile;
    public string Message_Text;
    public string IMG_Res;
    public int Next_Chat_ID;
    public int Branch_Group_ID;
}

[CreateAssetMenu(fileName="ChatDBEventDataTable", menuName="DataTable/ChatDBEvent")]
public class ChatDBEventDataTable : ScriptableObject, ISerializationCallbackReceiver
{
    public List<ChatDBEventData> DataList = new List<ChatDBEventData>();

    [NonSerialized]
    public Dictionary<int, ChatDBEventData> DataMap = new Dictionary<int, ChatDBEventData>();

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
                UnityEngine.Debug.LogWarning($"[ChatDBEventDataTable] Duplicate key: {data.Chat_ID}");
            }
        }
    }
}
