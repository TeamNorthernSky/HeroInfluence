// [JC 독립 구현 대응표 / 기준 JC 0705af74]
// 원본 파일: Assets/_ProtoType_Merge/ASB/Scripts/Battle/Command/IBattleActionCommand.cs
// 원본 객체: IBattleActionCommand -> JcIBattleActionCommand
// 동일 이름의 함수는 원본 함수와 1:1 대응합니다. 별도 변경 함수에는 차이를 추가로 명시합니다.
// 목적: ASB 원본과 본게임 참조를 수정하지 않고 테스트 전투 제어를 독립시킵니다.
// 공용 데이터·유닛·모델·연출 에셋은 원본을 참조합니다. 이 파일은 자동 동기화되지 않습니다.
using System.Collections;

namespace ASB.Work.Battle.Command
{
    /// <summary>
    /// 전투 중 발생하는 사건(스킬, 대사, 상태이상 등)의 최소 단위.
    /// </summary>
    public interface JcIBattleActionCommand
    {
        /// <summary>
        /// 연쇄 깊이. 최초 행동이 0이고, 그 행동이 낳은 후속 액션이 1, 그 후속이 2… 로 늘어난다.
        /// BattleManager가 큐에 넣을 때 설정하며, 무한 연쇄를 막는 상한 판정에 쓰인다.
        /// </summary>
        int Depth { get; set; }

        IEnumerator Execute(JcBattleManager battleManager);
    }
}
