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
        [Tooltip("켜면 변경한 설정을 실행 중인 이 효과에 갱신합니다. 파일 저장과는 별개이며 이미 시작된 시간표는 재시전하여 확인합니다.")]
        [SerializeField] private bool livePreview = true;

        [Header("크기 / 거동")]
        [Tooltip("발 월드 크기(m, 쿼드 한 변)")]
        [SerializeField] private float size = 0.9f;
        [Tooltip("빌보드 방식 — 정면 / 정면+월드수직 롤 정렬(광선 축과 일직선) / Y축 고정")]
        [SerializeField] private JcBillboardSolver.Mode billboardMode = JcBillboardSolver.Mode.CameraFacing;

        [Header("접점 핀 (빌보드-3D 정합, 260806)")]
        [Tooltip("★켜면 쿼드 로컬 접점(anchorLocal)이 발 좌표(3D 앵커)에 못 박힌다 — 카메라 각도 무관하게 광선 시작점과 일치.")]
        [SerializeField] private bool anchorPin = true;
        [Tooltip("쿼드 로컬 접점(±0.5). (0,-0.27) ≈ 발바닥 하단(canvasScale 0.55 기준). 씬 뷰 기즈모(청록 구)로 확인.")]
        [SerializeField] private Vector2 anchorLocal = new Vector2(0f, -0.27f);
        [Tooltip("둥실 bob 진폭(m)")]
        [Range(0f, 0.3f)] [SerializeField] private float bobAmp = 0.04f;
        [Tooltip("둥실 bob 빈도(Hz)")]
        [Range(0f, 6f)] [SerializeField] private float bobFreq = 1.2f;

        [Header("룩 (livePreview 시 preset 사용)")]
        [Tooltip("몸통 필 색(반투명 채움)")]
        [ColorUsage(true, true)] [SerializeField] private Color fillColor = new Color(0.85f, 0.9f, 1f);
        [Tooltip("몸통 불투명도(배경 가림 정도)")]
        [Range(0f, 1f)] [SerializeField] private float fillOpacity = 0.8f;
        [Tooltip("림(경계 밴드) 색")]
        [ColorUsage(true, true)] [SerializeField] private Color rimColor = Color.white;
        [Tooltip("림 밝기")]
        [Range(0f, 8f)] [SerializeField] private float rimIntensity = 2.5f;
        [Tooltip("외곽 글로우 색(실루엣 밖 번짐)")]
        [ColorUsage(true, true)] [SerializeField] private Color glowColor = new Color(0.8f, 0.9f, 1f);
        [Tooltip("외곽 글로우 밝기")]
        [Range(0f, 8f)] [SerializeField] private float glowIntensity = 1.5f;
        [Tooltip("패드 프린트 발광 색(HDR)")]
        [ColorUsage(true, true)] [SerializeField] private Color padColor = new Color(1f, 0.95f, 0.8f);
        [Tooltip("패드 프린트 발광 세기")]
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

        /// <summary>bob 포함 쿼드 중심 위치(레거시 — 핀이 켜져 있으면 접점이 아니라 중심이다).</summary>
        public Vector3 CurrentPosition => transform.position;

        /// <summary>★시각 접점(핀 지점)의 월드 좌표 — 광선 시작 추적의 정본. 핀이 꺼져 있으면 쿼드 중심.</summary>
        public Vector3 AnchorPosition => anchorPin ? _basePos + Vector3.up * CurrentBob() : transform.position;

        private float CurrentBob() => bobAmp * Mathf.Sin((Time.time * bobFreq + _bobPhase) * Mathf.PI * 2f);

        private void Awake()
        {
            EnsureInit();
            if (!_visible) _mr.enabled = false;   // pre-Awake Show 가 켜둔 상태는 덮지 않는다
        }

        /// <summary>★지연 초기화 — Instantiate 중 루트 OnEnable→Play 가 자식 Awake 보다 먼저 와도 안전(VFX 부품 공통 규격).</summary>
        private void EnsureInit()
        {
            if (_mr != null) return;
            _mr = GetComponent<MeshRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
        }

        /// <summary>변형 프리셋 런타임 교체.</summary>
        public void SetPreset(PawSpritePreset p) => preset = p;

        /// <summary>현재 프리셋 — 발 좌표(등장/재등장)의 정본. 오케스트레이터·호출자가 읽어간다.</summary>
        public PawSpritePreset Preset => preset;

        public void Show()
        {
            EnsureInit();
            _visible = true;
            _bobPhase = Random.value;
            _mr.enabled = true;
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

            // ★순서 고정: 앵커 확정 → 회전 → 핀 위치(회전된 접점 기준으로 중심을 되민다).
            Vector3 anchor = _basePos + Vector3.up * CurrentBob();
            transform.localScale = Vector3.one * (size * _scaleMul);

            if (_cam == null) _cam = Camera.main;
            if (_cam) transform.rotation = JcBillboardSolver.SolveRotation(_cam, anchor, billboardMode, transform.rotation);

            transform.position = anchorPin
                ? JcBillboardSolver.SolvePinnedCenter(anchor, transform.rotation, anchorLocal, size * _scaleMul)
                : anchor;

            ApplyVisual();
        }

        private void PullFromPreset()
        {
            var t = preset.TransformSource;   // ★트랜스폼(크기·거동)은 따름 규칙(변종→Basic), 룩은 자기 것
            size = t.size;
            billboardMode = t.billboardMode;
            anchorPin = t.anchorPin;
            anchorLocal = t.anchorLocal;
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

#if UNITY_EDITOR
        /// <summary>접점 튜닝용 — 청록 구(접점)가 광선 시작(발 좌표)과 겹쳐야 한다.</summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 p = transform.TransformPoint((Vector3)anchorLocal);
            Gizmos.DrawWireSphere(p, 0.05f);
            Gizmos.DrawLine(transform.position, p);
        }
#endif
    }
}
