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
    public int ipCost;
    public int classSkillEffect;
    public int classSkillRange;
    public int classSkillRangeLine;
    public int classSkillTarget;
    /// <summary>
    /// SkillTargetingMapper 패턴 인덱스 목록(0=중심 셀, 9=열, 10=전체 등).
    /// 기존 aoePatternIndices를 통합한 필드입니다.
    /// </summary>
    public List<int> boundary = new List<int>();

    public int multiTargetCount;
    public float skillValue;
    public float skillSubValue;
}
