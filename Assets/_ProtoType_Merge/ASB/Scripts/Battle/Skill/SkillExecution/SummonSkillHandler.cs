namespace ASB.Work.Battle.SkillExecution
{
    // 소환 스킬(FV40001_3 절망의 굴레, FV40004_1 율리아를 가둔 장막): 시전자 BossController 설정대로 미니언 소환.
    // 데미지 없음. target은 실행 파이프라인 통과용이라 소환 결과엔 쓰지 않음(AI가 유효한 살아있는 대상 1명을 넘긴다).
    // 소환은 캐스트 종료 후(OnPostExecution, 반격 처리 뒤)에 실행한다.
    public sealed class SummonSkillHandler : ISkillEffectHandler
    {
        public SkillExecutionResult Execute(
            BattleCharactor caster, BattleCharactor target,
            SkillData skillData, SkillData additionalSkillData)
        {
            if (caster == null || skillData == null || caster.IsDead)
            {
                return SkillExecutionResult.Failed();
            }

            SkillExecutionResult result = SkillExecutionResult.SuccessResult(caster, skillData);

            BossController boss = caster.GetComponent<BossController>();
            if (boss == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"[Summon] {caster.UnitName}: BossController 없음 → 소환 스킵 (skill={skillData.skillKey})");
                return result;
            }

            string skillKey = skillData.skillKey;
            result.OnPostExecution += _ => boss.TrySummonForSkill(skillKey);
            return result;
        }
    }
}
