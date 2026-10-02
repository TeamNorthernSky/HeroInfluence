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

    /// <summary>
    /// 피해·회복·상태이상 결과가 없어도 시전 연출을 1회 재생해야 하는 핸들러 표시(소환 등).
    /// BattleManager가 연출을 마친 뒤 OnPostExecution을 실행한다. 미구현 핸들러는 현행대로 연출 없음.
    /// </summary>
    public interface ICastOnlyPresentationHandler
    {
    }
}
