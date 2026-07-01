using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 유닛이 타겟 앞으로 전진하는 연출 액션.
    /// </summary>
    public class MoveToTargetAction : BattleSequenceAction
    {
        private readonly CharactorAnimationController _anim;
        private readonly Transform _target;
        private readonly float _approachDistance;
        private readonly float _duration;

        public MoveToTargetAction(
            CharactorAnimationController anim,
            Transform target,
            float approachDistance,
            float duration)
        {
            _anim = anim;
            _target = target;
            _approachDistance = approachDistance;
            _duration = duration;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_anim == null || _target == null)
            {
                yield break;
            }

            yield return _anim.StartCoroutine(_anim.MoveToTarget(_target, _approachDistance, _duration));
        }
    }
}
