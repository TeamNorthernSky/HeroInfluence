using System;
using System.Collections;
using ASB.Work.Battle.Core;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 범용 투사체 연출: 빈 컨테이너(ProjectileController)를 발사 소켓에서 피격 소켓으로 날리고,
    /// 도착한 순간 onHit 콜백(BattleManager.CommitDamage 경유)을 호출합니다.
    /// SkillPresentationData.ProjectilePrefab이 있는 스킬에서 ResolveHitAction 대신 사용됩니다.
    /// </summary>
    public class SpawnProjectileAction : BattleSequenceAction
    {
        private readonly BattleCharactor _actor;
        private readonly BattleCharactor _target;
        private readonly int _skillIndex;
        private readonly Func<BattleHitResult> _onHit;
        private readonly string _targetAnimTrigger;
        private readonly float _battleSpeed;
        private readonly BattleVisualDirector _visual;
        private readonly SkillPresentationData _presentation;

        public SpawnProjectileAction(
            BattleCharactor actor,
            BattleCharactor target,
            int skillIndex,
            Func<BattleHitResult> onHit,
            string targetAnimTrigger,
            float battleSpeed,
            BattleVisualDirector visual,
            SkillPresentationData presentation)
        {
            _actor = actor;
            _target = target;
            _skillIndex = skillIndex;
            _onHit = onHit;
            _targetAnimTrigger = targetAnimTrigger;
            _battleSpeed = Mathf.Max(0.01f, battleSpeed);
            _visual = visual;
            _presentation = presentation;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            if (_target == null || _presentation == null || _presentation.ProjectilePrefab == null)
            {
                ResolveHit();
                yield break;
            }

            Transform fireSocket = _actor?.GetComponent<UnitVisualProfile>()?.AttackEffectSocket ?? _actor?.transform;
            Transform hitSocket = _target.GetComponent<UnitVisualProfile>()?.HitEffectSocket ?? _target.transform;
            if (fireSocket == null || hitSocket == null)
            {
                ResolveHit();
                yield break;
            }

            var containerGO = new GameObject($"Projectile_{_skillIndex}");
            ProjectileController controller = containerGO.AddComponent<ProjectileController>();
            UnityEngine.Object.Instantiate(_presentation.ProjectilePrefab, containerGO.transform);

            yield return host.StartCoroutine(controller.Fly(
                fireSocket.position,
                hitSocket.position,
                _presentation.FlightTime,
                _presentation.TrajectoryType,
                _presentation.ArcHeight,
                _battleSpeed));

            UnityEngine.Object.Destroy(containerGO);

            ResolveHit();
        }

        private void ResolveHit()
        {
            BattleHitResult result = _onHit?.Invoke();
            if (result == null || ShouldSkipHitPresentation(result))
            {
                return;
            }

            _visual?.PlayHitEffect(_target, result.SkillIndex);
            _visual?.PlayHitSfx(_target, result.SkillIndex);
            _visual?.ShowDamagePopup(result);
            PlayHitAnimation();
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
            if (_target == null || string.IsNullOrEmpty(_targetAnimTrigger))
            {
                return;
            }

            _target.EnsureAnimationController();
            _target.Anim?.SetAnimationSpeed(_battleSpeed);
            _target.Anim?.PlayGenericAnimation(_targetAnimTrigger);
        }
    }
}
