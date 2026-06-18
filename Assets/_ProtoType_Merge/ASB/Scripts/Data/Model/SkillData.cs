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
    public int IPCost
    {
        get => ipCost;
        set => ipCost = value;
    }
    public int classSkillEffect;
    public int classSkillRange;
    public int EnemySkill1Range = -1;
    public int EnemySkill2Range = -1;
    public int classSkillRangeLine;
    public int classSkillTarget;
    /// <summary>
    /// SkillTargetingMapper 패턴 인덱스 목록(0=중심 셀, 9=열 등). 전체 진영 타격은 classSkillTarget==2로 처리합니다.
    /// 기존 aoePatternIndices를 통합한 필드입니다.
    /// </summary>
    public List<int> boundary = new List<int>();

    public int multiTargetCount;
    /// <summary>0=후보 전체, 1=multiTargetCount명 랜덤 (classSkillTarget==1·TargetAroundRandom).</summary>
    public int multiTargetType;
    public float skillValue;
    public float skillSubValue;

    [Header("Animation (Hybrid)")]
    public string AnimationTrigger = "Attack";
    public string StateName;
    public bool UseAnimEvent;
    public float HitDelay = 0.25f;
    public float TotalDelay = 0.5f;
    public string TargetAnimationTrigger;

    /// <summary>비어 있으면 classSkillEffect 기준: 딜(0)→Hit, 힐/부활(1·2)→HealReceive.</summary>
    public string ResolvedTargetAnimationTrigger
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TargetAnimationTrigger))
            {
                return TargetAnimationTrigger.Trim();
            }

            return IsHealOrBuffSkill() ? "HealReceive" : "Hit";
        }
    }

    private bool IsHealOrBuffSkill()
    {
        // classSkillEffect: 0=공격, 1=힐, 2=부활 (TargetingHelper·BattleManager와 동일)
        return classSkillEffect == 1 || classSkillEffect == 2;
    }
}
