namespace ASB.Work.Battle.SkillExecution
{
    public interface ITargetSelector
    {
        BattleCharactor SelectTarget(SkillExecutionContext context);
    }
}
