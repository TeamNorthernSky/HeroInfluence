using System.Collections;

namespace ASB.Work.Battle.Command
{
    /// <summary>
    /// 반격 스킬 실행 1건. 기존 BattleManager.ExecuteCounterSkill을 그대로 감싼 커맨드입니다.
    /// </summary>
    internal class CounterAttackActionCommand : IBattleActionCommand
    {
        private readonly BattleManager.CounterAttackRequest _request;

        public int Depth { get; set; }

        public CounterAttackActionCommand(BattleManager.CounterAttackRequest request)
        {
            _request = request;
        }

        public IEnumerator Execute(BattleManager battleManager)
        {
            yield return battleManager.StartCoroutine(battleManager.ExecuteCounterSkill(_request));
        }
    }
}
