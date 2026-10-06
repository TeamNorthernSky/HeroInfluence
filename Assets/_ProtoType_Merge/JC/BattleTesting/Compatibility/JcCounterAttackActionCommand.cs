// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Command/CounterAttackActionCommand.cs
// 원본 객체: CounterAttackActionCommand -> JcCounterAttackActionCommand
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System.Collections;

namespace ASB.Work.Battle.Command
{
    /// <summary>
    /// 반격 스킬 실행 1건. 기존 JcBattleManager.ExecuteCounterSkill을 그대로 감싼 커맨드입니다.
    /// </summary>
    internal class JcCounterAttackActionCommand : JcIBattleActionCommand
    {
        private readonly JcBattleManager.CounterAttackRequest _request;

        public int Depth { get; set; }

        public JcCounterAttackActionCommand(JcBattleManager.CounterAttackRequest request)
        {
            _request = request;
        }

        // 원본 함수 대응: CounterAttackActionCommand.Execute (Assets/_ProtoType_Merge/ASB/Scripts/Battle/Command/CounterAttackActionCommand.cs)

        public IEnumerator Execute(JcBattleManager battleManager)
        {
            yield return battleManager.StartCoroutine(battleManager.ExecuteCounterSkill(_request));
        }
    }
}
