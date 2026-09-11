// 튜토리얼 2 — 처치/보상 흐름(추정). 콘텐츠 TBD, 지시서 §21 참조.
// 결과창 단계는 OnBattleResultShown/OnBattleResultAccepted 사용(§9 생명주기).
public sealed class TutorialBattle02Flow : TutorialBattleFlow
{
    public override string BattleKey => "TUT_02"; // TODO
    public override int ZoneId => -1;             // TODO

    // TODO(콘텐츠): OnBattleEntered/OnUnitDied/OnBattleResultShown 등 스텝 구현
}
