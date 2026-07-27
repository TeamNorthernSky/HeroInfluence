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
        private readonly string _animationStateName;
        private readonly float _blendInSeconds;

        public MoveToTargetAction(
            CharactorAnimationController anim,
            Transform target,
            float approachDistance,
            float duration,
            string animationStateName,
            float blendInSeconds)
        {
            _anim = anim;
            _target = target;
            _approachDistance = approachDistance;
            _duration = duration;
            _animationStateName = animationStateName;
            _blendInSeconds = blendInSeconds;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_anim == null || _target == null)
            {
                yield break;
            }

            yield return _anim.StartCoroutine(_anim.MoveToTarget(
                _target, _approachDistance, _duration, _animationStateName, _blendInSeconds));
        }
    }
}
