using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// Taosenaiyo 요소: 마법진 장판(바닥 쿼드, 셰이더 JC/VFX/TaoMagicCircle).
    /// 링/육망성/눈금/회전/펄스는 셰이더가 절차 처리. 이 컴포넌트는 크기·위치·퍼짐 팝·MPB 구동.
    /// ★마스터 페이드: 자체 상태머신 없이 오케스트레이터가 SetEnvelope 주입(HealGroundShine 동형).
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class TaoMagicCircle : MonoBehaviour
    {
        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영.")]
        [SerializeField] private TaoMagicCirclePreset preset;
        [SerializeField] private bool livePreview = true;

        [Header("크기 / 위치 (월드 m)")]
        [SerializeField] private float radius = 1.1f;
        [SerializeField] private float groundOffsetY = 0.02f;
        [Tooltip("전방 오프셋(m) — 수직 모드 전용(260807).")]
        [Range(0f, 5f)] [SerializeField] private float forwardOffset = 0f;

        [Header("등장 (선형보간, 260807 — livePreview 시 preset 사용)")]
        [Tooltip("등장 보간 시간(초). 0 = 즉시(기존 동작).")]
        [Range(0f, 2f)] [SerializeField] private float appearTime = 0f;
        [Range(0f, 1f)] [SerializeField] private float appearStartScale = 0.3f;
        [Range(0f, 1f)] [SerializeField] private float appearStartAlpha = 0f;
        private float _appearT;

        [Header("색 (livePreview 시 preset 사용)")]
        [ColorUsage(true, true)] [SerializeField] private Color color = new Color(1f, 0.85f, 0.35f);
        [Range(0f, 8f)] [SerializeField] private float intensity = 2.2f;

        [Header("배치 모드")]
        [Tooltip("★수직 모드(260807, 배리지 손앞 마법진) — 바닥에 눕히지 않고 SetFacing 방향을 바라보는 수직 배치.\n" +
                 "OFF = 기존 장판(바닥 수평). 부활 오라는 OFF 그대로.")]
        [SerializeField] private bool upright;
        private Vector3 _facing = Vector3.forward;

        /// <summary>수직 모드의 바라볼 방향(시전자 전방 등). 호출자가 Play 전후 아무 때나 넣는다.</summary>
        public void SetFacing(Vector3 f) { if (f.sqrMagnitude > 1e-6f) _facing = f.normalized; }

        /// <summary>수직 모드 토글 — 절차 조립(배리지)용.</summary>
        public void SetUpright(bool on) => upright = on;

        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private Vector3 _center;
        private float _envelope = 1f;
        private bool _playing;

        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int Ring1ID = Shader.PropertyToID("_Ring1");
        private static readonly int Ring2ID = Shader.PropertyToID("_Ring2");
        private static readonly int Ring3ID = Shader.PropertyToID("_Ring3");
        private static readonly int LineWidthID = Shader.PropertyToID("_LineWidth");
        private static readonly int LineSoftID = Shader.PropertyToID("_LineSoft");
        private static readonly int HexRadiusID = Shader.PropertyToID("_HexRadius");
        private static readonly int HexWidthID = Shader.PropertyToID("_HexWidth");
        private static readonly int SquareRadiusID = Shader.PropertyToID("_SquareRadius");
        private static readonly int SquareWidthID = Shader.PropertyToID("_SquareWidth");
        private static readonly int SatOrbitID = Shader.PropertyToID("_SatOrbit");
        private static readonly int SatCountID = Shader.PropertyToID("_SatCount");
        private static readonly int SatRadiusID = Shader.PropertyToID("_SatRadius");
        private static readonly int SpokeCountID = Shader.PropertyToID("_SpokeCount");
        private static readonly int SpokeInnerID = Shader.PropertyToID("_SpokeInner");
        private static readonly int SpokeOuterID = Shader.PropertyToID("_SpokeOuter");
        private static readonly int TickRadiusID = Shader.PropertyToID("_TickRadius");
        private static readonly int TickWidthID = Shader.PropertyToID("_TickWidth");
        private static readonly int TickCountID = Shader.PropertyToID("_TickCount");
        private static readonly int TickDutyID = Shader.PropertyToID("_TickDuty");
        private static readonly int RotInnerID = Shader.PropertyToID("_RotInner");
        private static readonly int RotOuterID = Shader.PropertyToID("_RotOuter");
        private static readonly int PulseAmpID = Shader.PropertyToID("_PulseAmp");
        private static readonly int PulseFreqID = Shader.PropertyToID("_PulseFreq");
        private static readonly int RingIntensityID = Shader.PropertyToID("_RingIntensity");
        private static readonly int HexIntensityID = Shader.PropertyToID("_HexIntensity");
        private static readonly int SquareIntensityID = Shader.PropertyToID("_SquareIntensity");
        private static readonly int SatIntensityID = Shader.PropertyToID("_SatIntensity");
        private static readonly int SpokeIntensityID = Shader.PropertyToID("_SpokeIntensity");
        private static readonly int TickIntensityID = Shader.PropertyToID("_TickIntensity");
        private static readonly int CenterGlowID = Shader.PropertyToID("_CenterGlow");
        private static readonly int CenterFalloffID = Shader.PropertyToID("_CenterFalloff");
        private static readonly int EdgeFadeID = Shader.PropertyToID("_EdgeFade");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private void Awake()
        {
            EnsureInit();
            if (!_playing) _mr.enabled = false;   // pre-Awake Play 가 켜둔 상태는 덮지 않는다
        }

        /// <summary>★지연 초기화 — Instantiate 중 루트 OnEnable→Play 가 자식 Awake 보다 먼저 와도 안전(VFX 부품 공통 규격).</summary>
        private void EnsureInit()
        {
            if (_mr != null) return;
            _mr = GetComponent<MeshRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
        }

        public void SetPreset(TaoMagicCirclePreset p) => preset = p;

        public void Play(Vector3 center)
        {
            EnsureInit();
            _center = center;
            if (livePreview && preset) PullFromPreset();
            _playing = true;
            _appearT = 0f;   // 등장 보간 재시작
            _mr.enabled = true;
            Apply();
        }

        public void Stop()
        {
            _playing = false;
            if (_mr) _mr.enabled = false;
        }

        public void SetEnvelope(float f) => _envelope = Mathf.Clamp01(f);

        private void Update()
        {
            if (!_playing) return;
            _appearT += Time.deltaTime;
            if (livePreview && preset) PullFromPreset();
            Apply();
        }

        private void PullFromPreset()
        {
            // ★라이브 따름(260807) — 형태·움직임은 TransformSource(Alter→Basic), 색·밝기만 자기 것.
            //   이거 없이는 「Alter 인스펙터는 잠겨 있는데 라이브는 잠긴 자기 값을 읽는」 모순이 생긴다.
            var t = preset.TransformSource;
            radius = t.radius;
            groundOffsetY = t.groundOffsetY;
            forwardOffset = t.forwardOffset;
            appearTime = t.appearTime;
            appearStartScale = t.appearStartScale;
            appearStartAlpha = t.appearStartAlpha;
            color = preset.color;
            intensity = preset.intensity;
        }

        private void Apply()
        {
            // ★등장 선형보간(260807) — 크기·알파가 초기값→1 로 등속 증가. appearTime 0 = 즉시.
            //   (구 spreadGrow 엔벨로프 팝은 등장 보간으로 대체·폐지 — 엔벨로프는 이제 알파만 쥔다)
            float appear01 = appearTime > 1e-4f ? Mathf.Clamp01(_appearT / appearTime) : 1f;
            float appearScale = Mathf.Lerp(appearStartScale, 1f, appear01);
            float appearAlpha = Mathf.Lerp(appearStartAlpha, 1f, appear01);

            float size = Mathf.Max(radius, 1e-4f) * 2f * appearScale;
            transform.localScale = new Vector3(size, size, 1f);
            if (upright)
            {
                // 수직 배치 — 손앞 마법진. groundOffsetY(위) + forwardOffset(전방·바라보는 방향)로 미세 조정.
                transform.position = _center + Vector3.up * groundOffsetY + _facing * forwardOffset;
                transform.rotation = Quaternion.LookRotation(_facing);
            }
            else
            {
                transform.position = _center + Vector3.up * groundOffsetY;
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힘
            }

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mr.GetPropertyBlock(_mpb);
            if (livePreview && preset)
            {
                var t = preset.TransformSource;   // ★따름 — 문양·회전·펄스는 Basic 정본
                _mpb.SetColor(ColorID, color);
                _mpb.SetFloat(IntensityID, intensity);
                _mpb.SetFloat(Ring1ID, t.ring1);
                _mpb.SetFloat(Ring2ID, t.ring2);
                _mpb.SetFloat(Ring3ID, t.ring3);
                _mpb.SetFloat(LineWidthID, t.lineWidth);
                _mpb.SetFloat(LineSoftID, t.lineSoft);
                _mpb.SetFloat(HexRadiusID, t.hexRadius);
                _mpb.SetFloat(HexWidthID, t.hexWidth);
                _mpb.SetFloat(SquareRadiusID, t.squareRadius);
                _mpb.SetFloat(SquareWidthID, t.squareWidth);
                _mpb.SetFloat(SatOrbitID, t.satOrbit);
                _mpb.SetFloat(SatCountID, t.satCount);
                _mpb.SetFloat(SatRadiusID, t.satRadius);
                _mpb.SetFloat(SpokeCountID, t.spokeCount);
                _mpb.SetFloat(SpokeInnerID, t.spokeInner);
                _mpb.SetFloat(SpokeOuterID, t.spokeOuter);
                _mpb.SetFloat(TickRadiusID, t.tickRadius);
                _mpb.SetFloat(TickWidthID, t.tickWidth);
                _mpb.SetFloat(TickCountID, t.tickCount);
                _mpb.SetFloat(TickDutyID, t.tickDuty);
                _mpb.SetFloat(RotInnerID, t.rotInner);
                _mpb.SetFloat(RotOuterID, t.rotOuter);
                // ★요소별 밝기(260807) — 문양 배분은 형태 취급 = 따름(t).
                _mpb.SetFloat(RingIntensityID, t.ringIntensity);
                _mpb.SetFloat(HexIntensityID, t.hexIntensity);
                _mpb.SetFloat(SquareIntensityID, t.squareIntensity);
                _mpb.SetFloat(SatIntensityID, t.satIntensity);
                _mpb.SetFloat(SpokeIntensityID, t.spokeIntensity);
                _mpb.SetFloat(TickIntensityID, t.tickIntensity);
                // ★중심 광채(260807 재편) — 밝기는 변종 자유(preset), 퍼짐·깜빡임은 따름(t).
                //   퍼짐(0~1, 값↑=넓게)은 셰이더 감쇠 지수로 역매핑, 주기(초)는 rad/s 로 환산.
                _mpb.SetFloat(PulseAmpID, t.centerPulseAmp);
                _mpb.SetFloat(PulseFreqID, (2f * Mathf.PI) / Mathf.Max(t.centerPulsePeriod, 0.1f));
                _mpb.SetFloat(CenterGlowID, preset.centerGlow);
                _mpb.SetFloat(CenterFalloffID, Mathf.Lerp(8f, 0.5f, t.centerSpread));
                _mpb.SetFloat(EdgeFadeID, t.edgeFade);
            }
            _mpb.SetFloat(FadeMulID, _envelope * appearAlpha);   // 엔벨로프 × 등장 알파(선형)
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
