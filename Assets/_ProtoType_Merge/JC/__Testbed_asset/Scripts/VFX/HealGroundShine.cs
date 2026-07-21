using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 힐 오라 E-5: 바닥 장판 — 방사형 샤인 스파크 디스크(바닥에 눕는 쿼드).
    /// 중심 광채 + 회전 스파크 광선 + 외곽 소프트 수렴은 셰이더(JC/VFX/HealGroundShine)가 절차적 처리.
    /// 이 컴포넌트는 디스크의 크기(지름)·위치(지면)·퍼짐 팝·재질 파라미터(MPB)를 구동.
    /// 좌표(중심=착지점 지면)는 호출자 주입: Play(center). HealOrbitVfx가 다른 요소들과 함께 구동.
    ///
    /// ★마스터 페이드: 자체 상태머신 없이 오케스트레이터가 SetEnvelope(_fade)로 주입 → 오브와 완전 동기.
    /// ★런타임 프리뷰: preset + livePreview 켜면 플레이 중 값 즉시 반영(재질은 비파괴 MPB).
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class HealGroundShine : MonoBehaviour
    {
        [Header("런타임 프리뷰")]
        [Tooltip("지정하면 livePreview에서 이 프리셋 값을 매 프레임 반영.")]
        [SerializeField] private HealGroundShinePreset preset;
        [Tooltip("플레이 중 preset 값을 반영. 끄면 아래 필드값/베이크된 재질값 사용.")]
        [SerializeField] private bool livePreview = true;

        [Header("크기 / 위치 (월드 m)")]
        [SerializeField] private float discRadius = 0.8f;
        [SerializeField] private float rayRadius = 1.1f;
        [SerializeField] private float groundOffsetY = 0.02f;

        [Header("색 / 밝기 (livePreview 시 preset 사용)")]
        [ColorUsage(true, true)] [SerializeField] private Color color = new Color(1f, 0.88f, 0.35f);
        [Range(0f, 8f)] [SerializeField] private float intensity = 1.8f;
        [Range(0f, 1f)] [SerializeField] private float opacity = 1.0f;

        [Header("경계 수렴 / 레이 (뾰족별)")]
        [Range(0.01f, 1f)] [SerializeField] private float edgeSoft = 0.35f;
        [Range(0f, 0.3f)] [SerializeField] private float sideSoft = 0.06f;
        [Range(0.2f, 6f)] [SerializeField] private float centerFalloff = 1.6f;
        [Range(0f, 48f)] [SerializeField] private float rayDensity = 14f;
        [Range(0.5f, 8f)] [SerializeField] private float raySharp = 2.5f;
        [Range(-180f, 180f)] [SerializeField] private float rayRotSpeed = 18f;
        [Range(0f, 0.95f)] [SerializeField] private float valleyWidth = 0f;

        [Header("솟아남")]
        [Range(0f, 1f)] [SerializeField] private float spreadGrow = 0.5f;

        private MeshRenderer _mr;
        private MaterialPropertyBlock _mpb;
        private Vector3 _center;
        private float _envelope = 1f;   // 오케스트레이터가 주입하는 마스터 페이드(0~1)
        private bool _playing;

        private static readonly int ColorID = Shader.PropertyToID("_Color");
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");
        private static readonly int DiscRadiusID = Shader.PropertyToID("_DiscRadius");
        private static readonly int RayRadiusID = Shader.PropertyToID("_RayRadius");
        private static readonly int EdgeSoftID = Shader.PropertyToID("_EdgeSoft");
        private static readonly int SideSoftID = Shader.PropertyToID("_SideSoft");
        private static readonly int CenterFalloffID = Shader.PropertyToID("_CenterFalloff");
        private static readonly int RayDensityID = Shader.PropertyToID("_RayDensity");
        private static readonly int RaySharpID = Shader.PropertyToID("_RaySharp");
        private static readonly int RayRotSpeedID = Shader.PropertyToID("_RayRotSpeed");
        private static readonly int ValleyWidthID = Shader.PropertyToID("_ValleyWidth");
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
            discRadius = preset.discRadius;
            rayRadius = preset.rayRadius;
            groundOffsetY = preset.groundOffsetY;
            color = preset.color;
            intensity = preset.intensity;
            opacity = preset.opacity;
            edgeSoft = preset.edgeSoft;
            sideSoft = preset.sideSoft;
            centerFalloff = preset.centerFalloff;
            rayDensity = preset.rayDensity;
            raySharp = preset.raySharp;
            rayRotSpeed = preset.rayRotSpeed;
            valleyWidth = preset.valleyWidth;
            spreadGrow = preset.spreadGrow;
        }

        /// <summary>쿼드 크기 = 2×max(디스크, 레이외곽) 반경. 퍼짐 팝 + 지면 위치. 회전 고정(눕힘).</summary>
        private void ApplyTransform()
        {
            float maxR = Mathf.Max(discRadius, rayRadius, 1e-4f);
            // 퍼짐 팝: 페이드인 구간(envelope<1) 동안 전체가 spreadGrow만큼 작은 값에서 퍼져나감.
            float grow = Mathf.Lerp(1f - spreadGrow, 1f, _envelope);
            float size = maxR * 2f * grow;
            transform.localScale = new Vector3(size, size, 1f);
            transform.position = _center + Vector3.up * groundOffsetY;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힘
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
                _mpb.SetFloat(EdgeSoftID, edgeSoft);
                _mpb.SetFloat(SideSoftID, sideSoft);
                _mpb.SetFloat(CenterFalloffID, centerFalloff);
                _mpb.SetFloat(RayDensityID, rayDensity);
                _mpb.SetFloat(RaySharpID, raySharp);
                _mpb.SetFloat(RayRotSpeedID, rayRotSpeed);
                _mpb.SetFloat(ValleyWidthID, valleyWidth);
            }
            // 정규화 반경(쿼드=max 반경 기준)은 스케일과 짝이라 항상 주입
            float mr = Mathf.Max(discRadius, rayRadius, 1e-4f);
            _mpb.SetFloat(DiscRadiusID, Mathf.Clamp01(discRadius / mr));
            _mpb.SetFloat(RayRadiusID, Mathf.Clamp01(rayRadius / mr));
            _mpb.SetFloat(FadeMulID, _envelope);
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
