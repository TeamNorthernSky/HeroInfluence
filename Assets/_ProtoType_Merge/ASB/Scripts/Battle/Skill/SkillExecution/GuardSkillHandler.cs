namespace ASB.Work.Battle.SkillExecution
{
    // 대신 맞기(FV20003_2): 시전 시 즉시 데미지 없이 A가 B(같은 편)를 보호 시작.
    // 실제 가로채기는 플레이어가 B를 '단일' 공격할 때 BattleManager.TryApplyGuardRedirect에서 발동.
    public sealed class GuardSkillHandler : ISkillEffectHandler
    {
        public SkillExecutionResult Execute(
            BattleCharactor caster, BattleCharactor target,
            SkillData skillData, SkillData additionalSkillData)
        {
            if (caster == null || target == null || skillData == null)
            {
                return SkillExecutionResult.Failed();
            }

            // 유효성: 자기 자신/사망/적 대상 보호 금지(잘못된 데이터·수동 호출 방어).
            if (caster == target || caster.IsDead || target.IsDead || caster.IsPlayer != target.IsPlayer)
            {
                return SkillExecutionResult.Failed();
            }

            caster.BeginGuard(target);
            return SkillExecutionResult.SuccessResult(caster, skillData);
        }
    }
}
