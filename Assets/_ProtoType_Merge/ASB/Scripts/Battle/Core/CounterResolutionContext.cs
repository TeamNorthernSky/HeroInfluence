using ASB.Work.Battle.SkillExecution;

/// <summary>
/// 반격(CounterAttack) 1회가 해소된 뒤 전달되는 읽기 전용 결과.
/// 최상위 스킬 결과(SkillResolutionContext)와 분리되어, 반격 자체를 트리거로 잡을 때 사용한다.
/// </summary>
public sealed class CounterResolutionContext
{
    public BattleCharactor CounterActor { get; }
    public BattleCharactor OriginalActor { get; }
    public BattleCharactor Target { get; }
    public SkillData Skill { get; }
    public SkillExecutionResult Result { get; }

    public CounterResolutionContext(
        BattleCharactor counterActor,
        BattleCharactor originalActor,
        BattleCharactor target,
        SkillData skill,
        SkillExecutionResult result)
    {
        CounterActor = counterActor;
        OriginalActor = originalActor;
        Target = target;
        Skill = skill;
        Result = result;
    }
}
