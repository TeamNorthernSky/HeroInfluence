using System.Collections;
using PrimeTween;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 원거리 유닛용: 위치 이동 없이 타겟 방향으로만 회전합니다.
    /// </summary>
    public class RotateToTargetAction : BattleSequenceAction
    {
        private readonly Transform _self;
        private readonly Transform _target;
        private readonly float _duration;

        public RotateToTargetAction(Transform self, Transform target, float duration)
        {
            _self = self;
            _target = target;
            _duration = duration;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_self == null || _target == null)
            {
                yield break;
            }

            Vector3 dir = (_target.position - _self.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                yield break;
            }

            Quaternion targetRotation = Quaternion.Euler(0f, Quaternion.LookRotation(dir.normalized).eulerAngles.y, 0f);
            yield return Tween.Rotation(_self, targetRotation, _duration).ToYieldInstruction();
        }
    }
}
