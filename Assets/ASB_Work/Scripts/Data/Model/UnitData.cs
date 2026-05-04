using System;
using UnityEngine;

/// <summary>
/// CSV 파싱 결과로 생성되는 "유닛 마스터 데이터" 모델.
/// </summary>
[Serializable]
public class UnitData
{
    [Header("Identity")]


    [SerializeField] public string Index;
    [SerializeField] public string UnitType;
    [SerializeField] public string Name;



    [Header("Stats")]
    // 스펙: baseStats 필드에 HP/Atk/DEF/Luck + Speed/CriticalRate/CounterRate/AvoidRate 포함
    [SerializeField] public StatBlock baseStats;

    /// <summary>CSV에 IsEnemy 열이 있으면 파싱. 없으면 false.</summary>
    [SerializeField] public bool IsEnemyRow;
}
