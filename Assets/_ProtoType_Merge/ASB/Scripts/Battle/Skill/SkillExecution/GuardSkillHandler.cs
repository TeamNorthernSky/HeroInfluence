namespace ASB.Work.Battle.SkillExecution
{
    // 대신 맞기(FV20003_2): 시전 결과에 버프를 담고, 비용 검사와 연출 후 B에게 적용한다.
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
            if (caster == target || caster.IsDead || caster.IsIncapacitated || target.IsDead
                || target.IsIncapacitated || caster.IsPlayer != target.IsPlayer)
            {
                return SkillExecutionResult.Failed();
            }

            return SkillExecutionResult.SuccessResult(caster, skillData)
                .AddStatusEffect(caster, target, StatusEffectType.guarded, 1);
        }
    }
}
