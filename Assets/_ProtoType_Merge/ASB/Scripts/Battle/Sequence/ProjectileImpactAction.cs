using System;
using System.Collections;
using ASB.Work.Battle.Core;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 원거리 투사체 비행을 ASB가 전적으로 소유한다(JC.VFX 의존 없음).
    /// 순수 아트 프리팹을 Instantiate해 IProjectileTrajectory를 따라 매 배속-프레임 이동시키고,
    /// 공유 HitDeliveryGate로 전달 결과(Arrived/Fallback/Cancelled)를 보고한다.
    /// 피해·상태이상·반격은 기존 전투 루틴에 그대로 있고, 이 액션은 "언제 실행되는가"만 게이팅한다.
    /// </summary>
    public sealed class ProjectileImpactAction : BattleSequenceAction
    {
        private readonly BattleCharactor _actor;
        private readonly BattleCharactor _target;
        private readonly ProjectileVisualData _visual;
        private readonly float _battleSpeed;
        private readonly HitDeliveryGate _deliveryGate;
        private readonly int _skillIndex;
        private readonly Vector3? _originOverride;
        private readonly Vector3? _destinationOverride;
        private readonly GameObject _existingInstance;

        public ProjectileDeliveryResult DeliveryResult { get; private set; } = ProjectileDeliveryResult.Arrived;
        public bool WasCancelled => DeliveryResult == ProjectileDeliveryResult.Cancelled;
        public Vector3 ImpactPoint { get; private set; }

        public ProjectileImpactAction(BattleCharactor actor, BattleCharactor target,
            ProjectileVisualData visual, float battleSpeed, HitDeliveryGate deliveryGate, int skillIndex,
            Vector3? originOverride = null, Vector3? destinationOverride = null, GameObject existingInstance = null)
        {
            _actor = actor;
            _target = target;
            _visual = visual;
            _battleSpeed = Mathf.Max(0.01f, battleSpeed);
            _deliveryGate = deliveryGate;
            _skillIndex = skillIndex;
            _originOverride = originOverride;
            _destinationOverride = destinationOverride;
            _existingInstance = existingInstance;
        }

        public override IEnumerator ExecuteRoutine(MonoBehaviour host)
        {
            // 진형 중심(destinationOverride) 발사는 특정 타깃의 생사와 무관하게 진행한다(낙하형 전체 공격).
            bool centerMode = _destinationOverride.HasValue;

            // 캐스터 무효 → 취소. 개별 타깃 발사는 타깃 생사도 확인(중심 발사는 제외).
            if (_actor == null || _actor.IsDead || (!centerMode && (_target == null || _target.IsDead)))
            {
                Complete(ProjectileDeliveryResult.Cancelled);
                yield break;
            }

            // 프리팹 미설정 → 기존 즉시 히트 흐름으로 폴백(히트를 잃지 않는다).
            if (_visual == null || _visual.Prefab == null)
            {
                Complete(ProjectileDeliveryResult.Fallback);
                yield break;
            }

            if (host == null || !host.gameObject.scene.IsValid())
            {
                Complete(ProjectileDeliveryResult.Cancelled);
                yield break;
            }

            GameObject instance;
            if (_existingInstance != null)
            {
                // 준비(차징) 단계에서 만든 인스턴스를 재사용 → 차징 연출이 끊기지 않는다. 새로 생성하지 않는다.
                instance = _existingInstance;
            }
            else
            {
                try
                {
                    instance = UnityEngine.Object.Instantiate(_visual.Prefab);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Projectile] skill={_skillIndex} instantiate 실패 → 즉시 히트 폴백. {exception.Message}");
                    Complete(ProjectileDeliveryResult.Fallback);
                    yield break;
                }
            }

            if (instance == null)
            {
                Complete(ProjectileDeliveryResult.Fallback);
                yield break;
            }

            // 체인 2차 등은 원점을 명시적으로 오버라이드(1차 도착 좌표). 없으면 기존 소켓 원점 사용.
            Vector3 start;
            if (_originOverride.HasValue)
            {
                start = _originOverride.Value;
            }
            else if (_existingInstance != null)
            {
                // 재사용(차징) 인스턴스는 차징하던 현재 위치에서 발사(원점으로 순간이동 방지).
                start = _existingInstance.transform.position;
            }
            else
            {
                Transform origin = ResolveOrigin(_actor);
                start = origin != null ? origin.position : _actor.transform.position;
            }
            Vector3 destination = _destinationOverride ?? (_target != null ? _target.transform.position : start);

            Transform tr = instance.transform;
            if (_existingInstance != null)
            {
                // Prepared charge was following the cast socket. Detach it at the hit event, preserving its world position.
                tr.SetParent(null, true);
            }
            tr.position = start;

            // 연출(트레일/파티클)은 여기서 관리한다 → 훅 컴포넌트가 없어도 순수 아트 프리팹이 동작한다.
            TrailRenderer[] trails = instance.GetComponentsInChildren<TrailRenderer>(true);
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            BattleProjectileVisual hook = instance.GetComponent<BattleProjectileVisual>();

            BeginCosmetics(trails, particles);
            hook?.OnLaunched();

            IProjectileTrajectory trajectory = ProjectileTrajectoryFactory.Create(_visual.Trajectory);
            trajectory.Init(start, destination, _visual);

            Transform tracked = (!centerMode && _visual.TrackTarget && _target != null) ? _target.transform : null;

            float distance = Vector3.Distance(start, destination);
            float expectedFlightSeconds = distance / Mathf.Max(0.01f, _visual.Speed);
            float grace = Mathf.Max(0.1f, _visual.TimeoutGraceSeconds);
            float safetyTimeoutSeconds = expectedFlightSeconds + grace;
            float elapsed = 0f;

            Vector3 lastPos = start;
            bool arrived = false;
            while (!arrived)
            {
                // 비행 중 캐스터/씬 무효 → 취소, 안전 정리. (중심 발사는 개별 타깃 생사 무시)
                if (_actor == null || _actor.IsDead || (!centerMode && (_target == null || _target.IsDead))
                    || instance == null || !instance.scene.IsValid())
                {
                    Cancel(instance, trails, particles);
                    yield break;
                }

                float dt = Time.deltaTime * _battleSpeed;
                elapsed += dt;

                arrived = trajectory.Step(dt, tracked, out Vector3 pos);

                if (_visual.AlignToVelocity)
                {
                    Vector3 dir = pos - lastPos;
                    if (dir.sqrMagnitude > 1e-6f)
                    {
                        tr.rotation = Quaternion.LookRotation(dir);
                    }
                }

                tr.position = pos;
                lastPos = pos;

                if (arrived)
                {
                    ImpactPoint = pos;
                    break;
                }

                // 타임아웃은 "피해 타이밍"이 아니라 데드락 방지 전용.
                if (elapsed >= safetyTimeoutSeconds)
                {
                    Debug.LogWarning(
                        $"[Projectile] skill={_skillIndex} 타임아웃. distance={distance:F2}, speed={_visual.Speed:F2}, " +
                        $"expected={expectedFlightSeconds:F2}, grace={grace:F2}, timeout={safetyTimeoutSeconds:F2}");
                    Cancel(instance, trails, particles);
                    yield break;
                }

                yield return null;
            }

            // 도착: 연출 훅 발화 → 전투는 즉시 진행(도착 프레임에 피해). 비주얼은 잔상 후 자체 소멸.
            hook?.OnImpact(ImpactPoint);
            EndTrails(trails);
            host.StartCoroutine(DestroyAfter(instance, Mathf.Max(0f, _visual.ImpactVisualLifetime), _battleSpeed));

            Complete(ProjectileDeliveryResult.Arrived);
        }

        /// <summary>도착 후 잔상(폭발/페이드/트레일)을 배속-시간 동안 보여준 뒤 정리한다.</summary>
        private static IEnumerator DestroyAfter(GameObject instance, float seconds, float battleSpeed)
        {
            float scaled = Mathf.Max(0.01f, battleSpeed);
            float t = 0f;
            while (instance != null && t < seconds)
            {
                t += Time.deltaTime * scaled;
                yield return null;
            }

            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance);
            }
        }

        private void Cancel(GameObject instance, TrailRenderer[] trails, ParticleSystem[] particles)
        {
            StopCosmetics(trails, particles);
            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance);
            }
            Complete(ProjectileDeliveryResult.Cancelled);
        }

        private void Complete(ProjectileDeliveryResult result)
        {
            DeliveryResult = result;
            _deliveryGate?.SetResult(result);
        }

        private void BeginCosmetics(TrailRenderer[] trails, ParticleSystem[] particles)
        {
            if (trails != null)
            {
                foreach (TrailRenderer trail in trails)
                {
                    if (trail == null)
                    {
                        continue;
                    }
                    if (_visual.TrailTime > 0f)
                    {
                        trail.time = _visual.TrailTime;
                    }
                    trail.Clear();
                    trail.emitting = true;
                }
            }

            if (particles != null)
            {
                foreach (ParticleSystem ps in particles)
                {
                    if (ps == null)
                    {
                        continue;
                    }
                    ps.Clear();
                    ps.Play();
                }
            }
        }

        private static void EndTrails(TrailRenderer[] trails)
        {
            if (trails == null)
            {
                return;
            }
            foreach (TrailRenderer trail in trails)
            {
                if (trail != null)
                {
                    trail.emitting = false;
                }
            }
        }

        private static void StopCosmetics(TrailRenderer[] trails, ParticleSystem[] particles)
        {
            if (trails != null)
            {
                foreach (TrailRenderer trail in trails)
                {
                    if (trail == null)
                    {
                        continue;
                    }
                    trail.emitting = false;
                    trail.Clear();
                }
            }

            if (particles != null)
            {
                foreach (ParticleSystem ps in particles)
                {
                    if (ps != null)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
        }

        private static Transform ResolveOrigin(BattleCharactor actor)
        {
            UnitVisualProfile profile = actor != null ? actor.GetComponent<UnitVisualProfile>() : null;
            if (profile != null && profile.AttackEffectSocket != null)
            {
                return profile.AttackEffectSocket;
            }

            UnitSocketHolder holder = actor != null ? actor.GetComponentInChildren<UnitSocketHolder>() : null;
            // 장착 무기의 Muzzle 우선, 없으면 기존처럼 왼손 마운트로 폴백.
            Transform origin = holder?.ResolveWeaponOrigin(WeaponSocket.Muzzle);
            return origin != null ? origin : actor != null ? actor.transform : null;
        }
    }
}
