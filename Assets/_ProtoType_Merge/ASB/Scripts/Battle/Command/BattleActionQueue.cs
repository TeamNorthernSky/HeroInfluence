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

        /// <summary>선입선출로 하나 꺼낸다. 비어 있으면 null.</summary>
        public IBattleActionCommand Dequeue() => _queue.Count > 0 ? _queue.Dequeue() : null;

        /// <summary>
        /// 큐가 빌 때까지 순서대로 실행한다.
        /// 매 바퀴 Count를 다시 보므로, 실행 중에 추가된 액션도 같은 루프가 이어서 처리한다(연쇄).
        /// </summary>
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
