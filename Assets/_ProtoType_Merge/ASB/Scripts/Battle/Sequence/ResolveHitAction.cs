using System;
using System.Collections;
using ASB.Work.Battle.Core;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 히트 콜백 실행 → 결과(BattleHitResult) 기반 이펙트·팝업 연출 → 타겟 피격 애니메이션.
    /// ApplyDamageAction을 대체합니다.
    /// </summary>
    public class ResolveHitAction : BattleSequenceAction
    {
        private readonly BattleCharactor _target;
        private readonly Func<BattleHitResult> _onHit;
        private readonly string _targetAnimTrigger;
        private readonly float _battleSpeed;
        private readonly BattleVisualDirector _visual;

        /// <param name="targetAnimTrigger">타겟에 재생할 애니메이션 트리거. null이면 피격 애니 생략.</param>
        public ResolveHitAction(
            BattleCharactor target,
            Func<BattleHitResult> onHit,
            string targetAnimTrigger,
            float battleSpeed,
            BattleVisualDirector visual = null)
        {
            _target = target;
            _onHit = onHit;
            _targetAnimTrigger = targetAnimTrigger;
            _battleSpeed = battleSpeed;
            _visual = visual;
        }

        public override IEnumerator ExecuteRoutine()
        {
            BattleHitResult result = _onHit?.Invoke();

            if (result != null)
            {
                _visual?.PlayHitEffect(_target, result.SkillIndex);
                _visual?.ShowDamagePopup(result);
            }

            if (_target == null || string.IsNullOrEmpty(_targetAnimTrigger))
            {
                yield break;
            }

            _target.EnsureAnimationController();
            _target.Anim?.SetAnimationSpeed(_battleSpeed);
            _target.Anim?.PlayGenericAnimation(_targetAnimTrigger);
        }
    }
}
