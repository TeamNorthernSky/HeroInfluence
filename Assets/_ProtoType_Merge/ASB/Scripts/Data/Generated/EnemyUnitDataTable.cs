// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyUnitData
{
    public string EnemyIndex;
    public string EnemyName;
    public string EnemyConcept;
    public int UnitMaxHP;
    public int UnitATK;
    public int UnitDEF;
    public float CriticalRate;
    public float CounterRate;
    public float ReduceRate;
    public int Speed;
    public string EnemySkill1_Name;
    public string EnemySkill1_Description;
    public int EnemySkill1Effect;
    public int EnemySkill1Range;
    public int EnemySkill1RangeLine;
    public int EnemySkill1Target;
    public List<int> EnemySkill1MultiTarget;
    public int EnemySkill1_MultiTargetType;
    public int EnemySkill1_MultiTargetCount;
    public float EnemySkill1Value;
    public string EnemySkill2_Name;
    public string EnemySkill2_Description;
    public int EnemySkill2Effect;
    public int EnemySkill2Range;
    public int EnemySkill2RangeLine;
    public int EnemySkill2Target;
    public List<int> EnemySkill2MultiTarget;
    public int EnemySkill2_MultiTargetType;
    public int EnemySkill2_MultiTargetCount;
    public float EnemySkill2Value;
    public string UnitAI;
    public int ExperiencePoint;
}

[CreateAssetMenu(fileName="EnemyUnitDataTable", menuName="DataTable/EnemyUnit")]
public class EnemyUnitDataTable : ScriptableObject
{
    public List<EnemyUnitData> DataList = new List<EnemyUnitData>();
}
