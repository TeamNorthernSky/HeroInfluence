using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleEntryMode
{
    Normal,
    Simulation
}

[Serializable]
public sealed class SimulationAllyInput
{
    public bool Enabled;
    public string UnitTemplateKey;
    public int Level = 1;
    public string WeaponKey;
    public int SkillIndex;
    [Range(0.01f, 1f)] public float HpRatio = 1f;
    // JC | 2026-11-12 | 모의전 시작 IP만 보관하며 실제 상한은 기존 성장 계산 결과로 제한합니다.
    [Min(0), Tooltip("시작 IP입니다. 선택한 랭크·레벨의 실제 IP 상한을 넘을 수 없습니다.")]
    public float InitialInfluence;
}

[Serializable]
public sealed class SimulationBattleRequest
{
    public List<SimulationAllyInput> Allies = new List<SimulationAllyInput>();
    public string EnemyGroupKey;
    public int EnemyLevel = 1;
}

[Serializable]
public sealed class SimulationAllyRuntimeData
{
    [SerializeField] private int runtimeUnitIndex;
    [SerializeField] private UnitPersistentData unitData;

    public int RuntimeUnitIndex => runtimeUnitIndex;
    public UnitPersistentData UnitData => unitData;

    public SimulationAllyRuntimeData(int runtimeUnitIndex, UnitPersistentData unitData)
    {
        this.runtimeUnitIndex = Mathf.Max(1, runtimeUnitIndex);
        this.unitData = unitData;
    }
}
