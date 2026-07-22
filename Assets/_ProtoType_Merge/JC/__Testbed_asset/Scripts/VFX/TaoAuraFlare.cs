using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// Taosenaiyo 요소: 발산 오라(초사이언풍 콘 셸, 셰이더 JC/VFX/TaoAuraFlare).
    /// 실린더 메시에 버텍스 플레어(위로 벌어짐)+펜선 스트릭은 셰이더가 처리.
    /// 이 컴포넌트는 크기·위치·팝·MPB 구동. 마스터 페이드는 오케스트레이터가 SetEnvelope 주입(HealAuraGlow 동형).
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class TaoAuraFlare : MonoBehaviour
    {
        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영.")]
        [SerializeField] private TaoAuraFlarePreset preset;
        [SerializeField] private bool livePreview = true;

        [Header("크기 / 위치 (월드 m)")]
        [Tooltip("바닥 지름(m). 상단은 플레어만큼 더 벌어짐")]
        [SerializeField] private float width = 1.0f;
        [Tooltip("기둥 높이(m)")]
        [SerializeField] private float height = 2.2f;
        [SerializeField] private float groundOffsetY = 0.02f;
        [Tooltip("페이드인 동안 커지는 팝 정도")]
        [Range(0f, 1f)] [SerializeField] private float riseGrow = 0.35f;

        [Header("색 (livePreview 시 preset 사용)")]
        [ColorUsage(true, true)] [SerializeField] private Color baseColor = new Color(1f, 0.82f, 0.3f);
        [Range(0f, 8f)] [SerializeField] private float baseIntensity = 1.2f;
        [ColorUsage(true, true)] [SerializeField] private Color lineColor = new Color(1f, 0.7f, 0.15f);
        [Range(0f, 10f)] [SerializeField] private float lineIntensity = 3.2f;

        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private Vector3 _center;
        private float _envelope = 1f;
        private bool _playing;

        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int LineColorID = Shader.PropertyToID("_LineColor");
        private static readonly int LineIntensityID = Shader.PropertyToID("_LineIntensity");
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
        private static readonly int FlareID = Shader.PropertyToID("_Flare");
        private static readonly int FlareCurveID = Shader.PropertyToID("_FlareCurve");
        private static readonly int FlareUpID = Shader.PropertyToID("_FlareUp");
        private static readonly int BaseBoostID = Shader.PropertyToID("_BaseBoost");
        private static readonly int YExtentID = Shader.PropertyToID("_YExtent");
        private static readonly int BottomFadeID = Shader.PropertyToID("_BottomFade");
        private static readonly int VerticalBiasID = Shader.PropertyToID("_VerticalBias");
        private static readonly int TopMinID = Shader.PropertyToID("_TopMin");
        private static readonly int TopMaxID = Shader.PropertyToID("_TopMax");
        private static readonly int TopSoftID = Shader.PropertyToID("_TopSoft");
        private static readonly int TopNoiseScaleID = Shader.PropertyToID("_TopNoiseScale");
        private static readonly int TopNoiseSpeedID = Shader.PropertyToID("_TopNoiseSpeed");
        private static readonly int FacePowerID = Shader.PropertyToID("_FacePower");
        private static readonly int LineCountID = Shader.PropertyToID("_LineCount");
        private static readonly int LineWidthID = Shader.PropertyToID("_LineWidth");
        private static readonly int LineSharpID = Shader.PropertyToID("_LineSharp");
        private static readonly int DashFreqID = Shader.PropertyToID("_DashFreq");
        private static readonly int DashDutyID = Shader.PropertyToID("_DashDuty");
        private static readonly int ScrollSpeedID = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int SprayIntensityID = Shader.PropertyToID("_SprayIntensity");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private void Awake()
        {
            _mr = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            var mf = GetComponent<MeshFilter>();
            if (mf) mf.sharedMesh = VfxShellMesh.Get();   // 세로 분할 셸 — 플레어 곡률이 실제로 보이게
            _mr.enabled = false;
            _playing = false;
        }

        public void SetPreset(TaoAuraFlarePreset p) => preset = p;

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
            width = preset.width;
            height = preset.height;
            groundOffsetY = preset.groundOffsetY;
            riseGrow = preset.riseGrow;
            baseColor = preset.baseColor;
            baseIntensity = preset.baseIntensity;
            lineColor = preset.lineColor;
            lineIntensity = preset.lineIntensity;
        }

        private void Apply()
        {
            float grow = Mathf.Lerp(1f - riseGrow, 1f, _envelope);
            float effH = Mathf.Max(height, 1e-4f) * grow;
            transform.localScale = new Vector3(width, effH * 0.5f, width);   // 유니티 실린더 y 하프익스텐트=1
            transform.position = _center + Vector3.up * (groundOffsetY + effH * 0.5f);
            transform.rotation = Quaternion.identity;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mr.GetPropertyBlock(_mpb);
            if (livePreview && preset)
            {
                _mpb.SetColor(ColorID, baseColor);
                _mpb.SetFloat(IntensityID, baseIntensity);
                _mpb.SetColor(LineColorID, lineColor);
                _mpb.SetFloat(LineIntensityID, lineIntensity);
                _mpb.SetFloat(OpacityID, preset.opacity);
                _mpb.SetFloat(FlareID, preset.flare);
                _mpb.SetFloat(FlareCurveID, preset.flareCurve);
                _mpb.SetFloat(FlareUpID, 0f);            // 본 오라 = 벨 고정
                _mpb.SetFloat(SprayIntensityID, 0f);     // 스프레이는 BaseSpray(스커트) 전담
                _mpb.SetFloat(BaseBoostID, preset.baseBoost);
                _mpb.SetFloat(BottomFadeID, preset.bottomFade);
                _mpb.SetFloat(VerticalBiasID, preset.verticalBias);
                _mpb.SetFloat(TopMinID, preset.topMin);
                _mpb.SetFloat(TopMaxID, preset.topMax);
                _mpb.SetFloat(TopSoftID, preset.topSoft);
                _mpb.SetFloat(TopNoiseScaleID, preset.topNoiseScale);
                _mpb.SetFloat(TopNoiseSpeedID, preset.topNoiseSpeed);
                _mpb.SetFloat(FacePowerID, preset.facePower);
                _mpb.SetFloat(LineCountID, preset.lineCount);
                _mpb.SetFloat(LineWidthID, preset.lineWidth);
                _mpb.SetFloat(LineSharpID, preset.lineSharp);
                _mpb.SetFloat(DashFreqID, preset.dashFreq);
                _mpb.SetFloat(DashDutyID, preset.dashDuty);
                _mpb.SetFloat(ScrollSpeedID, preset.scrollSpeed);
            }
            _mpb.SetFloat(YExtentID, 1f);
            _mpb.SetFloat(FadeMulID, _envelope);
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
