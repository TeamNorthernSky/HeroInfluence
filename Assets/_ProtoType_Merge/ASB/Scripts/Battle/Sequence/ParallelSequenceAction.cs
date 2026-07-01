using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 여러 액션을 동시에 실행하고, 전부 끝날 때까지 대기합니다.
    /// 서로 간섭하지 않는 액션들만 묶어야 합니다.
    /// </summary>
    public class ParallelSequenceAction : BattleSequenceAction
    {
        private readonly List<BattleSequenceAction> _actions;

        public ParallelSequenceAction(params BattleSequenceAction[] actions)
        {
            _actions = new List<BattleSequenceAction>(actions);
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            int remaining = _actions.Count;
            foreach (var action in _actions)
                host.StartCoroutine(Wrap(action.ExecuteRoutine(host), action.GetType().Name, () => remaining--));
            yield return new WaitUntil(() => remaining <= 0);
        }

        private IEnumerator Wrap(IEnumerator routine, string actionName, System.Action onDone)
        {
            while (true)
            {
                try
                {
                    if (!routine.MoveNext()) break;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ParallelAction] {actionName} 실패: {e.Message}");
                    break;
                }
                yield return routine.Current;
            }
            onDone();
        }
    }
}
