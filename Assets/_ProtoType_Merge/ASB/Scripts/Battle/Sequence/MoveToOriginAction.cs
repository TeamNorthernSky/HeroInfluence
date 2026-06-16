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

        public MoveToOriginAction(
            CharactorAnimationController anim,
            Vector3 origin,
            Quaternion originalRotation,
            float duration)
        {
            _anim = anim;
            _origin = origin;
            _originalRotation = originalRotation;
            _duration = duration;
        }

        public override IEnumerator ExecuteRoutine()
        {
            if (_anim == null)
            {
                yield break;
            }

            yield return _anim.StartCoroutine(_anim.MoveToOrigin(_origin, _originalRotation, _duration));
        }
    }
}
