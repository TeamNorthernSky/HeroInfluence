using System.Collections;
using System.Collections.Generic;

namespace ASB.Work.Battle.Command
{
    /// <summary>
    /// IBattleActionCommand를 순서대로 실행합니다.
    /// </summary>
    public class BattleActionQueue
    {
        private readonly Queue<IBattleActionCommand> _queue = new Queue<IBattleActionCommand>();

        public int Count => _queue.Count;

        public void Enqueue(IBattleActionCommand command)
        {
            if (command != null)
            {
                _queue.Enqueue(command);
            }
        }

        public void Clear() => _queue.Clear();

        public IEnumerator RunAll(BattleManager battleManager)
        {
            while (_queue.Count > 0)
            {
                IBattleActionCommand command = _queue.Dequeue();
                yield return battleManager.StartCoroutine(command.Execute(battleManager));
            }
        }
    }
}
