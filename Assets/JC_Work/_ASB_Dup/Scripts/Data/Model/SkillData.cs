using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ClassSkillSheet CSV 행 데이터. skillIndex를 키로 SkillDataLoader에 보관합니다.
/// </summary>
/// 



//public class MappingSkill

[Serializable]
public class SkillData
{
    public int skillIndex;
    public string skillClass;
    public int acquireLevel;
    public string skillName;
    public string description;
    public int classSkillEffect;
    public int classSkillRange;
    public int classSkillTarget;
    public List<Vector2Int> boundary = new List<Vector2Int>();

    /// <summary>
    /// AoE 전용: SkillTargetingMapper 패턴 인덱스(0=중심 셀). 비어 있으면 AoE 핸들러가 기본 전체 패턴(0~8)을 사용한다.
    /// </summary>
    public List<int> aoePatternIndices = new List<int>();

    public int multiTargetType;
    public int multiTargetCount;
    public float skillValue;
    public float skillSubValue;
}
