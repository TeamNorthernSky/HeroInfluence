using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 전투 턴 연출을 Idle Blend Tree의 TurnIdle(0=대기, 1=내 턴) 파라미터에 연결한다.
/// 턴이 없는 필드에서는 대기 자세를 유지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleTurnIdlePose : MonoBehaviour
{
    private static readonly int TurnIdleHash = Animator.StringToHash("TurnIdle");
    private static readonly int IdleStateHash = Animator.StringToHash("Idle");

    [SerializeField, Min(0f)] private float blendSeconds = 0.25f;

    private BattleCharactor _unit;
    private BattleFlowManager _flow;
    private Animator _animator;
    private PlayableDirector _director;
    private bool _hasParameter;
    private bool _turnResolved;
    // 행동은 확정됐지만 스킬명·카메라 연출이 먼저 나와 실제 동작은 아직 시작 전인 상태.
    private bool _releasePending;
    private float _current;
    private float _target;
    private float _nextFlowSearch;

    private void Awake()
    {
        _unit = GetComponent<BattleCharactor>();
        if (_unit == null) _unit = GetComponentInParent<BattleCharactor>();
        var animationController = GetComponent<CharactorAnimationController>();
        _animator = animationController != null ? animationController.Animator : null;
        if (_animator == null) _animator = GetComponentInChildren<Animator>(true);

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.nameHash != TurnIdleHash || parameter.type != AnimatorControllerParameterType.Float)
                    continue;
                _hasParameter = true;
                break;
            }
        }

        if (_hasParameter) _animator.SetFloat(TurnIdleHash, 0f);
    }

    private void OnEnable() => TryBindFlow();

    private void OnDisable()
    {
        UnbindFlow();
        _releasePending = false;
        _target = 0f;
        _current = 0f;
        if (_hasParameter && _animator != null) _animator.SetFloat(TurnIdleHash, 0f);
    }

    private void Update()
    {
        if (!_hasParameter) return;

        if (_flow == null && Time.unscaledTime >= _nextFlowSearch)
        {
            _nextFlowSearch = Time.unscaledTime + 1f;
            TryBindFlow();
        }

        if (_releasePending && HasActionMotionStarted())
        {
            _releasePending = false;
            _target = 0f;
        }

        float step = blendSeconds <= 0f ? 1f : Time.deltaTime / blendSeconds;
        _current = Mathf.MoveTowards(_current, _target, step);
        _animator.SetFloat(TurnIdleHash, _current);
    }

    private void TryBindFlow()
    {
        if (_unit == null) _unit = GetComponentInParent<BattleCharactor>();
        if (_flow != null || _unit == null || !_hasParameter) return;

        foreach (var candidate in FindObjectsOfType<BattleFlowManager>())
        {
            if (candidate.gameObject.scene != gameObject.scene) continue;
            _flow = candidate;
            _flow.OnTurnPresentationStarted += HandleTurnPresentationStarted;
            _flow.OnTurnStarted += HandleTurnStarted;
            _flow.OnTurnActionCommitted += HandleActionCommitted;
            _flow.OnTurnActionCancelled += HandleActionCancelled;
            _flow.OnTurnResolved += HandleTurnResolved;
            _flow.OnBattleEnded += HandleBattleEnded;
            _target = _flow.CurrentUnit == _unit && !_flow.IsActionInProgress ? 1f : 0f;
            break;
        }
    }

    private void UnbindFlow()
    {
        if (_flow == null) return;
        _flow.OnTurnPresentationStarted -= HandleTurnPresentationStarted;
        _flow.OnTurnStarted -= HandleTurnStarted;
        _flow.OnTurnActionCommitted -= HandleActionCommitted;
        _flow.OnTurnActionCancelled -= HandleActionCancelled;
        _flow.OnTurnResolved -= HandleTurnResolved;
        _flow.OnBattleEnded -= HandleBattleEnded;
        _flow = null;
    }

    /// <summary>
    /// 스킬 Timeline이 유닛의 Director로 재생되기 시작했거나 Animator가 Idle을 떠나면 실제 동작이 시작된 것으로 본다.
    /// 그 전에 대기 자세로 바꾸면 스킬명·카메라 연출 동안 팔이 먼저 내려간다.
    /// </summary>
    private bool HasActionMotionStarted()
    {
        if (_director == null && _unit != null) _unit.TryGetComponent(out _director);
        if (_director != null && _director.state == PlayState.Playing) return true;

        AnimatorStateInfo state = _animator.IsInTransition(0)
            ? _animator.GetNextAnimatorStateInfo(0)
            : _animator.GetCurrentAnimatorStateInfo(0);
        return state.shortNameHash != IdleStateHash;
    }

    private void HandleTurnPresentationStarted(BattleCharactor unit)
    {
        if (unit == _unit) _turnResolved = false;
        _releasePending = false;
        _target = unit == _unit ? 1f : 0f;
    }

    private void HandleTurnStarted(int roundIndex, BattleCharactor unit)
    {
        if (unit == _unit) _turnResolved = false;
        _releasePending = false;
        _target = unit == _unit ? 1f : 0f;
    }
    private void HandleActionCommitted(BattleCharactor unit)
    {
        if (unit == _unit) _releasePending = true;
    }
    private void HandleActionCancelled(BattleCharactor unit)
    {
        if (unit != _unit) return;
        _releasePending = false;
        if (!_turnResolved && !_unit.IsDead) _target = 1f;
    }
    private void HandleTurnResolved(TurnResolutionContext context)
    {
        if (context.Actor != _unit) return;
        _turnResolved = true;
        _releasePending = false;
        _target = 0f;
    }
    private void HandleBattleEnded(BattleResult result)
    {
        _turnResolved = true;
        _releasePending = false;
        _target = 0f;
    }
}
