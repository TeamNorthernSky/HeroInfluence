namespace ASB.Work.Battle.SkillExecution
{
    /// <summary>
    /// 특수 스킬(skillIndex 기반 커스텀) 실행을 위한 인터페이스.
    /// 일반 스킬은 레지스트리에 없을 때 BattleManager 기본 경로로 처리됩니다.
    /// </summary>
    public interface ISkillEffectHandler
    {
        SkillExecutionResult Execute(BattleCharactor caster, BattleCharactor target, SkillData skillData, SkillData additionalSkillData);
    }
}
