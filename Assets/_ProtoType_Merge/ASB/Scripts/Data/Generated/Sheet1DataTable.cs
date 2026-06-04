// Auto Generated. Do not modify.

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName="Sheet1DataTable", menuName="DataTable/Sheet1")]
public class Sheet1DataTable : ScriptableObject, ISerializationCallbackReceiver
{
    public List<Sheet1Data> DataList = new List<Sheet1Data>();

    [NonSerialized]
    public Dictionary<int, Sheet1Data> DataMap = new Dictionary<int, Sheet1Data>();

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        DataMap.Clear();
        foreach (var data in DataList)
        {
            if (!DataMap.ContainsKey(data.id))
            {
                DataMap.Add(data.id, data);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[Sheet1DataTable] Duplicate key: {data.id}");
            }
        }
    }
}
