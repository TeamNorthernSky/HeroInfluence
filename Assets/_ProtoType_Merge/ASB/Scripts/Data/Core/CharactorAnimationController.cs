using UnityEngine;

/// <summary>
/// 전투 유닛 Animator 제어 및 AnimEvent 수신. Animator는 자식 <c>model</c>에 부착된 것을 우선 탐색합니다.
/// </summary>
[DisallowMultipleComponent]
public class CharactorAnimationController : MonoBehaviour
{
    private const string ModelChildName = "model";
    private const string StateIdle = "Idle";
    private const string TriggerAttack = "Attack";
    private const string TriggerHit = "Hit";
    private const string BoolIsDead = "isDead";
    private const float CrossFadeDuration = 0.1f;

    [SerializeField] private Animator _animator;

    public bool IsHitEventReached { get; private set; }

    private void Awake()
    {
        CacheAnimator();
    }

    private void OnValidate()
    {
        if (_animator == null)
        {
            CacheAnimator();
        }
    }

    public void ResetHitEvent() => IsHitEventReached = false;

    /// <summary>Unity Animation Event에서 호출 (함수명: AniEvent_OnHit). model 브리지에서도 전달됩니다.</summary>
    public void AniEvent_OnHit() => IsHitEventReached = true;

    /// <summary>
    /// 스킬 인덱스 규칙에 따라 ClassSkill_N / WeaponSkill_N 상태로 CrossFade 하거나, null이면 기본 공격 트리거를 실행합니다.
    /// </summary>
    public void PlaySkillAnimation(SkillData skill)
    {
        if (_animator == null)
        {
            return;
        }

        if (skill == null)
        {
            _animator.SetTrigger(TriggerAttack);
            return;
        }

        int skillIdx = skill.skillIndex;
        int animNumber = (skillIdx / 10) % 10;
        if (animNumber == 0)
        {
            animNumber = 1;
        }

        string stateName = skillIdx >= 300000
            ? $"WeaponSkill_{animNumber}"
            : $"ClassSkill_{animNumber}";

        _animator.CrossFade(stateName, CrossFadeDuration);
    }

    /// <summary>코루틴 종료 시점 등에서 Transition 없이 Idle로 강제 복귀합니다.</summary>
    public void PlayIdleAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.CrossFade(StateIdle, CrossFadeDuration);
    }

    /// <summary>피격·사망·부활 등 범용 연출 (Hit 트리거 / isDead Bool).</summary>
    public void PlayGenericAnimation(string actionType)
    {
        if (_animator == null || string.IsNullOrWhiteSpace(actionType))
        {
            return;
        }

        switch (actionType.Trim())
        {
            case "Hit":
                _animator.SetTrigger(TriggerHit);
                break;
            case "Die":
                _animator.SetBool(BoolIsDead, true);
                break;
            case "Revive":
                _animator.SetBool(BoolIsDead, false);
                _animator.CrossFade(StateIdle, CrossFadeDuration);
                break;
            default:
                _animator.SetTrigger(actionType);
                break;
        }
    }

    /// <summary>레거시 호출 호환.</summary>
    public void PlayLegacyTrigger(string triggerName) => PlayGenericAnimation(triggerName);

    private void CacheAnimator()
    {
        if (_animator == null)
        {
            Transform model = transform.Find(ModelChildName);
            if (model != null)
            {
                _animator = model.GetComponent<Animator>();
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }
        }

        if (_animator == null)
        {
            Debug.LogWarning(
                $"[CharactorAnimationController] Animator not found on '{name}' (expected child '{ModelChildName}').",
                this);
            return;
        }

        EnsureAnimationEventBridge();
    }

    private void EnsureAnimationEventBridge()
    {
        Transform model = transform.Find(ModelChildName);
        GameObject eventHost = model != null
            ? model.gameObject
            : _animator != null
                ? _animator.gameObject
                : null;

        if (eventHost == null)
        {
            return;
        }

        AnimationEventBridge bridge = eventHost.GetComponent<AnimationEventBridge>();
        if (bridge == null)
        {
            bridge = eventHost.AddComponent<AnimationEventBridge>();
        }

        bridge.Bind(this);
    }
}
