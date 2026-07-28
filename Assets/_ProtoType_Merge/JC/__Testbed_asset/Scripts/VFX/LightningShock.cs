using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「체인 라이팅」 감전 잔류 팔로워.
    /// ShockAura 쿼드 1장이 대상 트랜스폼을 따라다니며 _Seed 플리커로 명멸,
    /// duration 경과 시 페이드 후 자기 파괴한다. ChainLightningVfx가 템플릿에서 복제·Init.
    /// </summary>
    public class LightningShock : MonoBehaviour
    {
        [Tooltip("아우라 렌더러(자기 자신).")]
        [SerializeField] private Renderer auraRenderer;
        [Tooltip("배경 버스트 렌더러(자식 ShockBg, 알파 블렌드). 아크와 시드를 공유해 함께 명멸.")]
        [SerializeField] private Renderer bgRenderer;

        private Transform _target;
        private Vector3 _offset;
        private float _duration = 1f;
        private float _fadeTime = 0.25f;
        private float _flickerRate = 18f;
        private float _t;
        private bool _running;
        private MaterialPropertyBlock _mpb;
        private static readonly int SeedID = Shader.PropertyToID("_Seed");
        private static readonly int OpacityID = Shader.PropertyToID("_Opacity");

        public bool IsDone => !_running;

        public void Init(Transform target, Vector3 offset, float worldSize,
                         float duration, float fadeTime, float flickerRate)
        {
            _target = target;
            _offset = offset;
            _duration = Mathf.Max(duration, 0.05f);
            _fadeTime = Mathf.Clamp(fadeTime, 0.01f, _duration);
            _flickerRate = flickerRate;
            transform.localScale = new Vector3(worldSize, worldSize, 1f);
            Follow();
            _t = 0f;
            _running = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_running) return;
            _t += Time.deltaTime;
            Follow();

            _mpb ??= new MaterialPropertyBlock();
            float seed = Mathf.Floor(Time.time * _flickerRate);
            float fade = 1f - Mathf.Clamp01((_t - (_duration - _fadeTime)) / _fadeTime);
            Drive(auraRenderer, seed, fade);
            Drive(bgRenderer, seed, fade);

            if (_t >= _duration)
            {
                _running = false;
                Destroy(gameObject);
            }
        }

        private void Drive(Renderer r, float seed, float fade)
        {
            if (r == null) return;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(SeedID, seed);
            _mpb.SetFloat(OpacityID, fade);
            r.SetPropertyBlock(_mpb);
        }

        private void Follow()
        {
            if (_target != null) transform.position = _target.position + _offset;
        }
    }
}
