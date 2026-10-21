using UnityEngine;

/// <summary>
/// 피격(Hit) 넉백 연출. Hit 애니가 재생될 때 외형(Model)만 공격자 반대쪽으로 살짝 밀었다가 원래 자리로 되돌린다.
/// <list type="bullet">
/// <item>루트 transform은 건드리지 않는다 → 칸 위치·OccupiedCell, 근접 접근·복귀, 대신 맞기 자리 교환(GuardSwapPresenter)이
///   루트를 옮기는 중에 맞아도 서로 덮어쓰지 않는다. 끝나면 외형 로컬 위치를 시작 값으로 정확히 되돌린다.</item>
/// <item>시간은 Hit 애니와 같은 시계(Time.deltaTime × 전투 배속 × 히트스톱 배율)로 잰다.</item>
/// <item>연속 피격은 현재 밀린 위치에서 다시 민다. 사망 피격은 Hit 대신 Dead를 재생하므로 넉백도 없다.</item>
/// </list>
/// 이 컴포넌트를 붙인 유닛에만 적용된다(현재 방패병 Unit_VillanSheild_20003).
/// </summary>
[DisallowMultipleComponent]
public sealed class HitKnockbackMotion : MonoBehaviour
{
    private const string HitActionName = "Hit";
    private const float MinDirectionSqr = .0001f;

    [Tooltip("피격 이벤트원. 비면 같은 오브젝트의 BattleCharactor.")]
    [SerializeField] private BattleCharactor owner;
    [Tooltip("밀어낼 외형 루트. 비면 자식 'Model'. 루트 자신을 넣으면 칸 위치가 흔들리므로 넣지 않는다.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("최대 밀림 거리(월드 m).")]
    [SerializeField, Min(0f)] private float distance = .3f;
    [Tooltip("밀려나는 시간(전투 초).")]
    [SerializeField, Min(.01f)] private float pushSeconds = .07f;
    [Tooltip("최대 거리에서 머무는 시간(전투 초).")]
    [SerializeField, Min(0f)] private float holdSeconds = .03f;
    [Tooltip("제자리로 돌아오는 시간(전투 초).")]
    [SerializeField, Min(.01f)] private float returnSeconds = .18f;

    private CharactorAnimationController _anim;
    private Vector3 _restLocalPosition;
    private Vector3 _fromOffset;
    private Vector3 _peakOffset;
    private Vector3 _offset;
    private float _elapsed;
    private bool _active;

    internal bool IsActive => _active;

    private void Awake()
    {
        if (owner == null) owner = GetComponent<BattleCharactor>();
        if (visualRoot == null) visualRoot = transform.Find("Model");
        if (visualRoot == transform) visualRoot = null;
        if (owner == null || visualRoot == null)
            Debug.LogWarning($"[HitKnockbackMotion] owner/visualRoot 없음 → 넉백 생략. ({name})", this);
    }

    private void OnEnable()
    {
        if (owner == null) return;
        owner.EnsureAnimationController();
        _anim = owner.Anim;
        if (_anim != null) _anim.GenericAnimationPlayed += HandleGenericAnimationPlayed;
    }

    private void OnDisable()
    {
        if (_anim != null) _anim.GenericAnimationPlayed -= HandleGenericAnimationPlayed;
        _anim = null;
        Finish();
    }

    private void Update()
    {
        if (!_active) return;
        float speed = _anim != null ? _anim.CurrentAnimSpeed * _anim.HitStopSpeedMultiplier : 1f;
        Tick(Time.deltaTime * speed);
    }

    private void HandleGenericAnimationPlayed(string action, BattleCharactor source)
    {
        if (action != HitActionName || owner == null || owner.IsDead || visualRoot == null) return;
        Begin(ResolveDirection(source));
    }

    /// <summary>공격자 → 자신 수평 방향. 공격자를 모르거나 겹쳐 있으면 바라보는 방향의 반대(뒤).</summary>
    private Vector3 ResolveDirection(BattleCharactor source)
    {
        Vector3 direction = source != null && source != owner
            ? transform.position - source.transform.position
            : Vector3.zero;
        direction.y = 0f;
        if (direction.sqrMagnitude < MinDirectionSqr)
        {
            direction = -transform.forward;
            direction.y = 0f;
        }

        return direction.sqrMagnitude < MinDirectionSqr ? Vector3.zero : direction.normalized;
    }

    internal void Begin(Vector3 worldDirection)
    {
        if (visualRoot == null) return;
        if (!_active)
        {
            _restLocalPosition = visualRoot.localPosition;
            _offset = Vector3.zero;
        }

        _fromOffset = _offset;
        _peakOffset = worldDirection * distance;
        _elapsed = 0f;
        _active = true;
    }

    /// <summary>전투 시간 dt만큼 진행. 끝나면 외형을 시작 로컬 위치로 정확히 되돌린다.</summary>
    internal void Tick(float battleDeltaTime)
    {
        if (!_active) return;
        if (visualRoot == null)
        {
            _active = false;
            return;
        }

        _elapsed += Mathf.Max(0f, battleDeltaTime);
        if (_elapsed < pushSeconds)
        {
            float t = _elapsed / pushSeconds;
            _offset = Vector3.LerpUnclamped(_fromOffset, _peakOffset, 1f - (1f - t) * (1f - t));
        }
        else if (_elapsed < pushSeconds + holdSeconds)
        {
            _offset = _peakOffset;
        }
        else
        {
            float u = (_elapsed - pushSeconds - holdSeconds) / returnSeconds;
            if (u >= 1f)
            {
                Finish();
                return;
            }

            _offset = Vector3.LerpUnclamped(_peakOffset, Vector3.zero, Mathf.SmoothStep(0f, 1f, u));
        }

        // 월드 방향을 매 프레임 부모 공간으로 바꾼다 — 넉백 중 루트가 회전해도 밀리는 방향은 그대로.
        Transform parent = visualRoot.parent;
        visualRoot.localPosition = _restLocalPosition + (parent != null ? parent.InverseTransformVector(_offset) : _offset);
    }

    private void Finish()
    {
        if (!_active) return;
        _active = false;
        _offset = Vector3.zero;
        if (visualRoot != null) visualRoot.localPosition = _restLocalPosition;
    }
}
