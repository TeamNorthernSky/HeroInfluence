using System;
using UnityEngine;

[System.Serializable]
public struct PrefabMapping
{
    public string unitIndex;   // CSV의 Index (예: "10001")
    public GameObject prefab;  // 인스펙터에서 연결할 프리팹
}
