using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>Spin Sweep의 Entry 월드 좌표까지 기존 이동 애니메이션으로 접근한다.</summary>
    public sealed class MoveToWorldPositionAction : BattleSequenceAction
    {
        private readonly CharactorAnimationController _animation;
        private readonly Vector3 _destination;
        private readonly float _duration;
        private readonly string _animationStateName;
        private readonly float _blendInSeconds;

        public MoveToWorldPositionAction(CharactorAnimationController animation, Vector3 destination, float duration,
            string animationStateName, float blendInSeconds)
        {
            _animation = animation;
            _destination = destination;
            _duration = duration;
            _animationStateName = animationStateName;
            _blendInSeconds = blendInSeconds;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_animation == null)
            {
                yield break;
            }

            yield return _animation.StartCoroutine(_animation.MoveToPosition(
                _destination, _duration, _animationStateName, _blendInSeconds));
        }
    }
}
