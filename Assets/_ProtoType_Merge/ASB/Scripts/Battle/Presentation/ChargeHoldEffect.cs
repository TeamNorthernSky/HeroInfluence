using UnityEngine;

/// <summary>
/// 유지형(held) 차지 이펙트. Spawn Cue로 생성되면 파티클을 재생하며 그 자리(또는 소켓)에서 유지되고,
/// 후속 Stop/Signal Cue가 올 때까지 사라지지 않는다. 전투 타입(피해/상태이상)은 절대 참조하지 않는다.
/// 프리팹에 이 컴포넌트가 있어야 UnitEffectPresenter가 InstanceKey로 held 등록을 할 수 있다.
/// </summary>
[DisallowMultipleComponent]
public class ChargeHoldEffect : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    [Tooltip("재생할 파티클. 비우면 자식에서 자동 수집한다. Looping을 켜야 Stop 전까지 유지된다.")]
    [SerializeField] private ParticleSystem[] _systems;

    [Tooltip("생성 소켓(공중 소켓/손 소켓 등)에 붙어 따라다닐지 여부. false면 생성 순간 위치에 월드 고정.")]
    [SerializeField] private bool _followSocket = true;

    [Tooltip("Stop 후 잔상이 남을 시간(초). 이후 오브젝트 파괴.")]
    [SerializeField] private float _fadeOutSeconds = 0.15f;

    private bool _stopped;

    [Header("플레어봄 소환 및 비행")]
    [Tooltip("켜면 소환한 구체를 발사 단계에 넘깁니다. Signal로 파괴하지 않으며 기본값은 기존 종료 방식입니다.")]
    [SerializeField] private bool _handoffToProjectile;
    [Tooltip("소환 상승·성장 시간(전투 1배속 초). 발사 시각은 타임라인이 소유합니다.")]
    [SerializeField, Min(.01f)] private float _summonSeconds = .304f;
    [Tooltip("소환 시작 위치에서 머리 위 소켓까지 상승하는 월드 거리(m).")]
    [SerializeField, Min(0)] private float _riseHeight = .45f;
    [Tooltip("소환 시작 크기 배율. 1은 원래 크기이며 0은 보이지 않는 크기입니다.")]
    [SerializeField, Range(0,1)] private float _startScale = .12f;
    private Transform _anchor;
    private Vector3 _fullScale;
    private float _elapsed, _speed = 1f, _visualTime;
    private bool _summoning;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _block;

    /// <summary>Spawn 순간 1회. 소켓에 붙이고 파티클을 재생한다.</summary>
    public void Play(SkillEffectContext ctx)
    {
        _stopped = false;
        var volumeVisual = GetComponent<JC.VFX.FlareVolumeVisual>();
        if (volumeVisual != null && volumeVisual.preset != null)
        {
            _summonSeconds = volumeVisual.preset.summonSeconds;
            _riseHeight = volumeVisual.preset.riseHeight;
            _startScale = volumeVisual.preset.startScale;
            volumeVisual.SetSpeed(ctx?.PlaybackSpeed ?? 1f);
            volumeVisual.Initialize();
        }
        if (_followSocket && ctx != null && ctx.SocketTransform != null)
        {
            transform.SetParent(ctx.SocketTransform, worldPositionStays: true);
        }

        if (_systems == null || _systems.Length == 0)
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        foreach (ParticleSystem ps in _systems)
        {
            if (ps == null)
            {
                continue;
            }
            ps.Clear();
            ps.Play();
        }
        if (_handoffToProjectile)
        {
            _anchor = ctx?.SocketTransform;
            _speed = Mathf.Max(.01f, ctx?.PlaybackSpeed ?? 1f);
            _fullScale = transform.localScale;
            _elapsed = _visualTime = 0;
            _summoning = true;
            UpdateSummon(0);
        }
    }

    private void Update()
    {
        if (!_handoffToProjectile || _stopped) return;
        _visualTime += Time.deltaTime * _speed;
        if (_summoning)
        {
            _elapsed += Time.deltaTime * _speed;
            UpdateSummon(Mathf.Clamp01(_elapsed / Mathf.Max(.01f, _summonSeconds)));
        }
        SetVisual(Vector3.zero, -1f);
    }

    private void UpdateSummon(float t)
    {
        var volumeVisual = GetComponent<JC.VFX.FlareVolumeVisual>();
        float riseProgress = volumeVisual != null && volumeVisual.preset != null
            ? (volumeVisual.preset.startAtApex ? 1f : Mathf.Clamp01(t * _summonSeconds / volumeVisual.preset.RiseSeconds)) : t;
        float u = 1f - Mathf.Pow(1f - riseProgress, 3);
        if (_anchor != null) transform.position = _anchor.position - Vector3.up * (_riseHeight * (1f-u));
        transform.localScale = _fullScale * Mathf.Lerp(_startScale, 1, u);
        volumeVisual?.SummonPose(t);
    }

    public void BeginFlight(float speed)
    {
        if (!_handoffToProjectile) return;
        // Preserve the final summon pose before detaching, including a scaled socket parent.
        if (_summoning) UpdateSummon(1);
        transform.SetParent(null, true);
        _summoning = false;
        _speed = Mathf.Max(.01f, speed);
        GetComponent<JC.VFX.FlareVolumeVisual>()?.Begin(_speed);
    }

    public void UpdateFlightVisual(Vector3 direction, float progress)
    {
        if (_handoffToProjectile)
        {
            SetVisual(direction.normalized, Mathf.Clamp01(progress));
            GetComponent<JC.VFX.FlareVolumeVisual>()?.Pose(direction, progress);
        }
    }

    private void SetVisual(Vector3 direction, float progress)
    {
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>(true);
        if (_block == null) _block = new MaterialPropertyBlock();
        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(_block);
            _block.SetFloat("_EffectTime", _visualTime);
            _block.SetFloat("_UseEffectTime", 1);
            if (progress >= 0)
            {
                _block.SetVector("_TravelDirection", direction);
                _block.SetFloat("_FlightAmount", Mathf.Lerp(.35f, 1, progress));
            }
            renderer.SetPropertyBlock(_block);
        }
    }

    public void HideAtImpact()
    {
        if (!_handoffToProjectile) return;
        _stopped = true;
        var volumeVisual = GetComponent<JC.VFX.FlareVolumeVisual>();
        if (volumeVisual != null) { volumeVisual.End(); return; }
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        foreach (var light in GetComponentsInChildren<Light>(true)) light.enabled = false;
    }

    /// <summary>"fire" Cue를 Signal로 쓸 때. 여기서는 Stop과 동일 취급하고 수명 종료를 알린다.</summary>
    public bool Signal(SkillEffectContext ctx)
    {
        if (_handoffToProjectile) return false; // The projectile marker consumes the registered handle.
        Stop();
        return true;
    }

    /// <summary>"fire" Cue를 Stop으로 쓸 때. 방출만 멈춰 잔상을 남기고 정리한다.</summary>
    public void Stop()
    {
        if (_stopped)
        {
            return;
        }
        _stopped = true;
        if (_handoffToProjectile)
        {
            _summoning = false;
            Destroy(gameObject);
            return;
        }

        if (_systems != null)
        {
            foreach (ParticleSystem ps in _systems)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        Destroy(gameObject, Mathf.Max(0f, _fadeOutSeconds));
    }
}
