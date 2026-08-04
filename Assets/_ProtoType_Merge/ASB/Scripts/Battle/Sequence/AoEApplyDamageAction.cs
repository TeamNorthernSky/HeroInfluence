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
        private readonly ProjectileVisualData _projectileVisual;

        private readonly HitDeliveryGate _deliveryGate;
        public AoEApplyDamageAction(
            List<DamageContext> contexts,
            List<Func<BattleHitResult>> hitCallbacks,
            int pairCount,
            float battleSpeed,
            BattleVisualDirector visual = null,
            ProjectileVisualData projectileVisual = null,
            HitDeliveryGate deliveryGate = null)
        {
            _contexts = contexts;
            _hitCallbacks = hitCallbacks;
            _pairCount = pairCount;
            _battleSpeed = battleSpeed;
            _visual = visual;
            _projectileVisual = projectileVisual;
            _deliveryGate = deliveryGate ?? new HitDeliveryGate();
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {

            if (_projectileVisual != null && _pairCount > 0)
            {
                BattleCharactor caster = _contexts[0]?.Caster;
                BattleCharactor primaryTarget = _contexts[0]?.Target;
                int skillIndex = _contexts[0] != null ? _contexts[0].SkillIndex : 0;

                // 낙하형 등: 도착점을 대상 진형 중심으로.
                Vector3? destinationOverride = _projectileVisual.TargetFormationCenter ? ComputeFormationCenter() : (Vector3?)null;

                // 준비(차징) 단계에서 만든 인스턴스를 재사용(연출 연속). held 등록을 해제해 이후 Stop되지 않게 한다.
                GameObject prepared = null;
                if (_projectileVisual.UsePreparedCharge && caster != null && !string.IsNullOrEmpty(_projectileVisual.ChargeInstanceKey))
                {
                    PresentationRuntimeContext ctx = caster.GetComponent<PresentationRuntimeContext>();
                    if (ctx != null && ctx.TryGetHandle(_projectileVisual.ChargeInstanceKey, out ISkillEffectHandle handle))
                    {
                        prepared = (handle as Component)?.gameObject;
                        ctx.RemoveHandle(_projectileVisual.ChargeInstanceKey);
                    }
                }

                var projectile = new ProjectileImpactAction(caster, primaryTarget, _projectileVisual, _battleSpeed, _deliveryGate, skillIndex,
                    originOverride: null, destinationOverride: destinationOverride, existingInstance: prepared);
                yield return projectile.ExecuteRoutine(host);
                if (!_deliveryGate.ShouldPlayImpactPresentation)
                {
                    yield break;
                }
            }

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

        /// <summary>대상 진형 중심(현재 대상들 위치 평균). 낙하형 투사체 도착점으로 사용.</summary>
        private Vector3 ComputeFormationCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            if (_contexts != null)
            {
                for (int i = 0; i < _contexts.Count; i++)
                {
                    BattleCharactor target = _contexts[i]?.Target;
                    if (target != null)
                    {
                        sum += target.transform.position;
                        count++;
                    }
                }
            }
            return count > 0 ? sum / count : Vector3.zero;
        }
    }
}
