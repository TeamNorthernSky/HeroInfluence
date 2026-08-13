namespace ASB.Work.Battle.SkillExecution
{
    // 절망의 굴렁쇠(FV40005_1): 대상 행 광역피해 + 시전자 최대체력 자해(사망).
    // - 행 광역 해석은 BaseAoESkillHandler 재사용(데이터 classSkillTarget/boundary가 행 패턴을 인코딩).
    // - 자해는 캐스트 종료 시점(반격 큐 처리 후, BattleManager.cs:645)에 실행된다.
    public sealed class SelfDestructRowAoEHandler : BaseAoESkillHandler
    {
        // 광역 데미지는 AoEDamageSkillHandler와 동일.
        protected override void ApplyAdditionaDamage(
            BattleCharactor caster, BattleCharactor target, SkillData skillData,
            int Count, SkillExecutionResult result, bool? sharedIsCritical = null)
        {
            result.AddDamage(SkillEffectHelper.ApplyStandardDamage(
                caster, target, skillData.skillValue, skillData.skillIndex,
                skillData.classSkillRange, sharedIsCritical: sharedIsCritical));
        }

        // 행에 대상이 없어도 자해는 발생해야 한다("돌진 후 사라짐").
        // base가 Failed면 성공 결과로 교체해 사후 훅을 보장한다.
        public override SkillExecutionResult Execute(
            BattleCharactor caster, BattleCharactor target,
            SkillData skillData, SkillData additionalSkillData)
        {
            SkillExecutionResult result = base.Execute(caster, target, skillData, additionalSkillData);
            if (result == null || !result.Success)
            {
                result = SkillExecutionResult.SuccessResult(caster, skillData);
            }

            // 행 광역 데미지 적용 후 시전자 최대체력 자해 → 전투식 무시, 정확히 MaxHp.
            result.OnPostExecution += _ =>
            {
                if (caster != null && !caster.IsDead)
                {
                    caster.TakeDamage(caster.MaxHp);
                }
            };
            return result;
        }
    }
}
