using System;
using System.Collections;
using System.Collections.Generic;
using ASB.Work.Battle.Core;
using UnityEngine;

namespace ASB.Work.Battle.Sequence
{
    /// <summary>
    /// 원거리 투사체 비행을 ASB가 전적으로 소유하고, JC 구체의 표시·도착 버스트를 연결한다.
    /// 순수 아트 프리팹을 Instantiate해 IProjectileTrajectory를 따라 매 배속-프레임 이동시키고,
    /// 공유 HitDeliveryGate로 전달 결과(Arrived/Fallback/Cancelled)를 보고한다.
    /// 피해·상태이상·반격은 기존 전투 루틴에 그대로 있고, 이 액션은 "언제 실행되는가"만 게이팅한다.
    /// </summary>
    public sealed class ProjectileImpactAction : BattleSequenceAction
    {
        public static event Action<BattleCharactor, Transform> FlightStarted;
        public static event Action<BattleCharactor, Transform, Vector3, bool> FlightFinished;
        private Transform cameraFlight;
        private readonly BattleCharactor _actor;
        private readonly BattleCharactor _target;
        private readonly ProjectileVisualData _visual;
        private readonly float _battleSpeed;
        private readonly HitDeliveryGate _deliveryGate;
        private readonly int _skillIndex;
        private readonly Vector3? _originOverride;
        private readonly Vector3? _destinationOverride;
        private readonly GameObject _existingInstance;
        // 동시 발사(ExecuteParallel)가 정한 공통 도착 시간. 0이면 거리/속도 기준.
        private float _arrivalSecondsOverride;

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

        /// <summary>
        /// 한 번의 시전에 대상별 구체를 같은 프레임에 발사하고 모두 도착할 때까지 기다린다.
        /// 모든 구체는 같은 순간 착탄한다: 지정값(SimultaneousArrivalSeconds)이 없으면 가장 먼 대상의 비행 시간을 쓴다.
        /// </summary>
        public static IEnumerator ExecuteParallel(
            MonoBehaviour host, BattleCharactor actor, IReadOnlyList<BattleCharactor> targets,
            ProjectileVisualData visual, float battleSpeed, HitDeliveryGate deliveryGate,
            int skillIndex, GameObject preparedPrimary = null)
        {
            if (host == null || targets == null)
                yield break;

            var projectiles = new List<ProjectileImpactAction>();
            var seen = new HashSet<BattleCharactor>();
            for (int i = 0; i < targets.Count; i++)
            {
                BattleCharactor target = targets[i];
                if (target == null || target.IsDead || !seen.Add(target))
                    continue;

                bool primary = projectiles.Count == 0;
                projectiles.Add(new ProjectileImpactAction(
                    actor, target, visual, battleSpeed, primary ? deliveryGate : null, skillIndex,
                    existingInstance: primary ? preparedPrimary : null));
            }

            // 발사와 같은 프레임에 계산하므로 각 구체의 출발점·도착점이 실제 비행과 일치한다.
            float arrivalSeconds = visual != null ? visual.SimultaneousArrivalSeconds : 0f;
            if (arrivalSeconds <= 0f)
            {
                for (int i = 0; i < projectiles.Count; i++)
                    arrivalSeconds = Mathf.Max(arrivalSeconds, projectiles[i].EstimateFlightSeconds());
            }

            var flights = new List<Coroutine>(projectiles.Count);
            for (int i = 0; i < projectiles.Count; i++)
            {
                projectiles[i]._arrivalSecondsOverride = arrivalSeconds;
                flights.Add(host.StartCoroutine(projectiles[i].ExecuteRoutine(host)));
            }

            for (int i = 0; i < flights.Count; i++)
                yield return flights[i];
        }

