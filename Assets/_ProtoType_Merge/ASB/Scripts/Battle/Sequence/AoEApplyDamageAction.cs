using System;
using System.Collections;
using System.Collections.Generic;
using ASB.Work.Battle.Core;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 광역 스킬 전용: 전 타겟에 동시 데미지 + 피격 애니메이션 + 연출.
    /// </summary>
    public class AoEApplyDamageAction : BattleSequenceAction
    {
        private readonly List<DamageContext> _contexts;
        private readonly List<Func<BattleHitResult>> _hitCallbacks;
        private readonly int _pairCount;
        private readonly float _battleSpeed;
        private readonly BattleVisualDirector _visual;

        public AoEApplyDamageAction(
            List<DamageContext> contexts,
            List<Func<BattleHitResult>> hitCallbacks,
            int pairCount,
            float battleSpeed,
            BattleVisualDirector visual = null)
        {
            _contexts = contexts;
            _hitCallbacks = hitCallbacks;
            _pairCount = pairCount;
            _battleSpeed = battleSpeed;
            _visual = visual;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            for (int i = 0; i < _pairCount; i++)
            {
                BattleHitResult result = _hitCallbacks[i]?.Invoke();

                BattleCharactor hitTarget = _contexts[i]?.Target;
                if (hitTarget == null)
                {
                    continue;
                }

                if (result != null)
                {
                    _visual?.PlayHitEffect(hitTarget, result.SkillIndex);
                    _visual?.ShowDamagePopup(result);
                }

                hitTarget.EnsureAnimationController();
                hitTarget.Anim?.SetAnimationSpeed(_battleSpeed);
                hitTarget.Anim?.PlayGenericAnimation("Hit");
            }

            yield break;
        }
    }
}
