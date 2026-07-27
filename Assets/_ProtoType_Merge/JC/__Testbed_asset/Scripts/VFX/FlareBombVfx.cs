using System.Collections;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 스킬 연출 오케스트레이터.
    /// 기획서 스텝: 구체 소환(캐릭터 좌측 상단) → 적 1명에게 발사 → 발화(탄착) 이펙트.
    /// Play(caster, target): 차징(FlareBombOrbFull 성장) → 포물선 비행 → 탄착(FlareBombImpact) → 종료.
    /// 리스크(오발)는 호출자가 target에 아군 Transform을 넘기면 그대로 재현된다.
    /// </summary>
    public class FlareBombVfx : VfxEffect
    {
        [Header("References")]
        [Tooltip("차징 오브(FlareBombOrbFull 인스턴스). 비행체를 겸한다.")]
        [SerializeField] private GameObject chargeOrb;
        [Tooltip("탄착 이펙트(FlareBombImpact).")]
        [SerializeField] private FlareBombImpact impact;

        [Header("Flight FX (궤적·불씨)")]
        [Tooltip("비행 FX 루트(트레일+불씨). 오브의 자식이 아니라 별도 팔로워 — 탄착 후에도 잔불이 남는다.")]
        [SerializeField] private Transform flightFx;
        [Tooltip("비행 궤적 트레일.")]
        [SerializeField] private TrailRenderer flightTrail;
        [Tooltip("궤적을 따라 휘날리는 불씨 파티클(rate over distance).")]
        [SerializeField] private ParticleSystem emberParticles;
        [Tooltip("탄착 후 잔불(트레일 페이드·불씨 소멸) 유지 시간(초).")]
        [SerializeField] private float lingerTime = 0.5f;

        [Header("Summon")]
        [Tooltip("소환 위치(캐스터 로컬 오프셋). 기획서 = 캐릭터 좌측 상단.")]
        [SerializeField] private Vector3 summonOffset = new Vector3(-0.45f, 1.6f, 0f);
        [Tooltip("차징 유지 시간(초). 성장 완료 후 이 시간까지 대기 후 발사.")]
        [SerializeField] private float chargeDuration = 0.8f;
        [Tooltip("오브가 0→1 스케일로 자라는 시간(초).")]
        [SerializeField] private float chargeGrowTime = 0.25f;

        [Header("Flight")]
        [Tooltip("오브 전체 스케일 배율(차징·비행 공통). 1=오브 프리팹 원본 크기.")]
        [SerializeField] private float orbScale = 1f;
        [Tooltip("비행 속력(m/s). duration = 거리/speed.")]
        [SerializeField] private float speed = 9f;
        [Tooltip("포물선 정점 높이(m).")]
        [SerializeField] private float arcHeight = 0.6f;
        [Tooltip("비행 중 오브 스케일 배율(차징 대비).")]
        [SerializeField] private float flightScale = 0.8f;
        [Tooltip("명중점 높이(m, 타깃 피벗 기준). 적 몸통 중앙에 맞춘다.")]
        [SerializeField] private float targetHeight = 0.8f;

        private Coroutine _co;
        private Vector3 _orbScale0 = Vector3.one;
        private bool _scaleCaptured;

        public float ChargeDuration { get => chargeDuration; set => chargeDuration = value; }
        public float ChargeGrowTime { get => chargeGrowTime; set => chargeGrowTime = value; }
        public float Speed { get => speed; set => speed = value; }
        public float ArcHeight { get => arcHeight; set => arcHeight = value; }
        public float FlightScale { get => flightScale; set => flightScale = value; }
        public float TargetHeight { get => targetHeight; set => targetHeight = value; }
        public Vector3 SummonOffset { get => summonOffset; set => summonOffset = value; }
        public float LingerTime { get => lingerTime; set => lingerTime = value; }
        public float OrbScale { get => orbScale; set => orbScale = value; }

        private void Awake()
        {
            CaptureOrbScale();
            if (chargeOrb != null) chargeOrb.SetActive(false);
            if (flightFx != null) flightFx.gameObject.SetActive(false);
        }

        private void CaptureOrbScale()
        {
            if (_scaleCaptured || chargeOrb == null) return;
            _orbScale0 = chargeOrb.transform.localScale;
            _scaleCaptured = true;
        }

        public override void Play(Transform origin, Transform target)
        {
            if (origin == null || target == null || chargeOrb == null) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Run(origin, target));
        }

        public override void Stop()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            if (chargeOrb != null)
            {
                chargeOrb.SetActive(false);
                chargeOrb.transform.localScale = _orbScale0;
            }
            if (impact != null) impact.Stop();
            if (flightTrail != null) { flightTrail.emitting = false; flightTrail.Clear(); }
            if (emberParticles != null) emberParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (flightFx != null) flightFx.gameObject.SetActive(false);
            IsPlaying = false;
        }

        private IEnumerator Run(Transform origin, Transform target)
        {
            IsPlaying = true;
            CaptureOrbScale();

            Vector3 startPos = origin.TransformPoint(summonOffset);
            var orb = chargeOrb.transform;
            orb.position = startPos;
            orb.localScale = Vector3.zero;
            chargeOrb.SetActive(true);

            // 비행 FX 준비(차징 중에는 침묵)
            if (flightFx != null)
            {
                flightFx.position = startPos;
                flightFx.gameObject.SetActive(true);
            }
            if (flightTrail != null) { flightTrail.emitting = false; flightTrail.Clear(); }
            if (emberParticles != null) emberParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 차징: 성장 후 유지
            float t = 0f;
            while (t < chargeDuration)
            {
                t += Time.deltaTime;
                float g = Mathf.Clamp01(t / Mathf.Max(chargeGrowTime, 0.01f));
                orb.localScale = _orbScale0 * orbScale * (1f - Mathf.Pow(1f - g, 3f));   // easeOutCubic
                yield return null;
            }

            // 비행: 포물선(발사 시점의 타깃 위치 고정) + 궤적·불씨 방출
            Vector3 endPos = target.position + Vector3.up * targetHeight;
            orb.localScale = _orbScale0 * orbScale * flightScale;
            if (flightTrail != null) { flightTrail.Clear(); flightTrail.emitting = true; }
            if (emberParticles != null) emberParticles.Play();
            float dist = Vector3.Distance(startPos, endPos);
            float dur = dist / Mathf.Max(speed, 0.01f);
            float ft = 0f;
            while (ft < dur)
            {
                ft += Time.deltaTime;
                float u = Mathf.Clamp01(ft / dur);
                Vector3 pos = Vector3.Lerp(startPos, endPos, u);
                pos.y += arcHeight * 4f * u * (1f - u);
                orb.position = pos;
                if (flightFx != null) flightFx.position = pos;
                yield return null;
            }

            // 탄착 — 트레일·불씨는 방출만 멈추고 잔불이 사그라들게 둔다
            chargeOrb.SetActive(false);
            orb.localScale = _orbScale0;
            if (flightTrail != null) flightTrail.emitting = false;
            if (emberParticles != null) emberParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (impact != null)
            {
                impact.Play(endPos);
                while (impact.IsPlaying) yield return null;
            }
            if (lingerTime > 0f) yield return new WaitForSeconds(lingerTime);
            if (flightFx != null) flightFx.gameObject.SetActive(false);

            IsPlaying = false;
            _co = null;
            RaiseFinished();
        }
    }
}
