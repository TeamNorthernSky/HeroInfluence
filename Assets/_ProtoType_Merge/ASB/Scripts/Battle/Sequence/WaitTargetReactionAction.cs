using System.Collections;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 타겟의 Hit 또는 Die 애니메이션이 끝날 때까지 대기합니다.
    /// - 사망: Die 클립이 끝날 때까지 대기 후 Idle 복귀 없음
    /// - 생존: minWait 보장 후 Hit 클립 종료 시 Idle 복귀
    /// ReturnToIdleIfAlive를 대체합니다.
    /// </summary>
    public class WaitTargetReactionAction : BattleSequenceAction
    {
        private readonly BattleCharactor _target;
        private readonly float _battleSpeed;
        private readonly float _minWait;
        private readonly float _maxWait;

        public WaitTargetReactionAction(
            BattleCharactor target,
            float battleSpeed,
            float minWait = 0.15f,
            float maxWait = 0.6f)
        {
            _target = target;
            _battleSpeed = battleSpeed;
            _minWait = minWait;
            _maxWait = maxWait;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_target == null) yield break;

            _target.EnsureAnimationController();
            CharactorAnimationController anim = _target.Anim;
            if (anim == null) yield break;

            if (_target.IsDead)
            {
                yield return WaitForState(anim, "Die", 0f, _maxWait, 0.95f);
                // 사망 상태 유지 — Idle 복귀 없음
            }
            else
            {
                yield return WaitForState(anim, "Hit", _minWait, _maxWait, 0.9f);
                _target.Anim?.PlayIdleAnimation();
            }
        }

        private IEnumerator WaitForState(
            CharactorAnimationController anim,
            string stateName,
            float minWait,
            float maxWait,
            float endThreshold)
        {
            float elapsed = 0f;

            // minWait 동안 무조건 대기 (애니 상태 진입 보장)
            while (elapsed < minWait)
            {
                elapsed += Time.deltaTime * _battleSpeed;
                yield return null;
            }

            // maxWait 이내에 상태가 끝날 때까지 대기
            elapsed = 0f;
            while (elapsed < maxWait)
            {
                if (anim.IsStateNearEnd(stateName, endThreshold))
                    yield break;

                elapsed += Time.deltaTime * _battleSpeed;
                yield return null;
            }
        }
    }
}
