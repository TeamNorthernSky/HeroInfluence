using System.Collections;

namespace ASB.Work.Battle.Command
{
    /// <summary>
    /// 전투 중 발생하는 사건(스킬, 대사, 상태이상 등)의 최소 단위.
    /// </summary>
    public interface IBattleActionCommand
    {
        /// <summary>
        /// 연쇄 깊이. 최초 행동이 0이고, 그 행동이 낳은 후속 액션이 1, 그 후속이 2… 로 늘어난다.
        /// BattleManager가 큐에 넣을 때 설정하며, 무한 연쇄를 막는 상한 판정에 쓰인다.
        /// </summary>
        int Depth { get; set; }

        IEnumerator Execute(BattleManager battleManager);
    }
}
