using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// PawForYou 요소: 광선/스트릭 공용 렌더러(셰이더 JC/VFX/PawBeam).
    /// 쿼드 로컬 +Y가 from→to 축. 축 둘레로만 카메라를 향하는 실린더형 빌보드.
    /// 자체 상태머신 없음 — 오케스트레이터가 SetLine/SetExtend/SetEnvelope 주입.
    /// 수직 빔(발→대상)과 워프 스트릭(시전자→대상 머리)이 프리셋만 달리해 같은 컴포넌트를 씀.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class PawBeam : MonoBehaviour
    {
        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영.")]
        [SerializeField] private PawBeamPreset preset;
        [SerializeField] private bool livePreview = true;

        [Header("폭 / 룩 (livePreview 시 preset 사용)")]
        [Tooltip("광선 월드 폭(m)")]
        [SerializeField] private float width = 0.35f;
        [ColorUsage(true, true)] [SerializeField] private Color coreColor = new Color(1f, 1f, 0.9f);
        [ColorUsage(true, true)] [SerializeField] private Color glowColor = new Color(1f, 0.8f, 0.25f);
        [Range(0f, 10f)] [SerializeField] private float intensity = 2.5f;
        [Tooltip("착탄 플래시 색(광선 변형 색과 매칭). 오케스트레이터가 읽어감.")]
        [ColorUsage(true, true)] [SerializeField] private Color hitFlashColor = new Color(1f, 0.9f, 0.5f);

        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private Camera _cam;
        private Vector3 _from, _to;
        private float _extend = 1f;
        private float _envelope = 1f;
        private bool _visible;

        private static readonly int CoreColorID = Shader.PropertyToID("_CoreColor");
        private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int CoreWidthID = Shader.PropertyToID("_CoreWidth");
        private static readonly int GlowFalloffID = Shader.PropertyToID("_GlowFalloff");
        private static readonly int EdgeSoftID = Shader.PropertyToID("_EdgeSoft");
        private static readonly int NoiseScaleID = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseScrollID = Shader.PropertyToID("_NoiseScroll");
        private static readonly int NoiseAmountID = Shader.PropertyToID("_NoiseAmount");
        private static readonly int PulseAmpID = Shader.PropertyToID("_PulseAmp");
        private static readonly int PulseFreqID = Shader.PropertyToID("_PulseFreq");
        private static readonly int CapSoftStartID = Shader.PropertyToID("_CapSoftStart");
        private static readonly int CapSoftEndID = Shader.PropertyToID("_CapSoftEnd");
        private static readonly int FrontSoftID = Shader.PropertyToID("_FrontSoft");
        private static readonly int ExtendID = Shader.PropertyToID("_Extend");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        /// <summary>착탄 플래시 색(변형 색 매칭). 프리뷰 중이면 프리셋 값.</summary>
        public Color HitFlashColor => livePreview && preset ? preset.hitFlashColor : hitFlashColor;

        private void Awake()
        {
            _mr = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            _mr.enabled = false;
            _visible = false;
        }

        /// <summary>변형 프리셋 런타임 교체.</summary>
        public void SetPreset(PawBeamPreset p) => preset = p;

        /// <summary>현재 프리셋 — 광선 끝 좌표의 정본. 오케스트레이터가 읽어간다.</summary>
        public PawBeamPreset Preset => preset;

        public void Show()
        {
            _visible = true;
            if (_mr) _mr.enabled = true;
        }

        public void Hide()
        {
            _visible = false;
            if (_mr) _mr.enabled = false;
        }

        /// <summary>축 주입: from(uv.y=0, 신장 시작) → to(uv.y=1). 매 프레임 갱신 가능.</summary>
        public void SetLine(Vector3 from, Vector3 to)
        {
            _from = from;
            _to = to;
        }

        /// <summary>신장 진행도(0~1). from쪽부터 to쪽으로 자람.</summary>
        public void SetExtend(float e) => _extend = Mathf.Clamp01(e);

        /// <summary>마스터 페이드 주입(0~1).</summary>
        public void SetEnvelope(float f) => _envelope = Mathf.Clamp01(f);

        private void LateUpdate()
        {
            if (!_visible) return;
            if (livePreview && preset) PullFromPreset();

            Vector3 axis = _to - _from;
            float len = axis.magnitude;
            if (len < 1e-4f) return;
            Vector3 up = axis / len;
            Vector3 mid = (_from + _to) * 0.5f;

            transform.position = mid;
            transform.localScale = new Vector3(width, len, 1f);

            // 실린더형 빌보드: 축(up) 고정, 축 둘레로만 카메라를 향함
            if (_cam == null) _cam = Camera.main;
            if (_cam)
            {
                Vector3 camDir = mid - _cam.transform.position;
                Vector3 right = Vector3.Cross(up, camDir);
                if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(up, _cam.transform.up);
                right.Normalize();
                Vector3 fwd = Vector3.Cross(right, up);
                transform.rotation = Quaternion.LookRotation(fwd, up);
            }

            ApplyVisual();
        }

        private void PullFromPreset()
        {
            width = preset.TransformSource.width;   // ★트랜스폼은 따름 규칙(변종→Basic), 룩은 자기 것
            coreColor = preset.coreColor;
            glowColor = preset.glowColor;
            intensity = preset.intensity;
            hitFlashColor = preset.hitFlashColor;
        }

        private void ApplyVisual()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mr.GetPropertyBlock(_mpb);
            if (livePreview && preset)
            {
                var t = preset.TransformSource;   // ★단면·노이즈·캡은 따름 규칙, 색·밝기는 자기 것
                _mpb.SetColor(CoreColorID, coreColor);
                _mpb.SetColor(GlowColorID, glowColor);
                _mpb.SetFloat(IntensityID, intensity);
                _mpb.SetFloat(CoreWidthID, t.coreWidth);
                _mpb.SetFloat(GlowFalloffID, t.glowFalloff);
                _mpb.SetFloat(EdgeSoftID, t.edgeSoft);
                _mpb.SetFloat(NoiseScaleID, t.noiseScale);
                _mpb.SetFloat(NoiseScrollID, t.noiseScroll);
                _mpb.SetFloat(NoiseAmountID, t.noiseAmount);
                _mpb.SetFloat(PulseAmpID, t.pulseAmp);
                _mpb.SetFloat(PulseFreqID, t.pulseFreq);
                _mpb.SetFloat(CapSoftStartID, t.capSoftStart);
                _mpb.SetFloat(CapSoftEndID, t.capSoftEnd);
                _mpb.SetFloat(FrontSoftID, t.frontSoft);
            }
            _mpb.SetFloat(ExtendID, _extend);
            _mpb.SetFloat(FadeMulID, _envelope);
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
