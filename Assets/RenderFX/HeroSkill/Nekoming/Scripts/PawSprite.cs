using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// PawForYou 요소: 고양이 발 빌보드(SDF 절차 셰이더 JC/VFX/PawSprite).
    /// 자체 상태머신 없음 — 오케스트레이터(PawForYouVfx)가 위치/스케일/페이드를 매 프레임 주입.
    /// 이 컴포넌트는 빌보드 회전 + 미세 bob + 룩(MPB 비파괴) 담당.
    /// ★런타임 프리뷰: preset + livePreview 켜면 플레이 중 값 즉시 반영.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class PawSprite : MonoBehaviour
    {
        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영.")]
        [SerializeField] private PawSpritePreset preset;
        [SerializeField] private bool livePreview = true;

        [Header("크기 / 거동")]
        [Tooltip("발 월드 크기(m, 쿼드 한 변)")]
        [SerializeField] private float size = 0.9f;
        [Tooltip("빌보드 Y축만: 수직 유지·수평만 카메라. 끄면 완전 정면")]
        [SerializeField] private bool billboardYOnly = false;
        [Range(0f, 0.3f)] [SerializeField] private float bobAmp = 0.04f;
        [Range(0f, 6f)] [SerializeField] private float bobFreq = 1.2f;

        [Header("룩 (livePreview 시 preset 사용)")]
        [ColorUsage(true, true)] [SerializeField] private Color fillColor = new Color(0.85f, 0.9f, 1f);
        [Range(0f, 1f)] [SerializeField] private float fillOpacity = 0.8f;
        [ColorUsage(true, true)] [SerializeField] private Color rimColor = Color.white;
        [Range(0f, 8f)] [SerializeField] private float rimIntensity = 2.5f;
        [ColorUsage(true, true)] [SerializeField] private Color glowColor = new Color(0.8f, 0.9f, 1f);
        [Range(0f, 8f)] [SerializeField] private float glowIntensity = 1.5f;
        [ColorUsage(true, true)] [SerializeField] private Color padColor = new Color(1f, 0.95f, 0.8f);
        [Range(0f, 8f)] [SerializeField] private float padIntensity = 2f;
        [Tooltip("워프 순간 플래시 색(발 변형 색과 매칭). 오케스트레이터가 읽어감.")]
        [ColorUsage(true, true)] [SerializeField] private Color warpFlashColor = Color.white;

        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private Camera _cam;
        private Vector3 _basePos;
        private float _scaleMul;
        private float _envelope = 1f;
        private bool _visible;
        private float _bobPhase;

        private static readonly int CanvasScaleID = Shader.PropertyToID("_CanvasScale");
        private static readonly int ToeCountID = Shader.PropertyToID("_ToeCount");
        private static readonly int ToeSpreadID = Shader.PropertyToID("_ToeSpread");
        private static readonly int ToeDistID = Shader.PropertyToID("_ToeDist");
        private static readonly int ToeRadiusID = Shader.PropertyToID("_ToeRadius");
        private static readonly int PalmRadiusXID = Shader.PropertyToID("_PalmRadiusX");
        private static readonly int PalmRadiusYID = Shader.PropertyToID("_PalmRadiusY");
        private static readonly int PalmOffsetYID = Shader.PropertyToID("_PalmOffsetY");
        private static readonly int FusionID = Shader.PropertyToID("_Fusion");
        private static readonly int PadMainRadiusID = Shader.PropertyToID("_PadMainRadius");
        private static readonly int PadMainSquashID = Shader.PropertyToID("_PadMainSquash");
        private static readonly int PadToeRadiusID = Shader.PropertyToID("_PadToeRadius");
        private static readonly int PadToeDistMulID = Shader.PropertyToID("_PadToeDistMul");
        private static readonly int PadColorID = Shader.PropertyToID("_PadColor");
        private static readonly int PadIntensityID = Shader.PropertyToID("_PadIntensity");
        private static readonly int FillColorID = Shader.PropertyToID("_FillColor");
        private static readonly int FillOpacityID = Shader.PropertyToID("_FillOpacity");
        private static readonly int InnerGradID = Shader.PropertyToID("_InnerGrad");
        private static readonly int RimColorID = Shader.PropertyToID("_RimColor");
        private static readonly int RimIntensityID = Shader.PropertyToID("_RimIntensity");
        private static readonly int RimWidthID = Shader.PropertyToID("_RimWidth");
        private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");
        private static readonly int GlowRangeID = Shader.PropertyToID("_GlowRange");
        private static readonly int WobbleAmpID = Shader.PropertyToID("_WobbleAmp");
        private static readonly int WobbleSpeedID = Shader.PropertyToID("_WobbleSpeed");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        /// <summary>워프 플래시 색(변형 색 매칭). 프리뷰 중이면 프리셋 값.</summary>
        public Color WarpFlashColor => livePreview && preset ? preset.warpFlashColor : warpFlashColor;

        /// <summary>bob 포함 현재 위치(빔 시작점 추적용).</summary>
        public Vector3 CurrentPosition => transform.position;

        private void Awake()
        {
            _mr = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            _mr.enabled = false;
            _visible = false;
        }

        /// <summary>변형 프리셋 런타임 교체.</summary>
        public void SetPreset(PawSpritePreset p) => preset = p;

        /// <summary>현재 프리셋 — 발 좌표(등장/재등장)의 정본. 오케스트레이터·호출자가 읽어간다.</summary>
        public PawSpritePreset Preset => preset;

        public void Show()
        {
            _visible = true;
            _bobPhase = Random.value;
            if (_mr) _mr.enabled = true;
        }

        public void Hide()
        {
            _visible = false;
            if (_mr) _mr.enabled = false;
        }

        /// <summary>기준 위치 주입(bob 미포함). 오케스트레이터가 매 프레임 호출.</summary>
        public void SetBasePosition(Vector3 pos) => _basePos = pos;

        /// <summary>스케일 배수 주입(팝/스쿼시 타임라인은 오케스트레이터 소유).</summary>
        public void SetScaleMul(float s) => _scaleMul = Mathf.Max(0f, s);

        /// <summary>마스터 페이드 주입(0~1).</summary>
        public void SetEnvelope(float f) => _envelope = Mathf.Clamp01(f);

        private void LateUpdate()
        {
            if (!_visible) return;
            if (livePreview && preset) PullFromPreset();

            float bob = bobAmp * Mathf.Sin((Time.time * bobFreq + _bobPhase) * Mathf.PI * 2f);
            transform.position = _basePos + Vector3.up * bob;
            transform.localScale = Vector3.one * (size * _scaleMul);

            if (_cam == null) _cam = Camera.main;
            if (_cam)
            {
                if (billboardYOnly)
                {
                    Vector3 f = transform.position - _cam.transform.position;
                    f.y = 0f;
                    if (f.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
                }
                else transform.rotation = _cam.transform.rotation;
            }

            ApplyVisual();
        }

        private void PullFromPreset()
        {
            var t = preset.TransformSource;   // ★트랜스폼(크기·거동)은 따름 규칙(변종→Basic), 룩은 자기 것
            size = t.size;
            billboardYOnly = t.billboardYOnly;
            bobAmp = t.bobAmp;
            bobFreq = t.bobFreq;
            fillColor = preset.fillColor;
            fillOpacity = preset.fillOpacity;
            rimColor = preset.rimColor;
            rimIntensity = preset.rimIntensity;
            glowColor = preset.glowColor;
            glowIntensity = preset.glowIntensity;
            padColor = preset.padColor;
            padIntensity = preset.padIntensity;
            warpFlashColor = preset.warpFlashColor;
        }

        private void ApplyVisual()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mr.GetPropertyBlock(_mpb);
            if (livePreview && preset)
            {
                var t = preset.TransformSource;   // ★형태(SDF)·움직임은 따름 규칙, 색·밝기는 자기 것
                _mpb.SetFloat(CanvasScaleID, t.canvasScale);
                _mpb.SetFloat(ToeCountID, t.toeCount);
                _mpb.SetFloat(ToeSpreadID, t.toeSpreadDeg);
                _mpb.SetFloat(ToeDistID, t.toeDist);
                _mpb.SetFloat(ToeRadiusID, t.toeRadius);
                _mpb.SetFloat(PalmRadiusXID, t.palmRadiusX);
                _mpb.SetFloat(PalmRadiusYID, t.palmRadiusY);
                _mpb.SetFloat(PalmOffsetYID, t.palmOffsetY);
                _mpb.SetFloat(FusionID, t.fusion);
                _mpb.SetFloat(PadMainRadiusID, t.padMainRadius);
                _mpb.SetFloat(PadMainSquashID, t.padMainSquash);
                _mpb.SetFloat(PadToeRadiusID, t.padToeRadius);
                _mpb.SetFloat(PadToeDistMulID, t.padToeDistMul);
                _mpb.SetColor(PadColorID, padColor);
                _mpb.SetFloat(PadIntensityID, padIntensity);
                _mpb.SetColor(FillColorID, fillColor);
                _mpb.SetFloat(FillOpacityID, fillOpacity);
                _mpb.SetFloat(InnerGradID, preset.innerGrad);
                _mpb.SetColor(RimColorID, rimColor);
                _mpb.SetFloat(RimIntensityID, rimIntensity);
                _mpb.SetFloat(RimWidthID, t.rimWidth);
                _mpb.SetColor(GlowColorID, glowColor);
                _mpb.SetFloat(GlowIntensityID, glowIntensity);
                _mpb.SetFloat(GlowRangeID, t.glowRange);
                _mpb.SetFloat(WobbleAmpID, t.wobbleAmp);
                _mpb.SetFloat(WobbleSpeedID, t.wobbleSpeed);
            }
            _mpb.SetFloat(FadeMulID, _envelope);
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
