// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SelfDestructRowAoEHandler.cs
// 원본 객체: SelfDestructRowAoEHandler -> JcSelfDestructRowAoEHandler
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
namespace ASB.Work.Battle.SkillExecution
{
    // 절망의 굴렁쇠(FV40005_1): 대상 행 광역피해 + 시전자 최대체력 자해(사망).
    // - 행 광역 해석은 JcBaseAoESkillHandler 재사용(데이터 classSkillTarget/boundary가 행 패턴을 인코딩).
    // - 자해는 캐스트 종료 시점(반격 큐 처리 후, JcBattleManager.cs:645)에 실행된다.
    public sealed class JcSelfDestructRowAoEHandler : JcBaseAoESkillHandler
    {
        // 광역 데미지는 AoEDamageSkillHandler와 동일.
        // 원본 함수 대응: SelfDestructRowAoEHandler.ApplyAdditionaDamage (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SelfDestructRowAoEHandler.cs)
        protected override void ApplyAdditionaDamage(
            BattleCharactor caster, BattleCharactor target, SkillData skillData,
            int Count, SkillExecutionResult result, bool? sharedIsCritical = null)
        {
            result.AddDamage(JcSkillEffectHelper.ApplyStandardDamage(
                caster, target, skillData.skillValue, skillData.skillIndex,
                skillData.classSkillRange, sharedIsCritical: sharedIsCritical));
        }

        // 행에 대상이 없어도 자해는 발생해야 한다("돌진 후 사라짐").
        // base가 Failed면 성공 결과로 교체해 사후 훅을 보장한다.
        // 원본 함수 대응: SelfDestructRowAoEHandler.Execute (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SelfDestructRowAoEHandler.cs)
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
