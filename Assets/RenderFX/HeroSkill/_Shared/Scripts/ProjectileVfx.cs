using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「아픈 거 다 날아가라」(HealSkill)의 발사체 원본.
    ///   LetsFightingLove(본발사/연쇄)·Taosenaiyo(유성)가 프리팹 복제+베이크로 재사용하는 공용 컴포넌트.
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

        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 발사(Show)마다 프리셋 전 항목을 읽고, 색·크기는 매 프레임 반영(비파괴 MPB).\n" +
                 "개발 루프: 시전 → 프리셋 조절 → 재시전 → … → 플레이 종료 후 「프리팹에 적용」으로 저장.\n" +
                 "★변종은 제 프리셋을 물 것 — 은백 프리팹에 금 프리셋을 꽂으면 색이 금으로 덮인다.")]
        [SerializeField] private ProjectileOrbPreset preset;
        [SerializeField] private bool livePreview = true;

        /// <summary>
        /// ★착탄 순간(도착점 터치) 발생 — 착지 오라처럼 <b>착탄에 반응하는 연출</b>은 이쪽을 구독한다.
        /// OnFinished 는 도착 버스트까지 끝난 뒤(착탄 +0.3초쯤)라 시그널로는 늦다.
        /// 비행 시간은 거리의 함수라 타이머로는 이 시점을 맞출 수 없다 — 모든 투사체 스킬 공통 규약(260805).
        /// </summary>
        public event System.Action<ProjectileVfx> OnImpact;

        private enum State { Idle, Shown, Flying, Bursting }
        private State _state = State.Idle;
        private Vector3 _start, _end;
        private float _flightT, _flightDur, _burstT;
        private float _fade = 1f;
        private MaterialPropertyBlock _mpb;
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        // 라이브 프리뷰 대상 캐시 — 이름으로 찾는다(프리팹 구조: Visual/CoreInner·RimShell + Sparkles·SpikeBurst·StarFlash)
        private Renderer _coreR, _rimR;
        private ParticleSystem _sparkles, _spikeBurst, _starFlash;
        private bool _lookedUp;

        private bool Live => livePreview && preset != null;

        /// <summary>호출자(스테퍼/큐 드라이버)가 시작점·탄착점을 읽어 가는 창구.</summary>
        public ProjectileOrbPreset Preset => preset;

        private Transform ScaleTarget => visualRoot != null ? visualRoot
            : (fadeRenderers != null && fadeRenderers.Length > 0 && fadeRenderers[0] != null ? fadeRenderers[0].transform.parent : transform);

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            Hide();
        }

        /// <summary>비행 튜닝값 런타임 주입(프리셋 라이브 프리뷰용 — TaoMeteorTuner 등).</summary>
        public void ApplyTuning(float newWorldSize, float newSpeed, float newArcHeight, float newTrailTime)
        {
            worldSize = newWorldSize;
            speed = newSpeed;
            arcHeight = newArcHeight;
            trailTime = newTrailTime;
        }

        public void Show(Vector3 worldPos)
        {
            if (Live) PullFromPreset();   // ★시전마다 최신 프리셋 값으로 — 「조절 → 재시전」 개발 루프의 핵심
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
            // 색·밝기는 비행 중에도 즉시 반영 — 저스티스 프리뷰와 같은 감각
            if (Live && IsPlaying) ApplyLookMpb();

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
                    OnImpact?.Invoke(this);
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

        // ── 런타임 프리뷰 ────────────────────────────────────────────

        private void LookupPreviewTargets()
        {
            if (_lookedUp) return;
            _lookedUp = true;
            if (fadeRenderers != null)
                foreach (var r in fadeRenderers)
                {
                    if (!r) continue;
                    if (r.name.Contains("Core")) _coreR = r;
                    else if (r.name.Contains("Rim")) _rimR = r;
                }
            _sparkles = FindPs("Sparkles");
            _spikeBurst = FindPs("SpikeBurst");
            _starFlash = FindPs("StarFlash");
        }

        private ParticleSystem FindPs(string childName)
        {
            var t = transform.Find(childName);
            return t ? t.GetComponent<ParticleSystem>() : null;
        }

        /// <summary>프리셋 전 항목을 인스턴스에 반영 — 에디터 「프리팹에 적용」과 같은 매핑, 대상만 런타임 인스턴스.</summary>
        private void PullFromPreset()
        {
            LookupPreviewTargets();
            var p = preset;
            var t = p.TransformSource;   // ★트랜스폼은 따름 규칙 적용(Alter → Basic)

            // 비행/크기/트레일/버스트
            worldSize = t.worldSize;
            speed = t.speed;
            arcHeight = t.arcHeight;
            trailTime = t.trailTime;
            trailAutoBySpeed = t.trailAutoBySpeed;
            trailLengthWorld = t.trailLengthWorld;
            burstScaleMul = t.burstScaleMul;
            burstDuration = t.burstDuration;

            // 코어/림 상대 크기
            if (_coreR) _coreR.transform.localScale = Vector3.one * t.coreSize;
            if (_rimR) _rimR.transform.localScale = Vector3.one * t.rimSize;

            // 파티클 구성(반짝임 / 방사 레이 / 스타)
            if (_sparkles)
            {
                var m = _sparkles.main;
                m.startSize = new ParticleSystem.MinMaxCurve(p.sparkleSizeMin, p.sparkleSizeMax);
                m.startLifetime = p.sparkleLifetime;
                var e = _sparkles.emission; e.rateOverTime = p.sparkleRate;
                var sh = _sparkles.shape; sh.radius = p.sparkleShapeRadius;
            }
            if (_spikeBurst)
            {
                var m = _spikeBurst.main;
                m.startSpeed = p.raySpeed;
                m.startLifetime = p.raySpeed > 0.01f ? p.rayReachRadius / p.raySpeed : 0.25f;
                m.startSize = new ParticleSystem.MinMaxCurve(p.raySizeMin, p.raySizeMax);
                var e = _spikeBurst.emission; e.rateOverTime = p.rayRate;
                var sh = _spikeBurst.shape; sh.radius = p.rayCenterSize;
                var rr = _spikeBurst.GetComponent<ParticleSystemRenderer>();
                if (rr) rr.lengthScale = p.rayLengthScale;
            }
            if (_starFlash)
            {
                var m = _starFlash.main; m.startSize = p.starSize; m.startLifetime = p.starLifetime;
                var e = _starFlash.emission; e.rateOverTime = p.starRate;
                var r = _starFlash.rotationOverLifetime;
                r.z = new ParticleSystem.MinMaxCurve(p.starSpinSpeed * Mathf.Deg2Rad);
            }
        }

        /// <summary>색·밝기·스파이크를 MPB 로 매 프레임 반영(비파괴 — 재질 자산은 「적용」 버튼이 굳힌다).</summary>
        private void ApplyLookMpb()
        {
            var p = preset;
            var c = p.chargeRef;   // ★구체 룩 공유 — 「거기 생겨난 구체가 날아간다」. 지정 시 차지 오브의 룩을 그대로.
            if (_coreR)
            {
                _coreR.GetPropertyBlock(_mpb);
                _mpb.SetColor("_CoreColor", c != null ? c.coreColor : p.coreColor);
                _mpb.SetFloat("_CoreIntensity", c != null ? c.coreIntensity : p.coreIntensity);
                _mpb.SetFloat("_CorePower", c != null ? c.corePower : p.corePower);
                _mpb.SetFloat("_CoreSpikeAmount", c != null ? c.coreSpikeAmount : p.coreSpikeAmount);
                _mpb.SetFloat("_RimSpikeAmount", c != null ? c.rimSpikeAmount : p.rimSpikeAmount);
                _mpb.SetFloat("_SpikeFreq", c != null ? c.spikeFreq : p.spikeFreq);
                _mpb.SetFloat("_SpikeSpeed", c != null ? c.spikeSpeed : p.spikeSpeed);
                _mpb.SetFloat("_SpikeSharp", c != null ? c.spikeSharp : p.spikeSharp);
                _mpb.SetFloat(FadeMulID, _fade);
                _coreR.SetPropertyBlock(_mpb);
            }
            if (_rimR)
            {
                _rimR.GetPropertyBlock(_mpb);
                _mpb.SetColor("_RimColor", c != null ? c.rimColor : p.rimColor);
                _mpb.SetFloat("_RimIntensity", c != null ? c.rimIntensity : p.rimIntensity);
                _mpb.SetFloat("_RimPower", c != null ? c.rimPower : p.rimPower);
                _mpb.SetFloat(FadeMulID, _fade);
                _rimR.SetPropertyBlock(_mpb);
            }
            SetPsColor(_sparkles, p.sparkleColor);
            SetPsColor(_spikeBurst, p.rayColor);
            SetPsColor(_starFlash, p.starColor);
        }

        private void SetPsColor(ParticleSystem ps, Color c)
        {
            if (!ps) return;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (!r) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorID, c);
            r.SetPropertyBlock(_mpb);
        }

        private void SetWorldSize(float ws)
        {
            var tr = ScaleTarget;
            float parentScale = tr.parent ? Mathf.Max(1e-6f, tr.parent.lossyScale.x) : 1f;
            tr.localScale = Vector3.one * (ws / parentScale);
        }

        private void SetFade(float v)
        {
            _fade = v;
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
