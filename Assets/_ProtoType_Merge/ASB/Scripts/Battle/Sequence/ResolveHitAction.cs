using System;
using System.Collections;
using ASB.Work.Battle.Core;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 히트 콜백 실행 → 결과(BattleHitResult) 기반 이펙트·팝업 연출 → 타겟 피격 애니메이션.
    /// ApplyDamageAction을 대체합니다.
    /// </summary>
    public class ResolveHitAction : BattleSequenceAction
    {
        private readonly BattleCharactor _actor;
        private readonly BattleCharactor _target;
        private readonly Func<BattleHitResult> _onHit;
        private readonly string _targetAnimTrigger;
        private readonly float _battleSpeed;
        private readonly BattleVisualDirector _visual;

        /// <param name="targetAnimTrigger">타겟에 재생할 애니메이션 트리거. null이면 피격 애니 생략.</param>
        public ResolveHitAction(
            BattleCharactor actor,
            BattleCharactor target,
            Func<BattleHitResult> onHit,
            string targetAnimTrigger,
            float battleSpeed,
            BattleVisualDirector visual = null)
        {
            _actor = actor;
            _target = target;
            _onHit = onHit;
            _targetAnimTrigger = targetAnimTrigger;
            _battleSpeed = Mathf.Max(0.01f, battleSpeed);
            _visual = visual;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            GetHitDelays(out float damagePopupDelay, out float hitAnimationDelay);

            float elapsed = 0f;
            bool damageResolved = false;
            bool hitAnimationPlayed = !ShouldPlayHitAnimation();

            while (!damageResolved || !hitAnimationPlayed)
            {
                elapsed += Time.deltaTime * _battleSpeed;

                if (!damageResolved && elapsed >= damagePopupDelay)
                {
                    ResolveDamageAndPresentation();
                    damageResolved = true;
                }

                if (!hitAnimationPlayed && ShouldPlayHitAnimation() && elapsed >= hitAnimationDelay)
                {
                    PlayHitAnimation();
                    hitAnimationPlayed = true;
                }

                yield return null;
            }
        }

        private void GetHitDelays(out float damagePopupDelay, out float hitAnimationDelay)
        {
            UnitVisualProfile actorProfile = _actor?.GetComponent<UnitVisualProfile>();
            bool isArcher = actorProfile?.HoldArrow != null;

            if (isArcher)
            {
                damagePopupDelay = actorProfile?.ArrowDamagePopupDelay ?? 0f;
                hitAnimationDelay = 0f;
                return;
            }

            UnitVisualProfile targetProfile = _target?.GetComponent<UnitVisualProfile>();
            damagePopupDelay = targetProfile?.HitDamagePopupDelay ?? 0f;
            hitAnimationDelay = targetProfile?.HitAnimationDelay ?? 0f;
        }

        private bool ShouldPlayHitAnimation()
        {
            return _target != null && !string.IsNullOrEmpty(_targetAnimTrigger);
        }

        private void ResolveDamageAndPresentation()
        {
            BattleHitResult result = _onHit?.Invoke();
            if (result == null || ShouldSkipHitPresentation(result))
            {
                return;
            }

            _visual?.PlayHitEffect(_target, result.SkillIndex);
            _visual?.ShowDamagePopup(result);
        }

        private bool ShouldSkipHitPresentation(BattleHitResult result)
        {
            return _target != null
                   && _target.IsDead
                   && result.Damage <= 0f
                   && !result.IsHeal;
        }

        private void PlayHitAnimation()
        {
            // 피해 확정이 먼저 실행되어 Die가 Dead를 재생했다면 피격 애니메이션으로 덮어쓰지 않습니다.
            if (_target == null || _target.IsDead)
            {
                return;
            }

            _target.EnsureAnimationController();
            _target.Anim?.SetAnimationSpeed(_battleSpeed);
            _target.Anim?.PlayGenericAnimation(_targetAnimTrigger);
        }
    }
}