using System.Linq;
/// <summary>
/// 첫 전투의 4인 소개와 블랙 불릿 고정 조작 안내.
/// 각 캐릭터의 첫 행동 턴에 한 번 안내하고, 결과 안내는 기존 경로를 유지합니다.
/// </summary>
public sealed class TutorialBattle01Flow : TutorialBattleFlow
{
    public override string BattleKey => "TUT_01";
    public override int ZoneId => -1;

    private readonly System.Collections.Generic.HashSet<string> introduced = new System.Collections.Generic.HashSet<string>();
    private bool step3Done;

    public override void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (unit == null || !unit.IsPlayer) return;
        foreach (string id in new[] { "10001", "10002", "10003", "10004" })
        {
            if (introduced.Contains(id) || !TutorialUnitMatcher.MatchesTemplateId(unit, TutorialUnitSide.Player, id)) continue;
            var guide = unit.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<JC.Tutorial.JcTutorialBattleGuide>(true)).FirstOrDefault();
            if (guide == null || guide.gameObject.scene != unit.gameObject.scene) return;
            if (ShowUi("step1") && guide.BeginCharacter(unit, id)) introduced.Add(id);
            else HideUi();
            return;
        }
    }

    public override void OnUIAction(string actionId)
    {
        // 기존 버튼 이벤트 연결을 유지합니다. 지정 조작 안내는 자체 행동 제약을 먼저 해제합니다.
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
