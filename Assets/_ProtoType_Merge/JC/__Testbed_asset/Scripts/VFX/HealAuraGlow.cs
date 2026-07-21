using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 힐 오라 E-3: 발밑에서 위로 솟는 광채 기둥(실린더 셸 표면).
    /// 세로 그라데이션·좌우 실루엣 소프트·미세 세로 질감은 셰이더(JC/VFX/HealAuraGlow)가 절차적 처리.
    /// 이 컴포넌트는 실린더의 크기(지름/높이)·위치(발밑)·솟아오름 팝·재질 파라미터(MPB)를 구동.
    /// 실린더는 대칭이라 빌보드 불필요(Y축 직립 고정). 좌표(중심=착지점 지면)는 호출자 주입: Play(center).
    ///
    /// ★마스터 페이드: 자체 상태머신 없이 오케스트레이터가 SetEnvelope(_fade)로 주입 → 오브와 완전 동기.
    /// ★런타임 프리뷰: preset + livePreview 켜면 플레이 중 값 즉시 반영(재질은 비파괴 MPB).
    /// ★메시: 기본 Cylinder(반경0.5·높이2·Y[-1,1]). 늘린 Sphere로 교체하면 타원 돔이 됨(셰이더 그대로).
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class HealAuraGlow : MonoBehaviour
    {
        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영.")]
        [SerializeField] private HealAuraGlowPreset preset;
        [Tooltip("플레이 중 preset 값을 반영. 끄면 아래 필드값/베이크된 재질값 사용.")]
        [SerializeField] private bool livePreview = true;

        [Header("크기 / 위치 (월드 m)")]
        [SerializeField] private float width = 1.6f;   // 지름
        [SerializeField] private float height = 2.0f;
        [SerializeField] private float groundOffsetY = 0f;

        [Header("색 / 밝기 (livePreview 시 preset 사용)")]
        [ColorUsage(true, true)] [SerializeField] private Color color = new Color(1f, 0.85f, 0.28f);
        [Range(0f, 8f)] [SerializeField] private float intensity = 1.8f;
        [Range(0f, 1f)] [SerializeField] private float opacity = 1.0f;

        [Header("세로 형태 / 실루엣")]
        [Range(0f, 0.5f)] [SerializeField] private float bottomFade = 0.06f;
        [Range(0.1f, 5f)] [SerializeField] private float verticalBias = 1.4f;
        [Range(0.3f, 8f)] [SerializeField] private float facePower = 1.6f;

        [Header("상단 경계 (min==max → 평면)")]
        [Range(0f, 1f)] [SerializeField] private float topMin = 0.70f;
        [Range(0f, 1f)] [SerializeField] private float topMax = 0.92f;
        [Range(0f, 0.5f)] [SerializeField] private float topSoft = 0.12f;
        [Range(0.5f, 10f)] [SerializeField] private float topNoiseScale = 3.0f;
        [Range(0f, 4f)] [SerializeField] private float topNoiseSpeed = 0.6f;

        [Header("세로 광선 / 일렁임 / 솟아남")]
        [Range(0f, 80f)] [SerializeField] private float streakTiling = 18f;
        [Range(0f, 1f)] [SerializeField] private float streakStrength = 0.12f;
        [Range(-4f, 4f)] [SerializeField] private float streakScroll = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float wobbleAmount = 0.15f;
        [Range(0f, 6f)] [SerializeField] private float wobbleSpeed = 1.2f;
        [Range(0f, 1f)] [SerializeField] private float riseGrow = 0.4f;

        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private Vector3 _center;
        private float _envelope = 1f;   // 오케스트레이터가 주입하는 마스터 페이드(0~1)
        private bool _playing;

        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
        private static readonly int BottomFadeID = Shader.PropertyToID("_BottomFade");
        private static readonly int VerticalBiasID = Shader.PropertyToID("_VerticalBias");
        private static readonly int TopMinID = Shader.PropertyToID("_TopMin");
        private static readonly int TopMaxID = Shader.PropertyToID("_TopMax");
        private static readonly int TopSoftID = Shader.PropertyToID("_TopSoft");
        private static readonly int TopNoiseScaleID = Shader.PropertyToID("_TopNoiseScale");
        private static readonly int TopNoiseSpeedID = Shader.PropertyToID("_TopNoiseSpeed");
        private static readonly int FacePowerID = Shader.PropertyToID("_FacePower");
        private static readonly int StreakTilingID = Shader.PropertyToID("_StreakTiling");
        private static readonly int StreakStrengthID = Shader.PropertyToID("_StreakStrength");
        private static readonly int StreakScrollID = Shader.PropertyToID("_StreakScroll");
        private static readonly int WobbleAmountID = Shader.PropertyToID("_WobbleAmount");
        private static readonly int WobbleSpeedID = Shader.PropertyToID("_WobbleSpeed");
        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private void Awake()
        {
            _mr = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            _mr.enabled = false;
            _playing = false;
        }

        /// <summary>중심(착지점 지면) 주입 + 표시 시작.</summary>
        public void Play(Vector3 center)
        {
            _center = center;
            if (livePreview && preset) PullFromPreset();
            _playing = true;
            _mr.enabled = true;
            ApplyTransform();
            ApplyVisual();
        }

        /// <summary>즉시 정지·숨김.</summary>
        public void Stop()
        {
            _playing = false;
            if (_mr) _mr.enabled = false;
        }

        /// <summary>오케스트레이터가 매 프레임 마스터 페이드값을 주입(오브와 동기).</summary>
        public void SetEnvelope(float f) => _envelope = Mathf.Clamp01(f);

        private void Update()
        {
            if (!_playing) return;
            if (livePreview && preset) PullFromPreset();
            ApplyTransform();
            ApplyVisual();
        }

        private void PullFromPreset()
        {
            width = preset.width;
            height = preset.height;
            groundOffsetY = preset.groundOffsetY;
            color = preset.color;
            intensity = preset.intensity;
            opacity = preset.opacity;
            bottomFade = preset.bottomFade;
            verticalBias = preset.verticalBias;
            topMin = preset.topMin;
            topMax = preset.topMax;
            topSoft = preset.topSoft;
            topNoiseScale = preset.topNoiseScale;
            topNoiseSpeed = preset.topNoiseSpeed;
            facePower = preset.facePower;
            streakTiling = preset.streakTiling;
            streakStrength = preset.streakStrength;
            streakScroll = preset.streakScroll;
            wobbleAmount = preset.wobbleAmount;
            wobbleSpeed = preset.wobbleSpeed;
            riseGrow = preset.riseGrow;
        }

        /// <summary>실린더 크기(지름/높이) + 솟아오름 팝 + 발밑 위치. 빌보드 없음(Y축 직립).</summary>
        private void ApplyTransform()
        {
            // 솟아오름 팝: 페이드인 구간(envelope<1) 동안 height를 riseGrow만큼 낮은 값에서 자라남.
            float grow = Mathf.Lerp(1f - riseGrow, 1f, _envelope);
            float effH = height * grow;

            // 기본 Cylinder: 반경0.5(지름1)·높이2(Y[-1,1]) → scale.xz=지름, scale.y=높이/2
            transform.localScale = new Vector3(width, effH * 0.5f, width);
            transform.position = _center + Vector3.up * (groundOffsetY + effH * 0.5f);
            transform.rotation = Quaternion.identity;
        }

        /// <summary>재질 파라미터(MPB). _FadeMul은 항상 envelope로, 프리뷰 켜지면 나머지도 preset로.</summary>
        private void ApplyVisual()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mr.GetPropertyBlock(_mpb);
            if (livePreview && preset)
            {
                _mpb.SetColor(ColorID, color);
                _mpb.SetFloat(IntensityID, intensity);
                _mpb.SetFloat(OpacityID, opacity);
                _mpb.SetFloat(BottomFadeID, bottomFade);
                _mpb.SetFloat(VerticalBiasID, verticalBias);
                _mpb.SetFloat(TopMinID, topMin);
                _mpb.SetFloat(TopMaxID, topMax);
                _mpb.SetFloat(TopSoftID, topSoft);
                _mpb.SetFloat(TopNoiseScaleID, topNoiseScale);
                _mpb.SetFloat(TopNoiseSpeedID, topNoiseSpeed);
                _mpb.SetFloat(FacePowerID, facePower);
                _mpb.SetFloat(StreakTilingID, streakTiling);
                _mpb.SetFloat(StreakStrengthID, streakStrength);
                _mpb.SetFloat(StreakScrollID, streakScroll);
                _mpb.SetFloat(WobbleAmountID, wobbleAmount);
                _mpb.SetFloat(WobbleSpeedID, wobbleSpeed);
            }
            _mpb.SetFloat(FadeMulID, _envelope);
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
