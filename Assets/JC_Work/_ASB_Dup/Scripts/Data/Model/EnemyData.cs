using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>적 유닛 마스터 데이터. 전투/스폰은 UnitData와 동일하며 적 전용 확장 필드를 둡니다.</summary>
[Serializable]
public class EnemyData : UnitData
{
    public string UnitAI;
    public float ExperiencePoint;
    public List<int> DropItem1Index = new List<int>();
    public List<int> DropItem1Count = new List<int>();
    public List<float> DropItem1Rate = new List<float>();
}
