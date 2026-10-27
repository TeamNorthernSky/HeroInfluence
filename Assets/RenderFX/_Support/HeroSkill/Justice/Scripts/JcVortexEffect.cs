using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 전진하는 수직 회오리(용권풍). ASB 연출 규약(ISkillEffectBehaviour/ISkillEffectHandle) 구현체.
    /// 하오마루 열풍참 · 야스오 Q3 계열 — 발차기로 일으킨 회오리가 타깃 쪽으로 나아간다.
    ///
    /// ★소켓에 자식으로 붙이지 않는다. 저스티스 모델의 소켓은 lossyScale이 약 200배(비균등)라
    /// 자식이 되면 이펙트가 부풀고 세로로 늘어난다([[JcSocketTrailEffect]]와 같은 이유).
    /// 스폰 시점의 소켓 위치만 한 번 읽고, 이후에는 독립적으로 전진한다.
    ///
    /// ★회오리 자체의 형상은 만들지 않는다. 파티클의 공전·상승·트레일이 그린다.
    /// 이 컴포넌트가 맡는 것은 「어디서 시작해 어느 방향으로 얼마나 나아가는가」뿐이다.
    ///
    /// Cue 사용법:
    ///   Spawn               → Play()   : 회오리 시작, duration 뒤 방출 정지 후 자멸
    ///   Stop  + 같은 Key    → Stop()   : 즉시 방출 정지(조기 종료)
    /// </summary>
    [DisallowMultipleComponent]
    public class JcVortexEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
    {
        [Header("Refs")]
        [Tooltip("회오리를 이루는 파티클. 비우면 자식에서 전부 탐색한다.")]
        [SerializeField] private ParticleSystem[] swirls;

        [Header("배치")]
        [Tooltip("스폰 지점(발 소켓) 기준 오프셋(m). 소켓의 200배 스케일은 무시하고 회전만 적용한다.\n" +
                 "y를 내려 발밑 바닥에서 회오리가 서게 한다.")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, -0.6f, 0f);

        [Tooltip("회오리 축을 항상 월드 수직으로 고정한다. 끄면 스폰 시점의 소켓 기울기를 따른다.")]
        [SerializeField] private bool keepAxisVertical = true;

        [Header("전진")]
        [Tooltip("타깃 쪽으로 나아가는 속도(m/s). 0이면 제자리에서 돈다.")]
        [SerializeField, Min(0f)] private float travelSpeed = 6f;

        [Tooltip("최대 전진 거리(m). 이 거리에 닿으면 멈춘다.")]
        [SerializeField, Min(0f)] private float travelDistance = 5f;

        [Tooltip("전진 시작까지의 지연(초). 발이 최대 신전에 머무는 동안 제자리에서 감기게 한다.")]
        [SerializeField, Min(0f)] private float travelDelay = 0.12f;

        [Header("수명")]
        [Tooltip("방출을 유지하는 시간(초). 이후 자동으로 정지 절차에 들어간다.")]
        [SerializeField, Min(0.05f)] private float duration = 0.9f;

        [Tooltip("방출을 끈 뒤 잔광이 사라질 때까지의 추가 여유(초).")]
        [SerializeField, Min(0f)] private float fadeOutExtraSeconds = 0.5f;

        [SerializeField] private bool logLifecycle;

        private Vector3 travelDir;
        private float traveled;
        private float elapsed;
        private bool running;
        private bool stopped;

        // 프리셋 바인더가 밀어넣는 값
        public Vector3 SpawnOffset { get => spawnOffset; set => spawnOffset = value; }
        public float TravelSpeed { get => travelSpeed; set => travelSpeed = Mathf.Max(0f, value); }
        public float TravelDistance { get => travelDistance; set => travelDistance = Mathf.Max(0f, value); }
        public float TravelDelay { get => travelDelay; set => travelDelay = Mathf.Max(0f, value); }
        public float Duration { get => duration; set => duration = Mathf.Max(0.05f, value); }
        public float FadeOutExtraSeconds { get => fadeOutExtraSeconds; set => fadeOutExtraSeconds = Mathf.Max(0f, value); }

        private void Awake() => EnsureRefs();

        /// <summary>★지연 초기화 — Instantiate 중 Awake 선행이 보장되지 않아도 안전(VFX 부품 공통 규격).</summary>
        private void EnsureRefs()
        {
            if (swirls == null || swirls.Length == 0)
            {
                swirls = GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        public void Play(SkillEffectContext ctx)
        {
            EnsureRefs();

            // 어떤 경우에도 소켓 자식이 되지 않는다(스케일 오염 차단).
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;

            Vector3 origin = ResolveOrigin(ctx);
            travelDir = ResolveDirection(ctx, origin);

            transform.position = origin;
            // 파티클은 로컬 +Y를 축으로 공전한다. 전진 방향을 로컬 +Z에 맞춰 두면
            // 나중에 방향성 있는 요소(잔해·바람결)를 붙일 때 기준이 생긴다.
            transform.rotation = Quaternion.LookRotation(travelDir, Vector3.up);

            traveled = 0f;
            elapsed = 0f;
            stopped = false;
            running = true;

            // 스폰 지점에서 원점까지 선이 그어지지 않도록 항상 비우고 시작한다.
            for (int i = 0; i < swirls.Length; i++)
            {
                if (swirls[i] == null) continue;
                swirls[i].Clear(true);
                var em = swirls[i].emission;
                em.enabled = true;
                swirls[i].Play(true);
            }

            if (logLifecycle)
            {
                Debug.Log("[JcVortex] Play  origin=" + origin.ToString("F2") + "  dir=" + travelDir.ToString("F2"), this);
            }
        }

        /// <summary>스폰 위치. 소켓이 있으면 그 위치에 오프셋을 얹고, 없으면 컨텍스트가 준 위치를 쓴다.</summary>
        private Vector3 ResolveOrigin(SkillEffectContext ctx)
        {
            if (ctx == null) return transform.position + spawnOffset;

            Transform socket = ctx.SocketTransform;
            if (socket != null)
            {
                // ★TransformPoint 금지 — 소켓의 200배 스케일이 오프셋에 곱해진다. 회전만 적용한다.
                return socket.position + (keepAxisVertical ? spawnOffset : socket.rotation * spawnOffset);
            }
            return ctx.SpawnPosition + spawnOffset;
        }

        /// <summary>전진 방향. 시전자→타깃을 수평으로 눕힌다(회오리 축은 수직 유지).</summary>
        private Vector3 ResolveDirection(SkillEffectContext ctx, Vector3 origin)
        {
            Vector3 dir = Vector3.forward;

            if (ctx != null)
            {
                Transform target = ctx.PrimaryTarget != null ? ctx.PrimaryTarget.transform : null;
                if (target != null) dir = target.position - origin;
                else if (ctx.TargetPosition != Vector3.zero) dir = ctx.TargetPosition - origin;
                else if (ctx.Caster != null) dir = ctx.Caster.transform.forward;
            }

            dir.y = 0f;   // 회오리는 수직축을 유지한 채 바닥을 따라 나아간다
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            return dir.normalized;
        }

        private void Update()
        {
            if (!running) return;

            float dt = Time.deltaTime;
            elapsed += dt;

            if (!stopped && elapsed >= travelDelay && travelSpeed > 0f && traveled < travelDistance)
            {
                float step = Mathf.Min(travelSpeed * dt, travelDistance - traveled);
                transform.position += travelDir * step;
                traveled += step;
            }

            if (!stopped && elapsed >= duration)
            {
                StopEmitting();
            }
        }

        public bool Signal(SkillEffectContext ctx)
        {
            StopEmitting();
            return false;
        }

        public void Stop() => StopEmitting();

        private void StopEmitting()
        {
            if (stopped) return;
            EnsureRefs();
            stopped = true;

            // 방출만 끊고 이미 나온 입자·나선 줄기는 수명대로 사라지게 둔다.
            float life = fadeOutExtraSeconds;
            for (int i = 0; i < swirls.Length; i++)
            {
                if (swirls[i] == null) continue;

                var em = swirls[i].emission;
                em.enabled = false;
                swirls[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);

                var main = swirls[i].main;
                // trails.lifetime은 입자 수명에 대한 배율(1 고정 정책).
                float psLife = main.startLifetime.constantMax;
                float tail = psLife + psLife * swirls[i].trails.lifetime.constantMax;
                if (tail > life) life = tail;
            }

            if (logLifecycle)
            {
                Debug.Log("[JcVortex] Stop  " + life.ToString("F2") + "초 뒤 정리", this);
            }

            Destroy(gameObject, life + fadeOutExtraSeconds);
        }
    }
}