        /// <summary>지금 발사하면 걸릴 비행 시간(가속 보정 포함, 배속 반영 전). 발사 불가면 0.</summary>
        private float EstimateFlightSeconds()
        {
            if (_actor == null || _visual == null || _visual.Prefab == null
                || (!_destinationOverride.HasValue && (_target == null || _target.IsDead)))
                return 0f;

            Vector3 start = ResolveStart();
            return FlightSecondsFor(start, ResolveDestination(start), out _);
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

            ChargeHoldEffect chargeVisual = instance.GetComponent<ChargeHoldEffect>();
            chargeVisual?.BeginFlight(_battleSpeed);
            Vector3 start = ResolveStart();
            Vector3 destination = ResolveDestination(start);

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

            // JC 구체는 Awake에서 숨겨지므로 표시만 켜고 궤적 이동은 이 액션에 맡깁니다.
            JC.VFX.ProjectileVfx orbVisual = instance.GetComponent<JC.VFX.ProjectileVfx>();
            orbVisual?.Show(start);
            BeginCosmetics(trails, particles);
            hook?.OnLaunched();
            cameraFlight = tr;
            FlightStarted?.Invoke(_actor, tr);

            IProjectileTrajectory trajectory = ProjectileTrajectoryFactory.Create(_visual.Trajectory);
            trajectory.Init(start, destination, _visual);

            Transform tracked = (!centerMode && _visual.TrackTarget && _target != null) ? _target.transform : null;

            float expectedFlightSeconds = FlightSecondsFor(start, destination, out float distance);
            if (_arrivalSecondsOverride > 0f && trajectory is IFixedArrivalTrajectory fixedArrival)
            {
                // 동시 착탄: 시간만 공통값으로 바꾸고 포물선·가속 곡선은 그대로 둔다. 타임아웃도 같은 기준.
                fixedArrival.SetArrivalSeconds(_arrivalSecondsOverride);
                expectedFlightSeconds = _arrivalSecondsOverride;
            }
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
                chargeVisual?.UpdateFlightVisual(pos - lastPos, elapsed / Mathf.Max(.001f, expectedFlightSeconds));
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
            BattleManager.Instance?.VisualDirector?.RegisterProjectileImpact(instance);
            orbVisual?.BurstAt(ImpactPoint);
            chargeVisual?.HideAtImpact();
            if (_visual.HideVisualOnArrival) instance.GetComponent<JC.VFX.JcSimpleVisualPart>()?.Stop();
            hook?.OnImpact(ImpactPoint);
            EndTrails(trails);
            float visualLifetime = Mathf.Max(0f, _visual.ImpactVisualLifetime);
            var volume = instance.GetComponent<JC.VFX.FlareVolumeVisual>();
            if (volume != null && volume.preset != null)
                visualLifetime = Mathf.Max(visualLifetime, volume.preset.trailLifetime);
            host.StartCoroutine(DestroyAfter(instance, visualLifetime, _battleSpeed));

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
            if (cameraFlight != null)
            {
                FlightFinished?.Invoke(_actor, cameraFlight, cameraFlight.position, result == ProjectileDeliveryResult.Arrived);
                cameraFlight = null;
            }
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

        private Vector3 ResolveStart()
        {
            // 체인 2차 등은 원점을 명시적으로 오버라이드(1차 도착 좌표). 없으면 기존 소켓 원점 사용.
            if (_originOverride.HasValue)
                return _originOverride.Value;

            // 재사용(차징) 인스턴스는 차징하던 현재 위치에서 발사(원점으로 순간이동 방지).
            if (_existingInstance != null)
                return _existingInstance.transform.position;

            Transform origin = ResolveOrigin(_actor);
            return origin != null ? origin.position : _actor.transform.position;
        }

        private Vector3 ResolveDestination(Vector3 start)
        {
            return _destinationOverride ?? (_target != null ? _target.transform.position + _visual.TargetOffset : start);
        }

        /// <summary>거리/속도 기준 비행 시간(가속 보정 포함, 배속 반영 전).</summary>
        private float FlightSecondsFor(Vector3 start, Vector3 destination, out float distance)
        {
            bool centerMode = _destinationOverride.HasValue;
            Vector3 timingDestination = _visual.PreserveRootFlightTime && !centerMode
                ? destination - _visual.TargetOffset : destination;
            distance = Vector3.Distance(start, timingDestination);
            float seconds = distance / Mathf.Max(0.01f, _visual.Speed);
            if (_visual.Trajectory != ProjectileTrajectoryType.OverheadDrop)
                seconds *= ProjectileAcceleration.DurationScale(_visual.Acceleration, _visual.AccelerationMultiplier);
            return seconds;
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
