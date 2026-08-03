using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 호 획 V2 — 재설계(260729). ASB 연출 규약(ISkillEffectBehaviour) 구현체.
    /// 구버전(JcArcSweepEffect)은 레거시로 무수정 보존, 이 컴포넌트가 대체한다.
    ///
    /// 책임을 최소로 줄였다: 「앵커를 반경·각도의 호 궤도로 주행시킨다」가 전부다
    /// (easeOut 1 = 등속, 초과 = 촤악 감속. 밀도·수축 구간은 호 위 위치 기준이라 easeOut과 무관하게 형상 유지).
    /// 획의 형태·일생은 파티클(프리셋 ArcStrokeGroup)이 전담한다:
    ///   두께: 0 → 최대(증가 시간) → 0 (소멸 구간)   ← 획 일생 규칙
    ///   소멸: 전체 유지 후 알파 페이드 — 뒤에서 지워지는 혜성 없음
    ///        (트레일 배율 1 + dieWithParticles로 성립. 바인더 ApplyArcStroke 참조)
    ///
    /// 레거시에서 검증된 기술은 유지한다:
    ///   서브스텝 수동 시뮬레이션 — 프레임당 이동이 크면 갓 태어난 획의 첫 세그먼트가
    ///     직선 글리치로 찍힌다. 프레임을 쪼개 이동↔시뮬레이트를 교대 진행.
    ///   말단 방출 컷 — 잔여 스윕이 획 일생 Min보다 짧으면 방출을 멈춘다(자동, 파라미터 아님).
    ///     Min = 「의미 있는 획의 하한」이므로 그보다 짧게 압축될 획은 태어나지 않는다.
    ///     (구 컷은 growTime 절대 초 기준이었으나 grow가 일생 비율로 전환되며 전제 소멸 — 260729.
    ///      비율 체계에서는 성장이 항상 자기 사망 전에 끝나 「정지 후 제자리 성장」이 정의상 불가능하다.)
    ///   소켓 비부모 — 소켓 lossyScale 약 200배라 위치만 읽는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcArcStrokeEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Tooltip("호를 주행하는 앵커. 파티클이 이 아래에 있다. 비우면 첫 자식.")]
        [SerializeField] private Transform anchor;

        [Header("궤도 (xz 평면 · 시계 각도: 타깃 방향=12시)")]
        [Tooltip("호 시작각(도). 210 = 7시.")]
        [SerializeField] private float angleStart = 210f;
        [Tooltip("호 끝각(도). 끝 < 시작 = 반시계, 끝 > 시작 = 시계, 차이 360 초과 = 한 바퀴 이상.")]
        [SerializeField] private float angleEnd = 30f;
        [Tooltip("궤도 반경(m).")]
        [SerializeField, Min(0.05f)] private float radius = 1.6f;
        [Tooltip("호를 다 긋는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.02f)] private float sweepDuration = 0.5f;
        [Tooltip("주행 가속 곡선. 1=등속, 클수록 초반이 빠르고 끝에서 감속(촤악).")]
        [SerializeField, Range(0.3f, 4f)] private float easeOut = 1f;
        [Tooltip("노즐 상자 기울기(도) — 궤도 평면 내 로컬 회전. 0 = 반경 방향 정렬, 기울인 채로 공전한다.")]
        [SerializeField, Range(-45f, 45f)] private float nozzleTilt = 0f;

        [Header("배치 · 정리")]
        [Tooltip("스폰 지점(소켓) 기준 오프셋(m). 소켓 스케일은 무시하고 그대로 더한다.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);

        [Header("밀도 점진")]
        [Tooltip("방출 밀도가 0 → 100%로 선형 증가하는 구간(스윕 전체=1.0 기준). 0이면 즉시 풀 밀도.")]
        [SerializeField, Range(0f, 0.5f)] private float emissionRamp = 0f;
        [Tooltip("방출 밀도가 100% → 0으로 선형 감소하는 구간(스윕 끝 기준). 0이면 끝까지 풀 밀도.")]
        [SerializeField, Range(0f, 0.5f)] private float emissionDecay = 0f;
        [Tooltip("풀 밀도일 때의 거리당 방출 수. 바인더가 프리셋에서 채운다.")]
        [SerializeField, Min(0f)] private float baseRateOverDistance = 6f;

        [Header("획 일생 (잔여 시간 클램프)")]
        [Tooltip("획 일생 최소(초). 바인더가 프리셋에서 채운다.")]
        [SerializeField, Min(0.02f)] private float strokeLifeMin = 0.6f;
        [Tooltip("획 일생 최대(초). ★실제로는 「태어난 시점의 잔여 스윕 시간」을 넘지 못한다 —\n" +
                 "스윕이 끝난 뒤까지 살아남아 제자리에서 굵어졌다 사라지는 획을 원천 차단한다.")]
        [SerializeField, Min(0.05f)] private float strokeLifeMax = 1.2f;
        [Tooltip("켜면: 잔여 시간이 자기 수명보다 짧아질 획은 아예 그리지 않는다(선별 — 수명 압축 없음).\n" +
                 "끄면: 잔여 시간으로 수명을 조인다(클램프 — 생명주기 압축).")]
        [SerializeField] private bool skipShortRemainder = true;

        [Header("강제 수축 (획 머리 성장 정지 → 가늘어져 0)")]
        [Tooltip("수축 시작 구간(스윕 기준, Emission Decay 문법). 0 = 수축 없음.")]
        [SerializeField, Range(0f, 0.5f)] private float thinDecay = 0f;
        [Tooltip("먼 곳 가속 배수. 1 = 차등 없음, 클수록 바깥 획이 먼저 0에 닿는다.")]
        [SerializeField, Min(1f)] private float thinFarBoost = 2f;
        [Tooltip("차등 기준 안쪽 반경(m). 바인더가 궤도 반경-퍼짐으로 채운다.")]
        [SerializeField, Min(0f)] private float thinRadiusInner = 1f;
        [Tooltip("차등 기준 바깥 반경(m). 바인더가 궤도 반경+퍼짐으로 채운다.")]
        [SerializeField, Min(0.01f)] private float thinRadiusOuter = 2f;
        [Tooltip("두께 곡선에서 하강(소멸 구간)이 시작되는 수명 비율. 바인더가 계산해 채운다.")]
        [SerializeField, Range(0f, 0.99f)] private float fadeStartNorm = 0.75f;
        [Tooltip("전체 소멸까지의 여유(초). 획 일생이 더 길면 그쪽을 따른다.")]
        [SerializeField, Min(0f)] private float extraLinger = 0.2f;

        [SerializeField] private bool logLifecycle;

        /// <summary>서브스텝당 목표 이동 거리(m). 첫 세그먼트 직선화의 최대 길이가 된다.</summary>
        const float SubStepTargetMeters = 0.15f;

        private ParticleSystem[] strokes;
        private float elapsed;
        private bool running;
        private bool simulating;
        private bool emissionCut;

        // 프리셋 바인더가 밀어넣는 값
        public float AngleStart { get => angleStart; set => angleStart = value; }
        public float AngleEnd { get => angleEnd; set => angleEnd = value; }
        public float Radius { get => radius; set => radius = Mathf.Max(0.05f, value); }
        public float SweepDuration { get => sweepDuration; set => sweepDuration = Mathf.Max(0.02f, value); }
        public float EaseOut { get => easeOut; set => easeOut = Mathf.Clamp(value, 0.3f, 4f); }
        public float NozzleTilt { get => nozzleTilt; set => nozzleTilt = Mathf.Clamp(value, -45f, 45f); }
        public Vector3 SpawnOffset { get => spawnOffset; set => spawnOffset = value; }
        public float EmissionRamp { get => emissionRamp; set => emissionRamp = Mathf.Clamp(value, 0f, 0.5f); }
        public float EmissionDecay { get => emissionDecay; set => emissionDecay = Mathf.Clamp(value, 0f, 0.5f); }
        public float BaseRateOverDistance { get => baseRateOverDistance; set => baseRateOverDistance = Mathf.Max(0f, value); }
        public float StrokeLifeMin { get => strokeLifeMin; set => strokeLifeMin = Mathf.Max(0.02f, value); }
        public float StrokeLifeMax { get => strokeLifeMax; set => strokeLifeMax = Mathf.Max(0.05f, value); }
        public bool SkipShortRemainder { get => skipShortRemainder; set => skipShortRemainder = value; }
        public float ThinDecay { get => thinDecay; set => thinDecay = Mathf.Clamp(value, 0f, 0.5f); }
        public float ThinFarBoost { get => thinFarBoost; set => thinFarBoost = Mathf.Max(1f, value); }
        public float ThinRadiusInner { get => thinRadiusInner; set => thinRadiusInner = Mathf.Max(0f, value); }
        public float ThinRadiusOuter { get => thinRadiusOuter; set => thinRadiusOuter = Mathf.Max(0.01f, value); }
        public float FadeStartNorm { get => fadeStartNorm; set => fadeStartNorm = Mathf.Clamp(value, 0f, 0.99f); }

        private readonly System.Collections.Generic.HashSet<uint> thinned = new System.Collections.Generic.HashSet<uint>();
        private ParticleSystem.Particle[] thinBuffer;

        /// <summary>
        /// ★강제 수축 — 생명주기 재조준.
        /// 살아 있는 각 획을 두께 곡선의 「하강 구간에서 자기 현재 두께와 같은 지점」으로
        /// 점프시킨다: 성장 정지 + 현재 두께에서 이어서 하강 → 0. 두께가 연속이라 팝이 없다.
        /// 이미 그어진 몸통(정점에 구워진 폭)은 건드리지 않는다 — 붓을 떼는 동작만 강제한다.
        ///
        /// 하강 시간 D = 잔여 스윕 ÷ 거리배수(궤도 중심에서 멀수록 큼) —
        /// 먼 획은 일찍 0에 닿고, 가장 가까운 획도 스윕 종료에는 0에 도달한다.
        ///
        /// 수학: 하강 구간 [fadeStart, 1]에서 곡선값 v(n) = (1-n)/(1-fadeStart).
        /// 현재 두께비 s에 해당하는 지점 n' = 1 - s×(1-fadeStart).
        /// 남은 하강을 D초에 맞추려면 startLifetime' = D/(1-n'), remaining' = D.
        /// </summary>
        private void ApplyThinRetarget(float remainingSweep)
        {
            EnsureRefs();
            float fadeSpan = Mathf.Max(1f - fadeStartNorm, 0.01f);

            for (int i = 0; i < strokes.Length; i++)
            {
                var ps = strokes[i];
                if (ps == null) continue;

                int alive = ps.particleCount;
                if (alive == 0) continue;
                if (thinBuffer == null || thinBuffer.Length < alive) thinBuffer = new ParticleSystem.Particle[Mathf.Max(alive, 64)];
                int n = ps.GetParticles(thinBuffer);
                bool changed = false;

                for (int k = 0; k < n; k++)
                {
                    if (thinned.Contains(thinBuffer[k].randomSeed)) continue;

                    float startSize = thinBuffer[k].startSize;
                    if (startSize <= 0.0001f) { thinned.Add(thinBuffer[k].randomSeed); continue; }
                    float s = Mathf.Clamp01(thinBuffer[k].GetCurrentSize(ps) / startSize);   // 현재 두께비(곡선값)

                    // 거리 차등 — 궤도 중심(이펙트 루트)에서 먼 획일수록 빨리 하강.
                    Vector3 wp = ps.transform.TransformPoint(thinBuffer[k].position);
                    Vector3 flat = wp - transform.position; flat.y = 0f;
                    float radialT = Mathf.Clamp01((flat.magnitude - thinRadiusInner) / Mathf.Max(thinRadiusOuter - thinRadiusInner, 0.001f));
                    float d = Mathf.Max(remainingSweep / Mathf.Lerp(1f, thinFarBoost, radialT), 0.02f);

                    float nPrime = 1f - s * fadeSpan;                 // 하강 구간에서 두께 s인 지점
                    thinBuffer[k].startLifetime = d / Mathf.Max(1f - nPrime, 0.001f);
                    thinBuffer[k].remainingLifetime = d;
                    thinned.Add(thinBuffer[k].randomSeed);
                    changed = true;
                }
                if (changed) ps.SetParticles(thinBuffer, n);
            }
        }

        /// <summary>
        /// 선별 모드의 생존 배율 — 「수명이 잔여 시간을 넘는 획은 안 태어남」을
        /// 균등분포 기준 확률로 환산한 값. 잔여 ≥ Max → 1, 잔여 ≤ Min → 0, 사이는 선형.
        /// (수명 범위를 잔여로 상한한 것과 조합하면 개별 거부 추출과 수학적으로 동일)
        /// </summary>
        private float SurvivalFraction(float remaining)
        {
            if (remaining >= strokeLifeMax) return 1f;
            if (remaining <= strokeLifeMin) return 0f;
            return (remaining - strokeLifeMin) / Mathf.Max(strokeLifeMax - strokeLifeMin, 1e-4f);
        }

        /// <summary>
        /// ★잔여 시간 클램프 — 이번에 태어날 획의 수명을 「스윕 종료까지 남은 시간」으로 조인다.
        /// Min/Max와 무관하게 어떤 획도 스윕 끝을 넘겨 살 수 없다 → 전원 스윕 종료와 함께 소멸.
        /// startLifetime 변경은 새로 방출되는 입자에만 적용되므로, 이미 태어난 획은 영향받지 않는다.
        /// </summary>
        private void ClampLifetimeToRemaining(float remaining)
        {
            EnsureRefs();
            float max = Mathf.Clamp(strokeLifeMax, 0.02f, Mathf.Max(remaining, 0.02f));
            float min = Mathf.Min(strokeLifeMin, max);
            for (int i = 0; i < strokes.Length; i++)
            {
                if (strokes[i] == null) continue;
                var main = strokes[i].main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(min, max);
            }
        }

        /// <summary>
        /// ★휘어진 진행도 — 시간 진행도 t(0~1)를 호 위 위치(0~1)로 사상한다.
        /// easeOut 1 = 등속, 초과 = 초반 빠르고 말단 감속. 앵커 이동·밀도 구간·수축 트리거가
        /// 전부 이 값을 쓰므로, easeOut을 바꿔도 화면상 형상(어느 호 구간에서 무엇이 일어나는지)이 유지된다.
        /// </summary>
        private float EasedProgress(float t)
            => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), easeOut);

        /// <summary>호 위 위치 p(0~1)에서의 방출 밀도 배율 — 점진(앞) × 감소(뒤). ★공간 기준.</summary>
        private float EmissionFactor(float p)
        {
            float up = emissionRamp > 0.001f ? Mathf.Clamp01(p / emissionRamp) : 1f;
            float down = emissionDecay > 0.001f ? Mathf.Clamp01((1f - p) / emissionDecay) : 1f;
            return up * down;
        }

        private void Awake() => EnsureRefs();

        /// <summary>에디트 모드(Awake 미호출)에서도 안전하도록 지연 초기화.</summary>
        private void EnsureRefs()
        {
            if (anchor == null && transform.childCount > 0) anchor = transform.GetChild(0);
            if (strokes == null || strokes.Length == 0) strokes = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void Play(SkillEffectContext ctx)
        {
            EnsureRefs();

            // 어떤 경우에도 소켓 자식이 되지 않는다(스케일 오염 차단).
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            if (ctx != null)
            {
                Transform socket = ctx.SocketTransform;
                // ★TransformPoint 금지 — 소켓 스케일이 오프셋에 곱해진다.
                transform.position = (socket != null ? socket.position : ctx.SpawnPosition) + spawnOffset;
                transform.rotation = Quaternion.LookRotation(ResolveForward(ctx), Vector3.up);
            }

            elapsed = 0f;
            running = true;
            simulating = true;
            emissionCut = false;

            // 퇴화 조합 감시 — Min ≥ 스윕이면 「Min보다 짧은 획은 없다」는 선언에 의해
            // 방출 창이 0이 된다. 의도일 수도 있으나 조용히 사라지면 튜닝 함정이므로 알린다.
            if (strokeLifeMin >= sweepDuration)
                Debug.LogWarning("[JcArcStroke] Stroke Life Min(" + strokeLifeMin.ToString("F2") +
                                 "s) ≥ Sweep Duration(" + sweepDuration.ToString("F2") +
                                 "s) — 방출 창이 0이라 획이 태어나지 않습니다.", this);
            MoveAnchor(0f);
            ClampLifetimeToRemaining(sweepDuration);   // 시작부터 규칙 적용 — Max가 스윕보다 길어도 조여진다
            thinned.Clear();                           // 수축 재조준 이력 초기화

            for (int i = 0; i < strokes.Length; i++)
            {
                if (strokes[i] == null) continue;
                strokes[i].Clear(true);
                var em = strokes[i].emission;
                em.enabled = true;
                // 밀도 성형·선별이 켜져 있으면 t=0의 배율로 시작 — Update가 진행도에 맞춰 조절한다.
                if (emissionRamp > 0.001f || emissionDecay > 0.001f || skipShortRemainder)
                    em.rateOverDistance = baseRateOverDistance * EmissionFactor(0f)
                        * (skipShortRemainder ? SurvivalFraction(sweepDuration) : 1f);
                strokes[i].Simulate(0f, false, true, false);   // restart — 이후는 Update가 전진시킨다
            }

            // 정리 예약: 잔여 시간 클램프로 모든 획이 스윕 종료와 함께 소멸하므로 주행 + 여유면 충분.
            Destroy(gameObject, sweepDuration + extraLinger);

            if (logLifecycle)
            {
                Debug.Log("[JcArcStroke] Play  origin=" + transform.position.ToString("F2"), this);
            }
        }

        /// <summary>전진 기준(시계 12시 방향) = 시전자→타깃 수평. 없으면 시전자 정면.</summary>
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
                // ★이번 프레임의 「실제」 이동량으로 쪼갠다 — 평균 속도(dt/스윕) 기준이면
                // Ease Out의 빠른 초반(실속도 최대 easeOut배)에서 첫 세그먼트 직선 글리치가 부활한다.
                float t0 = Mathf.Clamp01(elapsed / sweepDuration);
                float t1 = Mathf.Clamp01((elapsed + dt) / sweepDuration);
                float frameMove = span * Mathf.Abs(EasedProgress(t1) - EasedProgress(t0));
                steps = Mathf.Clamp(Mathf.CeilToInt(frameMove / SubStepTargetMeters), 1, 16);
            }

            // 강제 수축 — 시작 구간(호 위 위치 기준) 진입 후, 아직 재조준 안 된 획을 프레임당 1회 처리한다.
            // (재조준은 획당 한 번뿐이라 이후 프레임은 신규 탄생분만 스캔한다.
            //  트리거는 공간 기준이지만 하강 시간 D는 잔여 「초」가 필요하므로 시간 잔여를 넘긴다.)
            if (thinDecay > 0.001f && running)
            {
                float pNow = EasedProgress(Mathf.Clamp01(elapsed / sweepDuration));
                if (pNow >= 1f - thinDecay)
                {
                    ApplyThinRetarget(Mathf.Max(sweepDuration - elapsed, 0.02f));
                }
            }

            float sub = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                if (running)
                {
                    elapsed += sub;
                    float t = Mathf.Clamp01(elapsed / sweepDuration);
                    float p = EasedProgress(t);   // 호 위 위치 — 앵커·밀도 구간이 공유
                    MoveAnchor(p);

                    if (!emissionCut)
                    {
                        float remaining = sweepDuration - elapsed;

                        // 태어날 획의 수명 범위를 잔여 시간으로 상한 (양쪽 모드 공통).
                        ClampLifetimeToRemaining(remaining);

                        // 선별 모드: 수명이 잔여를 넘는 획은 아예 안 태어남 → 생존 확률만큼 방출을 줄인다.
                        float survival = skipShortRemainder ? SurvivalFraction(remaining) : 1f;

                        // 밀도 성형(점진·감소 — 호 위 위치 기준) × 선별 생존 배율(시간 기준).
                        bool shaping = emissionRamp > 0.001f || emissionDecay > 0.001f || skipShortRemainder;
                        if (shaping)
                        {
                            float factor = EmissionFactor(p) * survival;
                            for (int i = 0; i < strokes.Length; i++)
                            {
                                if (strokes[i] == null) continue;
                                var em2 = strokes[i].emission;
                                em2.rateOverDistance = baseRateOverDistance * factor;
                            }
                        }
                    }

                    // 말단 방출 컷 — 잔여가 Min 아래로 내려가면 그 뒤에 태어날 획은 전부
                    // Min보다 짧게 압축될 운명이므로 방출을 멈춘다. 선별 모드(ON)에서는
                    // survival이 같은 지점에서 0이 되어 두 모드의 방출 종료점이 일치한다.
                    if (!emissionCut && sweepDuration - elapsed < strokeLifeMin)
                    {
                        emissionCut = true;
                        SetEmission(false);
                    }
                    if (t >= 1f)
                    {
                        running = false;
                        SetEmission(false);
                        if (logLifecycle) Debug.Log("[JcArcStroke] 주행 종료 — 잔상 페이드 진행", this);
                    }
                }

                for (int i = 0; i < strokes.Length; i++)
                {
                    if (strokes[i] != null) strokes[i].Simulate(sub, false, false, false);
                }
            }
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

        /// <summary>진행도(0~1) → 궤도 위 지점. 시작각에서 끝각으로 숫자 그대로 보간(래핑 없음).</summary>
        private void MoveAnchor(float progress)
        {
            EnsureRefs();
            if (anchor == null) return;

            float clockDeg = Mathf.LerpUnclamped(angleStart, angleEnd, progress);
            float rad = clockDeg * Mathf.Deg2Rad;

            // 시계 각도 → 로컬 xz: 12시=+Z이므로 x=sin, z=cos.
            anchor.localPosition = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);

            // ★공전하는 직사각형 노즐 — 상자의 X축이 반경 방향(중심→앵커)을 향하도록 돌린다.
            // 앵커 자체를 회전시키면 Local 시뮬레이션의 기존 입자까지 휘둘리므로,
            // 신규 입자에만 적용되는 shape.rotation을 쓴다.
            // rotY(φ)가 X축을 (cosφ, 0, -sinφ)로 보내므로, 반경 방향 (sinθ, 0, cosθ)에는 φ = θ − 90°.
            // 노즐 기울기는 여기에 더해지는 로컬 오프셋 — 기울어진 채 공전한다(펜촉 눕히기).
            float shapeYaw = clockDeg - 90f + nozzleTilt;
            for (int i = 0; i < strokes.Length; i++)
            {
                if (strokes[i] == null) continue;
                var shape = strokes[i].shape;
                if (!shape.enabled) continue;
                shape.rotation = new Vector3(0f, shapeYaw, 0f);
            }
        }
    }
}
