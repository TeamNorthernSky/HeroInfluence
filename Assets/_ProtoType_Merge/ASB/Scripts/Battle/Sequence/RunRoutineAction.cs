using System;
using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>BattleManager가 조립한 보조 코루틴을 기존 연출 큐 안에서 순차 실행한다.</summary>
    public sealed class RunRoutineAction : BattleSequenceAction
    {
        private readonly Func<MonoBehaviour, IEnumerator> _routineFactory;

        public RunRoutineAction(Func<MonoBehaviour, IEnumerator> routineFactory)
        {
            _routineFactory = routineFactory;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            IEnumerator routine = _routineFactory?.Invoke(host);
            if (routine != null)
            {
                yield return routine;
            }
        }
    }
}