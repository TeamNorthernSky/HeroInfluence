using System;
using UnityEngine;

/// <summary>튜토리얼 규칙이 실행할 액션(무엇). 대부분 기존 런타임 기능 호출. 실행은 TutorialActionExecutor.</summary>
public enum TutorialActionType
{
    ShowUi,             // uiKey (비블로킹)
    ShowBlockingUi,     // uiKey + requiredActionId (원자적: 검증→표시→성공시만 잠금)
    HideUi,
    ReleaseRuleEffects, // 이 규칙(scopeId)의 효과만 해제
    AcquireFlowLock,    // 전투만 정지(UI 없이)
    Spawn,              // spawnSide + spawnUnitId + spawnGridNumber
    SetHp, SetInfluence,// target
    AddMinimumHp, AddStatModifier, // target + lifetime (scopeId=ruleId)
    SetStep, SetFlag,
    CustomHook,         // stringValue = hookId
    CompleteTutorial,   // → flow.Complete()
}

/// <summary>하나의 액션 정의. class 사용(인스펙터 기본값·가변 리스트 안정성).</summary>
[Serializable]
public sealed class TutorialAction
{
    public TutorialActionType type = TutorialActionType.ShowUi;

    [Header("UI (ShowUi/ShowBlockingUi)")]
    public string uiKey;                 // TutorialUiSheet 참조
    public string requiredActionId;      // ShowBlockingUi: 진행에 필요한 입력(바인딩 검사 대상)

    [Header("대상 유닛 (SetHp/제약/스탯)")]
    public TutorialUnitSide targetSide = TutorialUnitSide.Any;
    public string targetTemplateId;

    [Header("스폰")]
    public TutorialUnitSide spawnSide = TutorialUnitSide.Enemy;
    public string spawnUnitId;           // EnemySpawner/PlayerSpawner enemyId/unitId
    public int spawnGridNumber;

    [Header("값")]
    public float floatValue;             // SetHp/SetInfluence/AddMinimumHp
    public RuntimeStatModifier statModifier; // AddStatModifier(기존 타입)
    public int intValue;                 // SetStep
    public string stringValue;           // SetFlag 이름 / CustomHook id
    public TutorialEffectLifetime lifetime = TutorialEffectLifetime.Step;
}
