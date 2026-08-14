// Auto Generated. Do not modify.

using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyUnit4SectorData
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
    public float EnemySkill1SubValue;
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
    public float EnemySkill2SubValue;
    public string EnemySkill3_Name;
    public string EnemySkill3_Description;
    public int EnemySkill3Effect;
    public int EnemySkill3Range;
    public int EnemySkill3RangeLine;
    public int EnemySkill3Target;
    public List<int> EnemySkill3MultiTarget;
    public int EnemySkill3_MultiTargetType;
    public int EnemySkill3_MultiTargetCount;
    public float EnemySkill3Value;
    public float EnemySkill3SubValue;
    public string UnitAI;
    public int ExperiencePoint;
    public int LevelGrowthExperiencePoint;
    public int LevelGrowthMaxHP;
    public int LevelGrowthAtk;
    public int LevelGrowthDef;
}

[CreateAssetMenu(fileName="EnemyUnit4SectorDataTable", menuName="DataTable/EnemyUnit4Sector")]
public class EnemyUnit4SectorDataTable : ScriptableObject
{
    public List<EnemyUnit4SectorData> DataList = new List<EnemyUnit4SectorData>();
}
