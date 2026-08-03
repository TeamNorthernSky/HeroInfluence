using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 호 참격 — 가상 앵커 주행형. ASB 연출 규약(ISkillEffectBehaviour) 구현체.
    ///
    /// 빈 앵커가 캐릭터 주위 호 궤도(xz 평면)를 반시계로 주행하고,
    /// 앵커에 실린 파티클(Local 시뮬레이션)이 per-particle 트레일로 획을 긋는다.
    /// 발 궤적(JcSocketTrailEffect)과 같은 원리 — 소켓 대신 스크립트 궤도를 따를 뿐이다.
    ///
    /// 획의 이어짐/끊어짐은 입자 수명이 만든다: 수명이 긴 입자는 앵커를 끝까지 타고 가며
    /// 호를 관통하는 획을 남기고, 짧은 입자는 중간에 죽어 끊긴 획이 된다.
    ///
    /// ★셰이더 쿼드 방식에서 전환한 이유: 스윕 경계가 매 프레임 이동하며 픽셀이
    /// 켜졌다 꺼지는 깜빡임이 구조적으로 발생했다. 트레일은 정점 누적이라 경계가 없다.
    ///
    /// ★소켓에 자식으로 붙이지 않는다(소켓 lossyScale 약 200배 — 스케일 오염 차단).
    /// 스폰 시점의 소켓 위치·타깃 방향만 읽고, 이후는 독립 주행한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcArcSweepEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Tooltip("호를 주행하는 앵커. 파티클이 이 아래에 있다. 비우면 첫 자식.")]
        [SerializeField] private Transform anchor;

        [Header("궤도 (xz 평면 · 시계 각도: 타깃 방향=12시)")]
        [Tooltip("호 시작각(도). 210 = 7시.")]
        [SerializeField] private float angleStart = 210f;
        [Tooltip("호 끝각(도). 시작각에서 숫자 그대로 보간 — 끝 < 시작 = 반시계, 끝 > 시작 = 시계.\n" +
                 "차이가 360을 넘으면 한 바퀴 이상 돈다.")]
        [SerializeField] private float angleEnd = 30f;
        [Tooltip("궤도 반경(m).")]
        [SerializeField, Min(0.05f)] private float radius = 1.6f;

        [Header("주행")]
        [Tooltip("호를 다 긋는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.02f)] private float sweepDuration = 0.18f;
        [Tooltip("가속 곡선. 1=등속, 클수록 초반이 빠르고 끝에서 감속.")]
        [SerializeField, Min(0.1f)] private float easeOut = 2f;

        [Header("배치 · 정리")]
        [Tooltip("스폰 지점(소켓) 기준 오프셋(m). 소켓 스케일은 무시하고 그대로 더한다.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.2f, 0f);
        [Tooltip("주행 종료 후 잔광 여유(초). 파티클·트레일 수명이 더 길면 그쪽을 따른다.")]
        [SerializeField, Min(0f)] private float fadeOutExtraSeconds = 0.4f;

        [SerializeField] private bool logLifecycle;

        private ParticleSystem[] streaks;
        private float elapsed;
        private bool running;      // 앵커 주행 중
        private bool simulating;   // 수동 Simulate 구동 중(주행 종료 후 잔광 소멸까지 유지)
        private bool emissionCut;  // 말단 방출 컷 완료 여부
        private float emitTailCutoffSeconds;   // 주행 종료 전 이 시간부터 방출 정지

        /// <summary>
        /// ★말단 쐐기 방지 — ease-out 끝에서 앵커 속도가 0으로 수렴하는데, 그 직전에 태어난
        /// 입자는 이동 없이 폭만 자라 「길이 0 + 폭만 커지는 쐐기꼴」을 호 끝에 남긴다.
        /// 남은 주행 시간이 폭 성장 시간(endFade × 수명)보다 짧아지면 방출을 미리 끊는다.
        /// 바인더가 프리셋에서 계산해 밀어 넣는다.
        /// </summary>
        public float EmitTailCutoffSeconds { get => emitTailCutoffSeconds; set => emitTailCutoffSeconds = Mathf.Max(0f, value); }

        // 프리셋 바인더가 밀어넣는 값
        public float AngleStart { get => angleStart; set => angleStart = value; }
        public float AngleEnd { get => angleEnd; set => angleEnd = value; }
        public float Radius { get => radius; set => radius = Mathf.Max(0.05f, value); }
        public float SweepDuration { get => sweepDuration; set => sweepDuration = Mathf.Max(0.02f, value); }
        public float EaseOut { get => easeOut; set => easeOut = Mathf.Max(0.1f, value); }
        public Vector3 SpawnOffset { get => spawnOffset; set => spawnOffset = value; }
        public float FadeOutExtraSeconds { get => fadeOutExtraSeconds; set => fadeOutExtraSeconds = Mathf.Max(0f, value); }

        private void Awake()
        {
            if (anchor == null && transform.childCount > 0) anchor = transform.GetChild(0);
            streaks = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void Play(SkillEffectContext ctx)
        {
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
            MoveAnchor(0f);

            // ★수동 시뮬레이션으로 구동한다(Play 대신 매 프레임 Simulate).
            // 앵커가 한 프레임에 1m 이상 움직여서, 정상 재생으로는 갓 태어난 입자의
            // 첫 트레일 세그먼트가 「보간 스폰 지점 → 프레임 끝 위치」를 직선으로 잇는
            // 긴 가로 글리치가 찍혔다. 프레임을 서브스텝으로 쪼개 이동↔시뮬레이트를
            // 교대로 진행하면 세그먼트가 서브스텝 길이로 줄어 곡선에 붙는다.
            for (int i = 0; i < streaks.Length; i++)
            {
                if (streaks[i] == null) continue;
                streaks[i].Clear(true);
                var em = streaks[i].emission;
                em.enabled = true;
                streaks[i].Simulate(0f, false, true, false);   // restart — 이후는 Update가 전진시킨다
            }

            if (logLifecycle)
            {
                Debug.Log("[JcArcSweep] Play  origin=" + transform.position.ToString("F2"), this);
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

        /// <summary>주행 중 서브스텝당 목표 이동 거리(m). 첫 세그먼트 글리치의 최대 길이가 된다.</summary>
        const float SubStepTargetMeters = 0.15f;

        private void Update()
        {
            if (!simulating) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            int steps = 1;
            if (running)
            {
                // 이번 프레임 이동량을 어림해 서브스텝 수를 정한다(ease 미분 무시한 평균 기준 × 여유 2).
                float span = Mathf.Abs(angleEnd - angleStart) * Mathf.Deg2Rad * radius;
                float frameMove = span * (dt / Mathf.Max(sweepDuration, 0.01f));
                steps = Mathf.Clamp(Mathf.CeilToInt(frameMove / SubStepTargetMeters) * 2, 1, 16);
            }

            float sub = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                if (running)
                {
                    elapsed += sub;
                    float t = Mathf.Clamp01(elapsed / sweepDuration);
                    MoveAnchor(1f - Mathf.Pow(1f - t, easeOut));   // ease-out — 초반이 빠르다(촤악)

                    // 말단 방출 컷 — 종료 직전에 태어나 제자리에서 폭만 자라는 쐐기 입자를 차단.
                    if (!emissionCut && sweepDuration - elapsed <= emitTailCutoffSeconds)
                    {
                        emissionCut = true;
                        for (int i = 0; i < streaks.Length; i++)
                        {
                            if (streaks[i] == null) continue;
                            var em = streaks[i].emission;
                            em.enabled = false;
                        }
                    }

                    if (t >= 1f)
                    {
                        running = false;
                        StopAndScheduleCleanup();
                    }
                }

                for (int i = 0; i < streaks.Length; i++)
                {
                    if (streaks[i] != null) streaks[i].Simulate(sub, false, false, false);
                }
            }
        }

        /// <summary>
        /// 진행도(0~1)에 해당하는 궤도 위 지점으로 앵커를 옮긴다.
        /// 시작각 → 끝각을 **숫자 그대로** 보간한다(래핑 없음).
        /// 끝 &lt; 시작 = 반시계, 끝 &gt; 시작 = 시계, 차이가 360을 넘으면 한 바퀴 이상.
        /// 예) 210→30: 반시계 180° / 210→-150: 반시계 한 바퀴 / 210→570: 시계 한 바퀴.
        /// </summary>
        private void MoveAnchor(float progress)
        {
            if (anchor == null) return;

            float clockDeg = Mathf.LerpUnclamped(angleStart, angleEnd, progress);
            float rad = clockDeg * Mathf.Deg2Rad;

            // 시계 각도 → 로컬 xz: 12시=+Z이므로 x=sin, z=cos.
            anchor.localPosition = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
        }

        private void StopAndScheduleCleanup()
        {
            float life = fadeOutExtraSeconds;
            for (int i = 0; i < streaks.Length; i++)
            {
                if (streaks[i] == null) continue;
                // 수동 Simulate 구동이라 Stop은 부르지 않는다 — 방출만 끊고 잔광은 Update가 마저 시뮬레이트한다.
                var em = streaks[i].emission;
                em.enabled = false;

                // trails.lifetime은 입자 수명에 대한 배율(1 고정 정책).
                float psLife = streaks[i].main.startLifetime.constantMax;
                float tail = psLife + psLife * streaks[i].trails.lifetime.constantMax;
                if (tail > life) life = tail;
            }

            if (logLifecycle)
            {
                Debug.Log("[JcArcSweep] 주행 종료 — " + life.ToString("F2") + "초 뒤 정리", this);
            }

            Destroy(gameObject, life + fadeOutExtraSeconds);
        }
    }
}
