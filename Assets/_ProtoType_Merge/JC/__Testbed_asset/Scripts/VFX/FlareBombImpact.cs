using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 탄착 이펙트 원샷 드라이버.
    /// 스타버스트 쿼드 2장(지면 확산/수직 스파이크, FlareImpactBurst) + 지면 충격 링(FlareImpactRing)의
    /// _Progress를 MPB로 구동한다(기존 라이브 프리뷰 MPB 값 보존).
    /// Play(pos): 배치·활성화 → 진행 → 종료 시 비활성+RaiseFinished.
    /// 에디트 모드에서는 Awake가 돌지 않으므로 렌더러를 켠 채 프리셋의 previewProgress로 정지 프레임 튜닝 가능.
    /// </summary>
    public class FlareBombImpact : VfxEffect
    {
        [Header("References")]
        [Tooltip("스타버스트 렌더러들(BurstGround, BurstSpike).")]
        [SerializeField] private Renderer[] burstRenderers;
        [Tooltip("지면 충격 링 렌더러(GroundRing).")]
        [SerializeField] private Renderer ringRenderer;

        [Header("Timing")]
        [Tooltip("버스트 전체 재생 시간(초).")]
        [SerializeField] private float burstDuration = 0.7f;
        [Tooltip("링 전체 재생 시간(초).")]
        [SerializeField] private float ringDuration = 0.55f;
        [Tooltip("탄착점에서 링을 내릴 거리(m). 적 몸통 명중 시 지면까지의 높이.")]
        [SerializeField] private float ringGroundOffset = 0.8f;

        private float _t;
        private bool _running;
        private MaterialPropertyBlock _mpb;
        private static readonly int ProgressID = Shader.PropertyToID("_Progress");

        public float BurstDuration { get => burstDuration; set => burstDuration = value; }
        public float RingDuration { get => ringDuration; set => ringDuration = value; }
        public float RingGroundOffset { get => ringGroundOffset; set => ringGroundOffset = value; }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            SetActiveAll(false);
        }

        public override void Play() => Play(transform.position);

        public void Play(Vector3 worldPos)
        {
            transform.position = worldPos;
            if (ringRenderer != null)
                ringRenderer.transform.position = worldPos + Vector3.down * ringGroundOffset;
            _t = 0f;
            _running = true;
            IsPlaying = true;
            SetActiveAll(true);
            PushProgress();
        }

        public override void Stop()
        {
            _running = false;
            IsPlaying = false;
            SetActiveAll(false);
        }

        private void Update()
        {
            if (!_running) return;
            _t += Time.deltaTime;
            PushProgress();
            if (_t >= Mathf.Max(burstDuration, ringDuration))
            {
                _running = false;
                IsPlaying = false;
                SetActiveAll(false);
                RaiseFinished();
            }
        }

        private void PushProgress()
        {
            _mpb ??= new MaterialPropertyBlock();
            float burstP = Mathf.Clamp01(_t / Mathf.Max(burstDuration, 0.01f));
            float ringP = Mathf.Clamp01(_t / Mathf.Max(ringDuration, 0.01f));
            if (burstRenderers != null)
                foreach (var r in burstRenderers)
                    if (r != null) SetProgress(r, burstP);
            if (ringRenderer != null) SetProgress(ringRenderer, ringP);
        }

        private void SetProgress(Renderer r, float p)
        {
            r.GetPropertyBlock(_mpb);   // 프리셋 라이브 프리뷰 MPB 값 보존
            _mpb.SetFloat(ProgressID, p);
            r.SetPropertyBlock(_mpb);
        }

        private void SetActiveAll(bool on)
        {
            if (burstRenderers != null)
                foreach (var r in burstRenderers)
                    if (r != null) r.gameObject.SetActive(on);
            if (ringRenderer != null) ringRenderer.gameObject.SetActive(on);
        }
    }
}
