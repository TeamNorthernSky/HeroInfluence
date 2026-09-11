// 튜토리얼 1 — 조작 기본(추정). 콘텐츠(트리거 조건 값·문구) TBD, 지시서 §21 스텝표 참조.
// 필요한 콜백만 override하고, 조건 값은 함수 안 if로 판정한다(유닛은 §5 템플릿 ID로 FindUnit).
public sealed class TutorialBattle01Flow : TutorialBattleFlow
{
    public override string BattleKey => "TUT_01"; // TODO: 실제 BattleKey로 교체
    public override int ZoneId => -1;             // TODO: 실제 ZoneId로 교체

    // TODO(콘텐츠): OnBattleEntered/OnUIAction/OnCellClicked/OnSkillResolved 등 스텝 구현
}
