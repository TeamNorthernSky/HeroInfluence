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
    public float HpRatio = 1f;
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
