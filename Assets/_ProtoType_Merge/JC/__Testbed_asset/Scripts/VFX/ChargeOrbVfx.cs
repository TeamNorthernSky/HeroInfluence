using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 차징 구체 VFX. Play() 시 소켓 위치에서 startWorldSize→endWorldSize로 growDuration에 걸쳐 커지며,
    /// 씬-공간 트레일 팔로워가 손 궤적을 따라 파티클 트레일/반짝임을 남긴다.
    /// Stop() 시 구체는 즉시 사라지고 트레일/반짝임은 수명대로 자연 소멸.
    /// ★크기는 "월드 지름"을 목표로 부모 lossyScale를 보정(믹사모 본 lossyScale 100 등에도 견고).
    /// 트리거는 외부(하네스/스킬 로직)가 담당 — 이 컴포넌트는 룩과 거동만 책임진다.
    /// </summary>
    public class ChargeOrbVfx : VfxEffect
    {
        [Header("References")]
        [SerializeField] private Renderer coreRenderer;
        [SerializeField] private ParticleSystem sparks;
        [Tooltip("씬-공간 파티클 트레일 팔로워 프리팹(선택). Play 시 씬 루트에 1개 생성해 재사용.")]
        [SerializeField] private ChargeTrailFollower trailFollowerPrefab;

        [Header("Scale-In (월드 지름 기준)")]
        [Tooltip("시작 월드 지름(m). 확정값 0.01")]
        [SerializeField] private float startWorldSize = 0.015f;
        [Tooltip("최종 월드 지름(m).")]
        [SerializeField] private float endWorldSize = 0.11f;
        [Tooltip("커지는 데 걸리는 시간(초). 확정값 0.5")]
        [SerializeField] private float growDuration = 0.5f;

        private float _t;
        private bool _growing;
        private ChargeTrailFollower _follower;

        private void Awake() => ApplyHidden();

        public override void Play()
        {
            IsPlaying = true;
            _t = 0f;
            _growing = true;
            SetWorldSize(startWorldSize);
            if (coreRenderer) coreRenderer.enabled = true;
            if (sparks) { sparks.Clear(); sparks.Play(); }

            if (trailFollowerPrefab != null)
            {
                if (_follower == null)
                    _follower = Instantiate(trailFollowerPrefab);   // 씬 루트(부모 없음) = lossyScale 1
                _follower.Begin(transform.position);
            }
        }

        public override void Stop()
        {
            IsPlaying = false;
            _growing = false;
            if (coreRenderer) coreRenderer.enabled = false;                                   // 구체 즉시 사라짐
            if (sparks) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_follower) _follower.EndEmit();                                                // 트레일/반짝임 자연 소멸
            RaiseFinished();
        }

        private void ApplyHidden()
        {
            IsPlaying = false;
            _growing = false;
            if (coreRenderer) coreRenderer.enabled = false;
            if (sparks) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetWorldSize(startWorldSize);
        }

        private void Update()
        {
            if (_growing)
            {
                _t += Time.deltaTime;
                float k = growDuration > 0f ? Mathf.Clamp01(_t / growDuration) : 1f;
                SetWorldSize(Mathf.Lerp(startWorldSize, endWorldSize, k));
                if (k >= 1f) _growing = false;
            }
            if (IsPlaying && _follower) _follower.Follow(transform.position);
        }

        /// <summary>부모 lossyScale를 보정해 구체가 지정 월드 지름이 되도록 localScale을 설정.</summary>
        private void SetWorldSize(float worldSize)
        {
            float parentScale = transform.parent ? Mathf.Max(1e-6f, transform.parent.lossyScale.x) : 1f;
            transform.localScale = Vector3.one * (worldSize / parentScale);
        }
    }
}
