using System;
using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 데미지 콜백 실행 + 단일 타겟 피격 애니메이션.
    /// </summary>
    public class ApplyDamageAction : BattleSequenceAction
    {
        private readonly BattleCharactor _target;
        private readonly Action _onHitCallback;
        private readonly bool _playHitAnimation;
        private readonly float _battleSpeed;

        public ApplyDamageAction(
            BattleCharactor target,
            Action onHitCallback,
            bool playHitAnimation,
            float battleSpeed)
        {
            _target = target;
            _onHitCallback = onHitCallback;
            _playHitAnimation = playHitAnimation;
            _battleSpeed = battleSpeed;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            _onHitCallback?.Invoke();

            if (_playHitAnimation && _target != null)
            {
                _target.EnsureAnimationController();
                _target.Anim?.SetAnimationSpeed(_battleSpeed);
                _target.Anim?.PlayGenericAnimation("Hit");
            }

            yield break;
        }
    }
}
