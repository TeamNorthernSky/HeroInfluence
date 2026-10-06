// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/GuardSkillHandler.cs
// 원본 객체: GuardSkillHandler -> JcGuardSkillHandler
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
namespace ASB.Work.Battle.SkillExecution
{
    // 대신 맞기(FV20003_2): 시전 시 즉시 데미지 없이 A가 B(같은 편)를 보호 시작.
    // 실제 가로채기는 플레이어가 B를 '단일' 공격할 때 JcBattleManager.TryApplyGuardRedirect에서 발동.
    public sealed class JcGuardSkillHandler : ISkillEffectHandler
    {
        // 원본 함수 대응: GuardSkillHandler.Execute (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/GuardSkillHandler.cs)
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
