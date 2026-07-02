using System.Collections;

namespace ASB.Work.Battle.Command
{
    /// <summary>
    /// 전투 중 발생하는 사건(스킬, 대사, 상태이상 등)의 최소 단위.
    /// </summary>
    public interface IBattleActionCommand
    {
        IEnumerator Execute(BattleManager battleManager);
    }
}
