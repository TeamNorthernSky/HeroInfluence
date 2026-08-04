using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ASB.Work.Battle.SkillExecution;
using ASB.Work.Battle.Core;
using ASB.Work.Battle.Sequence;
using PrimeTween;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

/// <summary>
/// BattleAction 및 플레이어 입력에 의한 전투 실행. 데미지는 항상 target.TakeDamage로 적용합니다.
/// </summary>
[DisallowMultipleComponent]
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }
    public event Action<string> OnActionExecuted;

    /// <summary>UI 등 외부에서 배속 변경을 요청할 때 발생시킵니다. AutoBattleController.OnAutoBattleToggleRequested와 동일한 패턴.</summary>
    public static event Action<float> OnBattleSpeedChangeRequested;

    internal const int ClassSkillEffect_Damage = 0;
    internal const int ClassSkillEffect_Heal = 1;
    internal const int ClassSkillEffect_Revive = 2;
    internal const int ClassSkillEffect_Buff = 3;

    internal const float AnimEventTimeoutSeconds = 2f;

    // 다중 아군 힐에서 주 대상 이후 인접 아군 힐 이펙트를 순차로 낼 때의 스태거 간격(배속-시간, 초).
    private const float MultiHealStaggerSeconds = 0.15f;

    private readonly struct SkillExecutionOptions
    {
        public readonly float ExtraMultiplier;
        public readonly bool IsCounterAttack;
        public readonly bool CanTriggerCounter;

        public SkillExecutionOptions(float extraMultiplier, bool isCounterAttack, bool canTriggerCounter)
        {
            ExtraMultiplier = extraMultiplier;
            IsCounterAttack = isCounterAttack;
            CanTriggerCounter = canTriggerCounter;
        }

        public static SkillExecutionOptions Normal =>
            new SkillExecutionOptions(1f, false, true);

        public static SkillExecutionOptions CounterDefault =>
            new SkillExecutionOptions(0.5f, true, false);
    }

    internal readonly struct CounterAttackRequest
    {
        public readonly BattleCharactor Defender;
        public readonly BattleCharactor OriginalCaster;
        public readonly SkillData Skill;

        public CounterAttackRequest(BattleCharactor defender, BattleCharactor originalCaster, SkillData skill)
        {
            Defender = defender;
            OriginalCaster = originalCaster;
            Skill = skill;
        }
    }

    [Header("Battle Speed")]
    [SerializeField] private float _currentBattleSpeed = 1.0f;
    [SerializeField] private BattleFlowManager battleFlowManager;

    [Header("Visual")]
    [SerializeField] private BattleVisualDirector _visualDirector;

    // SkillPresentationCatalog provides animation slot, target reaction, and hit timing overrides.
    [Header("Skill Presentation Override")]
    [SerializeField] private SkillPresentationCatalog _presentationCatalog;

    // 연출 소유자. 규칙 계층은 확정된 결과만 넘기고, 재생은 전적으로 여기서 담당한다.
    private SkillPresentationDirector _presentationDirector;
    internal SkillPresentationDirector Presentation =>
        _presentationDirector ??= new SkillPresentationDirector(this);

    // 부활 캐스트-로컬 상태. 규칙 계층이 '누구를 몇 %로'를 정해 확정 델리게이트를 여기 걸어두면,
    // 연출(revive Cue)이 '언제 일어서는가'만 트리거한다. 트리거가 없어도 스윕이 확정을 보장한다.
    private Func<BattleHitResult> _pendingReviveCommit;

    // 연쇄(cascading) 액션 큐. 한 행동이 낳은 후속 행동(반격, 앞으로 추가될 발동형 효과 등)이 여기 쌓인다.
    // 드레인 중에 추가되면 바깥 루프가 그대로 집어가므로 깊이에 제한 없이 이어질 수 있다.
    // 그래서 MaxFollowUpDepth를 백스톱으로 둔다 — 데이터 실수 한 번이 무한 루프가 되는 것을 막는다.
    private readonly ASB.Work.Battle.Command.BattleActionQueue _followUpQueue =
        new ASB.Work.Battle.Command.BattleActionQueue();
    private bool _isDrainingFollowUps;
    private int _currentFollowUpDepth;
    private const int MaxFollowUpDepth = 8;

    public float CurrentBattleSpeed => _currentBattleSpeed;

    // 연출 소유자(SkillPresentationDirector)가 읽는 참조.
    // 필드는 여기 남겨 씬/프리팹 직렬화 값을 보존하고, 값만 넘긴다.
    internal BattleVisualDirector VisualDirector => _visualDirector;
    internal SkillPresentationCatalog PresentationCatalog => _presentationCatalog;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BattleManager] 중복 인스턴스가 감지되었습니다.");
        }

        Instance = this;
        PrimeTweenConfig.warnEndValueEqualsCurrent = false;

        if (battleFlowManager == null)
        {
            battleFlowManager = FindObjectOfType<BattleFlowManager>();
        }

        OnBattleSpeedChangeRequested += ChangeBattleSpeed;
        ChangeBattleSpeed(BattleRuntimeSettings.BattleSpeed);
    }

    public void ChangeBattleSpeed(float newSpeed)
    {
        BattleRuntimeSettings.SetBattleSpeed(newSpeed);
        _currentBattleSpeed = BattleRuntimeSettings.BattleSpeed;
        ApplyBattleSpeedToAllActiveUnits();
    }

    public void ApplyBattleSpeedToUnit(BattleCharactor unit)
    {
        if (unit == null || unit.IsDead || !unit.gameObject.activeInHierarchy)
        {
            return;
        }

        unit.EnsureAnimationController();
        unit.Anim?.SetAnimationSpeed(_currentBattleSpeed);
    }

    private void ApplyBattleSpeedToAllActiveUnits()
    {
        if (battleFlowManager == null)
        {
            battleFlowManager = FindObjectOfType<BattleFlowManager>();
        }

        if (battleFlowManager != null)
        {
            IReadOnlyList<BattleCharactor> participants = battleFlowManager.Participants;
            for (int i = 0; i < participants.Count; i++)
            {
                ApplyBattleSpeedToUnit(participants[i]);
            }

            return;
        }

        BattleCharactor[] fallbackUnits = FindObjectsByType<BattleCharactor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < fallbackUnits.Length; i++)
        {
            ApplyBattleSpeedToUnit(fallbackUnits[i]);
        }
    }

    internal IEnumerator WaitForBattleSeconds(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime * _currentBattleSpeed;
            yield return null;
        }
    }

    private IEnumerator WaitUntilHitEventOrBattleTimeout(
        CharactorAnimationController anim,
        float timeoutSeconds,
        System.Action<float> onBattleElapsed = null)
    {
        float elapsed = 0f;

        while (anim != null && !anim.IsHitEventReached && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime * _currentBattleSpeed;
            yield return null;
        }

        onBattleElapsed?.Invoke(Mathf.Min(elapsed, timeoutSeconds));
    }

    // TODO: 전투 VFX Instantiate 경로가 추가되면 생성 직후 ApplyBattleSpeedToVfx(vfxInstance)를 호출하세요.
    // 현재 TmpBattleScene 전투 스크립트에는 ParticleSystem 스킬 이펙트 Instantiate 코드가 없습니다.
    private void ApplyBattleSpeedToVfx(GameObject vfxInstance)
    {
        if (vfxInstance == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = vfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpeed *= _currentBattleSpeed;
        }
    }

    private void OnDestroy()
    {
        OnBattleSpeedChangeRequested -= ChangeBattleSpeed;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private BattleHitResult ApplyDamage(DamageContext context)
    {
        if (context.Caster == null || context.Target == null)
        {
            return BattleHitResult.Empty(context?.Target);
        }

        // 다단 히트 진행 중 사망 타겟에 대한 후속 타격은 무시합니다.
        if (context.Target.IsDead)
        {
            return BattleHitResult.Empty(context.Target);
        }

        bool wasDeadBefore = context.Target.IsDead;
        float finalDamage = CombatCalculator.CalculateDamage(context);
        context.Target.TakeDamage(finalDamage);
        bool isDeadAfter = context.Target.IsDead;
        Debug.Log($"[Combat] {context.Caster.UnitName} -> {context.Target.UnitName} dmg={finalDamage:F1} (Crit: {context.IsCritical})");

        return new BattleHitResult
        {
            Target = context.Target,
            Damage = finalDamage,
            IsCritical = context.IsCritical,
            TargetDied = isDeadAfter,
            WasDeadBefore = wasDeadBefore,
            IsDeadAfter = isDeadAfter,
            CausedDeath = !wasDeadBefore && isDeadAfter,
            SkillIndex = context.SkillIndex
        };
    }

    /// <summary>
    /// 데미지 수치/치명타/사망 여부만 순수 계산합니다. target.TakeDamage를 호출하지 않아 HP를 건드리지 않습니다.
    /// 연출 큐 조립 전에 이벤트(DamageEvent/DeathEvent)를 먼저 기록하기 위해 사용합니다.
    /// </summary>
    private BattleHitResult PredictDamage(DamageContext context)
    {
        if (context.Caster == null || context.Target == null)
        {
            return BattleHitResult.Empty(context?.Target);
        }

        if (context.Target.IsDead)
        {
            return BattleHitResult.Empty(context.Target);
        }

        float finalDamage = CombatCalculator.CalculateDamage(context);
        bool isDeadAfterPredicted = context.Target.CurrentHp - finalDamage <= 0f;

        return new BattleHitResult
        {
            Target = context.Target,
            Damage = finalDamage,
            IsCritical = context.IsCritical,
            TargetDied = isDeadAfterPredicted,
            WasDeadBefore = false,
            IsDeadAfter = isDeadAfterPredicted,
            CausedDeath = isDeadAfterPredicted,
            SkillIndex = context.SkillIndex
        };
    }

    /// <summary>
    /// PredictDamage로 미리 계산된 수치를 실제 target.TakeDamage로 반영합니다.
    /// 연출 타이밍(ResolveHitAction/AoEApplyDamageAction 콜백)에서 호출되어 HP바 갱신 시점을 유지합니다.
    /// </summary>
    private BattleHitResult CommitDamage(DamageContext context, BattleHitResult predicted)
    {
        if (context?.Target == null || predicted == null || context.Target.IsDead)
        {
            return BattleHitResult.Empty(context?.Target);
        }

        context.Target.TakeDamage(predicted.Damage);
        Debug.Log($"[Combat] {context.Caster?.UnitName} -> {context.Target.UnitName} dmg={predicted.Damage:F1} (Crit: {predicted.IsCritical})");
        return predicted;
    }

    public IEnumerator ApplySkillExecutionResultRoutine(SkillExecutionResult result, Action<bool> onCompleted = null)
    {
        if (result == null || !result.Success)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        float totalDamageDealt = 0f;
        // 연출이 확정하지 못한(취소·타임아웃) 히트를 규칙 계층이 나중에 일괄 확정하기 위한 목록.
        // 연출은 확정 '시점'만 정하고 '여부'는 정하지 못한다. 각 항목은 멱등하다(두 번 호출해도 1회만 적용).
        var pendingCommits = new List<Func<BattleHitResult>>();
        Presentation.ChainState = null; // 캐스트마다 초기화(이전 체인 상태 누수 방지)
        Presentation.ResetCastState();

        // 부활은 연출(데미지 구간)보다 먼저 확정 델리게이트를 준비해야 한다.
        // 4040처럼 부활 Cue가 공격 연출의 AttackPrepare 페이즈에 있는 경우, 그 시점에 트리거되어야 하기 때문.
        _pendingReviveCommit = BuildPendingReviveCommit(result, pendingCommits);

        if (result.DamageContexts != null)
        {
            bool isAoE = result.Handler is BaseAoESkillHandler;

            if (isAoE)
            {
                var aoeContexts = new List<DamageContext>();
                var hitCallbacks = new List<Func<BattleHitResult>>();
                var aoeDeliveryGate = new HitDeliveryGate();

                for (int i = 0; i < result.DamageContexts.Count; i++)
                {
                    DamageContext damageContext = result.DamageContexts[i];
                    if (damageContext == null)
                    {
                        continue;
                    }

                    if (damageContext.Target == null || damageContext.Target.IsDead || damageContext.Target.CurrentHp <= 0f)
                    {
                        continue;
                    }

                    if (!damageContext.IsCounterAttack && !damageContext.CanTriggerCounter)
                    {
                        damageContext.CanTriggerCounter = CanTriggerCounterattack(damageContext);
                    }

                    BattleHitResult predicted = PredictDamage(damageContext);

                    // 멱등 확정. 연출이 임팩트 시점에 호출하지만, 호출하지 않아도(취소·타임아웃)
                    // 루틴 종료 전 스윕이 반드시 확정한다.
                    bool committed = false;
                    Func<BattleHitResult> commit = () =>
                    {
                        if (committed)
                        {
                            return null;
                        }

                        committed = true;
                        BattleHitResult r = CommitDamage(damageContext, predicted);
                        result.RecordDamageResult(damageContext, r);
                        totalDamageDealt += r?.Damage ?? 0f;
                        return r;
                    };
                    pendingCommits.Add(commit);

                    aoeContexts.Add(damageContext);
                    hitCallbacks.Add(commit);
                }

                if (aoeContexts.Count > 0)
                {
                    var aoeQueue = new ASB.Work.Battle.Command.BattleActionQueue();
                    aoeQueue.Enqueue(new ASB.Work.Battle.Command.SkillActionCommand(aoeContexts, hitCallbacks, aoeDeliveryGate));
                    yield return StartCoroutine(aoeQueue.RunAll(this));
                }
            }
            else
            {
                // 체인 투사체 모드면 캐스트-로컬 상태를 준비(1차 도착점을 2차 원점으로 전달).
                Presentation.ChainState = Presentation.ResolveChainStateForCast(result);
                if (Presentation.ChainState != null)
                {
                    Presentation.PrepareChainTargets(result.DamageContexts, SkillPresentationDirector.NextActionInstanceId());
                }
                else
                {
                    Presentation.PrepareChainTargets(result.DamageContexts, 0);
                }

                bool chainPrimaryPresented = false;
                for (int i = 0; i < result.DamageContexts.Count; i++)
                {
                    DamageContext damageContext = result.DamageContexts[i];
                    if (damageContext == null)
                    {
                        continue;
                    }

                    if (damageContext.Target == null || damageContext.Target.IsDead || damageContext.Target.CurrentHp <= 0f)
                    {
                        continue;
                    }

                    // 1차 투사체가 취소되면 추가 타깃의 '볼트 연출'은 생략한다.
                    // 타깃은 규칙 계층이 이미 정했으므로 피해는 그대로 적용한다(아래에서 즉시 확정).
                    bool skipChainPresentation = Presentation.ChainState != null
                        && damageContext.Role == DamageRole.Additional
                        && Presentation.ChainState.IsCancelled;

                    if (!damageContext.IsCounterAttack && !damageContext.CanTriggerCounter)
                    {
                        damageContext.CanTriggerCounter = CanTriggerCounterattack(damageContext);
                    }

                    // ResolveSkillHitRoutine이 읽을 현재 컨텍스트 역할.
                    Presentation.ChainRole = damageContext.Role;

                    BattleHitResult predicted = PredictDamage(damageContext);
                    var deliveryGate = new HitDeliveryGate();

                    // 멱등 확정. 연출이 임팩트 시점에 호출하지만, 호출하지 않아도(취소·타임아웃)
                    // 루틴 종료 전 스윕이 반드시 확정한다.
                    bool committed = false;
                    Func<BattleHitResult> onHitCallback = () =>
                    {
                        if (committed)
                        {
                            return null;
                        }

                        committed = true;
                        BattleHitResult r = CommitDamage(damageContext, predicted);
                        result.RecordDamageResult(damageContext, r);
                        totalDamageDealt += r?.Damage ?? 0f;
                        return r;
                    };
                    pendingCommits.Add(onHitCallback);

                    // 체인이 끊겼으면 볼트가 오지 않으므로 기다리지 않는다. 피해만 즉시 확정하고 다음 대상으로.
                    if (skipChainPresentation)
                    {
                        onHitCallback();
                        continue;
                    }

                    bool isAdditionalChainTarget = Presentation.ChainState != null
                                                   && chainPrimaryPresented
                                                   && damageContext.Role == DamageRole.Additional;
                    if (isAdditionalChainTarget)
                    {
                        // 체인 번개의 추가 대상도 실제 볼트가 끝점에 닿은 후에만 피격 애니메이션·피해 UI를 적용한다.
                        if (Presentation.ChainActionInstanceId > 0)
                        {
                            SkillPresentationData chainPresentation = _presentationCatalog?.Get(damageContext.SkillIndex);
                            yield return Presentation.WaitForPresentationImpactRoutine(damageContext.Target, chainPresentation);
                            SkillData chainSkill = SkillPresentationDirector.ResolveSkillAnimationData(SkillPresentationDirector.TryGetSkillDataForDamageContext(damageContext));
                            yield return new ResolveHitAction(
                                damageContext.Caster,
                                damageContext.Target,
                                onHitCallback,
                                SkillPresentationDirector.ResolveAdditionalChainTargetAnimationTrigger(chainPresentation, chainSkill),
                                _currentBattleSpeed,
                                _visualDirector).ExecuteRoutine(this);
                        }
                        else
                        {
                            // 프리팹 누락 등으로 체인 도착 신호를 받을 수 없는 기존 폴백.
                            yield return WaitForBattleSeconds(0.12f);
                            yield return new ResolveHitAction(
                                damageContext.Caster,
                                damageContext.Target,
                                onHitCallback,
                                targetAnimTrigger: null,
                                battleSpeed: _currentBattleSpeed,
                                visual: _visualDirector).ExecuteRoutine(this);
                        }
                    }
                    else
                    {
                        SkillData hitAnimSkill = SkillPresentationDirector.ResolveSkillAnimationData(SkillPresentationDirector.TryGetSkillDataForDamageContext(damageContext));
                        var actionQueue = new ASB.Work.Battle.Command.BattleActionQueue();
                        actionQueue.Enqueue(new ASB.Work.Battle.Command.SkillActionCommand(
                            damageContext.Caster,
                            damageContext.Target,
                            hitAnimSkill,
                            playBasicAttackAnimation: false,
                            playTargetHitAnimation: true,
                            onHitCallback: onHitCallback,
                            deliveryGate));
                        yield return StartCoroutine(actionQueue.RunAll(this));
                        if (Presentation.ChainState != null && damageContext.Role == DamageRole.Primary)
                        {
                            chainPrimaryPresented = true;
                        }
                    }

                    float delay = Mathf.Max(0f, damageContext.DelayAfter);
                    if (delay > 0f)
                    {
                        yield return WaitForBattleSeconds(delay);
                    }
                }
            }
        }

        // 스윕: 연출이 취소·타임아웃돼 확정되지 않은 히트를 규칙 계층이 여기서 반드시 확정한다.
        // 이 한 줄이 "연출은 피해를 취소할 수 없다"를 보장한다.
        FlushPendingCommits(pendingCommits);

        if (result.HealContexts != null && result.HealContexts.Count > 0)
        {
            // 다중 아군 힐: 시전 애니는 첫(주) 대상에서 1회만 재생하고, 인접 아군은 시전을 다시 돌리지 않고
            // 짧은 딜레이(스태거) 후 힐 이펙트 + 힐만 적용한다. (첫 대상만 투척/Cue 등 전체 연출 수행)
            bool firstHealPresented = false;
            BattleCharactor previousHealTarget = null;
            var groupHealPresentationTargets = new List<BattleCharactor>();
            foreach (HealContext context in result.HealContexts)
            {
                if (context?.Target != null && !context.Target.IsDead && !groupHealPresentationTargets.Contains(context.Target))
                {
                    groupHealPresentationTargets.Add(context.Target);
                }
            }
            for (int i = 0; i < result.HealContexts.Count; i++)
            {
                HealContext healContext = result.HealContexts[i];
                if (healContext == null || healContext.Caster == null || healContext.Target == null)
                {
                    continue;
                }

                // 부활은 위에서 확정 델리게이트로 준비했고 연출도 스킬 자체의 revive Cue가 담당한다.
                // 힐 프리젠테이션(시전 애니 + 힐 이펙트)을 중복으로 돌리지 않는다.
                if (healContext.IsRevive)
                {
                    continue;
                }

                if (healContext.Target.IsDead)
                {
                    continue;
                }

                HealContext capturedHeal = healContext;
                bool healCommitted = false;
                Func<BattleHitResult> healCallback = () =>
                {
                    if (healCommitted)
                    {
                        return null;
                    }

                    healCommitted = true;
                    if (capturedHeal.IsRevive)
                    {
                        capturedHeal.Target.Revive(capturedHeal.ReviveHpRatio);
                    }
                    else
                    {
                        capturedHeal.Target.ApplyHeal(capturedHeal.HealAmount);
                    }
                    return new BattleHitResult
                    {
                        Target = capturedHeal.Target,
                        Damage = capturedHeal.HealAmount,
                        IsHeal = true,
                        SkillIndex = capturedHeal.SkillIndex
                    };
                };
                pendingCommits.Add(healCallback);

                if (!firstHealPresented)
                {
                    firstHealPresented = true;
                    SkillData healAnimSkill = SkillPresentationDirector.ResolveSkillAnimationData(SkillPresentationDirector.TryGetSkillDataForHealContext(healContext));
                    var healQueue = new ASB.Work.Battle.Command.BattleActionQueue();
                    healQueue.Enqueue(new ASB.Work.Battle.Command.SkillActionCommand(
                        healContext.Caster,
                        healContext.Target,
                        healAnimSkill,
                        playBasicAttackAnimation: false,
                        playTargetHitAnimation: false,
                        onHitCallback: healCallback,
                        deliveryGate: new HitDeliveryGate(),
                        presentationTargets: healContext.SkillIndex == 4030 ? groupHealPresentationTargets : null));
                    yield return StartCoroutine(healQueue.RunAll(this));
                    previousHealTarget = healContext.Target;
                }
                else
                {
                    // 인접 아군: 시전 애니 없이 스태거 딜레이 후 힐 이펙트 + 힐.
                    yield return WaitForBattleSeconds(MultiHealStaggerSeconds);
                    if (healContext.SkillIndex == 4030 && previousHealTarget != null)
                    {
                        yield return PlayGroupHealBounceProjectile(
                            previousHealTarget,
                            healContext.Target,
                            healContext.SkillIndex);
                    }
                    _visualDirector?.PlayHitEffect(healContext.Target, healContext.SkillIndex);
                    healCallback();
                    previousHealTarget = healContext.Target;
                }
            }
        }

        // 힐/부활도 동일하게, 연출이 확정하지 못했으면 여기서 확정한다.
        FlushPendingCommits(pendingCommits);

        if (result.StatusEffectContexts != null && result.StatusEffectContexts.Count > 0)
        {
            for (int i = 0; i < result.StatusEffectContexts.Count; i++)
            {
                StatusEffectContext statusContext = result.StatusEffectContexts[i];
                if (statusContext.Target == null || statusContext.Target.IsDead)
                {
                    continue;
                }

                // 투사체 전달 결과와 무관하게 적용한다. 연출은 규칙을 취소할 수 없다.
                ApplyStatusEffect(statusContext);
            }
        }

        var counterRequests = CollectCounterAttackRequests(result);
        if (counterRequests.Count > 0)
        {
            EnqueueCounterAttacks(counterRequests);
            // 이 루틴 자체가 연쇄 액션으로 실행 중이면 DrainFollowUps는 즉시 반환하고,
            // 방금 넣은 반격은 바깥 드레인 루프가 이어서 처리한다.
            yield return StartCoroutine(DrainFollowUps());
        }

        result.OnPostExecution?.Invoke(totalDamageDealt);
#if UNITY_EDITOR
        result.LogEventTrackingSummary();
#endif
        onCompleted?.Invoke(true);
    }

    /// <summary>
    /// 부활 컨텍스트의 확정 델리게이트를 만들어 스윕 목록에 등록하고 반환한다.
    /// 반환값은 연출(revive Cue)이 '일어서는 시점'을 정하기 위해 트리거한다.
    /// 트리거되지 않아도 스윕이 확정하므로 연출은 부활을 취소할 수 없다.
    /// </summary>
    private Func<BattleHitResult> BuildPendingReviveCommit(
        SkillExecutionResult result,
        List<Func<BattleHitResult>> pendingCommits)
    {
        if (result?.HealContexts == null)
        {
            return null;
        }

        for (int i = 0; i < result.HealContexts.Count; i++)
        {
            HealContext healContext = result.HealContexts[i];
            if (healContext == null || !healContext.IsRevive || healContext.Target == null)
            {
                continue;
            }

            HealContext capturedRevive = healContext;
            bool committed = false;
            Func<BattleHitResult> commit = () =>
            {
                if (committed)
                {
                    return null;
                }

                committed = true;
                capturedRevive.Target.Revive(capturedRevive.ReviveHpRatio);
                Debug.Log($"[Combat] {capturedRevive.Target.UnitName} 부활 (ratio={capturedRevive.ReviveHpRatio:0.##})");
                return new BattleHitResult
                {
                    Target = capturedRevive.Target,
                    Damage = 0f,
                    IsHeal = true,
                    SkillIndex = capturedRevive.SkillIndex
                };
            };

            pendingCommits.Add(commit);
            return commit; // 캐스트당 부활은 1건이다.
        }

        return null;
    }

    /// <summary>
    /// 연출의 revive Cue 시점에 호출된다. 부활 확정이 대기 중이면 지금 적용한다(멱등).
    /// </summary>
    internal bool HasPendingRevive => _pendingReviveCommit != null;

    internal void TriggerPendingRevive()
    {
        _pendingReviveCommit?.Invoke();
    }

    /// <summary>
    /// 연출이 확정하지 못한 히트를 규칙 계층이 일괄 확정한다.
    /// 각 항목은 멱등하므로 이미 확정된 것은 무시된다.
    /// </summary>
    private static void FlushPendingCommits(List<Func<BattleHitResult>> pendingCommits)
    {
        if (pendingCommits == null)
        {
            return;
        }

        for (int i = 0; i < pendingCommits.Count; i++)
        {
            pendingCommits[i]?.Invoke();
        }
    }

    private IEnumerator PlayGroupHealBounceProjectile(
        BattleCharactor source,
        BattleCharactor target,
        int skillIndex)
    {
        if (source == null || target == null || source.IsDead || target.IsDead)
        {
            yield break;
        }

        ProjectileVisualData projectileVisual = _presentationCatalog?.Get(skillIndex)?.GetProjectileVisual();
        if (projectileVisual == null)
        {
            yield break;
        }

        var projectile = new ProjectileImpactAction(
            source,
            target,
            projectileVisual,
            _currentBattleSpeed,
            null,
            skillIndex,
            originOverride: source.transform.position);
        yield return projectile.ExecuteRoutine(this);
    }

    public void ApplyStatusEffect(StatusEffectContext context)
    {
        if (context.Caster == null || context.Target == null)
        {
            return;
        }

        if (context.EffectType == StatusEffectType.none)
        {
            return;
        }

        SkillEffectHelper.ApplyStatusEffect(context);
    }

    /// <summary>ClassSkillSheet 행의 skillValue(예: 1.2 = 120%)를 배율로 적용해 클래스 스킬을 실행합니다.</summary>
    public System.Collections.IEnumerator ExecuteHostageSkill(
        BattleCharactor actor,
        HostageBattleActor target,
        SkillData skillData,
        Action<bool> onCompleted = null)
    {
        if (!HostageFriendlyFireResolver.CanTargetHostages(actor, skillData) || target == null || !target.IsSafe)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (!TryConsumeSkillInfluence(actor, skillData))
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        bool hitApplied = false;

        // 피해는 인질 규칙(ApplyFriendlyDamage, 스플래시 포함)으로 분기하고, 연출은 적 대상과 동일한
        // 공유 엔진(SkillActionCommand → RunSkillSequenceCore)을 재사용한다. 인질은 ISkillTarget으로만
        // 참여하므로 BattleCharactor로 캐스팅/컴포넌트 추가하지 않는다. 피격 리액션/데미지팝업은 인질에 없음.
        System.Func<BattleHitResult> onHit = () =>
        {
            List<HostageBattleActor> hitHostages = HostageFriendlyFireResolver.ApplySkillDamage(actor, target, skillData);
            for (int i = 0; i < hitHostages.Count; i++)
            {
                HostageBattleActor hitHostage = hitHostages[i];
                if (hitHostage != null)
                    _visualDirector?.PlayHitEffectAt(hitHostage.transform, skillData.skillIndex);
            }
            hitApplied = hitHostages.Count > 0;
            // 인질은 BattleCharactor가 아니므로 Target=null(피격애니/팝업 skip). 피해는 위에서 이미 적용.
            return new BattleHitResult { Target = null, Damage = 0f, SkillIndex = skillData.skillIndex };
        };

        // 인질 경로는 ApplySkillExecutionResultRoutine(적 피해 파이프라인)을 우회하므로 체인 상태를 직접 초기화한다.
        // (이전 캐스트 누수 방지 + 인질은 추가 체인 대상이 없는 단일 대상 Primary 볼트로 처리)
        Presentation.ChainState = null;
        Presentation.ResetCastState();
        Presentation.ChainRole = DamageRole.Primary;
        _pendingReviveCommit = null; // 인질 경로는 부활을 만들지 않는다. 이전 캐스트 잔여 참조 제거.

        var hostageQueue = new ASB.Work.Battle.Command.BattleActionQueue();
        hostageQueue.Enqueue(new ASB.Work.Battle.Command.SkillActionCommand(
            actor,
            target,
            skillData,
            playBasicAttackAnimation: false,
            playTargetHitAnimation: false,
            onHitCallback: onHit,
            deliveryGate: new HitDeliveryGate()));
        yield return StartCoroutine(hostageQueue.RunAll(this));

        if (hitApplied)
            OnActionExecuted?.Invoke(GetSkillDisplayName(skillData));

        onCompleted?.Invoke(hitApplied);
    }

    public IEnumerator ExecuteGridSkill(BattleCharactor actor, BattleCharactor target, SkillData classSkillRow, Action<bool> onCompleted = null)
    {
        // 코루틴이 강제 중단(StopAllCoroutines, 씬 전환 등)되면 DrainFollowUps의 finally가 실행되지 않아
        // 드레인 플래그가 남고 이후 모든 연쇄가 조용히 멈춘다. 최상위 진입 시 큐가 비어 있으면 안전하게 되돌린다.
        if (_followUpQueue.Count == 0 && _isDrainingFollowUps)
        {
            Debug.LogWarning("[BattleManager] 연쇄 드레인 플래그가 남아 있어 초기화한다(코루틴 강제 중단 추정).");
            _isDrainingFollowUps = false;
            _currentFollowUpDepth = 0;
        }

        if (classSkillRow == null || actor == null || target == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (actor.IsDead)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        bool reviveSkill = classSkillRow.classSkillEffect == ClassSkillEffect_Revive;
        if (target.IsDead && !reviveSkill)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (!target.IsDead && reviveSkill)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (!TryConsumeSkillInfluence(actor, classSkillRow))
        {
            Debug.LogWarning("[BattleManager] Influence가 부족하여 스킬을 사용할 수 없습니다.");
            onCompleted?.Invoke(false);
            yield break;
        }

        SkillExecutionResult result;
        if (SkillExecutionRegistry.TryGetHandler(classSkillRow.skillIndex, out ISkillEffectHandler custom))
        {
            result = custom.Execute(actor, target, classSkillRow, null);
            result.Handler = custom;
        }
        else
        {
            result = BuildDefaultSkillResult(actor, target, classSkillRow, SkillExecutionOptions.Normal);
        }

        // 인질 부수피해의 중심 칸은 피해 확정 '전'에 스냅샷한다.
        // 확정 뒤에 판정하면 대상이 이번 공격으로 죽었는지에 따라 결과가 뒤집힌다.
        GridCellRef hostageCollateralCenter =
            HostageFriendlyFireResolver.CaptureCollateralCenter(actor, target, classSkillRow);

        bool executed = false;
        yield return StartCoroutine(ApplySkillExecutionResultRoutine(result, success => executed = success));

        if (executed)
        {
            HostageFriendlyFireResolver.ApplyCollateralDamage(actor, hostageCollateralCenter, classSkillRow);
            OnActionExecuted?.Invoke(GetSkillDisplayName(classSkillRow));
        }

        onCompleted?.Invoke(executed);
    }

    private List<CounterAttackRequest> CollectCounterAttackRequests(SkillExecutionResult result)
    {
        var requests = new List<CounterAttackRequest>();
        if (result.DamageContexts == null) return requests;

        BattleCharactor originalCaster = result.DamageContexts
            .FirstOrDefault(ctx => ctx?.Caster != null)
            ?.Caster;
        if (originalCaster == null) return requests;

        // 투사체 전달 결과는 더 이상 반격 성립에 관여하지 않는다. 연출은 규칙을 취소할 수 없다.
        var candidates = result.DamageContexts
            .Where(ctx => ctx != null && ctx.CanTriggerCounter)
            .Select(ctx => ctx.Target)
            .Distinct()
            .Where(t => t != null && !t.IsDead && t.IsPlayer != originalCaster.IsPlayer);

        foreach (BattleCharactor defender in candidates)
        {
            SkillData skill = defender.SelectedSkillData;
            if (skill == null) continue;

            if (skill.classSkillEffect == ClassSkillEffect_Heal
                || skill.classSkillEffect == ClassSkillEffect_Revive
                || skill.classSkillEffect == ClassSkillEffect_Buff)
            {
                Debug.Log($"[Combat] {defender.UnitName} 반격 스킬({skill.skillIndex})이 데미지 스킬이 아니어서 반격 제외");
                continue;
            }

            if (!CombatCalculator.RollCounter(defender)) continue;

            requests.Add(new CounterAttackRequest(defender, originalCaster, skill));
        }

        return requests;
    }

    internal IEnumerator ExecuteCounterSkill(CounterAttackRequest req)
    {
        // 원 시전자가 쓰러졌으면 남은 반격은 전부 취소한다.
        // 반격은 실행 순간의 상태를 기준으로 성립하므로, 앞선 반격이 시전자를 죽였다면
        // 뒤따르던 반격들은 여기서 각각 걸러진다(연쇄 큐에 남아 있어도 실행되지 않는다).
        if (req.OriginalCaster == null || req.OriginalCaster.IsDead)
        {
            Debug.Log($"[Combat] 반격 취소: 원 시전자가 이미 쓰러졌다. defender={req.Defender?.UnitName ?? "null"}");
            yield break;
        }

        if (req.Skill == null || req.Defender == null || req.Defender.IsDead)
        {
            yield break;
        }

        if (req.Skill.classSkillEffect == ClassSkillEffect_Heal
            || req.Skill.classSkillEffect == ClassSkillEffect_Revive
            || req.Skill.classSkillEffect == ClassSkillEffect_Buff)
        {
            yield break;
        }

        Debug.Log($"[Combat] {req.Defender.UnitName} 근접 반격 발동! (계수 0.5)");

        // 반격은 커스텀 핸들러를 무시하고 기본 데미지 경로만 사용합니다.
        // Influence 소모 없음, 데미지 계수 0.5, 반격은 반격을 유발하지 않습니다.
        SkillExecutionResult result = BuildDefaultSkillResult(
            req.Defender,
            req.OriginalCaster,
            req.Skill,
            SkillExecutionOptions.CounterDefault);

        bool executed = false;
        yield return StartCoroutine(ApplySkillExecutionResultRoutine(result, success => executed = success));

        if (!executed)
        {
            Debug.LogWarning($"[BattleManager] {req.Defender.UnitName} 반격 실행 실패.");
        }
    }

    /// <summary>
    /// 반격 요청을 연쇄 큐에 싣는다.
    /// 여기 가드는 <b>사전 필터</b>일 뿐이고, 실제 판정은 실행 시점에 <see cref="ExecuteCounterSkill"/>이
    /// 다시 한다 — 반격 #1이 시전자를 죽이면 뒤따르던 반격들은 실행 직전에 취소된다.
    /// </summary>
    private void EnqueueCounterAttacks(List<CounterAttackRequest> requests)
    {
        foreach (CounterAttackRequest req in requests)
        {
            if (req.OriginalCaster == null || req.OriginalCaster.IsDead) break;
            if (req.Defender == null || req.Defender.IsDead) continue;

            EnqueueFollowUp(new ASB.Work.Battle.Command.CounterAttackActionCommand(req));
        }
    }

    /// <summary>
    /// 한 행동이 낳은 후속(연쇄) 액션을 큐에 싣는다.
    /// 이미 드레인 중이면 바깥 루프가 집어가므로 여기서는 넣기만 한다 — 이것이 연쇄가 성립하는 지점이다.
    /// 깊이 상한을 넘으면 거부하고 에러 로그를 남긴다(조용히 자르지 않는다).
    /// </summary>
    public void EnqueueFollowUp(ASB.Work.Battle.Command.IBattleActionCommand command)
    {
        if (command == null)
        {
            return;
        }

        int depth = _currentFollowUpDepth + 1;
        if (depth > MaxFollowUpDepth)
        {
            Debug.LogError(
                $"[BattleManager] 연쇄 깊이 상한({MaxFollowUpDepth})을 넘어 후속 액션을 거부했다. " +
                $"command={command.GetType().Name}, depth={depth}");
            return;
        }

        command.Depth = depth;
        _followUpQueue.Enqueue(command);
    }

    /// <summary>
    /// 연쇄 큐를 빌 때까지 실행한다. 실행 중 추가된 액션도 같은 루프가 이어서 처리한다.
    /// 이미 드레인 중이면 즉시 반환한다 — 중첩 드레인을 만들지 않고 바깥 루프에 맡긴다.
    /// </summary>
    private IEnumerator DrainFollowUps()
    {
        if (_isDrainingFollowUps)
        {
            yield break;
        }

        _isDrainingFollowUps = true;
        try
        {
            while (_followUpQueue.Count > 0)
            {
                ASB.Work.Battle.Command.IBattleActionCommand command = _followUpQueue.Dequeue();
                if (command == null)
                {
                    continue;
                }

                _currentFollowUpDepth = command.Depth;
                yield return StartCoroutine(command.Execute(this));
            }
        }
        finally
        {
            _currentFollowUpDepth = 0;
            _isDrainingFollowUps = false;
        }
    }

    private SkillExecutionResult BuildDefaultSkillResult(
        BattleCharactor actor,
        BattleCharactor target,
        SkillData skillData,
        SkillExecutionOptions options)
    {
        float multiplier = Mathf.Max(0.01f, skillData.skillValue) * options.ExtraMultiplier;

        if (skillData.classSkillEffect == ClassSkillEffect_Heal)
        {
            float healAmount = Mathf.Max(0f, actor.FinalStats.Atk * multiplier);
            return SkillExecutionResult
                .SuccessResult(actor, skillData)
                .AddHeal(actor, target, healAmount, skillData.skillIndex);
        }

        // 부활(2)·버프(3)는 전용 핸들러(SkillExecutionRegistry)에서만 지원한다.
        // 기본 경로로 흘리면 죽은 아군 대상 DamageContext가 만들어져 ApplyDamage의 IsDead 가드에 걸리고,
        // 아무 일도 일어나지 않는데 Success=true라 IP만 소모되고 OnActionExecuted까지 발생했다.
        // 조용히 실패하지 않도록 여기서 끊는다.
        if (skillData.classSkillEffect != ClassSkillEffect_Damage)
        {
            Debug.LogWarning(
                $"[BattleManager] skillIndex={skillData.skillIndex}의 classSkillEffect={skillData.classSkillEffect}는 " +
                "기본 스킬 경로가 처리할 수 없다(부활·버프는 전용 핸들러 필요). SkillExecutionRegistry에 핸들러를 등록할 것.");
            return SkillExecutionResult.Failed();
        }

        var context = new DamageContext
        {
            Caster = actor,
            Target = target,
            SkillMultiplier = multiplier,
            SkillIndex = skillData.skillIndex,
            IsRangedAttack = IsRangedSkill(actor, skillData),
            CanTriggerCounter = options.CanTriggerCounter
                && !options.IsCounterAttack
                && IsMeleeSkillRange(actor, skillData)
                && target.IsInFrontRow,
            IsCounterAttack = options.IsCounterAttack
        };
        context.IsCritical = CombatCalculator.RollCritical(context);

        return SkillExecutionResult
            .SuccessResult(actor, skillData)
            .AddDamage(context);
    }

    private static bool TryConsumeSkillInfluence(BattleCharactor actor, SkillData skillData)
    {
        if (actor == null || skillData == null)
        {
            return false;
        }

        // 기본 공격은 ExecuteBasicAttack 경로로 분리되어 있어 여기로 들어오지 않습니다.
        float cost = Mathf.Max(0f, skillData.IPCost);
        return actor.TryConsumeInfluence(cost);
    }


    private static void ApplyBuff(BattleCharactor actor, BattleCharactor target, SkillData skillData)
    {
        if (actor == null || target == null || skillData == null)
        {
            return;
        }

        Debug.Log(
            $"[Battle] GridBuff: {GetLabel(actor)} -> {GetLabel(target)} (x{Mathf.Max(0.01f, skillData.skillValue):0.##})");
    }

    public IEnumerator ExecuteGridSkill(BattleCharactor actor, BattleCharactor target, float skillPercent, Action<bool> onCompleted = null)
    {
        yield return StartCoroutine(ExecuteGridSkill(actor, target, skillPercent, isHeal: false, onCompleted));
    }

    private IEnumerator ExecuteGridSkill(BattleCharactor actor, BattleCharactor target, float skillPercent, bool isHeal, Action<bool> onCompleted = null)
    {
        if (actor == null || target == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (actor.IsDead || target.IsDead)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        float multiplier = Mathf.Max(0.01f, skillPercent / 100f);
        if (isHeal)
        {
            float heal = Mathf.Max(0f, actor.FinalStats.Atk * multiplier);
            target.ApplyHeal(heal);
            Debug.Log($"[Battle] GridHeal: {GetLabel(actor)} -> {GetLabel(target)} heal={heal:F1} ({skillPercent:0.##}%)");
            onCompleted?.Invoke(true);
            yield break;
        }

        var context = new DamageContext
        {
            Caster = actor,
            Target = target,
            SkillMultiplier = multiplier,
            SkillIndex = -2,
            IsRangedAttack = false,
            CanTriggerCounter = false,
            IsCounterAttack = false
        };
        context.IsCritical = CombatCalculator.RollCritical(context);
        float dealt = ApplyDamage(context)?.Damage ?? 0f;
        if (context.DelayAfter > 0f)
        {
            yield return WaitForBattleSeconds(context.DelayAfter);
        }
        Debug.Log($"[Battle] GridSkill: {GetLabel(actor)} -> {GetLabel(target)} dmg={dealt:F1} ({skillPercent:0.##}%)");
        onCompleted?.Invoke(true);
    }


    private static string GetSkillDisplayName(SkillData skillData)
    {
        if (skillData == null)
        {
            return "Use Skill";
        }

        if (!string.IsNullOrWhiteSpace(skillData.skillName))
        {
            return skillData.skillName;
        }

        return $"Use Skill (ID: {skillData.skillIndex})";
    }




    private static bool CanTriggerCounterattack(DamageContext context)
    {
        if (context == null || context.IsCounterAttack || context.Caster == null || context.Target == null)
        {
            return false;
        }

        if (context.Target.IsDead || !context.Target.IsInFrontRow)
        {
            return false;
        }

        if (context.SkillIndex == -1)
        {
            return true;
        }

        if (context.SkillIndex <= 0)
        {
            return false;
        }

        SkillData matchedSkill = context.Caster.availableSkills != null
            ? context.Caster.availableSkills.FirstOrDefault(s => s != null && s.skillIndex == context.SkillIndex)
            : null;
        if (matchedSkill == null)
        {
            return false;
        }


        return IsMeleeSkillRange(context.Caster, matchedSkill);
    }

    internal static bool IsMeleeSkillRange(BattleCharactor actor, SkillData skillData)
    {
        if (skillData == null)
        {
            return false;
        }

        if (actor != null && !actor.IsPlayer)
        {
            int enemyRange = ResolveEnemySkillRange(skillData);
            if (enemyRange >= 0)
            {
                return enemyRange == 0;
            }
        }

        return skillData.classSkillRange == 0;
    }

    private static bool IsRangedSkill(BattleCharactor actor, SkillData skillData)
    {
        return !IsMeleeSkillRange(actor, skillData);
    }

    private static int ResolveEnemySkillRange(SkillData skillData)
    {
        if (skillData == null)
        {
            return -1;
        }

        bool isEnemySkill = skillData.skillIndex >= 200000 && skillData.skillIndex < 300000;
        if (!isEnemySkill)
        {
            return skillData.classSkillRange;
        }

        int slot = Mathf.Abs(skillData.skillIndex % 10);
        if (slot == 1 && skillData.EnemySkill1Range >= 0)
        {
            return skillData.EnemySkill1Range;
        }

        if (slot == 2 && skillData.EnemySkill2Range >= 0)
        {
            return skillData.EnemySkill2Range;
        }

        return skillData.classSkillRange;
    }



    private static string GetLabel(BattleCharactor unit)
    {
        if (unit == null)
        {
            return "null";
        }

        string side = unit.IsPlayer ? "P" : "E";
        string id = string.IsNullOrWhiteSpace(unit.UnitId) ? "Unknown" : unit.UnitId;
        string name = string.IsNullOrWhiteSpace(unit.UnitName) ? "Unknown" : unit.UnitName;
        return $"{side}:{id}:{name}";
    }
}
