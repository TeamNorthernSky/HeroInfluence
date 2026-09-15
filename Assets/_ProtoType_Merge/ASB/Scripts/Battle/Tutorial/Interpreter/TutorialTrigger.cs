using System;
using UnityEngine;

/// <summary>튜토리얼 규칙의 발화 시점(트리거) 종류. 전투 이벤트에 대응.</summary>
public enum TutorialTriggerType
{
    Immediate,             // 이전 규칙 실행 직후(시퀀싱 연쇄)
    BattleEntered,
    RoundStarted,          // 라운드 시작(엔진이 합성)
    UnitTurnStarted,
    TurnResolved,
    SkillResolved,
    UnitDied,              // 관찰형
    UnitHpAtOrBelow,       // 관찰형
    AliveCountAtOrBelow,   // 관찰형
    UIAction,
    ParticipantRegistered,
    BattleResultShown,     // 전투 밖(결과창)
    BattleResultAccepted,  // 전투 밖(결과창)
}

/// <summary>
/// 하나의 트리거 조건(언제). 기본값 -1은 "무시". 판정은 TutorialTriggerEvaluator가 담당.
/// (struct 대신 class — Unity 인스펙터에서 -1 기본값 필드 이니셜라이저가 적용되도록)
/// </summary>
[Serializable]
public sealed class TutorialTrigger
{
    public TutorialTriggerType type = TutorialTriggerType.Immediate;

    [Header("라운드(범위, -1=무시)")]
    public int minRound = -1;
    public int maxRound = -1;

    [Header("유닛/스킬")]
    public TutorialUnitSide side = TutorialUnitSide.Any;
    public string unitTemplateId;
    public string skillKey;
    public string targetTemplateId;
    public bool requireTargetDeath;

    [Header("상태(관찰형)")]
    [Range(0f, 1f)] public float hpRatioAtOrBelow = 1f;
    public int aliveCountAtOrBelow = -1;

    [Header("입력")]
    public string uiActionId;

    [Header("시퀀싱(단계/플래그)")]
    public int requiredStep = -1;   // 정확 단계(우선). -1=무시
    public int minStep = -1;        // 범위 하한(requiredStep 미사용 시)
    public int maxStep = -1;        // 범위 상한
    public string requireFlag;      // 이 플래그가 set 이어야 매칭(빈값=무시)
}
