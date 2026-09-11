using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// 튜토리얼 한 전투의 진행 로직. 러너가 (ZoneId, BattleKey)로 선택해 전투 이벤트를 이 콜백으로 전달한다.
/// 조건 "값"은 콜백 매개변수가 아니라 구현 내부의 if로 판정한다.
/// </summary>
public interface ITutorialBattleFlow
{
    string BattleKey { get; }
    int ZoneId { get; }
    bool IsComplete { get; }

    void Attach(ITutorialBattleFlowHost host);

    void OnBattleEntered();
    void OnTurnStarted(int round, BattleCharactor unit);
    void OnTurnResolved(TurnResolutionContext ctx);
    void OnSkillResolved(SkillResolutionContext ctx);
    void OnUnitHpChanged(BattleCharactor unit, float current, float max);
    void OnUnitDied(BattleCharactor unit);
    void OnParticipantRegistered(BattleCharactor unit);
    void OnUIAction(string actionId);
    void OnCellClicked(GridCellRef cell);
    void OnTargetSelected(BattleCharactor actor, BattleCharactor target);
    void OnCounterResolved(CounterResolutionContext ctx);
    void OnBattleResultShown(BattleResult result);
    void OnBattleResultAccepted(BattleResult result);
}
