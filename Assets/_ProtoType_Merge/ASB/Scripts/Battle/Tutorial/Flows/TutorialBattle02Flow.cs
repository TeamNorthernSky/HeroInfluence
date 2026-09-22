/// <summary>
/// 튜토리얼 TUT_02 (디버그: step1만).
/// step1: 전투 시작(첫 턴 시작 시) 안내 + 전투 루프 잠금. 버튼 누르면 잠금 해제 → 전투 계속.
///        ⚠ OnBattleEntered가 아니라 첫 OnTurnStarted에 건다 — CurrentUnit·턴 순서 확정 전에 잠그면
///        턴 기반 HUD가 안 채워져 전투 UI가 흰색으로 뜨기 때문(TUT_01에서 확인).
/// UI는 host.ShowUi("step1") 경로(코드-온리: 씬 stepTexts/actionButtons와 key로 매칭).
/// </summary>
public sealed class TutorialBattle02Flow : TutorialBattleFlow
{
    public override string BattleKey => "TUT_02";
    public override int ZoneId => -1;

    private bool step1Done;   // 첫 턴에 1회만 발동

    public override void OnTurnStarted(int round, BattleCharactor unit)
    {
        // step1: 첫 턴 시작 시 안내(블로킹). 진영 무관 첫 유닛.
        //        (첫 '플레이어' 턴에만 띄우려면 조건에 && unit.IsPlayer 추가)
        if (!step1Done)
        {
            step1Done = true;
            // (UI 시트/컴포넌트 미배선이면 fail-open으로 잠금 없이 진행됨)
            ShowBlockingUi("step1");
        }
    }

    public override void OnUIAction(string actionId)
    {
        // step1 진행 버튼 → Step 수명 효과(=FlowLock) 해제 + UI 숨김 → 전투 루프 계속.
        if (actionId == "step1")
        {
            AdvanceStep();
            HideUi();
        }
    }
}
