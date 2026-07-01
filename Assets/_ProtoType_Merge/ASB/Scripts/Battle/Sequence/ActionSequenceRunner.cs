using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// BattleSequenceAction 큐를 순서대로 실행합니다.
    /// MonoBehaviour에서 StartCoroutine(runner.RunAll())으로 호출하세요.
    /// </summary>
    public class ActionSequenceRunner
    {
        private readonly Queue<BattleSequenceAction> _queue = new Queue<BattleSequenceAction>();

        public int Count => _queue.Count;

        public void Enqueue(BattleSequenceAction action)
        {
            if (action != null)
            {
                _queue.Enqueue(action);
            }
        }

        public void Clear() => _queue.Clear();

        public IEnumerator RunAll(MonoBehaviour host)
        {
            while (_queue.Count > 0)
            {
                BattleSequenceAction action = _queue.Dequeue();
                yield return host.StartCoroutine(action.ExecuteRoutine(host));
            }
        }
    }
}
