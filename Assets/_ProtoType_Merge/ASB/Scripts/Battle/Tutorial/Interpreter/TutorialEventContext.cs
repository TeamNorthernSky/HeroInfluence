using System.Collections.Generic;

/// <summary>
/// 현재 전투 이벤트의 데이터 스냅샷. DataDrivenTutorialFlow가 콜백에서 채워 TutorialTriggerEvaluator에 넘긴다.
/// (evaluator가 순수하도록 필요한 상태를 모두 담는다 — 라이브 조회 없음)
/// </summary>
public sealed class TutorialEventContext
{
    public TutorialTriggerType eventType;
    public int round;
    public BattleCharactor unit;                 // turn/death/hp/participant
    public float currentHp;
    public float maxHp;
    public SkillResolutionContext skill;         // SkillResolved
    public TurnResolutionContext turn;           // TurnResolved
    public string actionId;                      // UIAction
    public BattleResult result;                  // BattleResultShown/Accepted
    public int alivePlayerCount;
    public int aliveEnemyCount;

    // 엔진 상태(트리거 가드 판정용)
    public int currentStep;
    public IReadOnlyCollection<string> flags;
}
