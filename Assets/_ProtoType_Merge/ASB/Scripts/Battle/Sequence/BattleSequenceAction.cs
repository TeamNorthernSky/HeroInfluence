using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 전투 연출 큐의 최소 단위. 각 Action은 ExecuteRoutine이 끝날 때까지 순차 대기됩니다.
    /// </summary>
    public abstract class BattleSequenceAction
    {
        public abstract IEnumerator ExecuteRoutine();
    }
}
