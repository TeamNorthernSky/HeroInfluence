using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 호 파편 — 궤도를 주행하는 앵커가 접선 방향으로 파편을 튀긴다. ASB 연출 규약 구현체.
    ///
    /// 회전 톱날의 마찰 불꽃과 같은 운동이되 인상은 파편이다:
    ///   앵커가 호 궤도를 돌고, 그 지점의 **접선 방향**이 파편의 진행 방향이 된다.
    ///   파티클 시스템은 앵커의 자식이지만 시뮬레이션은 World —
    ///   방출 지점만 앵커를 따라가고, 튀어나간 파편은 월드에서 독립적으로 날아간다.
    ///
    /// 형상·채색은 JusticeArcShard 셰이더가 전담한다(이등변 삼각형 절단, 내부 흰 심 + 색 테두리).
    /// 절단점 난수는 Custom Vertex Streams(StableRandom)로 셰이더에 넘긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcArcShardEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Tooltip("호를 주행하는 앵커. 파티클이 이 아래에 있다. 비우면 첫 자식.")]
        [SerializeField] private Transform anchor;

        [Header("궤도 (xz 평면 · 시계 각도: 타깃 방향=12시)")]
        [Tooltip("호 시작각(도). 210 = 7시.")]
        [SerializeField] private float angleStart = 210f;
        [Tooltip("호 끝각(도). 끝 < 시작 = 반시계, 끝 > 시작 = 시계, 차이 360 초과 = 한 바퀴 이상.")]
        [SerializeField] private float angleEnd = 30f;
        [Tooltip("이 효과가 배치되거나 회전하는 반경(m)입니다. 높일수록 중심에서 멀어집니다.")]
        [SerializeField, Min(0.05f)] private float radius = 1.6f;
        [Tooltip("호를 다 긋는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.02f)] private float sweepDuration = 0.5f;
        [Tooltip("주행 가속 곡선. 1=등속, 클수록 초반이 빠르고 끝에서 감속(촤악), 1 미만은 반대(끝에서 가속). ※방출 밀도(거리 기준)와 아래 점진·감소·수축 구간(호 위 위치 기준)은 이 값과 무관하게 형상이 유지된다.")]
        [SerializeField, Range(0.3f, 4f)] private float easeOut = 1f;

        [Header("배치 · 정리")]
        [Tooltip("스폰 지점(소켓) 기준 오프셋(m). 소켓 스케일은 무시된다.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);
        [Tooltip("주행 종료 후 잔광 여유(초).")]
        [SerializeField, Min(0f)] private float extraLinger = 0.5f;
        [Tooltip("방출 종료 후 정리할 때 사용하는 최대 입자 수명(초)입니다. 실제 입자 수명보다 짧으면 잔상이 일찍 잘릴 수 있습니다.")]
        [SerializeField, Min(0f)] private float lifeMaxForCleanup = 0.7f;

        [Tooltip("생성·재생·종료 과정을 Console에 기록합니다. 효과 외형에는 영향을 주지 않습니다.")]
        [SerializeField] private bool logLifecycle;

        /// <summary>서브스텝당 목표 이동 거리(m) — 방출 지점이 프레임당 크게 튀는 것을 막는다.</summary>
        const float SubStepTargetMeters = 0.15f;

        private ParticleSystem[] systems;
        private float elapsed;
        private bool running;
        private bool simulating;

        // 프리셋 바인더가 밀어넣는 값
        public float AngleStart { get => angleStart; set => angleStart = value; }
        public float AngleEnd { get => angleEnd; set => angleEnd = value; }
        public float Radius { get => radius; set => radius = Mathf.Max(0.05f, value); }
        public float SweepDuration { get => sweepDuration; set => sweepDuration = Mathf.Max(0.02f, value); }
        public float EaseOut { get => easeOut; set => easeOut = Mathf.Clamp(value, 0.3f, 4f); }
        public Vector3 SpawnOffset { get => spawnOffset; set => spawnOffset = value; }
        public float ExtraLinger { get => extraLinger; set => extraLinger = Mathf.Max(0f, value); }
        public float LifeMaxForCleanup { get => lifeMaxForCleanup; set => lifeMaxForCleanup = Mathf.Max(0f, value); }

        private void Awake() => EnsureRefs();

        private void EnsureRefs()
        {
            if (anchor == null && transform.childCount > 0) anchor = transform.GetChild(0);
            if (systems == null || systems.Length == 0) systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        /// <summary>시간 → 궤도 진행도. easeOut 1 = 등속, 클수록 초반이 빠르고 말단 감속.</summary>
        private float EasedProgress(float t)
            => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), easeOut);

        public void Play(SkillEffectContext ctx)
        {
            EnsureRefs();

            // 소켓 자식이 되지 않는다(스케일 오염 차단) — 호 획·포인트 획과 같은 규칙.
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            if (ctx != null)
            {
                Transform socket = ctx.SocketTransform;
                transform.position = (socket != null ? socket.position : ctx.SpawnPosition) + spawnOffset;
                transform.rotation = Quaternion.LookRotation(ResolveForward(ctx), Vector3.up);
            }

            elapsed = 0f;
            running = true;
            simulating = true;

            MoveAnchor(0f);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                systems[i].Clear(true);
                var em = systems[i].emission;
                em.enabled = true;
                systems[i].Simulate(0f, false, true, false);
            }

            Destroy(gameObject, sweepDuration + lifeMaxForCleanup + extraLinger);

            if (logLifecycle)
                Debug.Log("[JcArcShard] Play  origin=" + transform.position.ToString("F2"), this);
        }

        /// <summary>전진 기준(시계 12시) = 시전자→타깃 수평. 없으면 시전자 정면.</summary>
        private static Vector3 ResolveForward(SkillEffectContext ctx)
        {
            Vector3 dir = Vector3.forward;
            if (ctx != null)
            {
                Transform target = ctx.PrimaryTarget != null ? ctx.PrimaryTarget.transform : null;
                if (target != null && ctx.Caster != null) dir = target.position - ctx.Caster.transform.position;
                else if (ctx.Caster != null) dir = ctx.Caster.transform.forward;
            }
            dir.y = 0f;
            return dir.sqrMagnitude < 1e-6f ? Vector3.forward : dir.normalized;
        }

        private void Update()
        {
            if (!simulating) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            int steps = 1;
            if (running)
            {
                float span = Mathf.Abs(angleEnd - angleStart) * Mathf.Deg2Rad * radius;
                float t0 = Mathf.Clamp01(elapsed / sweepDuration);
                float t1 = Mathf.Clamp01((elapsed + dt) / sweepDuration);
                float frameMove = span * Mathf.Abs(EasedProgress(t1) - EasedProgress(t0));
                steps = Mathf.Clamp(Mathf.CeilToInt(frameMove / SubStepTargetMeters), 1, 16);
            }

            float sub = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                if (running)
                {
                    elapsed += sub;
                    float t = Mathf.Clamp01(elapsed / sweepDuration);
                    MoveAnchor(EasedProgress(t));

                    if (t >= 1f)
                    {
                        running = false;
                        for (int i = 0; i < systems.Length; i++)
                        {
                            if (systems[i] == null) continue;
                            var em = systems[i].emission;
                            em.enabled = false;   // 방출만 멈춘다 — 이미 튄 파편은 계속 날아간다
                        }
                        if (logLifecycle) Debug.Log("[JcArcShard] 주행 종료 — 잔여 파편 비행", this);
                    }
                }

                for (int i = 0; i < systems.Length; i++)
                    if (systems[i] != null) systems[i].Simulate(sub, false, false, false);
            }
        }

        /// <summary>
        /// 진행도(0~1) → 궤도 위 지점 + 자세.
        /// ★앵커의 정면(+Z)을 그 지점의 **접선** 방향으로 맞춘다 —
        /// 파티클 shape가 앵커 로컬 +Z로 쏘므로, 파편이 접선을 따라 튄다.
        /// </summary>
        private void MoveAnchor(float progress)
        {
            EnsureRefs();
            if (anchor == null) return;

            float clockDeg = Mathf.LerpUnclamped(angleStart, angleEnd, progress);
            float rad = clockDeg * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Cos(rad);

            anchor.localPosition = new Vector3(sin * radius, 0f, cos * radius);

            // 위치 (sinθ, 0, cosθ)를 θ로 미분 → 접선 (cosθ, 0, −sinθ).
            // 각도가 감소하는 주행(반시계)이면 접선도 뒤집는다.
            float sign = (angleEnd >= angleStart) ? 1f : -1f;
            Vector3 tangent = new Vector3(cos, 0f, -sin) * sign;
            if (tangent.sqrMagnitude > 1e-6f)
                anchor.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
        }
    }
}
