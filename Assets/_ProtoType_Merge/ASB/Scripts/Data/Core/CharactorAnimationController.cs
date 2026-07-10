using System.Collections;
using PrimeTween;
using UnityEngine;

/// <summary>
/// 전투 유닛 Animator 제어 및 AnimEvent 수신. Animator는 자식 <c>model</c>에 부착된 것을 우선 탐색합니다.
/// </summary>
[DisallowMultipleComponent]
public class CharactorAnimationController : MonoBehaviour
{
    private const string StateIdle = "Idle";
    private const string TriggerAttack = "Attack";
    private const string TriggerHit = "Hit";
    private const string BoolIsDead = "isDead";
    private const float CrossFadeDuration = 0.1f;
    private const float StateEnterWaitTimeoutSeconds = 0.1f;
    private const float SkillClipEndNormalizedThreshold = 0.95f;
    private const float SkillClipEndLoopGuardSeconds = 5f;

    [SerializeField] private Animator _animator;

    public float CurrentAnimSpeed { get; private set; } = 1.0f;

    /// <summary>마지막 <see cref="WaitForSkillClipEnd"/> 호출에서 누적된 전투 배속 기준 대기 시간(초).</summary>
    public float LastClipWaitBattleSeconds { get; private set; }

    public bool IsHitEventReached { get; private set; }

    public void SetAnimationSpeed(float speedMultiplier)
    {
        CurrentAnimSpeed = Mathf.Max(0.01f, speedMultiplier);

        if (_animator != null)
        {
            _animator.speed = CurrentAnimSpeed;
        }
    }

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

        string stateName = ResolveSkillStateName(skill);
        if (string.IsNullOrEmpty(stateName))
        {
            return;
        }

        _animator.CrossFade(stateName, CrossFadeDuration);
    }

    /// <summary>
    /// CrossFade 스킬 상태 이름만 반환합니다. 기본 공격(SetTrigger)은 빈 문자열을 반환합니다.
    /// </summary>
    public string GetTargetStateName(SkillData skill)
    {
        if (skill == null)
        {
            return string.Empty;
        }

        int skillIdx = skill.skillIndex;
        int animNumber = (skillIdx / 10) % 10;
        if (animNumber == 0)
        {
            animNumber = 1;
        }

        return skillIdx >= 300000
            ? $"WeaponSkill_{animNumber}"
            : $"ClassSkill_{animNumber}";
    }

    /// <summary>근접 반격 연출용 CrossFade 상태명 (ClassSkill_1).</summary>
    public string GetCounterAttackStateName()
    {
        return "ClassSkill_1";
    }

    /// <summary>
    /// 대상 상태 진입 후 normalizedTime이 임계값에 도달할 때까지 대기합니다.
    /// </summary>
    public IEnumerator WaitForSkillClipEnd(string stateName)
    {
        LastClipWaitBattleSeconds = 0f;

        if (_animator == null || string.IsNullOrEmpty(stateName))
        {
            yield break;
        }

        float elapsedBattleAnimTime = 0f;
        while (!_animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)
               && elapsedBattleAnimTime < StateEnterWaitTimeoutSeconds)
        {
            yield return null;
            elapsedBattleAnimTime += Time.deltaTime * CurrentAnimSpeed;
        }

        if (!_animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
        {
            yield break;
        }

        while (elapsedBattleAnimTime < SkillClipEndLoopGuardSeconds)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(stateName))
            {
                LastClipWaitBattleSeconds = elapsedBattleAnimTime;
                yield break;
            }

            if (stateInfo.normalizedTime >= SkillClipEndNormalizedThreshold)
            {
                LastClipWaitBattleSeconds = elapsedBattleAnimTime;
                yield break;
            }

            yield return null;
            elapsedBattleAnimTime += Time.deltaTime * CurrentAnimSpeed;
        }

        LastClipWaitBattleSeconds = elapsedBattleAnimTime;
    }

    private static string ResolveSkillStateName(SkillData skill)
    {
        if (skill == null)
        {
            return TriggerAttack;
        }

        int skillIdx = skill.skillIndex;
        int animNumber = (skillIdx / 10) % 10;
        if (animNumber == 0)
        {
            animNumber = 1;
        }

        return skillIdx >= 300000
            ? $"WeaponSkill_{animNumber}"
            : $"ClassSkill_{animNumber}";
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

    /// <summary>피격·사망·부활 등 범용 연출. Has Exit Time 영향을 받지 않도록 CrossFade로 즉시 전환합니다.</summary>
    public void PlayGenericAnimation(string actionType)
    {
        if (_animator == null || string.IsNullOrWhiteSpace(actionType))
        {
            return;
        }

        switch (actionType.Trim())
        {
            case "Hit":
                _animator.ResetTrigger(TriggerHit);
                _animator.CrossFade("Hit", 0.03f, 0, 0f);
                break;
            case "Die":
                _animator.SetBool(BoolIsDead, true);
                _animator.CrossFade("Dead", 0.05f, 0, 0f);
                break;
            case "Revive":
                _animator.SetBool(BoolIsDead, false);
                _animator.CrossFade(StateIdle, CrossFadeDuration);
                break;
            default:
                _animator.CrossFade(actionType.Trim(), 0.05f, 0, 0f);
                break;
        }
    }

    /// <summary>현재 또는 다음 전환 중인 상태가 stateName인지 확인합니다.</summary>
    public bool IsInState(string stateName)
    {
        if (_animator == null) return false;
        return _animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)
            || _animator.GetNextAnimatorStateInfo(0).IsName(stateName);
    }

    /// <summary>현재 상태가 stateName이고 normalizedTime이 threshold 이상인지 확인합니다. 상태가 아니면 true 반환(이미 지남).</summary>
    public bool IsStateNearEnd(string stateName, float threshold = 0.9f)
    {
        if (_animator == null) return true;
        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(stateName)) return true;
        return info.normalizedTime >= threshold;
    }

    /// <summary>레거시 호출 호환.</summary>
    public void PlayLegacyTrigger(string triggerName) => PlayGenericAnimation(triggerName);

    public IEnumerator MoveToTarget(Transform target, float approachDistance, float duration)
    {
        if (_animator != null)
        {
            _animator.CrossFade("MoveForward", CrossFadeDuration);
        }

        Vector3 dir = (transform.position - target.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = transform.forward;
        }
        dir.Normalize();

        Vector3 destination = target.position + dir * approachDistance;
        destination.y = transform.position.y;

        Quaternion targetRotation = Quaternion.Euler(0f, Quaternion.LookRotation(-dir).eulerAngles.y, 0f);

        yield return Tween.Rotation(transform, targetRotation, 0.1f).ToYieldInstruction();
        yield return Tween.Position(transform, destination, duration).ToYieldInstruction();
    }

    public IEnumerator MoveToOrigin(Vector3 origin, Quaternion originalRotation, float duration)
    {
        if (_animator != null)
        {
            _animator.CrossFade("MoveReturn", CrossFadeDuration);
        }

        Tween.Rotation(transform, Quaternion.Euler(0f, originalRotation.eulerAngles.y, 0f), duration);
        yield return Tween.Position(transform, origin, duration).ToYieldInstruction();
    }

    private void CacheAnimator()
    {
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>(true);
        }

        if (_animator == null)
        {
            Debug.LogWarning(
                $"[CharactorAnimationController] Animator not found on '{name}' or its children.",
                this);
            return;
        }

        _animator.speed = CurrentAnimSpeed;
        EnsureAnimationEventBridge();
    }

    private void EnsureAnimationEventBridge()
    {
        GameObject eventHost = _animator != null ? _animator.gameObject : null;

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
