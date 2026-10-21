using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 율리아 손 위 대기 구체(기획 Y01)의 상태 전환. 구체 자신(손 소켓 자식)에 붙인다.
/// <list type="bullet">
/// <item>시전: 같은 손 소켓에 다른 이펙트(손 충전 FX_400010 등)가 붙어 재생 중이면 구체를 줄여 충전으로 넘긴다.
///   충전이 끝나 소켓에서 떨어지면 원래 크기로 돌아온다(Idle 복귀 시 작은 구체).</item>
/// <item>피격: 체력이 줄면 hitSeconds 동안 발광을 줄였다가 되돌린다(GetHit01 동안 축소, 기립 후 복귀).</item>
/// <item>감금(Y11): YuliaStasisVfx가 이 오브젝트를 끄므로 여기서는 다루지 않는다.</item>
/// </list>
/// 크기만 바꾸고 게임 상태는 바꾸지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class YuliaHandOrbState : MonoBehaviour
{
    private const float MinScale = 0.001f;

    [Tooltip("손 충전이 재생되는 동안의 크기 배율. 0이면 충전 이펙트만 보인다.")]
    [SerializeField, Range(0f, 1f)] private float castScale = 0f;
    [Tooltip("피격 동안의 크기 배율.")]
    [SerializeField, Range(0f, 1f)] private float hitScale = 0.45f;
    [Tooltip("피격 축소 유지 시간(전투 배속 기준 초).")]
    [SerializeField, Min(0f)] private float hitSeconds = 0.8f;
    [Tooltip("크기 전환 시간(전투 배속 기준 초).")]
    [SerializeField, Min(0.01f)] private float blendSeconds = 0.12f;

    private readonly List<JC.VFX.VfxEffect> _socketEffects = new List<JC.VFX.VfxEffect>();
    private BattleCharactor _owner;
    private Vector3 _baseScale;
    private bool _baseCached;
    private float _current = 1f;
    private float _hitTimer;
    private float _lastHp = -1f;

    private void OnEnable()
    {
        if (!_baseCached)
        {
            _baseScale = transform.localScale;
            _baseCached = true;
        }

        _owner = GetComponentInParent<BattleCharactor>();
        if (_owner != null)
        {
            _lastHp = _owner.CurrentHp;
            _owner.OnHpChanged += HandleHpChanged;
        }

        _current = 1f;
        _hitTimer = 0f;
        transform.localScale = _baseScale;
    }

    private void OnDisable()
    {
        if (_owner != null) _owner.OnHpChanged -= HandleHpChanged;
        _owner = null;
        if (_baseCached) transform.localScale = _baseScale;
    }

    private void HandleHpChanged(float currentHp, float maxHp)
    {
        bool hit = UnitHitFlashVfx.ShouldFlash(_lastHp, currentHp);
        _lastHp = currentHp;
        if (hit) _hitTimer = hitSeconds;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime * BattleSpeed();
        if (_hitTimer > 0f) _hitTimer -= dt;

        float target = ResolveTargetScale(IsSocketEffectPlaying(), _hitTimer > 0f, castScale, hitScale);
        _current = Mathf.MoveTowards(_current, target, dt / blendSeconds);
        // 스케일 0은 VFX 바운드를 무효로 만들 수 있어 아주 작은 값으로 둔다.
        transform.localScale = _baseScale * Mathf.Max(MinScale, _current);
    }

    // 손 충전은 VisualEffectGraphEffect의 소켓 부착으로 같은 소켓 아래에 붙고, 정지(Signal) 때 떨어진다.
    private bool IsSocketEffectPlaying()
    {
        Transform socket = transform.parent;
        if (socket == null) return false;

        socket.GetComponentsInChildren(false, _socketEffects);
        for (int i = 0; i < _socketEffects.Count; i++)
        {
            JC.VFX.VfxEffect effect = _socketEffects[i];
            if (effect == null || effect.transform == transform || effect.transform.IsChildOf(transform)) continue;
            if (effect.IsPlaying) return true;
        }

        return false;
    }

    private static float BattleSpeed()
    {
        return BattleManager.Instance != null ? Mathf.Max(0.01f, BattleManager.Instance.CurrentBattleSpeed) : 1f;
    }

    /// <summary>목표 크기 배율. 시전(충전) 우선, 그다음 피격, 평소 1.</summary>
    public static float ResolveTargetScale(bool charging, bool hit, float castScale, float hitScale)
    {
        if (charging) return castScale;
        return hit ? hitScale : 1f;
    }
}
