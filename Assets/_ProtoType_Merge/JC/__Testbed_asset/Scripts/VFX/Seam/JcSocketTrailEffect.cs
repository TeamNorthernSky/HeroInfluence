using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 소켓을 월드 공간에서 "따라다니는" 트레일 재료. ASB 연출 규약(ISkillEffectBehaviour/ISkillEffectHandle) 구현체.
    ///
    /// ★부모로 붙이지 않는 이유:
    /// 저스티스 모델의 소켓은 lossyScale이 약 200배(Armature localScale 100 × Model 2.0/2.2/2.0)이고,
    /// Y만 220이라 비균등이다. 소켓에 자식으로 붙이면 이펙트가 200배로 부풀고 세로로 늘어난다.
    /// 그래서 부모 관계를 만들지 않고 위치만 매 프레임 복사한다. 스케일·회전 왜곡이 원천 차단되고,
    /// 주먹이 회수된 뒤에도 잔광이 공중에 남아 자연스럽게 사라진다.
    ///
    /// Cue 사용법:
    ///   Spawn + InstanceKey  → Play()    : 추적 시작, 트레일 방출 on
    ///   Stop  + 같은 Key     → Stop()    : 방출 off, 잔광 페이드 후 자멸
    ///   Signal+ 같은 Key     → Signal()  : Stop과 동일하되 핸들은 유지(중간 신호용)
    /// </summary>
    [DisallowMultipleComponent]
    public class JcSocketTrailEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
    {
        [Header("Refs")]
        [Tooltip("함께 재생/정지할 파티클(궤적 PenStrokes + 입자 SparkDots). 비우면 자식에서 전부 탐색.")]
        [SerializeField] private ParticleSystem[] streaks;

        [Header("Follow")]
        [Tooltip("소켓 기준 오프셋(미터). 소켓의 200배 스케일은 무시하고 회전만 적용해 더한다.")]
        [SerializeField] private Vector3 socketOffset = Vector3.zero;

        [Header("Motion (소켓 위치 변화만으로 계산 — 캐릭터와 결합하지 않음)")]
        [Tooltip("이 이름의 자식을 진행 반대 방향으로 회전시킨다. 원뿔(Cone) 분사가 뒤쪽을 향하게 하는 용도.")]
        [SerializeField] private string alignChildName = "SparkDots";

        [Tooltip("속도 측정 평활화 계수. 클수록 반응이 빠르고 튄다.")]
        [SerializeField, Range(1f, 30f)] private float velocitySmoothing = 12f;

        [Tooltip("이 속도(m/s)에서 방출이 100%가 된다. 이보다 느리면 비례해 줄어든다.")]
        [SerializeField, Min(0.01f)] private float speedForFullEmission = 6f;

        [Tooltip("완전히 멈췄을 때의 방출 배율. 0이면 정지 시 방출이 끊긴다.")]
        [SerializeField, Range(0f, 1f)] private float emissionAtRest = 0.15f;

        [Tooltip("속도에 따른 방출 조절을 사용할지.")]
        [SerializeField] private bool scaleEmissionBySpeed = true;

        [Tooltip("뒤로 분출 세기. startSpeed에 (이 값 × 소켓속도)를 더한다. 방향은 원뿔이 정하므로 분사각이 유지된다.")]
        [SerializeField, Min(0f)] private float backwardEjectScale = 1.2f;

        [Tooltip("분사 축이 최초 기준 방향에서 벗어날 수 있는 최대 각도(도).\n" +
                 "주먹 회수 시 속도가 뒤집혀 분사가 적 쪽을 향하는 것을 막는다.")]
        [SerializeField, Range(0f, 180f)] private float alignMaxAngle = 60f;

        [Tooltip("기준 방향을 잡기 시작하는 최소 속도(m/s).")]
        [SerializeField, Min(0.01f)] private float alignRefMinSpeed = 1.5f;

        [Tooltip("방출을 끈 뒤 잔광이 다 사라질 때까지의 추가 여유(초).")]
        [SerializeField, Min(0f)] private float fadeOutExtraSeconds = 0.15f;

        [SerializeField] private bool logLifecycle;

        private Transform followTarget;
        private bool following;
        private bool stopped;

        private Vector3 lastPos;
        private bool hasLastPos;
        private Vector3 smoothedVelocity;
        private Transform alignChild;
        private ParticleSystem[] speedScaled;
        private float[] baseRateOverTime;

        /// <summary>측정된 소켓 속도(m/s). 캐릭터 컴포넌트를 참조하지 않고 위치 변화만으로 구한다.</summary>
        public Vector3 SocketVelocity => smoothedVelocity;

        // 프리셋 바인더가 밀어넣는 값
        public string AlignChildName { get => alignChildName; set { alignChildName = value; alignChild = null; } }
        public float SpeedForFullEmission { get => speedForFullEmission; set => speedForFullEmission = Mathf.Max(0.01f, value); }
        public float EmissionAtRest { get => emissionAtRest; set => emissionAtRest = Mathf.Clamp01(value); }
        public bool ScaleEmissionBySpeed { get => scaleEmissionBySpeed; set => scaleEmissionBySpeed = value; }
        public float BackwardEjectScale { get => backwardEjectScale; set => backwardEjectScale = Mathf.Max(0f, value); }
        public float AlignMaxAngle { get => alignMaxAngle; set => alignMaxAngle = Mathf.Clamp(value, 0f, 180f); }
        public float FadeOutExtraSeconds { get => fadeOutExtraSeconds; set => fadeOutExtraSeconds = Mathf.Max(0f, value); }
        public Vector3 SocketOffset { get => socketOffset; set => socketOffset = value; }

        private Vector3 alignRefDir;
        private bool hasAlignRef;
        private float[] baseSpeedMin;
        private float[] baseSpeedMax;

        private void Reset() => streaks = GetComponentsInChildren<ParticleSystem>(true);

        private void Awake()
        {
            if (streaks == null || streaks.Length == 0) streaks = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void Play(SkillEffectContext ctx)
        {
            followTarget = ctx != null ? ctx.SocketTransform : null;

            // 어떤 경우에도 소켓 자식이 되지 않는다(스케일 오염 차단).
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            if (followTarget != null)
            {
                transform.position = ResolvePosition();
            }

            // 스폰 지점(0,0,0)에서 소켓까지 선이 그어지지 않도록 항상 비우고 시작한다.
            for (int i = 0; i < streaks.Length; i++)
            {
                if (streaks[i] == null) continue;
                streaks[i].Clear(true);
                streaks[i].Play(true);
            }

            following = true;
            stopped = false;
            hasLastPos = false;
            smoothedVelocity = Vector3.zero;

            hasAlignRef = false;
            alignRefDir = Vector3.zero;

            // ★속도 연동·뒤로 분출은 **분사 계열(alignChildName)에만** 적용한다.
            // 예전에는 자식 파티클 전부에 걸어서, 궤적(Local 시뮬레이션) 입자까지
            // backwardEject × 속도만큼 튕겨나가 사방으로 긴 선을 긋는 사고가 났다.
            var picked = new List<ParticleSystem>();
            for (int i = 0; i < streaks.Length; i++)
            {
                if (streaks[i] == null) continue;
                if (!string.IsNullOrEmpty(alignChildName) && streaks[i].gameObject.name != alignChildName) continue;
                picked.Add(streaks[i]);
            }

            speedScaled = picked.ToArray();
            baseRateOverTime = new float[speedScaled.Length];
            baseSpeedMin = new float[speedScaled.Length];
            baseSpeedMax = new float[speedScaled.Length];
            for (int i = 0; i < speedScaled.Length; i++)
            {
                baseRateOverTime[i] = speedScaled[i].emission.rateOverTime.constant;
                var mm = speedScaled[i].main.startSpeed;
                baseSpeedMin[i] = mm.constantMin;
                baseSpeedMax[i] = mm.constantMax;
            }

            if (logLifecycle)
            {
                Debug.Log($"[JcSocketTrailEffect] Play — follow='{(followTarget != null ? followTarget.name : "NULL")}'", this);
            }
        }

        /// <returns>false = 핸들을 계속 보유(이후 Stop도 받을 수 있게).</returns>
        public bool Signal(SkillEffectContext ctx)
        {
            StopEmitting();
            return false;
        }

        public void Stop() => StopEmitting();

        private void StopEmitting()
        {
            if (stopped)
            {
                return;
            }

            stopped = true;
            following = false;

            float life = fadeOutExtraSeconds;

            // 방출만 끊고 이미 나온 입자·궤적은 수명대로 사라지게 둔다.
            for (int i = 0; i < streaks.Length; i++)
            {
                if (streaks[i] == null) continue;
                streaks[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);

                // per-particle 트레일은 입자가 죽은 뒤에도 trails.lifetime 만큼 더 남는다.
                float psLife = streaks[i].main.startLifetime.constantMax;
                ParticleSystem.TrailModule trails = streaks[i].trails;
                if (trails.enabled && !trails.dieWithParticles)
                {
                    psLife += trails.lifetime.constantMax;
                }

                float needed = psLife + fadeOutExtraSeconds;
                if (needed > life) life = needed;
            }

            if (logLifecycle)
            {
                Debug.Log($"[JcSocketTrailEffect] Stop — {life:F2}초 뒤 정리", this);
            }

            Destroy(gameObject, life);
        }

        private Vector3 ResolvePosition()
        {
            // TransformPoint를 쓰면 소켓의 200배 스케일이 오프셋에 곱해진다. 회전만 적용한다.
            return followTarget.position + followTarget.rotation * socketOffset;
        }

        private void LateUpdate()
        {
            if (!following)
            {
                return;
            }

            if (followTarget == null)
            {
                // 유닛이 사라진 경우: 추적만 멈추고 잔광은 남긴다.
                StopEmitting();
                return;
            }

            Vector3 next = ResolvePosition();
            UpdateMotion(next);
            transform.position = next;
        }

        /// <summary>
        /// 소켓 위치 변화만으로 속도를 구해 (1) 분사 방향 정렬 (2) 속도별 방출 조절에 쓴다.
        /// 캐릭터 컴포넌트를 전혀 참조하지 않으므로 ASB 쪽과 결합이 생기지 않는다.
        /// </summary>
        private void UpdateMotion(Vector3 next)
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Vector3 raw = hasLastPos ? (next - lastPos) / dt : Vector3.zero;
                smoothedVelocity = hasLastPos
                    ? Vector3.Lerp(smoothedVelocity, raw, Mathf.Clamp01(velocitySmoothing * dt))
                    : Vector3.zero;
            }
            lastPos = next;
            hasLastPos = true;

            float speed = smoothedVelocity.magnitude;

            // 진행 반대 방향으로 자식을 돌린다 → 원뿔 분사가 뒤쪽을 향한다.
            // ★기준 방향에서 alignMaxAngle 이상 벗어나지 못하게 클램프한다.
            //   주먹 회수 구간에서 속도가 뒤집히면 -velocity가 적 쪽을 가리키기 때문이다.
            if (!string.IsNullOrEmpty(alignChildName))
            {
                if (alignChild == null) alignChild = transform.Find(alignChildName);
                if (alignChild != null && speed > 0.05f)
                {
                    Vector3 want = -smoothedVelocity.normalized;

                    if (!hasAlignRef && speed >= alignRefMinSpeed)
                    {
                        alignRefDir = want;      // 최초의 유의미한 이동에서 기준축 확정
                        hasAlignRef = true;
                    }

                    if (hasAlignRef)
                    {
                        want = Vector3.RotateTowards(alignRefDir, want, alignMaxAngle * Mathf.Deg2Rad, 0f);
                    }

                    // want가 up과 평행하면 LookRotation이 퇴화한다(주먹이 수직으로 움직이는 경우).
                    Vector3 upRef = Mathf.Abs(Vector3.Dot(want, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
                    alignChild.rotation = Quaternion.LookRotation(want, upRef);
                }
            }

            if (speedScaled == null) return;

            float emitScale = scaleEmissionBySpeed
                ? Mathf.Lerp(emissionAtRest, 1f, Mathf.Clamp01(speed / speedForFullEmission))
                : 1f;

            for (int i = 0; i < speedScaled.Length; i++)
            {
                if (speedScaled[i] == null) continue;

                var em = speedScaled[i].emission;
                em.rateOverTime = baseRateOverTime[i] * emitScale;

                // 뒤로 분출 — 방향은 원뿔이 정하고, 세기만 소켓 속도에 비례해 더한다.
                // (Inherit Velocity를 쓰면 공통 직선 벡터가 되어 분사각이 무의미해진다.)
                if (backwardEjectScale > 0f)
                {
                    float add = backwardEjectScale * speed;
                    var main = speedScaled[i].main;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(baseSpeedMin[i] + add, baseSpeedMax[i] + add);
                }
            }
        }
    }
}
