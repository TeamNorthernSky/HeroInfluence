using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「프리즘 익스플로전」 연출 오케스트레이터 — 솔라 프리즘의 대형·단일·강화판.
    /// [차지] Play(caster,_): 전방에 대형 수정 1개 소환(팝) → 호버+자전+꼭지점 플레어+바닥광+빛 입자.
    /// [발사 시퀀스] Launch(targets): ①상승하며 상단 극점이 적 진형 중앙을 향해 수평으로 기움
    ///   (진동 램프업, 자전 가속) → ②본 트레일+나선 보조 트레일 2줄을 그리며 비행(접선 추적) →
    ///   ③진형 중앙 대폭발(FlareBombImpact 대형 2버스트+링) + 전체 연기 파티클 → 종료.
    /// 대상 배열은 호출자가 주입 — 탄착점 = 대상들의 평균 위치(진형 중앙).
    /// 자식 규약: PrismUnit(Crystal/Flare_0..3/GroundGlow/Embers/FlightTrail/Spiral_0..1)/Impact/Smoke.
    /// </summary>
    public class PrismExplosionVfx : VfxEffect
    {
        public event System.Action<Vector3> Impacted;
        public enum PartMode { Integrated, Charge, Flight }
        [Tooltip("Integrated는 기존 전체, Charge는 생성·대기, Flight는 발사·비행 부품입니다.")]
        [SerializeField] private PartMode partMode;
        [Tooltip("발사 신호에서 차징의 실제 위치를 이어받는 독립 비행 부품입니다.")]
        [SerializeField] private PrismExplosionVfx flightPartPrefab;
        [Tooltip("도착 시 생성하는 독립 폭발 부품입니다.")]
        [SerializeField] private FlareBombImpact impactPartPrefab;
        private PrismExplosionVfx flightPart;
        private IReadOnlyList<Transform> partTargets;
        private bool deferLaunch;
        public override void SetTargets(IReadOnlyList<VfxTarget> values)
        {
            var list = new List<Transform>();
            if (values != null) foreach (var value in values) if (value.anchor != null) list.Add(value.anchor);
            partTargets = list;
        }

        [Header("Layout")]
        [Tooltip("캐스터 전방 거리(m).")]
        [SerializeField] private float forwardOffset = 1.5f;
        [Tooltip("수정 중심 높이(m, 캐스터 기준).")]
        [SerializeField] private float hoverHeight = 1.1f;
        [Tooltip("수정 스케일(메시 원본 5.8m 기준 배율). 솔라 프리즘의 대형판.")]
        [SerializeField] private float prismScale = 0.4f;

        [Header("Motion")]
        [Tooltip("호버 진폭(m).")]
        [SerializeField] private float hoverAmp = 0.06f;
        [Tooltip("호버 주파수(Hz).")]
        [SerializeField] private float hoverFreq = 1.0f;
        [Tooltip("차지 자전 속도(도/초).")]
        [SerializeField] private float spinSpeed = 60f;
        [Tooltip("소환 팝 시간(초).")]
        [SerializeField] private float summonTime = 0.5f;

        [Header("Vertex Flare")]
        [Tooltip("벨트 꼭지점 로컬 반경(메시 로컬 단위). 큐브 유래=√2.")]
        [SerializeField] private float beltRadius = 1.414f;
        [Tooltip("벨트 꼭지점 로컬 높이(메시 로컬 단위).")]
        [SerializeField] private float beltLocalY = 0f;
        [Tooltip("벨트 기준 각도 오프셋(도). 큐브 유래=45.")]
        [SerializeField] private float beltAngleOffset = 45f;
        [Tooltip("플레어 점화 정렬 임계(0~1).")]
        [Range(0.5f, 0.999f)]
        [SerializeField] private float flareThreshold = 0.94f;
        [Tooltip("플레어 강도 곡선 지수.")]
        [Range(0.5f, 8f)]
        [SerializeField] private float flareSharp = 2.5f;
        [Tooltip("플레어 쿼드 월드 크기(m).")]
        [SerializeField] private float flareSize = 0.9f;

        [Header("Ground Glow")]
        [Tooltip("바닥광 쿼드 크기(m).")]
        [SerializeField] private float groundSize = 1.6f;
        [Tooltip("바닥광 상시 강도.")]
        [Range(0, 1)]
        [SerializeField] private float groundBase = 0.6f;
        [Tooltip("플레어 연동 맥동 강도.")]
        [Range(0, 1)]
        [SerializeField] private float groundPulse = 0.4f;
        [Tooltip("바닥광 높이 오프셋(m).")]
        [SerializeField] private float groundLift = 0.02f;

        [Header("Rise + Aim (발사 도입부)")]
        [Tooltip("상승·조준 시간(초). 서서히 떠오르며 수평으로 기운다.")]
        [SerializeField] private float riseTime = 0.8f;
        [Tooltip("추가 상승 높이(m).")]
        [SerializeField] private float riseHeight = 0.8f;
        [Tooltip("조준 진동 진폭(m). 발사 직전으로 갈수록 커진다.")]
        [SerializeField] private float tremorAmp = 0.05f;
        [Tooltip("조준 진동 주파수(Hz).")]
        [SerializeField] private float tremorFreq = 26f;
        [Tooltip("조준 중 자전 가속 배율(도달값).")]
        [SerializeField] private float aimSpinMul = 3f;

        [Header("Launch")]
        [Tooltip("비행 속력(m/s).")]
        [SerializeField] private float launchSpeed = 14f;
        [Tooltip("비행 중 자전 가속 배율.")]
        [SerializeField] private float launchSpinMul = 5f;
        [Tooltip("비행 포물선 정점 높이(m).")]
        [SerializeField] private float launchArc = 0.3f;
        [Tooltip("탄착점 높이(m, 진형 중앙 평균 위치 기준).")]
        [SerializeField] private float targetHeight = 0.9f;

        [Header("Spiral Trails")]
        [Tooltip("나선 보조 트레일 반경(m).")]
        [SerializeField] private float spiralRadius = 0.35f;
        [Tooltip("나선 회전 속도(회전/초).")]
        [SerializeField] private float spiralRate = 3f;

        [Header("Explosion")]
        [Tooltip("폭발 후 연기 잔류 대기(초). 이 시간 뒤 연출 종료.")]
        [SerializeField] private float smokeLinger = 1.2f;

        private Transform _unit;
        private Transform _crystal;
        private Renderer _crystalRenderer;
        private Renderer[] _flares = new Renderer[0];
        private Transform[] _flareTs = new Transform[0];
        private Renderer _ground;
        private ParticleSystem _embers;
        private TrailRenderer _flightTrail;
        private Transform[] _spirals = new Transform[0];
        private TrailRenderer[] _spiralTrails = new TrailRenderer[0];
        private FlareBombImpact _impact;
        private ParticleSystem _smoke;

        private Transform _origin;
        private bool _charging;
        private bool _sequencing;
        private float _t;
        private float _yaw;
        private MaterialPropertyBlock _mpb;
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");

        public bool IsCharging => _charging && !_sequencing;

        public float ForwardOffset { get => forwardOffset; set => forwardOffset = value; }
        public float HoverHeight { get => hoverHeight; set => hoverHeight = value; }
        public float PrismScale { get => prismScale; set => prismScale = value; }
        public float HoverAmp { get => hoverAmp; set => hoverAmp = value; }
        public float HoverFreq { get => hoverFreq; set => hoverFreq = value; }
        public float SpinSpeed { get => spinSpeed; set => spinSpeed = value; }
        public float SummonTime { get => summonTime; set => summonTime = value; }
        public float BeltRadius { get => beltRadius; set => beltRadius = value; }
        public float BeltLocalY { get => beltLocalY; set => beltLocalY = value; }
        public float BeltAngleOffset { get => beltAngleOffset; set => beltAngleOffset = value; }
        public float FlareThreshold { get => flareThreshold; set => flareThreshold = value; }
        public float FlareSharp { get => flareSharp; set => flareSharp = value; }
        public float FlareSize { get => flareSize; set => flareSize = value; }
        public float GroundSize { get => groundSize; set => groundSize = value; }
        public float GroundBase { get => groundBase; set => groundBase = value; }
        public float GroundPulse { get => groundPulse; set => groundPulse = value; }
        public float RiseTime { get => riseTime; set => riseTime = value; }
        public float RiseHeight { get => riseHeight; set => riseHeight = value; }
        public float TremorAmp { get => tremorAmp; set => tremorAmp = value; }
        public float TremorFreq { get => tremorFreq; set => tremorFreq = value; }
        public float AimSpinMul { get => aimSpinMul; set => aimSpinMul = value; }
        public float LaunchSpeed { get => launchSpeed; set => launchSpeed = value; }
        public float LaunchSpinMul { get => launchSpinMul; set => launchSpinMul = value; }
        public float LaunchArc { get => launchArc; set => launchArc = value; }
        public float TargetHeight { get => targetHeight; set => targetHeight = value; }
        public float SpiralRadius { get => spiralRadius; set => spiralRadius = value; }
        public float SpiralRate { get => spiralRate; set => spiralRate = value; }
        public float SmokeLinger { get => smokeLinger; set => smokeLinger = value; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            BuildCache();
            if (_unit != null) _unit.gameObject.SetActive(false);
        }

        private void BuildCache()
        {
            _unit = transform.Find("PrismUnit");
            if (_unit == null) return;
            _crystal = _unit.Find("Crystal");
            _crystalRenderer = _crystal != null ? _crystal.GetComponent<Renderer>() : null;
            var flares = new List<Renderer>();
            var flareTs = new List<Transform>();
            for (int k = 0; k < 4; k++)
            {
                var f = _unit.Find("Flare_" + k);
                if (f == null) continue;
                flares.Add(f.GetComponent<Renderer>());
                flareTs.Add(f);
            }
            _flares = flares.ToArray();
            _flareTs = flareTs.ToArray();
            var g = _unit.Find("GroundGlow");
            _ground = g != null ? g.GetComponent<Renderer>() : null;
            var e = _unit.Find("Embers");
            _embers = e != null ? e.GetComponent<ParticleSystem>() : null;
            var tr = _unit.Find("FlightTrail");
            _flightTrail = tr != null ? tr.GetComponent<TrailRenderer>() : null;
            var spirals = new List<Transform>();
            var spiralTrails = new List<TrailRenderer>();
            for (int s = 0; s < 2; s++)
            {
                var sp = _unit.Find("Spiral_" + s);
                if (sp == null) continue;
                spirals.Add(sp);
                spiralTrails.Add(sp.GetComponent<TrailRenderer>());
            }
            _spirals = spirals.ToArray();
            _spiralTrails = spiralTrails.ToArray();
            var im = transform.Find("Impact");
            _impact = im != null ? im.GetComponent<FlareBombImpact>() : null;
            var sm = transform.Find("Smoke");
            _smoke = sm != null ? sm.GetComponent<ParticleSystem>() : null;
        }

        public override void Play() { if (_origin != null) Play(_origin, _origin); }

        public override void Play(Transform origin, Transform target)
        {
            if (origin == null || _unit == null) return;
            StopAllCoroutines();
            _origin = origin;
            _t = 0f;
            _yaw = 0f;
            _charging = true;
            _sequencing = false;
            IsPlaying = true;
            if (_impact != null) _impact.Stop();
            if (_smoke != null) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_crystal != null) _crystal.gameObject.SetActive(true);
            SetTrails(false, true);
            _unit.gameObject.SetActive(true);
            if (_embers != null) { _embers.Clear(); _embers.Play(); }
            if (partMode == PartMode.Flight && !deferLaunch)
            {
                _t = summonTime; Update();
                Launch(partTargets != null && partTargets.Count > 0 ? partTargets : new[] { target });
            }
        }

        /// <summary>발사 시퀀스: 차지 중일 때만. 탄착점 = targets 평균 위치(진형 중앙).</summary>
        public void Launch(IReadOnlyList<Transform> targets)
        {
            if (!_charging || _sequencing) return;
            if (partMode == PartMode.Charge && flightPartPrefab != null)
            {
                flightPart = Instantiate(flightPartPrefab, transform);
                flightPart.PlaybackSpeed = PlaybackSpeed;
                flightPart.deferLaunch = true;
                flightPart.Play(_origin, _origin);
                flightPart._t = _t; flightPart._yaw = _yaw; flightPart.Update();
                flightPart._unit.SetPositionAndRotation(_unit.position, _unit.rotation);
                if (_crystal != null && flightPart._crystal != null)
                { flightPart._crystal.localScale = _crystal.localScale; flightPart._crystal.rotation = _crystal.rotation; }
                _charging = false; _sequencing = true;
                _unit.gameObject.SetActive(false);
                flightPart.OnFinished += OnFlightFinished;
                flightPart.Impacted += ForwardImpact;
                flightPart.Launch(targets);
                return;
            }
            _sequencing = true;
            StartCoroutine(Sequence(targets));
        }

        private void ForwardImpact(Vector3 position) => Impacted?.Invoke(position);
        private void OnFlightFinished(VfxEffect effect)
        { IsPlaying = false; _sequencing = false; RaiseFinished(); }
        public override void Stop()
        {
            if (flightPart != null)
            { flightPart.OnFinished -= OnFlightFinished; flightPart.Impacted -= ForwardImpact; flightPart.Stop(); Destroy(flightPart.gameObject); flightPart = null; }
            StopAllCoroutines();
            _charging = false;
            _sequencing = false;
            IsPlaying = false;
            if (_embers != null) _embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_smoke != null) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_impact != null) _impact.Stop();
            SetTrails(false, true);
            if (_unit != null) _unit.gameObject.SetActive(false);
        }

        private void SetTrails(bool emitting, bool clear)
        {
            if (_flightTrail != null)
            {
                _flightTrail.emitting = emitting;
                if (clear) _flightTrail.Clear();
            }
            foreach (var t in _spiralTrails)
            {
                if (t == null) continue;
                t.emitting = emitting;
                if (clear) t.Clear();
            }
        }

        private void SetOpacity(Renderer r, float v)
        {
            if (r == null) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OpacityID, v);
            r.SetPropertyBlock(_mpb);
        }

        private void OrientCrystal(Vector3 axis)
        {
            if (_crystal != null)
                _crystal.rotation = Quaternion.AngleAxis(_yaw, axis) * Quaternion.FromToRotation(Vector3.up, axis);
        }

        private Vector3 Center(IReadOnlyList<Transform> targets, Vector3 fallbackFrom, Vector3 fwd)
        {
            if (targets == null || targets.Count == 0)
                return fallbackFrom + new Vector3(fwd.x, 0f, fwd.z).normalized * 5f + Vector3.up * targetHeight;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var t in targets) { if (t == null) continue; sum += t.position; n++; }
            if (n == 0) return fallbackFrom + Vector3.forward * 5f;
            return sum / n + Vector3.up * targetHeight;
        }

        private IEnumerator Sequence(IReadOnlyList<Transform> targets)
        {
            Vector3 fwd = _origin != null ? _origin.forward : Vector3.forward;
            Vector3 startPos = _unit.position;
            Vector3 center = Center(targets, startPos, fwd);

            // ① 상승 + 조준(수평 눕기) + 진동 램프업 + 자전 가속
            float t = 0f;
            while (t < riseTime)
            {
                t += EffectDeltaTime;
                float e = Mathf.Clamp01(t / Mathf.Max(riseTime, 0.01f));
                float ease = e * e * (3f - 2f * e);   // smoothstep
                Vector3 pos = startPos + Vector3.up * (riseHeight * ease);
                Vector3 aimDir = (center - pos).normalized;
                Vector3 axis = Vector3.Slerp(Vector3.up, aimDir, ease);
                float trem = tremorAmp * e;
                Vector3 tremor = new Vector3(
                    Mathf.Sin(Time.time * tremorFreq * Mathf.PI * 2f) * trem,
                    Mathf.Sin(Time.time * tremorFreq * Mathf.PI * 2f * 1.31f + 1.7f) * trem,
                    Mathf.Sin(Time.time * tremorFreq * Mathf.PI * 2f * 0.77f + 3.9f) * trem);
                _unit.position = pos + tremor;
                _yaw += spinSpeed * (1f + (aimSpinMul - 1f) * e) * EffectDeltaTime;
                OrientCrystal(axis);
                foreach (var f in _flares) SetOpacity(f, 1f - e);
                if (_ground != null) SetOpacity(_ground, groundBase * (1f - e));
                yield return null;
            }
            foreach (var f in _flares) SetOpacity(f, 0f);
            if (_ground != null) SetOpacity(_ground, 0f);

            // ② 비행: 본 트레일 + 나선 보조 트레일 + 접선 추적
            if (_embers != null) _embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            SetTrails(true, true);
            Vector3 flyStart = _unit.position;
            float dist = Vector3.Distance(flyStart, center);
            float dur = dist / Mathf.Max(launchSpeed, 0.1f);
            float ft = 0f;
            Vector3 prev = flyStart;
            Vector3 flightDir = (center - flyStart).normalized;
            while (ft < dur)
            {
                ft += EffectDeltaTime;
                float e = Mathf.Clamp01(ft / dur);
                float e2 = e * e;
                Vector3 pos = Vector3.Lerp(flyStart, center, e2);
                pos.y += launchArc * 4f * e2 * (1f - e2);
                _unit.position = pos;
                Vector3 vel = pos - prev;
                prev = pos;
                if (vel.sqrMagnitude > 1e-8f) flightDir = vel.normalized;
                _yaw += spinSpeed * launchSpinMul * EffectDeltaTime;
                OrientCrystal(flightDir);
                // 나선 보조 트레일: 비행축 둘레 회전
                if (_spirals.Length > 0)
                {
                    Vector3 refUp = Mathf.Abs(flightDir.y) > 0.99f ? Vector3.forward : Vector3.up;
                    Vector3 b1 = Vector3.Cross(flightDir, refUp).normalized;
                    Vector3 b2 = Vector3.Cross(flightDir, b1);
                    for (int s = 0; s < _spirals.Length; s++)
                    {
                        float ang = ft * spiralRate * Mathf.PI * 2f + s * Mathf.PI;
                        _spirals[s].position = pos + (b1 * Mathf.Cos(ang) + b2 * Mathf.Sin(ang)) * spiralRadius;
                    }
                }
                yield return null;
            }

            // ③ 대폭발 + 전체 연기
            SetTrails(false, false);
            if (_crystal != null) _crystal.gameObject.SetActive(false);
            var impact = impactPartPrefab != null ? Instantiate(impactPartPrefab, transform) : _impact;
            if (impact != null) { impact.PlaybackSpeed = PlaybackSpeed; impact.Play(center); }
            Impacted?.Invoke(center);
            if (_smoke != null)
            {
                _smoke.transform.position = center;
                _smoke.Play();
            }
            if (impact != null)
                while (impact != null && impact.IsPlaying) yield return null;
            if (impactPartPrefab != null && impact != null) Destroy(impact.gameObject);
            if (smokeLinger > 0f) yield return new WaitForSeconds(smokeLinger / Mathf.Max(.01f, PlaybackSpeed));

            _unit.gameObject.SetActive(false);
            _charging = false;
            _sequencing = false;
            IsPlaying = false;
            RaiseFinished();
        }

        private void Update()
        {
            if (!_charging || _sequencing || _origin == null) return;
            _t += EffectDeltaTime;

            Vector3 fwd = _origin.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 basePos = _origin.position + fwd * forwardOffset;
            var cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : basePos + Vector3.back * 5f;

            float pop = 1f - Mathf.Pow(1f - Mathf.Clamp01(_t / Mathf.Max(summonTime, 0.01f)), 3f);
            float hover = hoverAmp * Mathf.Sin(Mathf.PI * 2f * hoverFreq * _t);
            Vector3 pos = basePos + Vector3.up * (hoverHeight + hover);
            _unit.position = pos;

            _yaw += spinSpeed * EffectDeltaTime;
            if (_crystal != null)
            {
                _crystal.rotation = Quaternion.Euler(0f, _yaw, 0f);
                _crystal.localScale = Vector3.one * (prismScale * pop);
            }
            SetOpacity(_crystalRenderer, pop);

            float maxInten = 0f;
            Vector3 toCam = camPos - pos; toCam.y = 0f; toCam = toCam.sqrMagnitude > 1e-4f ? toCam.normalized : Vector3.back;
            for (int k = 0; k < _flareTs.Length; k++)
            {
                float ang = (_yaw + beltAngleOffset + k * 90f) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                _flareTs[k].position = pos + (dir * beltRadius + Vector3.up * beltLocalY) * (prismScale * pop);
                float align = Vector3.Dot(dir, toCam);
                float inten = Mathf.Pow(Mathf.Clamp01((align - flareThreshold) / Mathf.Max(1f - flareThreshold, 1e-3f)), flareSharp);
                maxInten = Mathf.Max(maxInten, inten);
                _flareTs[k].localScale = new Vector3(flareSize, flareSize, 1f) * (0.7f + 0.5f * inten);
                SetOpacity(_flares[k], inten * pop);
            }

            if (_ground != null)
            {
                _ground.transform.position = new Vector3(basePos.x, _origin.position.y + groundLift, basePos.z);
                _ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                _ground.transform.localScale = new Vector3(groundSize, groundSize, 1f);
                SetOpacity(_ground, (groundBase + groundPulse * maxInten) * pop);
            }
        }
    }
}
