using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「솔라 프리즘」 연출 오케스트레이터.
    /// [차지] Play(caster,_): 전방에 크리스탈 N개 소환(스태거 팝) → 호버+자전+빛 입자,
    ///        벨트 꼭지점 카메라 정렬 시 스타 플레어 점멸, 바닥광 연동 맥동. Launch까지 유지.
    /// [발사] Launch(targets): 유닛별 스태거로 가속 자전하며 대상에게 직진(ease-in) →
    ///        개별 폭발(FlareBombImpact 재사용: 버스트+지면 링) → 전원 완료 시 종료.
    /// 대상 배열은 호출자가 주입(실전=스킬 시스템이 적 열 전달, 테스트베드=트리거 스캔).
    /// 리스크(급발진)의 "피해 미적용"은 로직 몫 — 연출은 동일.
    /// 유닛 자식 규약: Crystal/Flare_0..3/GroundGlow/Embers/Impact(ImpactBurst+ImpactRing).
    /// </summary>
    public class SolarPrismVfx : VfxEffect
    {
        public event System.Action<Vector3> Impacted;
        public enum PartMode { Integrated, Charge, Flight }
        [Tooltip("Integrated는 기존 전체, Charge는 생성·대기, Flight는 비행만 독립 재생합니다.")]
        [SerializeField] private PartMode partMode;
        [Tooltip("차징 부품이 발사 신호를 받으면 생성할 독립 비행 부품입니다.")]
        [SerializeField] private SolarPrismVfx flightPartPrefab;
        [Tooltip("대상별 도착 시 호출할 독립 착탄 부품입니다. 비우면 기존 내장 효과를 씁니다.")]
        [SerializeField] private FlareBombImpact impactPartPrefab;
        private SolarPrismVfx flightPart;
        private IReadOnlyList<Transform> partTargets;
        private bool deferLaunch;
        public override void SetTargets(IReadOnlyList<VfxTarget> values)
        {
            var list = new List<Transform>();
            if (values != null) foreach (var value in values) if (value.anchor != null) list.Add(value.anchor);
            partTargets = list;
        }

        [Header("References")]
        [Tooltip("프리즘 유닛 루트들(최대 개수만큼 배치, unitCount로 활성 수 제어).")]
        [SerializeField] private Transform[] units;

        [Header("Layout")]
        [Tooltip("활성 프리즘 수. 기획: 선택 열 적의 수만큼(테스트베드 기본 3).")]
        [Range(1, 5)]
        [SerializeField] private int unitCount = 3;
        [Tooltip("유닛 간 가로 간격(m).")]
        [SerializeField] private float spacing = 0.95f;
        [Tooltip("캐스터 전방 거리(m).")]
        [SerializeField] private float forwardOffset = 1.3f;
        [Tooltip("프리즘 중심 높이(m, 캐스터 기준).")]
        [SerializeField] private float hoverHeight = 0.95f;
        [Tooltip("프리즘 전체 스케일(메시 원본 높이 5.8m 기준 배율).")]
        [SerializeField] private float prismScale = 0.16f;

        [Header("Motion")]
        [Tooltip("호버 진폭(m).")]
        [SerializeField] private float hoverAmp = 0.07f;
        [Tooltip("호버 주파수(Hz).")]
        [SerializeField] private float hoverFreq = 1.1f;
        [Tooltip("자전 속도(도/초).")]
        [SerializeField] private float spinSpeed = 80f;
        [Tooltip("유닛별 자전 속도 지터(비율). 기계적 동기 회피.")]
        [Range(0, 0.5f)]
        [SerializeField] private float spinJitter = 0.15f;

        [Header("Summon")]
        [Tooltip("유닛 1개 소환 팝 시간(초).")]
        [SerializeField] private float summonTime = 0.35f;
        [Tooltip("유닛 간 소환 시차(초).")]
        [SerializeField] private float summonStagger = 0.12f;

        [Header("Vertex Flare")]
        [Tooltip("벨트 꼭지점 로컬 반경(메시 로컬 단위). 큐브 유래 메시=대각 √2.")]
        [SerializeField] private float beltRadius = 1.414f;
        [Tooltip("벨트 꼭지점 로컬 높이(메시 로컬 단위).")]
        [SerializeField] private float beltLocalY = 0f;
        [Tooltip("벨트 꼭지점 기준 각도 오프셋(도). 큐브 유래=45.")]
        [SerializeField] private float beltAngleOffset = 45f;
        [Tooltip("플레어 점화 정렬 임계(0~1, cos 기준). 클수록 정면 정렬 순간에만 점화.")]
        [Range(0.5f, 0.999f)]
        [SerializeField] private float flareThreshold = 0.94f;
        [Tooltip("플레어 강도 곡선 지수. 클수록 날카롭게 점멸.")]
        [Range(0.5f, 8f)]
        [SerializeField] private float flareSharp = 2.5f;
        [Tooltip("플레어 쿼드 월드 크기(m).")]
        [SerializeField] private float flareSize = 0.55f;

        [Header("Ground Glow")]
        [Tooltip("바닥광 쿼드 크기(m).")]
        [SerializeField] private float groundSize = 0.9f;
        [Tooltip("바닥광 상시 강도.")]
        [Range(0, 1)]
        [SerializeField] private float groundBase = 0.55f;
        [Tooltip("플레어 연동 맥동 강도.")]
        [Range(0, 1)]
        [SerializeField] private float groundPulse = 0.45f;
        [Tooltip("바닥광 높이 오프셋(m, 캐스터 발밑 기준. z-fight 방지).")]
        [SerializeField] private float groundLift = 0.02f;

        [Header("Launch (발사·폭발)")]
        [Tooltip("발사 속력(m/s).")]
        [SerializeField] private float launchSpeed = 12f;
        [Tooltip("유닛 간 발사 시차(초).")]
        [SerializeField] private float launchStagger = 0.08f;
        [Tooltip("발사 중 자전 가속 배율.")]
        [SerializeField] private float launchSpinMul = 3f;
        [Tooltip("비행 포물선 정점 높이(m). 0=직선.")]
        [SerializeField] private float launchArc = 0.2f;
        [Tooltip("명중점 높이(m, 대상 피벗 기준).")]
        [SerializeField] private float targetHeight = 0.8f;
        [Tooltip("탄두 정렬 블렌드 구간(비행 진행도 0~1). 이 구간 동안 직립→진행 방향으로 기운다.")]
        [Range(0.02f, 0.6f)]
        [SerializeField] private float aimBlend = 0.18f;

        private class Unit
        {
            public Transform root;
            public Transform crystal;
            public Renderer crystalRenderer;
            public Renderer[] flares;
            public Transform[] flareTs;
            public Renderer ground;
            public ParticleSystem embers;
            public FlareBombImpact impact;
            public TrailRenderer trail;
            public float spinMul;
            public float phase;
            public float yaw;
            public bool launched;
        }

        private readonly List<Unit> _units = new List<Unit>();
        private Transform _origin;
        private bool _charging;
        private bool _launching;
        private float _t;
        private MaterialPropertyBlock _mpb;
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");

        /// <summary>차지 유지 중(발사 전) 여부 — 트리거가 발사 타이밍 판단에 사용.</summary>
        public bool IsCharging => _charging && !_launching;

        public int UnitCount { get => unitCount; set => unitCount = Mathf.Clamp(value, 1, units != null ? units.Length : 1); }
        public float Spacing { get => spacing; set => spacing = value; }
        public float ForwardOffset { get => forwardOffset; set => forwardOffset = value; }
        public float HoverHeight { get => hoverHeight; set => hoverHeight = value; }
        public float PrismScale { get => prismScale; set => prismScale = value; }
        public float HoverAmp { get => hoverAmp; set => hoverAmp = value; }
        public float HoverFreq { get => hoverFreq; set => hoverFreq = value; }
        public float SpinSpeed { get => spinSpeed; set => spinSpeed = value; }
        public float SpinJitter { get => spinJitter; set => spinJitter = value; }
        public float SummonTime { get => summonTime; set => summonTime = value; }
        public float SummonStagger { get => summonStagger; set => summonStagger = value; }
        public float BeltRadius { get => beltRadius; set => beltRadius = value; }
        public float BeltLocalY { get => beltLocalY; set => beltLocalY = value; }
        public float BeltAngleOffset { get => beltAngleOffset; set => beltAngleOffset = value; }
        public float FlareThreshold { get => flareThreshold; set => flareThreshold = value; }
        public float FlareSharp { get => flareSharp; set => flareSharp = value; }
        public float FlareSize { get => flareSize; set => flareSize = value; }
        public float GroundSize { get => groundSize; set => groundSize = value; }
        public float GroundBase { get => groundBase; set => groundBase = value; }
        public float GroundPulse { get => groundPulse; set => groundPulse = value; }
        public float LaunchSpeed { get => launchSpeed; set => launchSpeed = value; }
        public float LaunchStagger { get => launchStagger; set => launchStagger = value; }
        public float LaunchSpinMul { get => launchSpinMul; set => launchSpinMul = value; }
        public float LaunchArc { get => launchArc; set => launchArc = value; }
        public float TargetHeight { get => targetHeight; set => targetHeight = value; }
        public float AimBlend { get => aimBlend; set => aimBlend = value; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            BuildCache();
            foreach (var u in _units) u.root.gameObject.SetActive(false);
        }

        private void BuildCache()
        {
            _units.Clear();
            if (units == null) return;
            for (int i = 0; i < units.Length; i++)
            {
                var root = units[i];
                if (root == null) continue;
                var u = new Unit { root = root };
                u.crystal = root.Find("Crystal");
                u.crystalRenderer = u.crystal != null ? u.crystal.GetComponent<Renderer>() : null;
                var flares = new List<Renderer>();
                var flareTs = new List<Transform>();
                for (int k = 0; k < 4; k++)
                {
                    var f = root.Find("Flare_" + k);
                    if (f == null) continue;
                    flares.Add(f.GetComponent<Renderer>());
                    flareTs.Add(f);
                }
                u.flares = flares.ToArray();
                u.flareTs = flareTs.ToArray();
                var g = root.Find("GroundGlow");
                u.ground = g != null ? g.GetComponent<Renderer>() : null;
                var e = root.Find("Embers");
                u.embers = e != null ? e.GetComponent<ParticleSystem>() : null;
                var im = root.Find("Impact");
                u.impact = im != null ? im.GetComponent<FlareBombImpact>() : null;
                var tr = root.Find("FlightTrail");
                u.trail = tr != null ? tr.GetComponent<TrailRenderer>() : null;
                u.spinMul = 1f + (i * 0.5f % 1f - 0.5f) * 2f * spinJitter;
                u.phase = i * 2.399f;   // 황금각 위상차
                _units.Add(u);
            }
        }

        public override void Play() { if (_origin != null) Play(_origin, _origin); }

        public override void Play(Transform origin, Transform target)
        {
            if (origin == null) return;
            if (_mpb == null) { _mpb = new MaterialPropertyBlock(); BuildCache(); }
            StopAllCoroutines();
            _origin = origin;
            _t = 0f;
            _charging = true;
            _launching = false;
            IsPlaying = true;
            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                bool on = i < unitCount;
                u.launched = false;
                u.yaw = u.phase * Mathf.Rad2Deg;
                if (u.impact != null) u.impact.Stop();
                if (u.crystal != null) u.crystal.gameObject.SetActive(true);
                if (u.trail != null) { u.trail.emitting = false; u.trail.Clear(); }
                u.root.gameObject.SetActive(on);
                if (!on) continue;
                if (u.embers != null) { u.embers.Clear(); u.embers.Play(); }
            }
            if (partMode == PartMode.Flight && !deferLaunch)
            {
                _t = summonTime + summonStagger * unitCount;
                Update();
                Launch(partTargets != null && partTargets.Count > 0 ? partTargets : new[] { target });
            }
        }

        /// <summary>발사: 차지 중일 때만. targets[i % n]로 유닛-대상 매핑(실전=적 열 주입).</summary>
        public void Launch(IReadOnlyList<Transform> targets)
        {
            if (!_charging || _launching) return;
            if (partMode == PartMode.Charge && flightPartPrefab != null)
            {
                flightPart = Instantiate(flightPartPrefab, transform);
                flightPart.PlaybackSpeed = PlaybackSpeed;
                flightPart.UnitCount = targets != null && targets.Count > 0 ? targets.Count : unitCount;
                flightPart.deferLaunch = true;
                flightPart.Play(_origin, _origin);
                flightPart._t = _t;
                flightPart.Update();
                for (int i = 0; i < _units.Count && i < flightPart._units.Count; i++)
                {
                    var from = _units[i]; var to = flightPart._units[i];
                    to.root.SetPositionAndRotation(from.root.position, from.root.rotation);
                    to.yaw = from.yaw;
                    if (to.crystal != null && from.crystal != null)
                    { to.crystal.localScale = from.crystal.localScale; to.crystal.rotation = from.crystal.rotation; }
                }
                _charging = false; _launching = true;
                foreach (var u in _units) u.root.gameObject.SetActive(false);
                flightPart.OnFinished += OnFlightFinished;
                flightPart.Impacted += ForwardImpact;
                flightPart.Launch(targets);
                return;
            }
            _launching = true;
            StartCoroutine(LaunchAll(targets));
        }

        private void ForwardImpact(Vector3 position) => Impacted?.Invoke(position);
        private void OnFlightFinished(VfxEffect effect)
        {
            IsPlaying = false; _launching = false;
            RaiseFinished();
        }
        public override void Stop()
        {
            if (flightPart != null) { flightPart.OnFinished -= OnFlightFinished; flightPart.Impacted -= ForwardImpact; flightPart.Stop(); Destroy(flightPart.gameObject); flightPart = null; }
            StopAllCoroutines();
            _charging = false;
            _launching = false;
            IsPlaying = false;
            foreach (var u in _units)
            {
                if (u.embers != null) u.embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (u.impact != null) u.impact.Stop();
                if (u.trail != null) { u.trail.emitting = false; u.trail.Clear(); }
                u.root.gameObject.SetActive(false);
            }
        }

        private void SetOpacity(Renderer r, float v)
        {
            if (r == null) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OpacityID, v);
            r.SetPropertyBlock(_mpb);
        }

        private IEnumerator LaunchAll(IReadOnlyList<Transform> targets)
        {
            int n = Mathf.Min(unitCount, _units.Count);
            var cos = new List<Coroutine>();
            for (int i = 0; i < n; i++)
            {
                Transform tgt = (targets != null && targets.Count > 0) ? targets[i % targets.Count] : null;
                cos.Add(StartCoroutine(LaunchUnit(_units[i], tgt, i * launchStagger)));
            }
            foreach (var c in cos) yield return c;
            _charging = false;
            _launching = false;
            foreach (var u in _units) u.root.gameObject.SetActive(false);
            IsPlaying = false;
            RaiseFinished();
        }

        private IEnumerator LaunchUnit(Unit u, Transform tgt, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay / Mathf.Max(.01f, PlaybackSpeed));
            u.launched = true;

            // 차지 부속 정리(입자는 방출만 중단, 플레어·바닥광 소등) + 궤적 트레일 점화
            if (u.embers != null) u.embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var f in u.flares) SetOpacity(f, 0f);
            if (u.ground != null) SetOpacity(u.ground, 0f);
            if (u.trail != null) { u.trail.Clear(); u.trail.emitting = true; }

            Vector3 fwd = _origin != null ? _origin.forward : Vector3.forward;
            Vector3 start = u.root.position;
            Vector3 end = tgt != null ? tgt.position + Vector3.up * targetHeight
                                      : start + new Vector3(fwd.x, 0f, fwd.z).normalized * 4f;
            float dist = Vector3.Distance(start, end);
            float dur = dist / Mathf.Max(launchSpeed, 0.1f);
            float t = 0f;
            Vector3 prev = start;
            Vector3 flightDir = (end - start).normalized;
            while (t < dur)
            {
                t += EffectDeltaTime;
                float e = Mathf.Clamp01(t / dur);
                float e2 = e * e;   // ease-in 가속
                Vector3 pos = Vector3.Lerp(start, end, e2);
                pos.y += launchArc * 4f * e2 * (1f - e2);
                u.root.position = pos;
                Vector3 vel = pos - prev;
                prev = pos;
                if (vel.sqrMagnitude > 1e-8f) flightDir = vel.normalized;
                // 탄두 정렬: 상단 극점(+Y)=전면, 하단 극점=후면. 직립→진행 방향 블렌드 후 접선 추적.
                Vector3 axis = Vector3.Slerp(Vector3.up, flightDir, Mathf.Clamp01(e / Mathf.Max(aimBlend, 0.01f)));
                u.yaw += spinSpeed * u.spinMul * launchSpinMul * EffectDeltaTime;
                if (u.crystal != null)
                    u.crystal.rotation = Quaternion.AngleAxis(u.yaw, axis) * Quaternion.FromToRotation(Vector3.up, axis);
                yield return null;
            }

            // 개별 폭발 — 트레일은 방출만 멈추고 잔광이 사그라들게 둔다
            if (u.trail != null) u.trail.emitting = false;
            if (u.crystal != null) u.crystal.gameObject.SetActive(false);
            var impact = impactPartPrefab != null ? Instantiate(impactPartPrefab, transform) : u.impact;
            if (impact != null)
            {
                impact.PlaybackSpeed = PlaybackSpeed;
                impact.Play(end);
                Impacted?.Invoke(end);
                while (impact != null && impact.IsPlaying) yield return null;
                if (impactPartPrefab != null && impact != null) Destroy(impact.gameObject);
            }
        }

        private void Update()
        {
            if (!_charging || _origin == null) return;
            _t += EffectDeltaTime;
            float dt = EffectDeltaTime;

            Vector3 fwd = _origin.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 center = _origin.position + fwd * forwardOffset;
            var cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : center + Vector3.back * 5f;

            for (int i = 0; i < _units.Count && i < unitCount; i++)
            {
                var u = _units[i];
                if (u.launched) continue;   // 발사된 유닛은 코루틴이 구동

                // 소환 팝(스태거) + 배치
                float pop = 1f - Mathf.Pow(1f - Mathf.Clamp01((_t - i * summonStagger) / Mathf.Max(summonTime, 0.01f)), 3f);
                float hover = hoverAmp * Mathf.Sin(Mathf.PI * 2f * hoverFreq * _t + u.phase);
                Vector3 basePos = center + right * ((i - (unitCount - 1) * 0.5f) * spacing);
                Vector3 pos = basePos + Vector3.up * (hoverHeight + hover);
                u.root.position = pos;

                // 자전(누적 야각) + 스케일
                u.yaw += spinSpeed * u.spinMul * dt;
                if (u.crystal != null)
                {
                    u.crystal.rotation = Quaternion.Euler(0f, u.yaw, 0f);
                    u.crystal.localScale = Vector3.one * (prismScale * pop);
                }
                SetOpacity(u.crystalRenderer, pop);

                // 벨트 꼭지점 플레어: 카메라 정렬 순간 점멸
                float maxInten = 0f;
                Vector3 toCam = camPos - pos; toCam.y = 0f; toCam = toCam.sqrMagnitude > 1e-4f ? toCam.normalized : Vector3.back;
                for (int k = 0; k < u.flareTs.Length; k++)
                {
                    float ang = (u.yaw + beltAngleOffset + k * 90f) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    Vector3 vPos = pos + (dir * beltRadius + Vector3.up * beltLocalY) * (prismScale * pop);
                    u.flareTs[k].position = vPos;
                    float align = Vector3.Dot(dir, toCam);
                    float inten = Mathf.Pow(Mathf.Clamp01((align - flareThreshold) / Mathf.Max(1f - flareThreshold, 1e-3f)), flareSharp);
                    maxInten = Mathf.Max(maxInten, inten);
                    u.flareTs[k].localScale = new Vector3(flareSize, flareSize, 1f) * (0.7f + 0.5f * inten);
                    SetOpacity(u.flares[k], inten * pop);
                }

                // 바닥 반사광: 상시 + 플레어 맥동
                if (u.ground != null)
                {
                    u.ground.transform.position = new Vector3(basePos.x, _origin.position.y + groundLift, basePos.z);
                    u.ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    u.ground.transform.localScale = new Vector3(groundSize, groundSize, 1f);
                    SetOpacity(u.ground, (groundBase + groundPulse * maxInten) * pop);
                }
            }
        }
    }
}
