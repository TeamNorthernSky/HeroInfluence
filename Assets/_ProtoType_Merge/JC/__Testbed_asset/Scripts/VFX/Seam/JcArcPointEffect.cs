using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 호 포인트 획 — 본 호 획(JcArcStrokeEffect) 위에 겹치는 액센트 대시. ASB 연출 규약 구현체.
    ///
    /// 구 하이라이트(코어)의 후계. 대시 1개 = 입자 1개:
    ///   입자는 헤드(앵커)에서 바깥 오프셋 δ ∈ [Offset Min, Max]를 갖고 태어나
    ///   자기 수명 진행 q에 따라 δ × (1−q)^k 로 본 궤도에 붙는다(수렴 재배치 패스).
    ///   수명이 곧 대시 길이 — 빈도(Rate)·길이(수명)·상한(Max Particles)의 방출 문법.
    ///
    /// ★수명 압축 없음 + 오버런(260729 확정): 어떤 대시도 수명이 조여지지 않는다.
    ///   급꺾임의 원인이 「시간 기반 수렴 vs 감속하는 헤드」의 속도비 붕괴였으므로,
    ///   수명이 남은 대시가 있으면 앵커가 Angle End를 지나 등속(호 평균 속도)으로
    ///   계속 주행하며 남은 호선을 마저 그린다.
    ///   Skip Short Remainder ON  = 스윕을 넘길 대시는 애초에 안 태어남 → 연장 없이 스윕과 함께 종료.
    ///   Skip Short Remainder OFF = 말미 대시도 전부 태어나고 스윕 종료 후 연장 주행으로 완성.
    ///   (토글은 어느 쪽이든 「붓 생성」만 제어한다 — 그려지는 중인 호선은 건드리지 않는다.)
    ///   강제 수축(Thin Decay)은 포인트에서 폐지 — 재조준이 수렴 q를 점프시켜 위치 팝을 만들었고,
    ///   오버런 체계에서는 존재 이유(스윕 종료 강제 마무리)도 소멸했다.
    ///
    /// 본 호와 같은 기술: 서브스텝 실이동량 시뮬레이션 / 공전 상자 노즐 / Ease Out / 소켓 비부모.
    /// ★의도적으로 별도 객체·별도 코드 — 동결된 본 획 코드를 열지 않기 위한 격리.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcArcPointEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Tooltip("호를 주행하는 앵커. 파티클이 이 아래에 있다. 비우면 첫 자식.")]
        [SerializeField] private Transform anchor;

        [Header("궤도 (xz 평면 · 시계 각도: 타깃 방향=12시 — 본 호와 동일 문법)")]
        [SerializeField] private float angleStart = 210f;
        [SerializeField] private float angleEnd = 30f;
        [SerializeField, Min(0.05f)] private float radius = 1.6f;
        [SerializeField, Min(0.02f)] private float sweepDuration = 0.5f;
        [SerializeField, Range(0.3f, 4f)] private float easeOut = 1f;

        [Header("수렴 (탄생 시 바깥 오프셋 → 수명 진행에 따라 본 궤도 합류)")]
        [SerializeField, Min(0f)] private float offsetMin = 0.3f;
        [SerializeField, Min(0f)] private float offsetMax = 0.9f;
        [SerializeField, Range(0.5f, 4f)] private float convergeExp = 1.5f;

        [Header("배치 · 정리")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);
        [SerializeField, Min(0f)] private float extraLinger = 0.2f;

        [Header("밀도 (호 위 위치 기준)")]
        [SerializeField, Range(0f, 0.5f)] private float emissionRamp = 0f;
        [SerializeField, Range(0f, 0.5f)] private float emissionDecay = 0f;
        [SerializeField, Min(0f)] private float baseRateOverDistance = 1.5f;

        [Header("대시 일생 (수명 = 대시 길이 — 압축 없음)")]
        [SerializeField, Min(0.02f)] private float strokeLifeMin = 0.15f;
        [SerializeField, Min(0.05f)] private float strokeLifeMax = 0.35f;
        [Tooltip("켜면: 스윕을 넘길 대시는 애초에 태어나지 않는다 → 연장 없이 스윕과 함께 종료.\n" +
                 "끄면: 말미 대시도 전부 태어나고, 스윕 종료 후 앵커가 등속으로 연장 주행하며 마저 그린다.")]
        [SerializeField] private bool skipShortRemainder = true;

        [SerializeField] private bool logLifecycle;

        const float SubStepTargetMeters = 0.15f;

        private ParticleSystem[] strokes;
        private float elapsed;
        private bool simulating;
        private bool emissionCut;

        // 프리셋 바인더가 밀어넣는 값
        public float AngleStart { get => angleStart; set => angleStart = value; }
        public float AngleEnd { get => angleEnd; set => angleEnd = value; }
        public float Radius { get => radius; set => radius = Mathf.Max(0.05f, value); }
        public float SweepDuration { get => sweepDuration; set => sweepDuration = Mathf.Max(0.02f, value); }
        public float EaseOut { get => easeOut; set => easeOut = Mathf.Clamp(value, 0.3f, 4f); }
        public float OffsetMin { get => offsetMin; set => offsetMin = Mathf.Max(0f, value); }
        public float OffsetMax { get => offsetMax; set => offsetMax = Mathf.Max(0f, value); }
        public float ConvergeExp { get => convergeExp; set => convergeExp = Mathf.Clamp(value, 0.5f, 4f); }
        public Vector3 SpawnOffset { get => spawnOffset; set => spawnOffset = value; }
        public float ExtraLinger { get => extraLinger; set => extraLinger = Mathf.Max(0f, value); }
        public float EmissionRamp { get => emissionRamp; set => emissionRamp = Mathf.Clamp(value, 0f, 0.5f); }
        public float EmissionDecay { get => emissionDecay; set => emissionDecay = Mathf.Clamp(value, 0f, 0.5f); }
        public float BaseRateOverDistance { get => baseRateOverDistance; set => baseRateOverDistance = Mathf.Max(0f, value); }
        public float StrokeLifeMin { get => strokeLifeMin; set => strokeLifeMin = Mathf.Max(0.02f, value); }
        public float StrokeLifeMax { get => strokeLifeMax; set => strokeLifeMax = Mathf.Max(0.05f, value); }
        public bool SkipShortRemainder { get => skipShortRemainder; set => skipShortRemainder = value; }

        // 대시별 탄생 오프셋 δ — 처음 관측될 때 현재 반경 성분으로 기록한다.
        private readonly System.Collections.Generic.Dictionary<uint, float> birthOffsets =
            new System.Collections.Generic.Dictionary<uint, float>();
        private ParticleSystem.Particle[] buffer;

        private void Awake() => EnsureRefs();

        private void EnsureRefs()
        {
            if (anchor == null && transform.childCount > 0) anchor = transform.GetChild(0);
            if (strokes == null || strokes.Length == 0) strokes = GetComponentsInChildren<ParticleSystem>(true);
        }

        /// <summary>
        /// 시간 → 궤도 진행도. 스윕 안에서는 Ease Out 곡선, 스윕을 넘으면(오버런)
        /// 등속(호 평균 속도)으로 그대로 연장된다 — LerpUnclamped가 각도를 계속 밀어 준다.
        /// </summary>
        private float OrbitProgress(float tRaw)
            => tRaw <= 1f ? 1f - Mathf.Pow(1f - Mathf.Clamp01(tRaw), easeOut) : 1f + (tRaw - 1f);

        private float EmissionFactor(float p)
        {
            float up = emissionRamp > 0.001f ? Mathf.Clamp01(p / emissionRamp) : 1f;
            float down = emissionDecay > 0.001f ? Mathf.Clamp01((1f - p) / emissionDecay) : 1f;
            return up * down;
        }

        /// <summary>
        /// 선별 모드의 생존 배율 — 「수명이 잔여를 넘는 대시는 안 태어남」을 균등분포 확률로 환산.
        /// 잔여 ≥ Max → 1, 잔여 ≤ Min → 0, 사이는 선형. 붓 생성만 제어하며 그려지는 호선은 건드리지 않는다.
        /// </summary>
        private float SurvivalFraction(float remaining)
        {
            if (remaining >= strokeLifeMax) return 1f;
            if (remaining <= strokeLifeMin) return 0f;
            return (remaining - strokeLifeMin) / Mathf.Max(strokeLifeMax - strokeLifeMin, 1e-4f);
        }

        private void SetEmission(bool on)
        {
            for (int i = 0; i < strokes.Length; i++)
            {
                if (strokes[i] == null) continue;
                var em = strokes[i].emission;
                em.enabled = on;
            }
        }

        public void Play(SkillEffectContext ctx)
        {
            EnsureRefs();

            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            if (ctx != null)
            {
                Transform socket = ctx.SocketTransform;
                transform.position = (socket != null ? socket.position : ctx.SpawnPosition) + spawnOffset;
                transform.rotation = Quaternion.LookRotation(ResolveForward(ctx), Vector3.up);
            }

            elapsed = 0f;
            simulating = true;
            emissionCut = false;

            if (skipShortRemainder && strokeLifeMin >= sweepDuration)
                Debug.LogWarning("[JcArcPoint] Stroke Life Min(" + strokeLifeMin.ToString("F2") +
                                 "s) ≥ Sweep Duration(" + sweepDuration.ToString("F2") +
                                 "s) — 선별 모드에서 방출 창이 0이라 대시가 태어나지 않습니다.", this);

            MoveAnchor(0f);
            birthOffsets.Clear();

            bool shaping = emissionRamp > 0.001f || emissionDecay > 0.001f || skipShortRemainder;
            for (int i = 0; i < strokes.Length; i++)
            {
                if (strokes[i] == null) continue;
                strokes[i].Clear(true);
                var em = strokes[i].emission;
                em.enabled = true;
                if (shaping)
                    em.rateOverDistance = baseRateOverDistance * EmissionFactor(0f)
                        * (skipShortRemainder ? SurvivalFraction(sweepDuration) : 1f);
                strokes[i].Simulate(0f, false, true, false);
            }

            // 오버런 허용(OFF) 시 말미 대시가 수명 Max만큼 스윕을 넘길 수 있다 — 정리 시점도 그만큼 늘린다.
            float overrun = skipShortRemainder ? 0f : strokeLifeMax;
            Destroy(gameObject, sweepDuration + overrun + extraLinger);

            if (logLifecycle)
                Debug.Log("[JcArcPoint] Play  origin=" + transform.position.ToString("F2"), this);
        }

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

            // 실이동량 기준 서브스텝 — 오버런 구간도 앵커가 계속 움직이므로 끝까지 계산한다.
            float span = Mathf.Abs(angleEnd - angleStart) * Mathf.Deg2Rad * (radius + Mathf.Max(offsetMin, offsetMax));
            float sweep = Mathf.Max(sweepDuration, 0.01f);
            float frameMove = span * Mathf.Abs(OrbitProgress((elapsed + dt) / sweep) - OrbitProgress(elapsed / sweep));
            int steps = Mathf.Clamp(Mathf.CeilToInt(frameMove / SubStepTargetMeters), 1, 16);

            float sub = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                elapsed += sub;
                float tRaw = elapsed / sweep;
                float clockDeg = MoveAnchor(OrbitProgress(tRaw));

                if (!emissionCut)
                {
                    if (tRaw >= 1f)
                    {
                        emissionCut = true;
                        SetEmission(false);
                        if (logLifecycle) Debug.Log("[JcArcPoint] 주행 종료 — 잔여 대시 연장 주행", this);
                    }
                    else
                    {
                        float remaining = sweepDuration - elapsed;
                        float survival = skipShortRemainder ? SurvivalFraction(remaining) : 1f;
                        bool shaping = emissionRamp > 0.001f || emissionDecay > 0.001f || skipShortRemainder;
                        if (shaping)
                        {
                            float factor = EmissionFactor(OrbitProgress(tRaw)) * survival;
                            for (int i = 0; i < strokes.Length; i++)
                            {
                                if (strokes[i] == null) continue;
                                var em2 = strokes[i].emission;
                                em2.rateOverDistance = baseRateOverDistance * factor;
                            }
                        }
                    }
                }

                // 수렴 재배치 — 생존 대시의 반경 성분을 δ×(1−q)^k 로 강제한다.
                ApplyConverge(clockDeg);

                for (int i = 0; i < strokes.Length; i++)
                {
                    if (strokes[i] != null) strokes[i].Simulate(sub, false, false, false);
                }
            }
        }

        /// <summary>
        /// ★수렴 재배치 — 입자는 로컬 좌표가 탄생 시 고정이라 스스로 궤도로 붙지 못한다.
        /// 매 서브스텝, 생존 입자의 위치에서 「현재 반경 방향 성분」만 δ×(1−q)^k 로 교체한다
        /// (δ = 처음 관측 시 기록한 탄생 오프셋, q = 수명 진행). 두께·수직 산포는 보존된다.
        /// 수명 압축이 없으므로 q는 늘 자기 시간표대로 흐른다 — 재조준류 개입 없음.
        /// </summary>
        private void ApplyConverge(float clockDeg)
        {
            float rad = clockDeg * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            for (int i = 0; i < strokes.Length; i++)
            {
                var ps = strokes[i];
                if (ps == null) continue;
                int alive = ps.particleCount;
                if (alive == 0) continue;
                if (buffer == null || buffer.Length < alive) buffer = new ParticleSystem.Particle[Mathf.Max(alive, 64)];
                int n = ps.GetParticles(buffer);

                for (int k = 0; k < n; k++)
                {
                    float startLife = Mathf.Max(buffer[k].startLifetime, 0.0001f);
                    float q = Mathf.Clamp01(1f - buffer[k].remainingLifetime / startLife);

                    Vector3 pos = buffer[k].position;
                    float rc = Vector3.Dot(pos, radial);

                    uint seed = buffer[k].randomSeed;
                    float delta;
                    if (!birthOffsets.TryGetValue(seed, out delta))
                    {
                        delta = Mathf.Max(rc, 0f);   // 탄생 직후 첫 관측 — 노즐이 준 바깥 오프셋
                        birthOffsets[seed] = delta;
                    }

                    float desired = delta * Mathf.Pow(1f - q, convergeExp);
                    buffer[k].position = pos - radial * rc + radial * desired;
                }
                ps.SetParticles(buffer, n);
            }
        }

        /// <summary>
        /// 궤도 진행도(오버런 시 1 초과) → 앵커는 궤도 위를 주행한다. 반환값은 현재 시계각.
        /// 노즐 상자는 반경 바깥 대역 [Offset Min, Max]에 배치·공전 —
        /// shape.position은 rotation의 영향을 받지 않으므로 반경 방향 이동을 매번 직접 준다.
        /// </summary>
        private float MoveAnchor(float progress)
        {
            EnsureRefs();

            float clockDeg = Mathf.LerpUnclamped(angleStart, angleEnd, progress);
            if (anchor == null) return clockDeg;

            float rad = clockDeg * Mathf.Deg2Rad;
            float sin = Mathf.Sin(rad);
            float cos = Mathf.Cos(rad);
            anchor.localPosition = new Vector3(sin * radius, 0f, cos * radius);

            float shapeYaw = clockDeg - 90f;
            float centerDist = (Mathf.Min(offsetMin, offsetMax) + Mathf.Max(offsetMin, offsetMax)) * 0.5f;
            Vector3 shapeCenter = new Vector3(sin * centerDist, 0f, cos * centerDist);
            for (int i = 0; i < strokes.Length; i++)
            {
                if (strokes[i] == null) continue;
                var shape = strokes[i].shape;
                if (!shape.enabled) continue;
                shape.rotation = new Vector3(0f, shapeYaw, 0f);
                shape.position = shapeCenter;
            }
            return clockDeg;
        }
    }
}
