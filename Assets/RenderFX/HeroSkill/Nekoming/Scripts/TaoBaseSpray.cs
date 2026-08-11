using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// Taosenaiyo 요소: 기초 방사 스프레이 스커트(셰이더 JC/VFX/TaoAuraFlare 공유, flareUp=1 콘).
    /// 위로 벌어지는 스커트 표면에 갈퀴 스트릭이 바깥·위로 뿜어지는 방사형 버스트 — 초사이언 바닥 갈퀴.
    /// 펜선/바닥부스트는 항상 끔(_LineIntensity=0, _BaseBoost=0). 마스터 페이드는 SetEnvelope 주입(본 오라 동형).
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class TaoBaseSpray : MonoBehaviour
    {
        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영.")]
        [SerializeField] private TaoBaseSprayPreset preset;
        [SerializeField] private bool livePreview = true;

        [Header("크기 (livePreview 시 preset 사용)")]
        [SerializeField] private float width = 0.9f;
        [SerializeField] private float height = 0.7f;
        [SerializeField] private float groundOffsetY = 0.02f;
        [Range(0f, 1f)] [SerializeField] private float riseGrow = 0.4f;

        [Header("룩 (livePreview 시 preset 사용)")]
        [ColorUsage(true, true)] [SerializeField] private Color sprayColor = new Color(1f, 0.62f, 0.12f);
        [Range(0f, 10f)] [SerializeField] private float sprayIntensity = 4.5f;

        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private Vector3 _center;
        private float _envelope = 1f;
        private bool _playing;

        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
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
        private static readonly int SprayColorID = Shader.PropertyToID("_SprayColor");
        private static readonly int SprayIntensityID = Shader.PropertyToID("_SprayIntensity");
        private static readonly int SprayHeightID = Shader.PropertyToID("_SprayHeight");
        private static readonly int SprayCountID = Shader.PropertyToID("_SprayCount");
        private static readonly int SprayWidthID = Shader.PropertyToID("_SprayWidth");
        private static readonly int SpraySharpID = Shader.PropertyToID("_SpraySharp");
        private static readonly int SprayDashFreqID = Shader.PropertyToID("_SprayDashFreq");
        private static readonly int SprayDashDutyID = Shader.PropertyToID("_SprayDashDuty");
        private static readonly int SpraySpeedID = Shader.PropertyToID("_SpraySpeed");
        private static readonly int SprayTaperID = Shader.PropertyToID("_SprayTaper");
        private static readonly int SprayRandomID = Shader.PropertyToID("_SprayRandom");
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

        public void SetPreset(TaoBaseSprayPreset p) => preset = p;

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
            sprayColor = preset.sprayColor;
            sprayIntensity = preset.sprayIntensity;
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
                var t = preset.TransformSource;   // ★따름 — 형태·갈퀴 기하는 Basic 정본, 색·불투명은 자기 것
                _mpb.SetColor(ColorID, preset.baseColor);
                _mpb.SetFloat(IntensityID, preset.baseIntensity);
                _mpb.SetFloat(OpacityID, preset.opacity);
                _mpb.SetFloat(FlareID, t.flare);
                _mpb.SetFloat(FlareCurveID, t.flareCurve);
                _mpb.SetFloat(BottomFadeID, t.bottomFade);
                _mpb.SetFloat(VerticalBiasID, t.verticalBias);
                _mpb.SetFloat(TopMinID, t.topMin);
                _mpb.SetFloat(TopMaxID, t.topMax);
                _mpb.SetFloat(TopSoftID, t.topSoft);
                _mpb.SetFloat(TopNoiseScaleID, t.topNoiseScale);
                _mpb.SetFloat(TopNoiseSpeedID, t.topNoiseSpeed);
                _mpb.SetFloat(FacePowerID, t.facePower);
                _mpb.SetColor(SprayColorID, sprayColor);
                _mpb.SetFloat(SprayIntensityID, sprayIntensity);
                _mpb.SetFloat(SprayHeightID, t.sprayHeight);
                _mpb.SetFloat(SprayCountID, t.sprayCount);
                _mpb.SetFloat(SprayWidthID, t.sprayWidth);
                _mpb.SetFloat(SpraySharpID, t.spraySharp);
                _mpb.SetFloat(SprayDashFreqID, t.sprayDashFreq);
                _mpb.SetFloat(SprayDashDutyID, t.sprayDashDuty);
                _mpb.SetFloat(SpraySpeedID, t.spraySpeed);
                _mpb.SetFloat(SprayTaperID, t.sprayTaper);
                _mpb.SetFloat(SprayRandomID, t.sprayRandom);
            }
            _mpb.SetFloat(FlareUpID, 1f);        // 스커트 = 위로 벌어짐 고정
            _mpb.SetFloat(LineIntensityID, 0f);  // 펜선 없음
            _mpb.SetFloat(BaseBoostID, 0f);
            _mpb.SetFloat(YExtentID, 1f);
            _mpb.SetFloat(FadeMulID, _envelope);
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
