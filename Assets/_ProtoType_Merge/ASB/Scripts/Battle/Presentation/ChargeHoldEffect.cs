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

    /// <summary>Spawn 순간 1회. 소켓에 붙이고 파티클을 재생한다.</summary>
    public void Play(SkillEffectContext ctx)
    {
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
    }

    /// <summary>"fire" Cue를 Signal로 쓸 때. 여기서는 Stop과 동일 취급하고 수명 종료를 알린다.</summary>
    public bool Signal(SkillEffectContext ctx)
    {
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
