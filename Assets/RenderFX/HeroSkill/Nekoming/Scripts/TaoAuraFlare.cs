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
        [Tooltip("켜면 변경한 설정을 실행 중인 이 효과에 갱신합니다. 파일 저장과는 별개이며 이미 시작된 시간표는 재시전하여 확인합니다.")]
        [SerializeField] private bool livePreview = true;

        [Header("크기 / 위치 (월드 m)")]
        [Tooltip("바닥 지름(m). 상단은 플레어만큼 더 벌어짐")]
        [SerializeField] private float width = 1.0f;
        [Tooltip("기둥 높이(m)")]
        [SerializeField] private float height = 2.2f;
        [Tooltip("지면 위 띄우는 높이(m)")]
        [SerializeField] private float groundOffsetY = 0.02f;
        [Tooltip("페이드인 동안 커지는 팝 정도")]
        [Range(0f, 1f)] [SerializeField] private float riseGrow = 0.35f;

        [Header("색 (livePreview 시 preset 사용)")]
        [Tooltip("바탕 글로우 색(은은한 배경)")]
        [ColorUsage(true, true)] [SerializeField] private Color baseColor = new Color(1f, 0.82f, 0.3f);
        [Tooltip("바탕 글로우 밝기(은은한 채움)")]
        [Range(0f, 8f)] [SerializeField] private float baseIntensity = 1.2f;
        [Tooltip("펜선 색(진한 스트로크)")]
        [ColorUsage(true, true)] [SerializeField] private Color lineColor = new Color(1f, 0.7f, 0.15f);
        [Tooltip("펜선 밝기")]
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
            EnsureInit();
            if (!_playing) _mr.enabled = false;   // pre-Awake Play 가 켜둔 상태는 덮지 않는다
        }

        /// <summary>★지연 초기화 — Instantiate 중 루트 OnEnable→Play 가 자식 Awake 보다 먼저 와도 안전(VFX 부품 공통 규격).</summary>
        private void EnsureInit()
        {
            if (_mr != null) return;
            _mr = GetComponent<MeshRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            var mf = GetComponent<MeshFilter>();
            if (mf) mf.sharedMesh = VfxShellMesh.Get();   // 세로 분할 셸 — 플레어 곡률이 실제로 보이게
        }

        public void SetPreset(TaoAuraFlarePreset p) => preset = p;

        public void Play(Vector3 center)
        {
            EnsureInit();
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
            // ★라이브 따름(260807) — 형태·움직임은 TransformSource(Alter→Basic), 색·밝기만 자기 것.
            var t = preset.TransformSource;
            width = t.width;
            height = t.height;
            groundOffsetY = t.groundOffsetY;
            riseGrow = t.riseGrow;
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
                var t = preset.TransformSource;   // ★따름 — 형태·라인 기하는 Basic 정본, 색·불투명은 자기 것
                _mpb.SetColor(ColorID, baseColor);
                _mpb.SetFloat(IntensityID, baseIntensity);
                _mpb.SetColor(LineColorID, lineColor);
                _mpb.SetFloat(LineIntensityID, lineIntensity);
                _mpb.SetFloat(OpacityID, preset.opacity);
                _mpb.SetFloat(FlareID, t.flare);
                _mpb.SetFloat(FlareCurveID, t.flareCurve);
                _mpb.SetFloat(FlareUpID, 0f);            // 본 오라 = 벨 고정
                _mpb.SetFloat(SprayIntensityID, 0f);     // 스프레이는 BaseSpray(스커트) 전담
                _mpb.SetFloat(BaseBoostID, t.baseBoost);
                _mpb.SetFloat(BottomFadeID, t.bottomFade);
                _mpb.SetFloat(VerticalBiasID, t.verticalBias);
                _mpb.SetFloat(TopMinID, t.topMin);
                _mpb.SetFloat(TopMaxID, t.topMax);
                _mpb.SetFloat(TopSoftID, t.topSoft);
                _mpb.SetFloat(TopNoiseScaleID, t.topNoiseScale);
                _mpb.SetFloat(TopNoiseSpeedID, t.topNoiseSpeed);
                _mpb.SetFloat(FacePowerID, t.facePower);
                _mpb.SetFloat(LineCountID, t.lineCount);
                _mpb.SetFloat(LineWidthID, t.lineWidth);
                _mpb.SetFloat(LineSharpID, t.lineSharp);
                _mpb.SetFloat(DashFreqID, t.dashFreq);
                _mpb.SetFloat(DashDutyID, t.dashDuty);
                _mpb.SetFloat(ScrollSpeedID, t.scrollSpeed);
            }
            _mpb.SetFloat(YExtentID, 1f);
            _mpb.SetFloat(FadeMulID, _envelope);
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
