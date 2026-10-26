using UnityEngine;
using UnityEngine.VFX;

public enum VisualEffectLifetimeMode
{
    /// <summary>재생 후 모든 시스템이 잠들면 스스로 끝난다.</summary>
    OneShot,
    /// <summary>Stop/Signal을 받을 때까지 유지한다.</summary>
    Loop,
    /// <summary>유닛 프리팹 자식용. 스스로 파괴하지 않고 배속만 따라간다. SetActive로 켜고 끈다.</summary>
    Persistent,
}

/// <summary>
/// VFX Graph(VisualEffect) 이펙트 재료의 공통 처리 — 수명, 우아한 정지, 배속.
/// <para>
/// 기존 정리 경로(<see cref="EffectFallbackRelease"/>, <see cref="OneShotEffectBehaviour"/>)는 ParticleSystem만 보므로
/// VFX Graph 재료는 고정 시간에 잘리거나 즉시 파괴된다. 이 컴포넌트는 스포너를 멈춘 뒤 남은 입자가 사라지면 정리한다.
/// </para>
/// <para>
/// 큐 시스템은 프리팹에서 <see cref="ISkillEffectBehaviour"/>를 하나만 찾으므로 <see cref="AttachedEffectBehaviour"/>와
/// 같이 붙이지 않는다. 소켓 부착이 필요하면 이 컴포넌트의 Socket Attach 옵션을 쓴다.
/// </para>
/// <para>
/// Ultra Mega Pack처럼 루트는 빈 컨테이너이고 VisualEffect가 자식에 있는 프리팹이 많아 루트에 붙이고 자식 전체를 제어한다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class VisualEffectGraphEffect : JC.VFX.VfxEffect, ISkillEffectBehaviour, ISkillEffectHandle, ISelfManagedEffectLifetime
{
    public const float MinPlayRate = 0.01f;
    /// <summary>재생 직후 초기 이벤트가 처리되기 전 "잠듦"으로 오판하지 않기 위한 최소 재생 시간(초).</summary>
    public const float MinPlaySeconds = 0.1f;
    /// <summary>Stop 직후 alive/awake 상태가 한두 프레임 늦게 갱신되는 것을 기다리는 시간(초).</summary>
    public const float MinStopSettleSeconds = 0.05f;

    [SerializeField] private VisualEffectLifetimeMode mode = VisualEffectLifetimeMode.OneShot;

    [SerializeField, Min(0f)]
    [Tooltip("안전 상한(이펙트 시간, 초). 넘으면 우아한 정지로 넘어간다. 0이면 무제한. Persistent에서는 무시.")]
    private float maxLifetimeSeconds = 8f;

    [SerializeField, Min(0f)]
    [Tooltip("Stop 뒤 남은 입자를 기다리는 상한(이펙트 시간, 초).")]
    private float fadeOutMaxSeconds = 2f;

    [SerializeField] private bool destroyOnFinish = true;
    [SerializeField] private UliaVfx.UliaSavedEffect productionComposite;

    [Header("Socket Attach")]
    [SerializeField] private bool attachToSocket;
    [SerializeField] private Vector3 localPositionOffset;
    [SerializeField] private Vector3 localEulerOffset;
    [SerializeField]
    [Tooltip("InheritSocket: 소켓/본 스케일 상속, UseVisualRoot: 모델 전체 스케일만 유지, World: 부모 스케일 무시.")]
    private AttachedEffectScaleMode scaleMode = AttachedEffectScaleMode.InheritSocket;
    [SerializeField] private Vector3 localScale = Vector3.one;

    private VisualEffect[] _effects;
    private ParticleSystem[] _particles;
    private float _elapsed;
    private float _stopElapsed;
    private bool _stopping;
    private bool _finished;

    public VisualEffectLifetimeMode Mode => mode;

    private void OnEnable()
    {
        // 초기 이벤트(OnPlay)로 자동 재생되는 재료도 Play 호출 없이 수명을 추적한다.
        CacheComponents();
        ResetState();
    }

    /// <summary>큐 경로. 배속과 소켓 부착을 컨텍스트에서 받는다.</summary>
    public void Play(SkillEffectContext ctx)
    {
        if (ctx != null) PlaybackSpeed = ctx.PlaybackSpeed;
        if (attachToSocket && ctx?.SocketTransform != null)
        {
            AttachedEffectBehaviour.AttachToSocket(transform, ctx, localPositionOffset, localEulerOffset, scaleMode, localScale);
        }

        Restart();
    }

    /// <summary>피격 이펙트 경로(BattleVisualDirector.PlayHitEffect)처럼 인자 없이 부르는 재생.</summary>
    public override void Play() => Restart();

    /// <summary>출발점→목표점 재생. 전기 아크(<see cref="ElectricArcEndpoints"/>)가 있으면 두 점을 잇는다.</summary>
    public override void Play(Transform origin, Transform target)
    {
        GetComponent<ElectricArcEndpoints>()?.Bind(origin, target);
        Restart();
    }

    /// <summary>우아한 정지: 스포너를 멈추고 남은 입자가 사라지면 정리한다. 중복 호출은 무시한다.</summary>
    public override void Stop()
    {
        if (_finished || _stopping) return;

        CacheComponents();
        _stopping = true;
        _stopElapsed = 0f;
        IsPlaying = false;
        if (productionComposite != null) productionComposite.gameObject.SetActive(false);
        for (int i = 0; i < _effects.Length; i++)
        {
            if (_effects[i] != null) _effects[i].Stop();
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] != null) _particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>소켓에서 분리하고 우아한 정지를 시작한다. 이후 수명은 이 재료가 책임진다.</summary>
    public bool Signal(SkillEffectContext ctx)
    {
        transform.SetParent(null, true);
        Stop();
        return true;
    }

    private void Update()
    {
        if (mode == VisualEffectLifetimeMode.Persistent) PlaybackSpeed = CurrentBattleSpeed();

        // 히트스톱은 PlaybackSpeed를 이벤트 없이 직접 바꾸므로 매 프레임 반영한다.
        ApplyPlayRate();
        if (_finished) return;

        float dt = EffectDeltaTime;
        if (_stopping)
        {
            _stopElapsed += dt;
            if (ShouldCompleteStop(_stopElapsed, fadeOutMaxSeconds, AnyAlive())) Finish();
            return;
        }

        if (mode == VisualEffectLifetimeMode.Persistent) return;

        _elapsed += dt;
        if (mode == VisualEffectLifetimeMode.OneShot && ShouldFinishNaturally(_elapsed, AnyAlive()))
        {
            Finish();
            return;
        }

        // 카메라 밖 컬링 등으로 시스템이 잠들지 않아도 상한에서 정리된다.
        if (HasExceededLifetime(_elapsed, maxLifetimeSeconds)) Stop();
    }

    private void Restart()
    {
        CacheComponents();
        ResetState();
        if (productionComposite != null)
        {
            productionComposite.gameObject.SetActive(true);
            productionComposite.Replay();
        }
        for (int i = 0; i < _effects.Length; i++)
        {
            VisualEffect effect = _effects[i];
            if (effect == null) continue;

            // Reinit은 초기 이벤트를 다시 보낸다. 초기 이벤트가 없는 그래프만 Play로 시작한다(중복 버스트 방지).
            effect.Reinit();
            if (string.IsNullOrEmpty(effect.initialEventName)) effect.Play();
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] == null) continue;
            _particles[i].Clear(true);
            _particles[i].Play(true);
        }

        ApplyPlayRate();
    }

    private void ResetState()
    {
        _elapsed = 0f;
        _stopElapsed = 0f;
        _stopping = false;
        _finished = false;
        IsPlaying = true;
    }

    private void Finish()
    {
        if (_finished) return;

        _finished = true;
        _stopping = false;
        IsPlaying = false;
        RaiseFinished();
        if (destroyOnFinish && mode != VisualEffectLifetimeMode.Persistent) Destroy(gameObject);
    }

    private void ApplyPlayRate()
    {
        if (_effects == null) return;

        float rate = ResolvePlayRate(PlaybackSpeed);
        for (int i = 0; i < _effects.Length; i++)
        {
            VisualEffect effect = _effects[i];
            if (effect != null && !Mathf.Approximately(effect.playRate, rate)) effect.playRate = rate;
        }

        if (_particles == null) return;
        for (int i = 0; i < _particles.Length; i++)
        {
            ParticleSystem particle = _particles[i];
            if (particle == null) continue;
            var main = particle.main;
            if (!Mathf.Approximately(main.simulationSpeed, rate)) main.simulationSpeed = rate;
        }
    }

    private bool AnyAlive()
    {
        for (int i = 0; i < _effects.Length; i++)
        {
            if (_effects[i] != null && _effects[i].HasAnySystemAwake()) return true;
        }

        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i] != null && _particles[i].IsAlive(true)) return true;
        }

        return false;
    }

    private void CacheComponents()
    {
        if (_effects == null) _effects = GetComponentsInChildren<VisualEffect>(true);
        if (_particles == null) _particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    private static float CurrentBattleSpeed()
    {
        return BattleManager.Instance != null ? Mathf.Max(MinPlayRate, BattleManager.Instance.CurrentBattleSpeed) : 1f;
    }

    public static float ResolvePlayRate(float playbackSpeed)
    {
        return float.IsNaN(playbackSpeed) ? 1f : Mathf.Max(MinPlayRate, playbackSpeed);
    }

    public static bool ShouldFinishNaturally(float elapsed, bool anyAlive)
    {
        return elapsed >= MinPlaySeconds && !anyAlive;
    }

    public static bool ShouldCompleteStop(float stopElapsed, float fadeOutMaxSeconds, bool anyAlive)
    {
        return stopElapsed >= fadeOutMaxSeconds || (stopElapsed >= MinStopSettleSeconds && !anyAlive);
    }

    public static bool HasExceededLifetime(float elapsed, float maxLifetimeSeconds)
    {
        return maxLifetimeSeconds > 0f && elapsed >= maxLifetimeSeconds;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (productionComposite == null && GetComponentInChildren<VisualEffect>(true) == null)
            Debug.LogWarning($"[VisualEffectGraphEffect] '{name}'과 자식에 VisualEffect가 없습니다.", this);

        // 같은 오브젝트에 다른 수명/재생 재료가 있으면 큐 시스템이 한쪽만 부르거나 고정 시간에 잘린다.
        if (GetComponent<OneShotEffectBehaviour>() != null)
            Debug.LogWarning($"[VisualEffectGraphEffect] '{name}'에 OneShotEffectBehaviour가 같이 있습니다. VFX Graph 재료가 즉시 파괴됩니다.", this);
        if (GetComponent<EffectFallbackRelease>() != null)
            Debug.LogWarning($"[VisualEffectGraphEffect] '{name}'에 EffectFallbackRelease가 같이 있습니다. 고정 시간에 잘립니다.", this);
        if (GetComponent<AttachedEffectBehaviour>() != null)
            Debug.LogWarning($"[VisualEffectGraphEffect] '{name}'에 AttachedEffectBehaviour가 같이 있습니다. 큐 시스템은 재생 컴포넌트를 하나만 부르므로 Socket Attach 옵션을 쓰세요.", this);
    }
#endif
}
