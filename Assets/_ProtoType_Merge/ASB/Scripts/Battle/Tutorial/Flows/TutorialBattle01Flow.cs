/// <summary>
/// 튜토리얼 TUT_01 (디버그: 각 step의 텍스트+버튼만 표시).
/// step1: 스트라이커(블랙불릿, 10003) 턴 시작 시 안내(전투 루프 잠금). 버튼 누르면 스트라이커 턴 계속.
/// step2: 서포터(네코밍, 10004) 턴 시작 시(1라운드) 안내(잠금). 버튼 누르면 네코밍 턴 계속.
/// step3: 승리 결과창 표시 직후 안내(비차단 — 전투 루프가 끝나 블로킹 불가). Accept로 flow 완료.
/// 유닛 식별은 templateId(CharactorScript.Data.Index) 기준 — 파티는 {10003, 10004} 전제.
/// UI는 host.ShowUi("key") 경로(코드-온리: 씬 stepTexts의 Text/Button과 key로 매칭).
/// </summary>
public sealed class TutorialBattle01Flow : TutorialBattleFlow
{
    public override string BattleKey => "TUT_01";
    public override int ZoneId => -1;

    // ── 조정 지점 ──
    private const string StrikerId = "10003";    // step1: 첫 아군(스트라이커/블랙불릿)
    private const string SupporterId = "10004";  // step2: 서포터(네코밍)

    // 각 step 1회만 발동하도록 가드
    private bool step1Done;
    private bool step2Done;
    private bool step3Done;

    public override void OnTurnStarted(int round, BattleCharactor unit)
    {
        // step1: 스트라이커(10003) 턴 시작 → 안내 + 전투 루프 잠금(버튼으로 진행).
        //        CurrentUnit·턴 순서가 확정돼 HUD가 그려진 뒤라 흰 화면이 없다.
        if (!step1Done &&
            TutorialUnitMatcher.MatchesTemplateId(unit, TutorialUnitSide.Player, StrikerId))
        {
            step1Done = true;
            // (UI 시트/컴포넌트 미배선이면 fail-open으로 잠금 없이 진행됨)
            ShowBlockingUi("step1");
            return;
        }

        // step2: 서포터(10004) 턴 시작(1라운드) → 안내 + 잠금(버튼으로 진행).
        if (!step2Done && round == 1 &&
            TutorialUnitMatcher.MatchesTemplateId(unit, TutorialUnitSide.Player, SupporterId))
        {
            step2Done = true;
            ShowBlockingUi("step2");
        }
    }

    public override void OnUIAction(string actionId)
    {
        // step1/step2 진행 버튼 → Step 수명 효과(=ShowBlockingUi가 잡은 FlowLock) 해제 + UI 숨김
        // → 해당 유닛(스트라이커/네코밍)의 턴이 계속 진행되어 전투 루프가 돈다.
        if (actionId == "step1" || actionId == "step2")
        {
            AdvanceStep();
            HideUi();
            return;
        }

        // step3 진행 버튼 → UI 숨김(비차단이라 잠금 없음). 완료는 결과창 Accept에서.
        if (actionId == "step3")
        {
            HideUi();
        }
    }

    public override void OnBattleResultShown(BattleResult result)
    {
        // step3: 승리 결과창 표시 직후 → 안내(비차단 오버레이).
        //        전투 루프가 이미 끝나 FlowLock을 잡을 수 없으므로 ShowUi(비차단)만 가능.
        if (!step3Done && result == BattleResult.Victory)
        {
            step3Done = true;
            ShowUi("step3");
        }
    }

    public override void OnBattleResultAccepted(BattleResult result)
    {
        // 결과창 Accept("UI가 다 진행") → UI 정리 + flow 완료 → 이후 내용 진행.
        HideUi();
        Complete();
    }
}
