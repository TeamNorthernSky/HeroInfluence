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
    public int skillIndex;              // [TEMP:STRKEY] 레거시 int 키 브리지 / 제거조건: 레지스트리·컨텍스트·연출·아이콘 전 소비부 string 이행 완료 후
    public string skillKey;             // 신규 string 키 (캐릭터=HS1010 / 무기=HCS001 / 적=FV20001_1)
    public SkillCategory category;      // §5-7 명시 분류 (Class/Enemy/Weapon). skillIndex>=200000/>=300000 판정 대체
    public int slot;                    // §5-7 적 스킬 슬롯(1/2), 그 외 0. skillIndex%10 판정 대체
    [Tooltip("원본 스킬의 리스크 식별값입니다. 리스크 실행부가 연결될 때 이 시전의 종류를 판별합니다.")]
    public string riskKey;
    [Tooltip("이번 시전의 협회 강화 저장 단계입니다. 1=기본, 6=+5이며 기본판·강화판이 공유합니다.")]
    public int enhancementLevel = 1;
    public float RiskChance => string.IsNullOrEmpty(riskKey) ? 0f : HeroSkillRules.RiskChance(enhancementLevel);
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
        // classSkillEffect: 0=공격, 1=힐, 2=부활, 3=버프, 4=디버프 (TargetingHelper·BattleManager와 동일)
        // 힐·부활·버프(아군 대상)는 HealReceive 애니, 디버프(적 대상)는 Hit 애니.
        return classSkillEffect == 1 || classSkillEffect == 2 || classSkillEffect == 3;
    }
}
