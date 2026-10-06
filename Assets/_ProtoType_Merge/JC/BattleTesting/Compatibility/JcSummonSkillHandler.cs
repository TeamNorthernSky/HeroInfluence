// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SummonSkillHandler.cs
// 원본 객체: SummonSkillHandler -> JcSummonSkillHandler
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
namespace ASB.Work.Battle.SkillExecution
{
    // 소환 스킬(FV40001_3 절망의 굴레, FV40004_1 율리아를 가둔 장막): 시전자 JcBossController 설정대로 미니언 소환.
    // 데미지 없음. target은 실행 파이프라인 통과용이라 소환 결과엔 쓰지 않음(AI가 유효한 살아있는 대상 1명을 넘긴다).
    // 소환은 캐스트 종료 후(OnPostExecution, 반격 처리 뒤)에 실행한다. 시전 연출은 그 전에 1회 재생된다.
    public sealed class JcSummonSkillHandler : ISkillEffectHandler, ICastOnlyPresentationHandler
    {
        // 원본 함수 대응: SummonSkillHandler.Execute (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SummonSkillHandler.cs)
        public SkillExecutionResult Execute(
            BattleCharactor caster, BattleCharactor target,
            SkillData skillData, SkillData additionalSkillData)
        {
            if (caster == null || skillData == null || caster.IsDead)
            {
                return SkillExecutionResult.Failed();
            }

            SkillExecutionResult result = SkillExecutionResult.SuccessResult(caster, skillData);

            JcBossController boss = caster.GetComponent<JcBossController>();
            if (boss == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"[Summon] {caster.UnitName}: JcBossController 없음 → 소환 스킵 (skill={skillData.skillKey})");
                return result;
            }

            string skillKey = skillData.skillKey;
            result.OnPostExecution += _ => boss.TrySummonForSkill(skillKey);
            return result;
        }
    }
}
