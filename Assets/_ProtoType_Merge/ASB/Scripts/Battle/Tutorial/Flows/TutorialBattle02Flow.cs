using System.Linq;

/// <summary>두 번째 전투의 첫 아군 턴에만 배치 규칙을 안내합니다.</summary>
public sealed class TutorialBattle02Flow : TutorialBattleFlow
{
    public override string BattleKey => "TUT_02";
    public override int ZoneId => -1;
    private bool formationShown, resultShown;

    public override void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (formationShown || unit == null || !unit.IsPlayer || unit.IsDead) return;
        var guide = unit.gameObject.scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<JC.Tutorial.JcTutorialBattleGuide>(true)).FirstOrDefault();
        if (guide == null) return;
        if (ShowUi("step1") && guide.BeginFormation(unit)) formationShown = true;
        else HideUi();
    }

    public override void OnBattleResultShown(BattleResult result)
    {
        if (!resultShown && result == BattleResult.Victory) { resultShown = true; ShowUi("step3"); }
    }
    public override void OnUIAction(string actionId) { if (actionId == "step1" || actionId == "step3") HideUi(); }
    public override void OnBattleResultAccepted(BattleResult result) { HideUi(); Complete(); }
}
