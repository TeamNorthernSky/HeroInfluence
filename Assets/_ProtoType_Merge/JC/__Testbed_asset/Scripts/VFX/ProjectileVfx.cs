using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 발사체 구체 VFX. 좌표(생성/목표)는 호출자가 주입, 거동(포물선 비행·도착 버스트)은 이 오브젝트가 소유.
    /// Show(pos): 정지 표시 → Launch(end): 포물선 비행 → 도착 시 1.5배 확대+가산 페이드 후 소멸.
    /// 코어는 2오브젝트 분리(CoreInner=스파이키 / RimShell=매끈 외곽선). 스타버스트 파티클 + 트레일이 궤적을 그림.
    /// </summary>
    public class ProjectileVfx : VfxEffect
    {
        [Header("References")]
        [Tooltip("크기 스케일 대상(CoreInner+RimShell을 담는 부모). 비우면 첫 fadeRenderer의 부모 사용.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("표시/가산 페이드 대상 렌더러들(CoreInner, RimShell).")]
        [SerializeField] private Renderer[] fadeRenderers;
        [Tooltip("함께 재생/정지할 파티클들(Sparkles, SpikeBurst 등).")]
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private TrailRenderer trail;

        [Header("Size")]
        [Tooltip("월드 지름(m). 차징 손 구체와 동일하게 0.11 권장")]
        [SerializeField] private float worldSize = 0.11f;

        [Header("Flight (속력 기반)")]
        [Tooltip("비행 속력(m/s). duration = 거리/speed")]
        [SerializeField] private float speed = 6f;
        [Tooltip("포물선 정점 높이(m)")]
        [SerializeField] private float arcHeight = 1.2f;

        [Header("Trail")]
        [Tooltip("궤적 트레일 유지 시간(초). 속도가 빠를수록 크게 잡아야 궤적이 끊기지 않고 이어짐.")]
        [SerializeField] private float trailTime = 0.4f;
        [Tooltip("켜면 trailTime을 무시하고 '트레일 길이(월드 m) / 속도'로 자동 계산.")]
        [SerializeField] private bool trailAutoBySpeed = false;
        [Tooltip("trailAutoBySpeed일 때 목표 궤적 길이(월드 m).")]
        [SerializeField] private float trailLengthWorld = 2.5f;

        [Header("Arrival Burst")]
        [SerializeField] private float burstScaleMul = 1.5f;
        [SerializeField] private float burstDuration = 0.3f;

        private enum State { Idle, Shown, Flying, Bursting }
        private State _state = State.Idle;
        private Vector3 _start, _end;
        private float _flightT, _flightDur, _burstT;
        private MaterialPropertyBlock _mpb;
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private Transform ScaleTarget => visualRoot != null ? visualRoot
            : (fadeRenderers != null && fadeRenderers.Length > 0 && fadeRenderers[0] != null ? fadeRenderers[0].transform.parent : transform);

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            Hide();
        }

        public void Show(Vector3 worldPos)
        {
            transform.position = worldPos;
            SetWorldSize(worldSize);
            SetFade(1f);
            SetRenderers(true);
            if (particleSystems != null) foreach (var ps in particleSystems) if (ps) { ps.Clear(); ps.Play(); }
            if (trail) { trail.Clear(); trail.emitting = false; }
            _state = State.Shown;
            IsPlaying = true;
        }

        public void Launch(Vector3 end)
        {
            _start = transform.position;
            _end = end;
            float dist = Vector3.Distance(_start, _end);
            _flightDur = speed > 0.01f ? dist / speed : 0.01f;
            _flightT = 0f;
            if (trail)
            {
                trail.time = (trailAutoBySpeed && speed > 0.01f) ? Mathf.Max(0.02f, trailLengthWorld / speed) : trailTime;
                trail.Clear();
                trail.emitting = true;
            }
            _state = State.Flying;
            IsPlaying = true;
        }

        public override void Play(Transform origin, Transform target)
        {
            Show(origin.position);
            Launch(target.position);
        }

        public override void Stop() => Hide();

        private void Hide()
        {
            SetRenderers(false);
            if (particleSystems != null) foreach (var ps in particleSystems) if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (trail) { trail.emitting = false; trail.Clear(); }
            SetFade(1f);
            _state = State.Idle;
            IsPlaying = false;
        }

        private void Update()
        {
            if (_state == State.Flying)
            {
                _flightT += Time.deltaTime / _flightDur;
                float t = Mathf.Clamp01(_flightT);
                Vector3 p = Vector3.Lerp(_start, _end, t);
                p.y += arcHeight * 4f * t * (1f - t);
                transform.position = p;
                if (t >= 1f)
                {
                    _state = State.Bursting;
                    _burstT = 0f;
                    if (trail) trail.emitting = false;
                    if (particleSystems != null) foreach (var ps in particleSystems) if (ps) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
            else if (_state == State.Bursting)
            {
                _burstT += Time.deltaTime;
                float k = burstDuration > 0f ? Mathf.Clamp01(_burstT / burstDuration) : 1f;
                SetWorldSize(worldSize * Mathf.Lerp(1f, burstScaleMul, k));
                SetFade(1f - k);
                if (k >= 1f)
                {
                    Hide();
                    RaiseFinished();
                }
            }
        }

        private void SetRenderers(bool on)
        {
            if (fadeRenderers == null) return;
            foreach (var r in fadeRenderers) if (r) r.enabled = on;
        }

        private void SetWorldSize(float ws)
        {
            var tr = ScaleTarget;
            float parentScale = tr.parent ? Mathf.Max(1e-6f, tr.parent.lossyScale.x) : 1f;
            tr.localScale = Vector3.one * (ws / parentScale);
        }

        private void SetFade(float v)
        {
            if (fadeRenderers == null) return;
            foreach (var r in fadeRenderers)
            {
                if (!r) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(FadeMulID, v);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
