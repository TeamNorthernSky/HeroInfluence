using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 유닛이 원래 위치로 복귀하는 연출 액션.
    /// </summary>
    public class MoveToOriginAction : BattleSequenceAction
    {
        private readonly CharactorAnimationController _anim;
        private readonly Vector3 _origin;
        private readonly Quaternion _originalRotation;
        private readonly float _duration;
        private readonly string _animationStateName;
        private readonly float _blendInSeconds;

        public MoveToOriginAction(
            CharactorAnimationController anim,
            Vector3 origin,
            Quaternion originalRotation,
            float duration,
            string animationStateName,
            float blendInSeconds)
        {
            _anim = anim;
            _origin = origin;
            _originalRotation = originalRotation;
            _duration = duration;
            _animationStateName = animationStateName;
            _blendInSeconds = blendInSeconds;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_anim == null)
            {
                yield break;
            }

            yield return _anim.StartCoroutine(_anim.MoveToOrigin(
                _origin, _originalRotation, _duration, _animationStateName, _blendInSeconds));
        }
    }
}
