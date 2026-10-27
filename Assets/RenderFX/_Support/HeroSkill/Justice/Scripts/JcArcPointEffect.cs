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

        [Header("수렴 (탄생 시 바깥 오프셋 → 수명 진행에 따라 본 궤도 합류)")]
        [Tooltip("시작 오프셋 최소(m). 대시마다 [최소, 최대]에서 랜덤 = 노이즈.")]
        [SerializeField, Min(0f)] private float offsetMin = 0.3f;
        [Tooltip("시작 오프셋 최대(m).")]
        [SerializeField, Min(0f)] private float offsetMax = 0.9f;
        [Tooltip("복귀 곡선 지수. 1 = 선형 수렴, 클수록 초반에 빠르게 붙고 이후 궤도에 밀착.")]
        [SerializeField, Range(0.5f, 4f)] private float convergeExp = 1.5f;

        [Header("배치 · 정리")]
        [Tooltip("스폰 지점(소켓) 기준 오프셋(m). 소켓 스케일은 무시된다.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);
        [Tooltip("주행 종료 후 잔광 여유(초).")]
        [SerializeField, Min(0f)] private float extraLinger = 0.2f;

        [Header("밀도 (호 위 위치 기준)")]
        [Tooltip("생성 밀도의 점진 구간(호 전체=1.0, 호 위 위치 기준). 0 = 처음부터 풀 밀도.  0.25 = 호의 앞 25% 구간 동안 밀도가 0 → 100%로 선형 증가. 호의 시작은 성기고 진행할수록 빽빽해진다.")]
        [SerializeField, Range(0f, 0.5f)] private float emissionRamp = 0f;
        [Tooltip("생성 밀도의 감소 구간(호 전체=1.0, 호 위 위치 기준). 0 = 끝까지 풀 밀도 유지(마지막에 한 번에 종료).  0.5 = 호의 중간부터 밀도가 100% → 0으로 선형 감소. 호의 끝으로 갈수록 획이 성겨진다.")]
        [SerializeField, Range(0f, 0.5f)] private float emissionDecay = 0f;
        [Tooltip("풀 밀도일 때의 거리당 방출 수. 바인더가 프리셋에서 채운다.")]
        [SerializeField, Min(0f)] private float baseRateOverDistance = 1.5f;

        [Header("대시 일생 (수명 = 대시 길이 — 압축 없음)")]
        [Tooltip("대시 수명 최소(초). 헤드를 따라 그리는 시간 = 대시 길이.")]
        [SerializeField, Min(0.02f)] private float strokeLifeMin = 0.15f;
        [Tooltip("획 일생 최대(초). ★실제로는 「태어난 시점의 잔여 스윕 시간」을 넘지 못한다 — 스윕이 끝난 뒤까지 살아남아 제자리에서 굵어졌다 사라지는 획을 원천 차단한다.")]
        [SerializeField, Min(0.05f)] private float strokeLifeMax = 0.35f;
        [Tooltip("켜면: 스윕을 넘길 대시는 애초에 태어나지 않는다 → 연장 없이 스윕과 함께 종료.\n" +
                 "끄면: 말미 대시도 전부 태어나고, 스윕 종료 후 앵커가 등속으로 연장 주행하며 마저 그린다.")]
        [SerializeField] private bool skipShortRemainder = true;

        [Tooltip("생성·재생·종료 과정을 Console에 기록합니다. 효과 외형에는 영향을 주지 않습니다.")]
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

        /// <summary>대시별 탄생 정보 — 오프셋 δ와 ★그때의 반경 방향.</summary>
        private struct Birth
        {
            public float delta;
            public Vector3 radial;
        }

        // 처음 관측될 때 기록하고, 이후 그 입자는 늘 자기 탄생 반경선 위에서만 움직인다.
        private readonly System.Collections.Generic.Dictionary<uint, Birth> births =
            new System.Collections.Generic.Dictionary<uint, Birth>();
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
            births.Clear();

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
        /// 매 서브스텝, 생존 입자의 위치에서 「자기 탄생 반경 방향 성분」만 δ×(1−q)^k 로 교체한다
        /// (δ·방향 = 처음 관측 시 기록, q = 수명 진행). 접선·수직 산포는 보존된다.
        /// 수명 압축이 없으므로 q는 늘 자기 시간표대로 흐른다 — 재조준류 개입 없음.
        ///
        /// ★탄생 방향을 입자마다 기록하는 이유(260730). 예전에는 살아 있는 모든 입자를
        ///   「앵커의 현재 반경 방향」 하나로 재배치했다. 입자는 자기가 태어난 각도의 반경선을 따라
        ///   들어와야 하는데 이미 돌아간 방향으로 투영하니, 서브스텝마다 Δθ² 크기의 오차가 남고
        ///   그것이 수명 내내 누적되어 입자가 바깥으로 끌려 나갔다(누적량 ≈ δ·Θ·Δθ/2).
        ///   대쉬(한 번 긋고 끝)에서는 묻혔지만 용권풍처럼 여러 바퀴를 돌면 회전이 빠를수록 심해져,
        ///   「회전수를 올리면 포인트 반경이 커진다」로 드러났다.
        ///   자기 반경선에 묶으면 회전량과 무관해진다 — 누적이 생길 자리가 없다.
        /// </summary>
        private void ApplyConverge(float clockDeg)
        {
            float rad = clockDeg * Mathf.Deg2Rad;
            Vector3 radialNow = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

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
                    uint seed = buffer[k].randomSeed;

                    Birth b;
                    if (!births.TryGetValue(seed, out b))
                    {
                        // 탄생 직후 첫 관측 — 지금 앵커 방향이 곧 이 입자의 반경선이다.
                        b.radial = radialNow;
                        b.delta = Mathf.Max(Vector3.Dot(pos, radialNow), 0f);   // 노즐이 준 바깥 오프셋
                        births[seed] = b;
                    }

                    float rc = Vector3.Dot(pos, b.radial);
                    float desired = b.delta * Mathf.Pow(1f - q, convergeExp);
                    buffer[k].position = pos - b.radial * rc + b.radial * desired;
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
