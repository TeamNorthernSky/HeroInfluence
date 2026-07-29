using System;
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

    /// <summary>AniEvent_HoldBegin으로 애니가 프리즈(정지)된 상태인지. WaitHit 등 타임아웃 시계는 이 동안 멈춰야 한다.</summary>
    public bool IsHolding { get; private set; }
    private Coroutine _holdRoutine;

    /// <summary>타격(AniEvent_OnHit) 누적 횟수. 단조 증가하며 절대 초기화하지 않습니다. 다단히트 판정용.</summary>
    public int HitEventCount { get; private set; }
    private int _hitWaitBaseline;

    public int ComboAdvanceCount { get; private set; }
    private int _comboWaitBaseline;

    public void SetAnimationSpeed(float speedMultiplier)
    {
        CurrentAnimSpeed = Mathf.Max(0.01f, speedMultiplier);

        // 홀드(프리즈) 중에는 배속 값만 갱신하고 애니는 계속 정지시킨다(재개 시 최신 배속 적용).
        if (_animator != null && !IsHolding)
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

    /// <summary>하위호환용 bool만 초기화합니다. HitEventCount는 건드리지 않습니다.</summary>
    public void ResetHitEvent() => IsHitEventReached = false;

    /// <summary>WaitHitAction이 대기 시작 전(애니 재생 직전)에 호출해 현재 HitEventCount를 baseline으로 기록합니다.</summary>
    public void BeginHitWait() => _hitWaitBaseline = HitEventCount;

    /// <summary>BeginHitWait 이후 새 타격 이벤트가 도착했는지. WaitHitAction 완료 판정에 사용합니다.</summary>
    public bool HasHitSinceWaitBegan => HitEventCount > _hitWaitBaseline;

    public void BeginComboWait() => _comboWaitBaseline = ComboAdvanceCount;

    public bool HasComboAdvancedSinceWaitBegan => ComboAdvanceCount > _comboWaitBaseline;

    /// <summary>Unity Animation Event에서 호출 (함수명: AniEvent_OnHit). model 브리지에서도 전달됩니다.</summary>
    public void AniEvent_OnHit()
    {
        HitEventCount++;
        IsHitEventReached = true;
    }

    public void AniEvent_AdvanceCombo()
    {
        ComboAdvanceCount++;
    }

    /// <summary>
    /// Unity Animation Event (함수명: AniEvent_ReturnIdle). 지정한 블렌드 시간(초, 배속-시간)으로
    /// 현재 상태에서 Idle로 CrossFade한다. 공격 애니 중간에 빠져나가 Idle로 블렌딩할 때 사용.
    /// OnHit과 같은 프레임에 두면 "공격과 동시에 Idle 블렌드 시작"이 된다.
    /// </summary>
    public void AniEvent_ReturnIdle(float blendSeconds)
    {
        if (_animator == null)
        {
            return;
        }
        float blend = Mathf.Max(0f, blendSeconds);
        Debug.Log($"[IdleDiag] AniEvent_ReturnIdle → CrossFade Idle (blend={blend:F2}) — 클립 이벤트가 Idle 전환 수행");
        if (blend <= 0f)
        {
            _animator.CrossFadeInFixedTime(StateIdle, 0f, 0, 0f);
        }
        else
        {
            _animator.CrossFadeInFixedTime(StateIdle, blend, 0, 0f);
        }
    }

    /// <summary>
    /// Unity Animation Event (함수명: AniEvent_HoldBegin). holdBattleSeconds("배속-시간" 초) 동안
    /// 애니메이션을 정지(freeze)시킨 뒤 자동 재개한다. 차지/앞동작 홀드 연출용.
    /// 같은 프레임에 AniEvent_PresentationCue("cast")를 두면 차지 이펙트가 정지 구간 동안 떠 있는다.
    /// </summary>
    public void AniEvent_HoldBegin(float holdBattleSeconds)
    {
        if (_animator == null || holdBattleSeconds <= 0f)
        {
            return;
        }

        if (_holdRoutine != null)
        {
            StopCoroutine(_holdRoutine);
        }
        _holdRoutine = StartCoroutine(HoldRoutine(holdBattleSeconds));
    }

    private IEnumerator HoldRoutine(float holdBattleSeconds)
    {
        IsHolding = true;
        Debug.Log($"[IdleDiag] HoldBegin 시작 (freeze {holdBattleSeconds:F2} 배속-초) — 이 동안 현재 프레임 포즈로 정지");
        _animator.speed = 0f; // 프리즈

        float elapsedBattle = 0f;
        while (elapsedBattle < holdBattleSeconds)
        {
            // 배속-시간 기준 누적: 전투 배속이 높을수록 실제 정지 시간은 짧아진다(전투 전체와 일관).
            elapsedBattle += Time.deltaTime * Mathf.Max(0.01f, CurrentAnimSpeed);
            yield return null;
        }

        IsHolding = false;
        _holdRoutine = null;
        if (_animator != null)
        {
            _animator.speed = CurrentAnimSpeed; // 최신 배속으로 재개
        }
    }

    private void OnDisable()
    {
        // 홀드 중 비활성화되면 코루틴이 멈춰 프리즈가 남을 수 있으니 상태를 정리한다.
        if (IsHolding)
        {
            IsHolding = false;
            _holdRoutine = null;
            if (_animator != null)
            {
                _animator.speed = CurrentAnimSpeed;
            }
        }
    }

    public void PlayState(string stateName, float blendSeconds)
    {
        if (_animator == null || string.IsNullOrWhiteSpace(stateName)) return;
        if (blendSeconds <= 0f) { _animator.Play(stateName.Trim(), 0, 0f); return; }
        _animator.CrossFadeInFixedTime(stateName.Trim(), blendSeconds, 0, 0f);
    }

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

        string stateName = GetTargetStateName(skill);
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

        return ResolveSkillStateName(skill);
    }

    /// <summary>근접 반격 연출용 CrossFade 상태명 (ClassSkill_1).</summary>
    public string GetCounterAttackStateName()
    {
        return ResolveFallbackSkillStateName(0);
    }

    /// <summary>
    /// 대상 상태 진입 후 normalizedTime이 임계값에 도달할 때까지 대기합니다.
    /// </summary>
    public IEnumerator WaitForSkillClipEnd(string stateName)
    {
        yield return WaitForSkillClipEndOrSignal(stateName, null);
    }

    /// <summary>
    /// Waits until the current skill state has only <paramref name="nextBlendSeconds"/> left before its end.
    /// The incoming state's blend duration determines when its outgoing state must begin transitioning.
    /// </summary>
    public IEnumerator WaitForSkillTransitionStart(string stateName, float nextBlendSeconds)
    {
        yield return WaitForSkillTransitionStartOrSignal(stateName, nextBlendSeconds, null);
    }

    /// <summary>
    /// Waits for the latest of the calculated blend start and an optional combo-advance signal.
    /// If the signal never arrives, the state naturally reaches its end before falling back.
    /// </summary>
    public IEnumerator WaitForSkillTransitionStartOrSignal(string stateName, float nextBlendSeconds,
        Func<bool> hasAdvanceSignal)
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

        AnimatorStateInfo enteredState = _animator.GetCurrentAnimatorStateInfo(0);
        if (!enteredState.IsName(stateName))
        {
            LastClipWaitBattleSeconds = elapsedBattleAnimTime;
            yield break;
        }

        if (enteredState.loop || enteredState.length <= Mathf.Epsilon)
        {
            Debug.LogWarning($"[CharactorAnimationController] Cannot calculate fixed-time blend start for looping or zero-length state '{stateName}'. Falling back to the legacy clip-end wait.", this);
            yield return WaitForSkillClipEndOrSignal(stateName, hasAdvanceSignal);
            yield break;
        }

        float safeBlendSeconds = Mathf.Max(0f, nextBlendSeconds);
        float transitionStartNormalized = safeBlendSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(1f - safeBlendSeconds / enteredState.length);
        bool blendStartPassedWithoutSignal = false;

        while (elapsedBattleAnimTime < SkillClipEndLoopGuardSeconds)
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(stateName))
            {
                LastClipWaitBattleSeconds = elapsedBattleAnimTime;
                yield break;
            }

            bool atBlendStart = stateInfo.normalizedTime >= transitionStartNormalized;
            if (hasAdvanceSignal == null)
            {
                if (atBlendStart)
                {
                    LastClipWaitBattleSeconds = elapsedBattleAnimTime;
                    yield break;
                }
            }
            else
            {
                bool signalReceived = hasAdvanceSignal();
                if (signalReceived && atBlendStart)
                {
                    if (blendStartPassedWithoutSignal)
                    {
                        Debug.LogWarning($"[CharactorAnimationController] Combo advance for '{stateName}' arrived after its calculated blend start ({transitionStartNormalized:0.###}). The overlap will be shorter than requested.", this);
                    }

                    LastClipWaitBattleSeconds = elapsedBattleAnimTime;
                    yield break;
                }

                if (atBlendStart)
                {
                    blendStartPassedWithoutSignal = true;
                }

                if (stateInfo.normalizedTime >= 1f)
                {
                    if (!signalReceived)
                    {
                        Debug.LogWarning($"[CharactorAnimationController] Combo advance for '{stateName}' was not received before the clip ended. Falling back to a no-overlap transition.", this);
                    }

                    LastClipWaitBattleSeconds = elapsedBattleAnimTime;
                    yield break;
                }
            }

            yield return null;
            elapsedBattleAnimTime += Time.deltaTime * CurrentAnimSpeed;
        }

        LastClipWaitBattleSeconds = elapsedBattleAnimTime;
    }


    /// <summary>
    /// 대상 state의 클립 종료 또는 외부 신호 중 먼저 도착한 시점까지 대기합니다.
    /// 신호가 없으면 <see cref="WaitForSkillClipEnd"/>와 동일하게 동작합니다.
    /// </summary>
    public IEnumerator WaitForSkillClipEndOrSignal(string stateName, Func<bool> hasEarlySignal)
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
            if (hasEarlySignal != null && hasEarlySignal())
            {
                LastClipWaitBattleSeconds = elapsedBattleAnimTime;
                yield break;
            }

            yield return null;
            elapsedBattleAnimTime += Time.deltaTime * CurrentAnimSpeed;
        }

        if (!_animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
        {
            LastClipWaitBattleSeconds = elapsedBattleAnimTime;
            yield break;
        }

        while (elapsedBattleAnimTime < SkillClipEndLoopGuardSeconds)
        {
            if (hasEarlySignal != null && hasEarlySignal())
            {
                LastClipWaitBattleSeconds = elapsedBattleAnimTime;
                yield break;
            }

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(stateName)
                || stateInfo.normalizedTime >= SkillClipEndNormalizedThreshold)
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
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(skill.StateName))
        {
            return skill.StateName.Trim();
        }

        return ResolveFallbackSkillStateName(skill.skillIndex);
    }

    private static string ResolveFallbackSkillStateName(int skillIndex)
    {
        int animNumber = (skillIndex / 10) % 10;
        if (animNumber == 0)
        {
            animNumber = 1;
        }

        return skillIndex >= 300000
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

    /// <summary>
            /// 지정 state가 현재 레이어 0을 소유할 때 normalizedTime과 state 길이를 반환합니다.
            /// 스핀 스윕처럼 애니메이션 시간을 외부 연출 진행도로 사용할 때만 사용합니다.
            /// </summary>
            public bool TryGetCurrentStateProgress(string stateName, out float normalizedTime, out float stateLength)
            {
                normalizedTime = 0f;
                stateLength = 0f;
                if (_animator == null || string.IsNullOrWhiteSpace(stateName))
                {
                    return false;
                }
        
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(stateName))
                {
                    return false;
                }
        
                normalizedTime = info.normalizedTime;
                stateLength = info.length;
                return true;
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

    public IEnumerator MoveToTarget(Transform target, float approachDistance, float duration,
        string animationStateName = null, float blendInSeconds = CrossFadeDuration)
    {
        if (_animator != null)
        {
            string stateName = string.IsNullOrWhiteSpace(animationStateName) ? "MoveForward" : animationStateName.Trim();
            PlayState(stateName, Mathf.Max(0f, blendInSeconds));
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

    /// <summary>월드 좌표 Entry로 향하는 스핀 스윕 전용 접근 이동.</summary>
            public IEnumerator MoveToPosition(Vector3 destination, float duration,
                string animationStateName = null, float blendInSeconds = CrossFadeDuration)
            {
                if (_animator != null)
                {
                    string stateName = string.IsNullOrWhiteSpace(animationStateName) ? "MoveForward" : animationStateName.Trim();
                    PlayState(stateName, Mathf.Max(0f, blendInSeconds));
                }
        
                destination.y = transform.position.y;
                Vector3 direction = destination - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    yield return Tween.Rotation(transform, rotation, 0.1f).ToYieldInstruction();
                }
        
                yield return Tween.Position(transform, destination, Mathf.Max(0.01f, duration)).ToYieldInstruction();
            }
        
            public IEnumerator MoveToOrigin(Vector3 origin, Quaternion originalRotation, float duration,
        string animationStateName = null, float blendInSeconds = CrossFadeDuration)
    {
        if (_animator != null)
        {
            string stateName = string.IsNullOrWhiteSpace(animationStateName) ? "MoveReturn" : animationStateName.Trim();
            PlayState(stateName, Mathf.Max(0f, blendInSeconds));
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
