/// <summary>
/// 튜토리얼 예시(테스트 스텝 구현).
/// step1: 전투 시작 시 안내 + 버튼으로 진행(전투 루프 잠금, 입력 마스킹 없음) + 아군 IP=50.
/// step2: 2라운드 10001 유닛 턴에 안내 + 적 최소HP=1(공격해도 안 죽게 — 공격 전에 적용).
/// 유닛 식별은 templateId(TutorialUnitMatcher) 기준 — 실제 값에 맞게 상단 상수만 조정.
/// UI는 host.ShowUi("key")(=TutorialBattleUI 시트) 경로. 버튼 actionId "step1"/"step2"로 진행.
/// </summary>
public sealed class TutorialBattle01Flow : TutorialBattleFlow
{
    public override string BattleKey => "TUT_01"; // TODO: 실제 진입 BattleKey로 교체
    public override int ZoneId => -1;             // TODO: 실제 ZoneId로 교체

    // ── 조정 지점 ──
    private const string HeroTemplateId = "10001";  // step2 트리거 유닛(플레이어)
    private const int Step2Round = 2;
    private const float AllyInfluence = 50f;
    private const float EnemyMinHp = 1f;

    // 0=시작전, 1=step1 표시중(진행 대기), 2=step1 완료(전투중), 3=step2 발동
    private int step;
    private bool step2MinHpReleased;   // step2 데모 공격 후 적 최소HP를 1회만 해제

    public override void OnBattleEntered()
    {
        step = 1;
        // 안내 표시 + 전투 루프 잠금(버튼 누를 때까지 대기). 다른 입력 마스킹은 없음.
        // (UI 시트/컴포넌트 미배선이면 fail-open으로 잠금 없이 진행됨)
        ShowBlockingUi("step1");

        // 아군 전원 IP = 50
        ForEachParticipant(player: true, u => u.SetInfluence(AllyInfluence));
    }

    public override void OnUIAction(string actionId)
    {
        // step1: 진행 버튼 → 잠금 해제(전투 루프 계속) + UI 숨김
        if (step == 1 && actionId == "step1")
        {
            step = 2;
            AdvanceStep();   // step1 FlowLock(Step 수명) 해제
            HideUi();
            return;
        }

        // step2: 확인 버튼 → step2 UI만 숨김 (적 최소HP 해제는 데모 공격 후 OnSkillResolved에서)
        if (step == 3 && actionId == "step2")
        {
            HideUi();
        }
    }

    public override void OnSkillResolved(SkillResolutionContext ctx)
    {
        // step2 데모 공격(10001의 공격)이 끝나면 적 최소HP를 해제 → 이후 정상 전투(이길 수 있음).
        if (step == 3 && !step2MinHpReleased && ctx != null &&
            TutorialUnitMatcher.MatchesTemplateId(ctx.Actor, TutorialUnitSide.Player, HeroTemplateId))
        {
            step2MinHpReleased = true;
            AdvanceStep();   // step 수명 효과(step2 적 최소HP) 해제
        }
    }

    public override void OnTurnStarted(int round, BattleCharactor unit)
    {
        // step2: 2라운드 + 10001 유닛 턴 → 안내 + 적 최소HP=1
        if (step == 2 && round == Step2Round &&
            TutorialUnitMatcher.MatchesTemplateId(unit, TutorialUnitSide.Player, HeroTemplateId))
        {
            step = 3;
            ShowUi("step2");   // 비차단 안내(플레이어가 스킬→대상→공격 진행)

            // 적 최소 HP = 1 — 공격 '전'(턴 시작 시)에 걸어야 실제 공격에서 안 죽음.
            ForEachParticipant(player: false, u =>
            {
                if (!u.IsDead)
                {
                    Track(u.AddMinimumHpConstraint(EnemyMinHp, this), TutorialEffectLifetime.Step);
                }
            });
        }
    }

    // TODO(step3/step4): 사용자가 스텝 정의하면 이어서 구현.

    private void ForEachParticipant(bool player, System.Action<BattleCharactor> action)
    {
        if (Host == null || Host.FlowManager == null)
        {
            return;
        }

        System.Collections.Generic.IReadOnlyList<BattleCharactor> units = Host.FlowManager.Participants;
        for (int i = 0; units != null && i < units.Count; i++)
        {
            BattleCharactor u = units[i];
            if (u != null && u.IsPlayer == player)
            {
                action(u);
            }
        }
    }
}
