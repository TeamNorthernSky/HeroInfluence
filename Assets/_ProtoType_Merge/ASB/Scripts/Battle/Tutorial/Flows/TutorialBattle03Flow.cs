// 튜토리얼 3 — 방패병/반격(추정). 콘텐츠 TBD, 지시서 §21 참조.
// 반격은 OnCounterResolved, 특정 유닛 턴은 OnTurnStarted+템플릿ID(§5) 사용.
public sealed class TutorialBattle03Flow : TutorialBattleFlow
{
    public override string BattleKey => "TUT_03"; // TODO
    public override int ZoneId => -1;             // TODO

    // TODO(콘텐츠): OnBattleEntered/OnSkillResolved/OnCounterResolved/OnTurnStarted/OnUnitDied 등 스텝 구현
}
