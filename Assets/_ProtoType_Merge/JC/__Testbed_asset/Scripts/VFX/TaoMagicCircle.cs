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
        [Range(0f, 1f)] [SerializeField] private float spreadGrow = 0.5f;

        [Header("색 (livePreview 시 preset 사용)")]
        [ColorUsage(true, true)] [SerializeField] private Color color = new Color(1f, 0.85f, 0.35f);
        [Range(0f, 8f)] [SerializeField] private float intensity = 2.2f;

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
        private static readonly int CenterGlowID = Shader.PropertyToID("_CenterGlow");
        private static readonly int CenterFalloffID = Shader.PropertyToID("_CenterFalloff");
        private static readonly int EdgeFadeID = Shader.PropertyToID("_EdgeFade");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private void Awake()
        {
            _mr = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            _mr.enabled = false;
            _playing = false;
        }

        public void SetPreset(TaoMagicCirclePreset p) => preset = p;

        public void Play(Vector3 center)
        {
            _center = center;
            if (livePreview && preset) PullFromPreset();
            _playing = true;
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
            if (livePreview && preset) PullFromPreset();
            Apply();
        }

        private void PullFromPreset()
        {
            radius = preset.radius;
            groundOffsetY = preset.groundOffsetY;
            spreadGrow = preset.spreadGrow;
            color = preset.color;
            intensity = preset.intensity;
        }

        private void Apply()
        {
            float grow = Mathf.Lerp(1f - spreadGrow, 1f, _envelope);
            float size = Mathf.Max(radius, 1e-4f) * 2f * grow;
            transform.localScale = new Vector3(size, size, 1f);
            transform.position = _center + Vector3.up * groundOffsetY;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힘

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mr.GetPropertyBlock(_mpb);
            if (livePreview && preset)
            {
                _mpb.SetColor(ColorID, color);
                _mpb.SetFloat(IntensityID, intensity);
                _mpb.SetFloat(Ring1ID, preset.ring1);
                _mpb.SetFloat(Ring2ID, preset.ring2);
                _mpb.SetFloat(Ring3ID, preset.ring3);
                _mpb.SetFloat(LineWidthID, preset.lineWidth);
                _mpb.SetFloat(LineSoftID, preset.lineSoft);
                _mpb.SetFloat(HexRadiusID, preset.hexRadius);
                _mpb.SetFloat(HexWidthID, preset.hexWidth);
                _mpb.SetFloat(SquareRadiusID, preset.squareRadius);
                _mpb.SetFloat(SquareWidthID, preset.squareWidth);
                _mpb.SetFloat(SatOrbitID, preset.satOrbit);
                _mpb.SetFloat(SatCountID, preset.satCount);
                _mpb.SetFloat(SatRadiusID, preset.satRadius);
                _mpb.SetFloat(SpokeCountID, preset.spokeCount);
                _mpb.SetFloat(SpokeInnerID, preset.spokeInner);
                _mpb.SetFloat(SpokeOuterID, preset.spokeOuter);
                _mpb.SetFloat(TickRadiusID, preset.tickRadius);
                _mpb.SetFloat(TickWidthID, preset.tickWidth);
                _mpb.SetFloat(TickCountID, preset.tickCount);
                _mpb.SetFloat(TickDutyID, preset.tickDuty);
                _mpb.SetFloat(RotInnerID, preset.rotInner);
                _mpb.SetFloat(RotOuterID, preset.rotOuter);
                _mpb.SetFloat(PulseAmpID, preset.pulseAmp);
                _mpb.SetFloat(PulseFreqID, preset.pulseFreq);
                _mpb.SetFloat(CenterGlowID, preset.centerGlow);
                _mpb.SetFloat(CenterFalloffID, preset.centerFalloff);
                _mpb.SetFloat(EdgeFadeID, preset.edgeFade);
            }
            _mpb.SetFloat(FadeMulID, _envelope);
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
