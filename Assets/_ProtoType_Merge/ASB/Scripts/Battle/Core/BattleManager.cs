using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ASB.Work.Battle.SkillExecution;
using ASB.Work.Battle.Core;
using ASB.Work.Battle.Sequence;
using PrimeTween;

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

    private const int ClassSkillEffect_Heal = 1;
    private const int ClassSkillEffect_Revive = 2;
    private const int ClassSkillEffect_Buff = 3;

    private const float AnimEventTimeoutSeconds = 2f;

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

    // 체인 투사체 캐스트-로컬 상태. 비-AoE 컨텍스트 루프에서 설정하고 ResolveSkillHitRoutine이 읽는다.
    // (코루틴을 가로지르지만 컨텍스트는 순차 실행이라 동시성 문제 없음 — _currentBattleSpeed와 동일 패턴)
    private ProjectileChainState _projectileChainState;
    private DamageRole _projectileChainRole = DamageRole.Primary;
    // 체인 번개는 일반 투사체처럼 매 타격마다 생성하지 않고, 프리팹별 런타임 인스턴스를 재사용한다.
    private readonly Dictionary<GameObject, JC.VFX.ChainLightningVfx> _chainLightningEffects = new();
    private List<Transform> _chainLightningTargets;
    private int _chainLightningActionInstanceId;
    private JC.VFX.ChainLightningVfx _chainLightningImpactEffect;

    public float CurrentBattleSpeed => _currentBattleSpeed;

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

    private IEnumerator WaitForBattleSeconds(float seconds)
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
        _projectileChainState = null; // 캐스트마다 초기화(이전 체인 상태 누수 방지)
        _chainLightningTargets = null;
        _chainLightningActionInstanceId = 0;
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
                _projectileChainState = ResolveChainStateForCast(result);
                _chainLightningTargets = BuildChainLightningTargets(result.DamageContexts);
                if (_projectileChainState != null)
                {
                    _chainLightningActionInstanceId = NextActionInstanceId();
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

                    // 1차 취소 시 추가 타깃 체인 전체 중단.
                    if (_projectileChainState != null
                        && damageContext.Role == DamageRole.Additional
                        && _projectileChainState.IsCancelled)
                    {
                        continue;
                    }

                    if (!damageContext.IsCounterAttack && !damageContext.CanTriggerCounter)
                    {
                        damageContext.CanTriggerCounter = CanTriggerCounterattack(damageContext);
                    }

                    // ResolveSkillHitRoutine이 읽을 현재 컨텍스트 역할.
                    _projectileChainRole = damageContext.Role;

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

                    bool isAdditionalChainTarget = _projectileChainState != null
                                                   && chainPrimaryPresented
                                                   && damageContext.Role == DamageRole.Additional;
                    if (isAdditionalChainTarget)
                    {
                        // 체인 번개의 추가 대상도 실제 볼트가 끝점에 닿은 후에만 피격 애니메이션·피해 UI를 적용한다.
                        if (_chainLightningActionInstanceId > 0)
                        {
                            SkillPresentationData chainPresentation = _presentationCatalog?.Get(damageContext.SkillIndex);
                            yield return WaitForPresentationImpactRoutine(damageContext.Target, chainPresentation);
                            SkillData chainSkill = ResolveSkillAnimationData(TryGetSkillDataForDamageContext(damageContext));
                            yield return new ResolveHitAction(
                                damageContext.Caster,
                                damageContext.Target,
                                onHitCallback,
                                ResolveAdditionalChainTargetAnimationTrigger(chainPresentation, chainSkill),
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
                        SkillData hitAnimSkill = ResolveSkillAnimationData(TryGetSkillDataForDamageContext(damageContext));
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
                        if (_projectileChainState != null && damageContext.Role == DamageRole.Primary)
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

                // 부활 컨텍스트는 죽은 대상이 정상 입력이므로 IsDead 필터를 통과시킨다.
                if (healContext.Target.IsDead && !healContext.IsRevive)
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
                    SkillData healAnimSkill = ResolveSkillAnimationData(TryGetSkillDataForHealContext(healContext));
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
            yield return StartCoroutine(FlushCounterAttacks(counterRequests));
        }

        result.OnPostExecution?.Invoke(totalDamageDealt);
#if UNITY_EDITOR
        result.LogEventTrackingSummary();
#endif
        onCompleted?.Invoke(true);
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
        _projectileChainState = null;
        _chainLightningTargets = null;
        _chainLightningActionInstanceId = 0;
        _projectileChainRole = DamageRole.Primary;

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

        bool executed = false;
        yield return StartCoroutine(ApplySkillExecutionResultRoutine(result, success => executed = success));

        if (executed)
        {
            HostageFriendlyFireResolver.ApplyCollateralDamage(actor, target, classSkillRow);
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
        if (req.Skill == null
            || req.Defender == null || req.Defender.IsDead
            || req.OriginalCaster == null || req.OriginalCaster.IsDead)
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

    private IEnumerator FlushCounterAttacks(List<CounterAttackRequest> requests)
    {
        var counterQueue = new ASB.Work.Battle.Command.BattleActionQueue();
        foreach (CounterAttackRequest req in requests)
        {
            if (req.OriginalCaster == null || req.OriginalCaster.IsDead) break;
            if (req.Defender == null || req.Defender.IsDead) continue;

            counterQueue.Enqueue(new ASB.Work.Battle.Command.CounterAttackActionCommand(req));
        }

        yield return StartCoroutine(counterQueue.RunAll(this));
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

    private static SkillData CreateDefaultAnimationSkillData()
    {
        return new SkillData
        {
            AnimationTrigger = "Attack",
            HitDelay = 0.25f,
            TotalDelay = 0.5f,
            TargetAnimationTrigger = "Hit",
            UseAnimEvent = false
        };
    }

    // Apply SkillPresentation overrides to the transient SkillData used by this sequence.
    private void ApplyPresentationOverride(SkillData skill)
    {
        if (skill == null || _presentationCatalog == null) return;
        SkillPresentationData presentation = _presentationCatalog.Get(skill.skillIndex);
        if (presentation == null) return;

        AttackBeat beat = GetPrimaryAttackBeat(presentation);
        string stateName = beat != null && !string.IsNullOrWhiteSpace(beat.AnimationStateName)
            ? beat.AnimationStateName.Trim()
            : presentation.ResolvedAnimationStateName;
        if (!string.IsNullOrWhiteSpace(stateName))
        {
            skill.StateName = stateName;
        }

        if (!string.IsNullOrWhiteSpace(presentation.TargetAnimationTriggerOverride))
        {
            skill.TargetAnimationTrigger = presentation.TargetAnimationTriggerOverride.Trim();
        }

        skill.UseAnimEvent = presentation.UseAnimEvent;
        skill.HitDelay = presentation.HitDelay;
    }

    private static AttackBeat GetPrimaryAttackBeat(SkillPresentationData presentation)
    {
        if (presentation?.Attack != null && presentation.Attack.Enabled
            && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0)
        {
            return presentation.Attack.Beats[0];
        }

        return null;
    }

    private static int _actionInstanceCounter;
    private static int NextActionInstanceId() => ++_actionInstanceCounter;

    // Schema=1(PhaseCue)일 때만 유닛 로컬 Cue 컨텍스트를 등록한다. 같은 actionId의 Beat 갱신은 Held Handle을 보존한다.
    private void SetupPresentationContext(BattleCharactor actor, BattleCharactor target, SkillData skill,
        SkillPresentationData presentation, int actionInstanceId, List<CueBinding> activeCues = null, string activeStateName = null,
        IReadOnlyList<BattleCharactor> aoeTargets = null, Vector3? targetPositionOverride = null)
    {
        if (actor == null || presentation == null || _visualDirector == null || !presentation.IsPhaseCue || actionInstanceId == 0) return;

        var cueMap = new Dictionary<string, RuntimeCue>();
        List<CueBinding> cues = activeCues ?? GetPrimaryAttackBeat(presentation)?.Cues;
        if (cues != null)
        {
            foreach (CueBinding binding in cues)
            {
                if (binding == null) continue;
                string key = binding.NormalizedCueName;
                if (string.IsNullOrEmpty(key) || cueMap.ContainsKey(key)) continue;

                var cue = new RuntimeCue
                {
                    Operation = binding.Operation,
                    InstanceKey = binding.NormalizedInstanceKey,
                    Anchor = binding.Anchor,
                    Socket = binding.Socket,
                };
                if (binding.EffectIds != null)
                {
                    foreach (int id in binding.EffectIds)
                    {
                        GameObject prefab = _visualDirector.GetRegisteredEffect(id);
                        if (prefab != null) cue.EffectPrefabs.Add(prefab);
                    }
                }
                if (binding.SoundIds != null) cue.SoundIds.AddRange(binding.SoundIds);
                cueMap[key] = cue;
            }
        }

        PresentationRuntimeContext context = actor.EnsurePresentationComponents();
        if (context == null) return;

        var effectContext = new SkillEffectContext
        {
            ActionInstanceId = actionInstanceId,
            Caster = actor,
            PrimaryTarget = target,
            // EachTarget 앵커가 대상 수만큼 스폰할 수 있도록 AoE 타깃 리스트를 그대로 보존. 없으면 주 타깃 1개.
            Targets = (aoeTargets != null && aoeTargets.Count > 0)
                ? aoeTargets
                : (target != null ? new List<BattleCharactor> { target } : null),
            // 인질 등 BattleCharactor가 아닌 대상은 위치만 오버라이드로 주입(캐스팅/컴포넌트 추가 없이 Target 앵커 스폰 지원).
            TargetPosition = targetPositionOverride ?? (target != null ? target.transform.position : actor.transform.position),
            SocketTransform = actor.GetComponent<UnitVisualProfile>()?.AttackEffectSocket ?? actor.transform,
            PlaybackSpeed = _currentBattleSpeed,
            HitIndex = 0,
            // 부활 스킬(4040): 공격 대상(적)과 다른 부활 아군에 이펙트를 꽂기 위한 참조. 부활 없으면 null.
            ReviveTarget = actor.PendingReviveTarget,
        };
        string expectedStateName = !string.IsNullOrWhiteSpace(activeStateName)
            ? activeStateName
            : skill != null ? skill.StateName : string.Empty;
        int expectedHash = !string.IsNullOrWhiteSpace(expectedStateName)
            ? Animator.StringToHash(expectedStateName.Trim())
            : 0;
        context.SetActive(actionInstanceId, effectContext, cueMap, expectedHash);
    }
    private static SkillData ResolveSkillAnimationData(SkillData source)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.AnimationTrigger))
        {
            SkillData defaults = CreateDefaultAnimationSkillData();
            if (source == null)
            {
                return defaults;
            }

            SkillData copy = CloneSkillDataForAnimation(source);
            copy.AnimationTrigger = defaults.AnimationTrigger;
            copy.HitDelay = defaults.HitDelay;
            copy.TotalDelay = defaults.TotalDelay;
            copy.TargetAnimationTrigger = defaults.TargetAnimationTrigger;
            copy.UseAnimEvent = defaults.UseAnimEvent;
            return copy;
        }

        return CloneSkillDataForAnimation(source);
    }

    private static SkillData CloneSkillDataForAnimation(SkillData source)
    {
        return new SkillData
        {
            skillIndex = source.skillIndex,
            skillClass = source.skillClass,
            acquireLevel = source.acquireLevel,
            skillName = source.skillName,
            description = source.description,
            ipCost = source.ipCost,
            classSkillEffect = source.classSkillEffect,
            classSkillRange = source.classSkillRange,
            EnemySkill1Range = source.EnemySkill1Range,
            EnemySkill2Range = source.EnemySkill2Range,
            classSkillRangeLine = source.classSkillRangeLine,
            classSkillTarget = source.classSkillTarget,
            boundary = source.boundary != null ? new System.Collections.Generic.List<int>(source.boundary) : new System.Collections.Generic.List<int>(),
            multiTargetType = source.multiTargetType,
            multiTargetCount = source.multiTargetCount,
            skillValue = source.skillValue,
            skillSubValue = source.skillSubValue,
            AnimationTrigger = source.AnimationTrigger,
            StateName = source.StateName,
            UseAnimEvent = source.UseAnimEvent,
            HitDelay = source.HitDelay,
            TotalDelay = source.TotalDelay,
            TargetAnimationTrigger = source.TargetAnimationTrigger
        };
    }

    private static SkillData TryGetSkillDataForDamageContext(DamageContext damageContext)
    {
        if (damageContext == null)
        {
            return null;
        }

        if (damageContext.Caster != null && damageContext.Caster.availableSkills != null)
        {
            for (int i = 0; i < damageContext.Caster.availableSkills.Count; i++)
            {
                SkillData skill = damageContext.Caster.availableSkills[i];
                if (skill != null && skill.skillIndex == damageContext.SkillIndex)
                {
                    return skill;
                }
            }
        }

        if (DHCsvTemplateCatalog.Instance != null)
        {
            SkillData loaded = DHCsvTemplateCatalog.Instance.GetSkillTemplate(damageContext.SkillIndex);
            if (loaded != null) return loaded;
        }

        return null;
    }

    private static SkillData TryGetSkillDataForHealContext(HealContext healContext)
    {
        if (healContext == null)
        {
            return null;
        }

        if (healContext.Caster != null && healContext.Caster.availableSkills != null)
        {
            for (int i = 0; i < healContext.Caster.availableSkills.Count; i++)
            {
                SkillData skill = healContext.Caster.availableSkills[i];
                if (skill != null && skill.skillIndex == healContext.SkillIndex)
                {
                    return skill;
                }
            }
        }

        if (DHCsvTemplateCatalog.Instance != null)
        {
            SkillData loaded = DHCsvTemplateCatalog.Instance.GetSkillTemplate(healContext.SkillIndex);
            if (loaded != null) return loaded;
        }

        return null;
    }

        private sealed class MovingAttackPlan
    {
        public MovingAttackPresentation Presentation;
        public Vector3 FormationCenter;
        public Vector3 OriginPosition;
        public MovingAttackPath.Points PathPoints;
        public List<BattleCharactor> Targets;
    }

    private static bool TryBuildMovingAttackPlan(BattleCharactor actor, CharactorAnimationController animation,
        SkillPresentationData presentation, IEnumerable<BattleCharactor> candidateTargets, out MovingAttackPlan plan,
        Vector3? fallbackCenter = null)
    {
        plan = null;
        MovingAttackPresentation sweep = presentation?.MovingAttack;
        if (sweep == null || !sweep.Enabled || actor == null || animation == null
            || string.IsNullOrWhiteSpace(sweep.AnimationStateName))
        {
            return false;
        }

        var targets = new List<BattleCharactor>();
        var seen = new HashSet<BattleCharactor>();
        if (candidateTargets != null)
        {
            foreach (BattleCharactor candidate in candidateTargets)
            {
                if (candidate != null && !candidate.IsDead && seen.Add(candidate))
                {
                    targets.Add(candidate);
                }
            }
        }

        // 인질 등 BattleCharactor가 아닌 타깃은 candidateTargets가 비므로, 명시된 중심점(인질 위치)으로 스윕한다.
        if (targets.Count == 0 && !fallbackCenter.HasValue) return false;

        Vector3 center;
        if (targets.Count > 0)
        {
            center = Vector3.zero;
            for (int i = 0; i < targets.Count; i++) center += targets[i].transform.position;
            center /= targets.Count;
        }
        else
        {
            center = fallbackCenter.Value;
        }

        Vector3 origin = actor.transform.position;
        plan = new MovingAttackPlan
        {
            Presentation = sweep,
            FormationCenter = center,
            OriginPosition = origin,
            PathPoints = MovingAttackPath.BuildPoints(sweep, origin, center),
            Targets = targets
        };
        return true;
    }

    private void EnqueueMovingAttackSequence(ActionSequenceRunner runner, MovingAttackPlan plan,
        BattleCharactor actor, BattleCharactor presentationTarget, SkillData skill,
        SkillPresentationData presentation, int actionInstanceId, CharactorAnimationController actorAnim,
        Func<MonoBehaviour, IEnumerator> primaryHitRoutine, List<ASB.Work.Battle.Sequence.MovingAttackHitTarget> sequentialHits,
        float nextBlendInSeconds, Action<float> onElapsed)
    {
        if (runner == null || plan == null) return;

        runner.Enqueue(new ASB.Work.Battle.Sequence.MovingAttackPresentationAction(
            actor, actorAnim, plan.Presentation, plan.PathPoints, plan.OriginPosition,
            () => SetupPresentationContext(actor, presentationTarget, skill, presentation, actionInstanceId,
                plan.Presentation.Cues, plan.Presentation.AnimationStateName),
            primaryHitRoutine, sequentialHits, _currentBattleSpeed, nextBlendInSeconds, onElapsed));
    }

    private void EnqueueMovingAttackApproach(ActionSequenceRunner runner, MovingAttackPlan plan,
        CharactorAnimationController actorAnim, UnitMovementProfile movement, MovePhase movePhase)
    {
        if (runner == null || plan == null || actorAnim == null || movement == null) return;

        string stateName = movePhase?.AnimationStateName;
        float blend = movePhase != null ? Mathf.Max(0f, movePhase.BlendInSeconds) : 0.1f;
        runner.Enqueue(new ASB.Work.Battle.Sequence.MoveToWorldPositionAction(
            actorAnim, plan.PathPoints.Entry, movement.MoveDuration / _currentBattleSpeed, stateName, blend));
    }

    private static float ResolveMovingAttackIncomingBlendInSeconds(MovingAttackPlan plan)
    {
        return plan?.Presentation != null ? Mathf.Max(0f, plan.Presentation.AnimationBlendInSeconds) : 0f;
    }

    private static float ResolveMovingAttackOutgoingBlendInSeconds(SkillPresentationData presentation,
        bool returnEnabled)
    {
        if (returnEnabled && presentation?.Return != null)
        {
            return Mathf.Max(0f, presentation.Return.BlendInSeconds);
        }

        return ResolvePostBlendInSeconds(presentation) ?? 0f;
    }

    private void EnqueueMovingAttackReturn(ActionSequenceRunner runner, CharactorAnimationController actorAnim,
        UnitMovementProfile movement, Vector3 originPosition, float originRotationY, ReturnPhase returnPhase)
    {
        if (movement == null) return;
        EnqueueSkillReturn(runner, actorAnim, movement, true, false, originPosition, originRotationY, returnPhase);
    }

internal IEnumerator RunSkillSequenceCore(
        BattleCharactor actor,
        ISkillTarget skillTarget,
        SkillData skill,
        bool playBasicAttackAnimation,
        bool playTargetHitAnimation,
        Func<BattleHitResult> onHitCallback,
        HitDeliveryGate deliveryGate,
        IReadOnlyList<BattleCharactor> presentationTargets = null)
    {
        // 전투유닛 타깃(적/아군)만 BattleCharactor. 인질 등은 null → 위치 기반 연출만 사용하고
        // 피격 리액션/투사체 대상 추적 등 BattleCharactor 전용 처리는 건너뛴다.
        BattleCharactor target = skillTarget as BattleCharactor;
        // 연출 위치 앵커(타깃 유형 무관). 인질은 셀 자식 transform, 전투유닛은 자기 transform.
        Transform targetTransform = skillTarget?.TargetTransform;
        Vector3 targetPosition = skillTarget != null ? skillTarget.TargetPosition : actor.transform.position;

        if (deliveryGate == null)
        {
            deliveryGate = new HitDeliveryGate();
        }

        if (actor == null)
        {
            onHitCallback?.Invoke();
            yield break;
        }

        // 특이 스킬 커스텀 연출 탈출구(등록 없으면 no-op → 기본 시퀀서).
        if (skill != null && SkillPresentationSequenceRegistry.TryGet(skill.skillIndex, out ISkillPresentationSequence customSeq))
        {
            yield return StartCoroutine(customSeq.Run(this, actor, target, skill, onHitCallback));
            yield break;
        }

        skill = ResolveSkillAnimationData(skill);
        ApplyPresentationOverride(skill);
        actor.EnsureAnimationController();
        CharactorAnimationController actorAnim = actor.Anim;
        actor.Anim?.SetAnimationSpeed(_currentBattleSpeed);

        string targetState = actorAnim != null
            ? actorAnim.GetTargetStateName(playBasicAttackAnimation ? null : skill)
            : string.Empty;

        ResolveSkillMovement(actor, target, skill, out UnitMovementProfile movement, out bool shouldMove, out bool shouldRotate, out Vector3 originPosition, out float originRotationY, targetTransform);

        string targetAnimTrigger = playTargetHitAnimation
            ? (skill?.ResolvedTargetAnimationTrigger ?? "Hit")
            : null;

        SkillPresentationData presentation = skill != null ? _presentationCatalog?.Get(skill.skillIndex) : null;
        int presentationActionInstanceId = presentation?.IsPhaseCue == true ? NextActionInstanceId() : 0;
        bool moveEnabled       = presentation?.Move?.Enabled ?? true;
        bool attackPrepEnabled = presentation?.AttackPrepare?.Enabled ?? true;
        bool returnEnabled     = presentation?.Return?.Enabled ?? true;
        bool usePhaseCue       = presentation?.IsPhaseCue == true;
                MovingAttackPlan spinSweepPlan = null;
                bool useMovingAttack = !playBasicAttackAnimation
                            && usePhaseCue
                            && moveEnabled
                            && movement != null
                            && TryBuildMovingAttackPlan(actor, actorAnim, presentation, new[] { target }, out spinSweepPlan,
                                fallbackCenter: target == null ? targetPosition : (Vector3?)null);

        float sequenceBattleElapsed = 0f;
        var runner = new ActionSequenceRunner();
        bool hasApproachMovementAnimation = !useMovingAttack && moveEnabled && shouldMove;
                bool hasReturnMovementAnimation = !useMovingAttack && returnEnabled && (shouldMove || shouldRotate);
                bool hasPhaseCueCombo = !playBasicAttackAnimation && presentation?.IsPhaseCue == true
                    && presentation.Attack != null && presentation.Attack.Enabled
                    && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0;
                float? movePrepareNextBlendSeconds = useMovingAttack
                    ? Mathf.Max(0f, presentation?.Move?.BlendInSeconds ?? 0.1f)
                    : hasPhaseCueCombo && !hasApproachMovementAnimation
                        ? ResolveMovePrepareNextBlendInSeconds(actorAnim, skill, presentation)
                        : null;
                float? postBlendAfterLastBeatSeconds = hasPhaseCueCombo && !hasReturnMovementAnimation
                    ? ResolvePostBlendInSeconds(presentation)
                    : null;
        
                EnqueuePhaseCuePrologue(runner, actor, target, skill, presentation, presentationActionInstanceId,
            movePrepareNextBlendSeconds, elapsed => sequenceBattleElapsed += elapsed);

        bool useCombo = !playBasicAttackAnimation && presentation?.IsPhaseCue == true
                    && presentation.Attack != null && presentation.Attack.Enabled
                    && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0;
        
                if (useMovingAttack)
                        {
                            EnqueueMovingAttackApproach(runner, spinSweepPlan, actorAnim, movement, presentation?.Move);
        
                            EnqueuePhaseCueAttackPrepare(runner, actor, target, skill, presentation, presentationActionInstanceId,
                                attackPrepEnabled ? ResolveMovingAttackIncomingBlendInSeconds(spinSweepPlan) : null,
                                elapsed => sequenceBattleElapsed += elapsed);
        
                            EnqueueMovingAttackSequence(runner, spinSweepPlan, actor, target, skill, presentation,
                                presentationActionInstanceId, actorAnim,
                                host => ResolveSkillHitWithPresentationDeliveryRoutine(host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate, presentation, presentationActionInstanceId, targetPosition, targetTransform),
                                null,
                                ResolveMovingAttackOutgoingBlendInSeconds(presentation, returnEnabled && movement != null),
                                elapsed => sequenceBattleElapsed += elapsed);
                        }
                        
                else
                {
                    if (moveEnabled)
                        EnqueueSkillApproach(runner, actor, target, actorAnim, movement, shouldMove, shouldRotate, presentation?.Move, targetTransform);
        
                    EnqueuePhaseCueAttackPrepare(runner, actor, target, skill, presentation, presentationActionInstanceId,
                        useCombo ? ResolveFirstAttackBeatBlendInSeconds(actorAnim, skill, presentation) : null,
                        elapsed => sequenceBattleElapsed += elapsed);
                    if (!usePhaseCue && attackPrepEnabled && _visualDirector != null && skill != null)
                        runner.Enqueue(new SpawnAttackEffectAction(actor, skill.skillIndex, _visualDirector));
        
                    if (useCombo)
                    {
                        runner.Enqueue(new ASB.Work.Battle.Sequence.ComboSkillAction(
                            actorAnim,
                            presentation.Attack.Beats,
                            (beat, index) => ResolveAttackBeatState(actorAnim, skill, presentation, beat, index),
                            (beat, stateName) => SetupPresentationContext(actor, target, skill, presentation, presentationActionInstanceId, beat?.Cues, stateName, presentationTargets,
                                targetPositionOverride: target == null ? targetPosition : (Vector3?)null),
                            host => ResolveSkillHitWithPresentationDeliveryRoutine(host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate, presentation, presentationActionInstanceId, targetPosition, targetTransform),
                            _currentBattleSpeed,
                            AnimEventTimeoutSeconds,
                            postBlendAfterLastBeatSeconds,
                            elapsed => sequenceBattleElapsed += elapsed));
                    }
                    else
                    {
                        if (usePhaseCue)
                            runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => ActivatePresentationCueRoutine(
                                actor, target, skill, presentation, presentationActionInstanceId, GetPrimaryAttackBeat(presentation)?.Cues, targetState, presentationTargets)));
                        runner.Enqueue(new PlaySkillAnimAction(actorAnim, skill, playBasicAttackAnimation, actor));
                        runner.Enqueue(new WaitHitAction(actorAnim, skill, _currentBattleSpeed, elapsed => sequenceBattleElapsed += elapsed, AnimEventTimeoutSeconds));
                        runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => ResolveSkillHitRoutine(
                            host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate, targetPosition, targetTransform)));
                        if (!string.IsNullOrEmpty(targetState))
                            runner.Enqueue(new WaitClipEndAction(actorAnim, targetState, elapsed => sequenceBattleElapsed += elapsed));
                    }
                }
        
                if (returnEnabled)
                {
                    if (useMovingAttack)
                        EnqueueMovingAttackReturn(runner, actorAnim, movement, originPosition, originRotationY, presentation?.Return);
                    else
                        EnqueueSkillReturn(runner, actorAnim, movement, shouldMove, shouldRotate, originPosition, originRotationY,
                            presentation?.Return);
                }
        
                if (!returnEnabled && !useMovingAttack && shouldRotate)
                {
                    EnqueueSkillReturn(runner, actorAnim, movement, false, true, originPosition, originRotationY,
                        presentation?.Return);
                }

                EnqueuePhaseCueEpilogue(runner, actor, target, skill, presentation, presentationActionInstanceId,
            elapsed => sequenceBattleElapsed += elapsed);

        runner.Enqueue(new ReturnToIdleAction(actor));

        yield return StartCoroutine(runner.RunAll(this));

        // 연출 종료: 늦게 도착한 이벤트가 다음 실행 컨텍스트를 오용하지 않도록 정리.
        ClearPresentationContext(actor);

        float remainingTotal = Mathf.Max(0f, skill.TotalDelay - sequenceBattleElapsed);
        yield return WaitForBattleSeconds(Mathf.Max(remainingTotal, 0.2f));

        if (target != null)
            yield return StartCoroutine(new WaitTargetReactionAction(target, _currentBattleSpeed).ExecuteRoutine(this));
    }


            private IEnumerator ResolveSkillHitWithPresentationDeliveryRoutine(
                MonoBehaviour host,
                BattleCharactor actor,
                BattleCharactor target,
                Func<BattleHitResult> onHitCallback,
                string targetAnimTrigger,
                bool playTargetHitAnimation,
                SkillData skill,
                HitDeliveryGate deliveryGate,
                SkillPresentationData presentation,
                int actionInstanceId,
                Vector3? targetPositionOverride = null,
                Transform targetTransformOverride = null)
            {
                yield return ASB.Work.Battle.Sequence.CustomEffectImpactAction.WaitForImpactRoutine(
                    presentation, actionInstanceId, deliveryGate);
                if (deliveryGate == null || !deliveryGate.ShouldPlayImpactPresentation)
                {
                    yield break;
                }

                yield return ResolveSkillHitRoutine(
                    host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate, targetPositionOverride, targetTransformOverride);
            }
        
            private IEnumerator ResolveAoEHitWithPresentationDeliveryRoutine(
                MonoBehaviour host,
                BattleCharactor actor,
                SkillPresentationData presentation,
                int actionInstanceId,
                HitDeliveryGate deliveryGate,
                List<DamageContext> contexts,
                List<Func<BattleHitResult>> hitCallbacks,
                int pairCount,
                ProjectileVisualData projectileVisual)
            {
                SignalCustomImpactEffectAtHit(actor, presentation);
                yield return ASB.Work.Battle.Sequence.CustomEffectImpactAction.WaitForImpactRoutine(
                    presentation, actionInstanceId, deliveryGate);
                if (deliveryGate == null || !deliveryGate.ShouldPlayImpactPresentation)
                {
                    yield break;
                }
        
                yield return new AoEApplyDamageAction(
                    contexts, hitCallbacks, pairCount, _currentBattleSpeed, _visualDirector, projectileVisual, deliveryGate)
                    .ExecuteRoutine(host);
            }
        
        
            private static void SignalCustomImpactEffectAtHit(BattleCharactor actor, SkillPresentationData presentation)
            {
                if (actor == null
                    || presentation == null
                    || presentation.ImpactDeliveryMode != SkillImpactDeliveryMode.CustomEffectImpact
                    || string.IsNullOrWhiteSpace(presentation.CustomImpactSignalInstanceKey))
                {
                    return;
                }
        
                PresentationRuntimeContext runtime = actor.GetComponent<PresentationRuntimeContext>();
                string instanceKey = presentation.CustomImpactSignalInstanceKey.Trim();
                if (runtime != null
                    && runtime.TryGetHandle(instanceKey, out ISkillEffectHandle handle)
                    && handle != null
                    && handle.Signal(null))
                {
                    runtime.RemoveHandle(instanceKey);
                }
            }
        
            private void EnqueuePhaseCuePrologue(ActionSequenceRunner runner, BattleCharactor actor, BattleCharactor target,
        SkillData skill, SkillPresentationData presentation, int actionInstanceId, float? nextBlendSeconds,
        Action<float> onElapsed)
    {
        EnqueueCuePhase(runner, actor, target, skill, presentation, actionInstanceId, presentation?.MovePrepare,
            nextBlendSeconds, onElapsed);
    }

    private void EnqueuePhaseCueAttackPrepare(ActionSequenceRunner runner, BattleCharactor actor, BattleCharactor target,
        SkillData skill, SkillPresentationData presentation, int actionInstanceId, float? nextBlendSeconds,
        Action<float> onElapsed)
    {
        EnqueueCuePhase(runner, actor, target, skill, presentation, actionInstanceId, presentation?.AttackPrepare,
            nextBlendSeconds, onElapsed);
    }

    private void EnqueuePhaseCueEpilogue(ActionSequenceRunner runner, BattleCharactor actor, BattleCharactor target,
        SkillData skill, SkillPresentationData presentation, int actionInstanceId, Action<float> onElapsed)
    {
        EnqueueCuePhase(runner, actor, target, skill, presentation, actionInstanceId, presentation?.Post,
            null, onElapsed);
    }

    private void EnqueueCuePhase(ActionSequenceRunner runner, BattleCharactor actor, BattleCharactor target,
        SkillData skill, SkillPresentationData presentation, int actionInstanceId, CuePhase phase,
        float? nextBlendSeconds, Action<float> onElapsed)
    {
        if (runner == null || presentation?.IsPhaseCue != true || phase == null)
            return;

        runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => PlayCuePhaseRoutine(
            actor, target, skill, presentation, actionInstanceId, phase, nextBlendSeconds, onElapsed)));
    }

    private static void ClearPresentationContext(BattleCharactor actor)
    {
        actor?.GetComponent<PresentationRuntimeContext>()?.Clear();
    }
    private IEnumerator ActivatePresentationCueRoutine(BattleCharactor actor, BattleCharactor target, SkillData skill,
        SkillPresentationData presentation, int actionInstanceId, List<CueBinding> cues, string stateName,
        IReadOnlyList<BattleCharactor> aoeTargets = null)
    {
        SetupPresentationContext(actor, target, skill, presentation, actionInstanceId, cues, stateName, aoeTargets);
        yield break;
    }

    private IEnumerator PlayCuePhaseRoutine(BattleCharactor actor, BattleCharactor target, SkillData skill,
        SkillPresentationData presentation, int actionInstanceId, CuePhase phase, float? nextBlendSeconds,
        Action<float> onElapsed)
    {
        if (phase == null || !phase.Enabled)
            yield break;

        string stateName = phase.AnimationStateName?.Trim();
        SetupPresentationContext(actor, target, skill, presentation, actionInstanceId, phase.Cues, stateName);

        // 4040의 ClassSkill_4 클립에는 revive Cue 이벤트가 없는 구성도 있으므로,
        // 부활 대상용 Cue만 준비 페이즈 시작 시 보장한다.
        if (skill?.skillIndex == 4040 && phase is AttackPreparePhase)
        {
            UnitEffectPresenter presenter = actor != null ? actor.GetComponent<UnitEffectPresenter>() : null;
            presenter?.PresentationCue("revive");

            BattleCharactor reviveTarget = actor?.PendingReviveTarget;
            if (reviveTarget != null && reviveTarget.IsDead)
            {
                reviveTarget.Revive(0.2f);
            }
        }

        CharactorAnimationController animation = actor?.Anim;
        if (!string.IsNullOrEmpty(stateName) && animation != null)
        {
            animation.PlayState(stateName, Mathf.Max(0f, phase.BlendInSeconds));
            if (nextBlendSeconds.HasValue)
            {
                yield return animation.WaitForSkillTransitionStart(stateName, nextBlendSeconds.Value);
            }
            else
            {
                yield return animation.WaitForSkillClipEnd(stateName);
            }
            onElapsed?.Invoke(animation.LastClipWaitBattleSeconds);
        }

        else if (phase is MovePreparePhase && phase.Cues != null)
        {
            // MovePrepare cues without an animation state fire immediately before movement.
            UnitEffectPresenter presenter = actor != null ? actor.GetComponent<UnitEffectPresenter>() : null;
            if (presenter != null)
            {
                for (int i = 0; i < phase.Cues.Count; i++)
                {
                    string cueName = phase.Cues[i]?.CueName;
                    if (!string.IsNullOrWhiteSpace(cueName))
                    {
                        presenter.PresentationCue(cueName);
                    }
                }
            }
        }

        if (phase is PostPhase post && post.ExtraDelay > 0f)
        {
            yield return WaitForBattleSeconds(post.ExtraDelay);
            onElapsed?.Invoke(post.ExtraDelay);
        }
    }

    private static string ResolveAttackBeatState(CharactorAnimationController anim, SkillData skill, SkillPresentationData presentation, AttackBeat beat, int beatIndex)
    {
        if (beat != null && !string.IsNullOrWhiteSpace(beat.AnimationStateName)) return beat.AnimationStateName.Trim();
        if (beatIndex > 0) return string.Empty;
        if (!string.IsNullOrWhiteSpace(presentation?.ResolvedAnimationStateName)) return presentation.ResolvedAnimationStateName;
        return anim != null ? anim.GetTargetStateName(skill) : string.Empty;
    }

    private static float? ResolveFirstAttackBeatBlendInSeconds(CharactorAnimationController anim, SkillData skill,
        SkillPresentationData presentation)
    {
        if (presentation?.Attack?.Beats == null)
        {
            return null;
        }

        for (int i = 0; i < presentation.Attack.Beats.Count; i++)
        {
            AttackBeat beat = presentation.Attack.Beats[i];
            if (beat == null)
            {
                continue;
            }

            string stateName = ResolveAttackBeatState(anim, skill, presentation, beat, i);
            if (!string.IsNullOrWhiteSpace(stateName))
            {
                return Mathf.Max(0f, beat.BlendInSeconds);
            }
        }

        return null;
    }

    private static float? ResolveMovePrepareNextBlendInSeconds(CharactorAnimationController anim, SkillData skill,
        SkillPresentationData presentation)
    {
        CuePhase attackPrepare = presentation?.AttackPrepare;
        if (attackPrepare != null && attackPrepare.Enabled
            && !string.IsNullOrWhiteSpace(attackPrepare.AnimationStateName))
        {
            return Mathf.Max(0f, attackPrepare.BlendInSeconds);
        }

        return ResolveFirstAttackBeatBlendInSeconds(anim, skill, presentation);
    }

    private static float? ResolvePostBlendInSeconds(SkillPresentationData presentation)
    {
        PostPhase post = presentation?.Post;
        if (post == null || !post.Enabled || string.IsNullOrWhiteSpace(post.AnimationStateName))
        {
            return null;
        }

        return Mathf.Max(0f, post.BlendInSeconds);
    }

    /// <summary>
    /// 캐스트의 투사체 전달 방식이 ChainAdditionalTargets면 체인 상태를 생성한다. 아니면 null(=일반 Single 동작).
    /// </summary>
    private ProjectileChainState ResolveChainStateForCast(SkillExecutionResult result)
    {
        if (result?.DamageContexts == null)
        {
            return null;
        }

        DamageContext first = null;
        for (int i = 0; i < result.DamageContexts.Count; i++)
        {
            if (result.DamageContexts[i] != null)
            {
                first = result.DamageContexts[i];
                break;
            }
        }
        if (first == null)
        {
            return null;
        }

        SkillPresentationData presentation = _presentationCatalog?.Get(first.SkillIndex);
        ProjectileVisualData projectileVisual = presentation?.ProjectileVisual;
        if (projectileVisual != null
            && projectileVisual.DeliveryMode == ProjectileDeliveryMode.ChainAdditionalTargets
            && (projectileVisual.Prefab != null || presentation.ChainLightningEffectPrefab != null))
        {
            return new ProjectileChainState();
        }
        return null;
    }

    private IEnumerator ResolveSkillHitRoutine(MonoBehaviour host, BattleCharactor actor, BattleCharactor target,
        Func<BattleHitResult> onHitCallback, string targetAnimTrigger, bool playTargetHitAnimation, SkillData skill,
        HitDeliveryGate deliveryGate, Vector3? targetPositionOverride = null, Transform targetTransformOverride = null)
    {
        SkillPresentationData presentation = skill != null ? _presentationCatalog?.Get(skill.skillIndex) : null;
        // 체인 번개의 1차 지점: 전투유닛은 자기 transform, 인질 등은 override transform.
        // (인질은 target=null이라 이전엔 스킵돼 이펙트가 아예 안 떴다.)
        Transform chainPrimaryTransform = target != null ? target.transform : targetTransformOverride;
        bool waitForChainLightningImpact = false;
        if (chainPrimaryTransform != null && _projectileChainRole == DamageRole.Primary)
        {
            waitForChainLightningImpact = PlayChainLightningEffect(presentation, actor, chainPrimaryTransform,
                _chainLightningTargets ?? (IReadOnlyList<Transform>)System.Array.Empty<Transform>());
        }

        if (waitForChainLightningImpact)
        {
            yield return WaitForPresentationImpactRoutine(target, presentation, deliveryGate);
            if (!deliveryGate.ShouldPlayImpactPresentation)
            {
                yield break;
            }
        }

        if (TryGetProjectileVisual(actor, skill, presentation, playTargetHitAnimation, out ProjectileVisualData projectileVisual))
        {
            bool isChain = projectileVisual.DeliveryMode == ProjectileDeliveryMode.ChainAdditionalTargets
                           && _projectileChainState != null;
            Vector3? originOverride = null;
            if (isChain && _projectileChainRole == DamageRole.Additional && _projectileChainState.HasPreviousImpact)
            {
                // 추가 타깃 투사체는 1차 도착 좌표에서 출발(타깃 사망에도 안전).
                originOverride = _projectileChainState.LastImpactPoint;
            }

            // 준비(차징) 단계에서 만든 held 인스턴스를 재사용(낙하형: 손에 소환한 이펙트를 그대로 발사).
            // 지금까지 AoE 경로(AoEApplyDamageAction)에만 있던 처리를 단일 대상에도 동일 적용한다.
            GameObject preparedCharge = null;
            if (projectileVisual.UsePreparedCharge && actor != null && !string.IsNullOrEmpty(projectileVisual.ChargeInstanceKey))
            {
                PresentationRuntimeContext chargeCtx = actor.GetComponent<PresentationRuntimeContext>();
                if (chargeCtx != null && chargeCtx.TryGetHandle(projectileVisual.ChargeInstanceKey, out ISkillEffectHandle chargeHandle))
                {
                    preparedCharge = (chargeHandle as Component)?.gameObject;
                    chargeCtx.RemoveHandle(projectileVisual.ChargeInstanceKey);
                }
            }

            // 인질 등 BattleCharactor가 아닌 타깃은 target이 null이라, 목적지를 명시(destinationOverride)해야
            // 투사체가 취소되지 않고 그 위치까지 날아간다. 전투유닛(target!=null)은 기존대로 타깃 추적.
            Vector3? projectileDestination = target != null ? (Vector3?)null : targetPositionOverride;
            var projectile = new ProjectileImpactAction(actor, target, projectileVisual, _currentBattleSpeed, deliveryGate, skill.skillIndex, originOverride, destinationOverride: projectileDestination, existingInstance: preparedCharge);
            yield return projectile.ExecuteRoutine(host);

            // 주 타깃 단계: 실제 도착 좌표를 저장 → 추가 타깃 투사체의 원점으로 사용.
            if (isChain && _projectileChainRole == DamageRole.Primary)
            {
                switch (projectile.DeliveryResult)
                {
                    case ProjectileDeliveryResult.Arrived:
                        _projectileChainState.LastImpactPoint = projectile.ImpactPoint;
                        _projectileChainState.HasPreviousImpact = true;
                        break;
                    case ProjectileDeliveryResult.Fallback:
                        // 폴백: 주 타깃의 그 시점 위치를 도착점으로 사용하고 체인 계속.
                        _projectileChainState.LastImpactPoint = target != null ? target.transform.position : projectile.ImpactPoint;
                        _projectileChainState.HasPreviousImpact = true;
                        break;
                    case ProjectileDeliveryResult.Cancelled:
                        // 1차 취소: 체인 전체 종료.
                        _projectileChainState.IsCancelled = true;
                        break;
                }
            }
            if (!deliveryGate.ShouldPlayImpactPresentation)
            {
                yield break;
            }
            }
        else
        {
            bool isArcher = actor != null && actor.GetComponent<UnitVisualProfile>()?.HoldArrow != null;
            bool shouldSpawnArrowImpact = playTargetHitAnimation && skill != null && skill.classSkillEffect == 0;
            if (isArcher && target != null && shouldSpawnArrowImpact)
            {
                yield return new ArrowImpactAction(actor, target, _currentBattleSpeed, targetAnimTrigger).ExecuteRoutine(host);
                targetAnimTrigger = null;
            }
        }
        yield return new ResolveHitAction(actor, target, onHitCallback, targetAnimTrigger, _currentBattleSpeed, _visualDirector).ExecuteRoutine(host);
    }

    private bool TryGetProjectileVisual(BattleCharactor actor, SkillData skill, SkillPresentationData presentation,
        bool playTargetHitAnimation, out ProjectileVisualData projectileVisual)
    {
        projectileVisual = presentation?.GetProjectileVisual();
        // ProjectileVisual이 명시적으로 설정(Prefab 존재)된 스킬은 힐/부활·근거리·피격애니 여부와 무관하게
        // 투사체 연출을 허용한다(예: 힐 구체를 아군에게 투척). Prefab 미설정이면 투사체 연출 없음.
        // (기존엔 원거리 공격 스킬만 허용했으나, 연출은 프레젠테이션이 명시하면 따르도록 완화)
        return skill != null && projectileVisual != null && projectileVisual.Prefab != null;
    }

    private ProjectileVisualData GetProjectileVisual(BattleCharactor actor, SkillData skill, SkillPresentationData presentation)
    {
        return TryGetProjectileVisual(actor, skill, presentation, true, out ProjectileVisualData projectileVisual)
            ? projectileVisual : null;
    }
    private void ResolveSkillMovement(
        BattleCharactor actor,
        BattleCharactor target,
        SkillData skill,
        out UnitMovementProfile movement,
        out bool shouldMove,
        out bool shouldRotate,
        out Vector3 originPosition,
        out float originRotationY,
        Transform targetTransformOverride = null)
    {
        originPosition = actor != null ? actor.transform.position : Vector3.zero;
        originRotationY = actor != null ? actor.transform.eulerAngles.y : 0f;
        movement = actor != null ? actor.GetComponent<UnitMovementProfile>() : null;

        // 인질 등 BattleCharactor가 아닌 타깃은 target이 null이라 위치(override)로만 접근/회전을 판단한다.
        bool hasTarget = target != null || targetTransformOverride != null;

        // Friendly projectiles must face their target before launch.
        SkillPresentationData presentation = skill != null ? _presentationCatalog?.Get(skill.skillIndex) : null;
        bool isFriendlyProjectile = actor != null
            && target != null
            && actor.IsPlayer == target.IsPlayer
            && (skill?.classSkillEffect == ClassSkillEffect_Heal || skill?.classSkillEffect == ClassSkillEffect_Revive)
            && presentation?.GetProjectileVisual() != null;

        shouldMove = movement != null
            && !movement.RotateOnly
            && hasTarget
            && !isFriendlyProjectile
            && IsMeleeSkillRange(actor, skill);
        shouldRotate = hasTarget && ((movement != null && movement.RotateOnly) || isFriendlyProjectile);
    }

    private void EnqueueSkillApproach(
        ActionSequenceRunner runner,
        BattleCharactor actor,
        BattleCharactor target,
        CharactorAnimationController actorAnim,
        UnitMovementProfile movement,
        bool shouldMove,
        bool shouldRotate,
        MovePhase movePhase,
        Transform targetTransformOverride = null)
    {
        if (actor == null)
        {
            return;
        }

        // 전투유닛은 자기 transform, 인질 등은 override transform으로 접근/회전 목표를 삼는다.
        Transform targetTransform = target != null ? target.transform : targetTransformOverride;
        if (targetTransform == null)
        {
            return;
        }

        string animationStateName = movePhase?.AnimationStateName;
        float blendInSeconds = movePhase != null ? Mathf.Max(0f, movePhase.BlendInSeconds) : 0.1f;

        if (shouldMove && movement != null)
        {
            runner.Enqueue(new MoveToTargetAction(
                actorAnim, targetTransform, movement.ApproachDistance, movement.MoveDuration / _currentBattleSpeed,
                animationStateName, blendInSeconds));
        }
        else if (shouldRotate)
        {
            float rotateDuration = movement != null ? movement.RotateDuration : 0.15f;
            runner.Enqueue(new RotateToTargetAction(actor.transform, targetTransform, rotateDuration / _currentBattleSpeed));
        }
    }

    private void EnqueueSkillReturn(
        ActionSequenceRunner runner,
        CharactorAnimationController actorAnim,
        UnitMovementProfile movement,
        bool shouldMove,
        bool shouldRotate,
        Vector3 originPosition,
        float originRotationY,
        ReturnPhase returnPhase)
    {
        if (runner == null || actorAnim == null || (!shouldMove && !shouldRotate))
        {
            return;
        }

        Quaternion originRotation = Quaternion.Euler(0f, originRotationY, 0f);
        string animationStateName = returnPhase?.AnimationStateName;
        float blendInSeconds = returnPhase != null ? Mathf.Max(0f, returnPhase.BlendInSeconds) : 0.1f;
        float duration = shouldMove
            ? (movement != null ? movement.ReturnDuration : 0.15f)
            : (movement != null ? movement.RotateReturnDuration : 0.15f);

        runner.Enqueue(new MoveToOriginAction(
            actorAnim, originPosition, originRotation, duration / _currentBattleSpeed,
            animationStateName, blendInSeconds));
    }

    /// <summary>
    /// 광역 스킬: 시전 애니 1회, HitDelay 시점에 전 타겟 동시 피격·데미지, 이후 전원 Idle 복귀.
    /// </summary>
    internal IEnumerator RunAoESkillSequence(List<DamageContext> contexts, List<Func<BattleHitResult>> hitCallbacks,
        HitDeliveryGate deliveryGate)
    {
        if (deliveryGate == null)
        {
            deliveryGate = new HitDeliveryGate();
        }

        if (contexts == null || contexts.Count == 0 || hitCallbacks == null || hitCallbacks.Count == 0)
        {
            yield break;
        }

        int pairCount = Mathf.Min(contexts.Count, hitCallbacks.Count);
        DamageContext leadContext = contexts[0];
        BattleCharactor actor = leadContext?.Caster;
        if (actor == null)
        {
            for (int i = 0; i < pairCount; i++)
            {
                hitCallbacks[i]?.Invoke();
            }

            yield break;
        }

        BattleCharactor primaryTarget = leadContext.Target;
        SkillData skill = ResolveSkillAnimationData(TryGetSkillDataForDamageContext(leadContext));
        ApplyPresentationOverride(skill);
        actor.EnsureAnimationController();
        CharactorAnimationController actorAnim = actor.Anim;
        actor.Anim?.SetAnimationSpeed(_currentBattleSpeed);

        string targetState = actorAnim != null
            ? actorAnim.GetTargetStateName(skill)
            : string.Empty;

        ResolveSkillMovement(actor, primaryTarget, skill, out UnitMovementProfile movement, out bool shouldMove, out bool shouldRotate, out Vector3 originPosition, out float originRotationY);

        SkillPresentationData presentation = skill != null ? _presentationCatalog?.Get(skill.skillIndex) : null;
        ProjectileVisualData projectileVisual = GetProjectileVisual(actor, skill, presentation);
        int presentationActionInstanceId = presentation?.IsPhaseCue == true ? NextActionInstanceId() : 0;
        bool moveEnabled       = presentation?.Move?.Enabled ?? true;
        bool attackPrepEnabled = presentation?.AttackPrepare?.Enabled ?? true;
        bool returnEnabled     = presentation?.Return?.Enabled ?? true;
        bool usePhaseCue       = presentation?.IsPhaseCue == true;
                var spinSweepCandidates = new List<BattleCharactor>();
                for (int i = 0; i < pairCount; i++)
                {
                    if (contexts[i]?.Target != null)
                    {
                        spinSweepCandidates.Add(contexts[i].Target);
                    }
                }
                MovingAttackPlan spinSweepPlan = null;
                bool useMovingAttack = usePhaseCue
                            && moveEnabled
                            && movement != null
                            && TryBuildMovingAttackPlan(actor, actorAnim, presentation, spinSweepCandidates,
                                out spinSweepPlan);

        float sequenceBattleElapsed = 0f;
        var runner = new ActionSequenceRunner();
        bool hasApproachMovementAnimation = !useMovingAttack && moveEnabled && shouldMove;
                bool hasReturnMovementAnimation = !useMovingAttack && returnEnabled && (shouldMove || shouldRotate);
                bool hasPhaseCueCombo = usePhaseCue && presentation.Attack != null && presentation.Attack.Enabled
                    && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0;
                float? movePrepareNextBlendSeconds = useMovingAttack
                    ? Mathf.Max(0f, presentation?.Move?.BlendInSeconds ?? 0.1f)
                    : hasPhaseCueCombo && !hasApproachMovementAnimation
                        ? ResolveMovePrepareNextBlendInSeconds(actorAnim, skill, presentation)
                        : null;
                float? postBlendAfterLastBeatSeconds = hasPhaseCueCombo && !hasReturnMovementAnimation
                    ? ResolvePostBlendInSeconds(presentation)
                    : null;
        
                EnqueuePhaseCuePrologue(runner, actor, primaryTarget, skill, presentation, presentationActionInstanceId,
            movePrepareNextBlendSeconds, elapsed => sequenceBattleElapsed += elapsed);

        bool useCombo = usePhaseCue && presentation.Attack != null && presentation.Attack.Enabled
                    && presentation.Attack.Beats != null && presentation.Attack.Beats.Count > 0;
        
                if (useMovingAttack)
                {
                    Func<MonoBehaviour, IEnumerator> primaryHitRoutine = null;
                    List<ASB.Work.Battle.Sequence.MovingAttackHitTarget> sequentialHits = null;
        
                    if (spinSweepPlan.Presentation.HitMode == MovingAttackHitMode.SequentialAoE)
                    {
                        sequentialHits = new List<ASB.Work.Battle.Sequence.MovingAttackHitTarget>();
                        for (int i = 0; i < pairCount; i++)
                        {
                            DamageContext context = contexts[i];
                            Func<BattleHitResult> callback = hitCallbacks[i];
                            if (context?.Target == null) continue;
        
                            sequentialHits.Add(new ASB.Work.Battle.Sequence.MovingAttackHitTarget
                            {
                                Target = context.Target,
                                HitRoutine = host => new AoEApplyDamageAction(
                                    new List<DamageContext> { context },
                                    new List<Func<BattleHitResult>> { callback },
                                    1, _currentBattleSpeed, _visualDirector, projectileVisual, deliveryGate).ExecuteRoutine(host)
                            });
                        }
                    }
                    else
                            {
                                if (spinSweepPlan.Presentation.HitMode == MovingAttackHitMode.SingleTarget)
                                {
                                    primaryHitRoutine = host => new AoEApplyDamageAction(
                                        new List<DamageContext> { contexts[0] },
                                        new List<Func<BattleHitResult>> { hitCallbacks[0] },
                                        1, _currentBattleSpeed, _visualDirector, projectileVisual, deliveryGate).ExecuteRoutine(host);
                                }
                                else
                                {
                                    primaryHitRoutine = host => new AoEApplyDamageAction(
                                        contexts, hitCallbacks, pairCount, _currentBattleSpeed, _visualDirector, projectileVisual, deliveryGate).ExecuteRoutine(host);
                                }
                            }
        
                            EnqueueMovingAttackApproach(runner, spinSweepPlan, actorAnim, movement, presentation?.Move);
        
                                    EnqueuePhaseCueAttackPrepare(runner, actor, primaryTarget, skill, presentation, presentationActionInstanceId,
                                        attackPrepEnabled ? ResolveMovingAttackIncomingBlendInSeconds(spinSweepPlan) : null,
                                        elapsed => sequenceBattleElapsed += elapsed);
        
                                    EnqueueMovingAttackSequence(runner, spinSweepPlan, actor, primaryTarget, skill, presentation,
                                        presentationActionInstanceId, actorAnim, primaryHitRoutine, sequentialHits,
                                        ResolveMovingAttackOutgoingBlendInSeconds(presentation, returnEnabled && movement != null),
                                        elapsed => sequenceBattleElapsed += elapsed);
                }
                else
                {
                    if (moveEnabled)
                        EnqueueSkillApproach(runner, actor, primaryTarget, actorAnim, movement, shouldMove, shouldRotate, presentation?.Move);
        
                    EnqueuePhaseCueAttackPrepare(runner, actor, primaryTarget, skill, presentation, presentationActionInstanceId,
                        useCombo ? ResolveFirstAttackBeatBlendInSeconds(actorAnim, skill, presentation) : null,
                        elapsed => sequenceBattleElapsed += elapsed);
                    if (!usePhaseCue && attackPrepEnabled && _visualDirector != null && skill != null)
                        runner.Enqueue(new SpawnAttackEffectAction(actor, skill.skillIndex, _visualDirector));
        
                    // AoE 타깃 리스트(EachTarget 앵커가 대상 수만큼 스폰하는 데 사용).
                    var aoePresentationTargets = new List<BattleCharactor>();
                    if (contexts != null)
                    {
                        foreach (DamageContext c in contexts)
                        {
                            if (c?.Target != null && !aoePresentationTargets.Contains(c.Target))
                            {
                                aoePresentationTargets.Add(c.Target);
                            }
                        }
                    }

                    if (useCombo)
                    {
                        runner.Enqueue(new ASB.Work.Battle.Sequence.ComboSkillAction(
                            actorAnim,
                            presentation.Attack.Beats,
                            (beat, index) => ResolveAttackBeatState(actorAnim, skill, presentation, beat, index),
                            (beat, stateName) => SetupPresentationContext(actor, primaryTarget, skill, presentation, presentationActionInstanceId, beat?.Cues, stateName, aoePresentationTargets),
                            host => ResolveAoEHitWithPresentationDeliveryRoutine(host, actor, presentation, presentationActionInstanceId, deliveryGate, contexts, hitCallbacks, pairCount, projectileVisual),
                            _currentBattleSpeed,
                            AnimEventTimeoutSeconds,
                            postBlendAfterLastBeatSeconds,
                            elapsed => sequenceBattleElapsed += elapsed));
                    }
                    else
                    {
                        if (usePhaseCue)
                            runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => ActivatePresentationCueRoutine(
                                actor, primaryTarget, skill, presentation, presentationActionInstanceId, GetPrimaryAttackBeat(presentation)?.Cues, targetState, aoePresentationTargets)));
                        runner.Enqueue(new PlaySkillAnimAction(actorAnim, skill, false));
                        runner.Enqueue(new WaitHitAction(actorAnim, skill, _currentBattleSpeed, elapsed => sequenceBattleElapsed += elapsed, AnimEventTimeoutSeconds));
                        runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => PlayChainLightningEffectRoutine(
                            presentation, actor, primaryTarget, contexts, pairCount)));
                        runner.Enqueue(new ASB.Work.Battle.Sequence.CustomEffectImpactAction(presentation, presentationActionInstanceId, deliveryGate, new AoEApplyDamageAction(contexts, hitCallbacks, pairCount, _currentBattleSpeed, _visualDirector, projectileVisual, deliveryGate)));
        
                        if (!string.IsNullOrEmpty(targetState))
                            runner.Enqueue(new WaitClipEndAction(actorAnim, targetState, elapsed => sequenceBattleElapsed += elapsed));
                    }
                }
        
                if (returnEnabled)
                {
                    if (useMovingAttack)
                        EnqueueMovingAttackReturn(runner, actorAnim, movement, originPosition, originRotationY, presentation?.Return);
                    else
                        EnqueueSkillReturn(runner, actorAnim, movement, shouldMove, shouldRotate, originPosition, originRotationY,
                            presentation?.Return);
                }
        
                if (!returnEnabled && !useMovingAttack && shouldRotate)
                {
                    EnqueueSkillReturn(runner, actorAnim, movement, false, true, originPosition, originRotationY,
                        presentation?.Return);
                }

                EnqueuePhaseCueEpilogue(runner, actor, primaryTarget, skill, presentation, presentationActionInstanceId,
            elapsed => sequenceBattleElapsed += elapsed);

        runner.Enqueue(new ReturnToIdleAction(actor));

        yield return StartCoroutine(runner.RunAll(this));

        ClearPresentationContext(actor);

        float remainingTotal = Mathf.Max(0f, skill.TotalDelay - sequenceBattleElapsed);
        yield return WaitForBattleSeconds(Mathf.Max(remainingTotal, 0.2f));

        for (int i = 0; i < pairCount; i++)
        {
            ReturnToIdleIfAlive(contexts[i]?.Target);
        }
    }

    private static List<Transform> BuildChainLightningTargets(IReadOnlyList<DamageContext> contexts)
    {
        var targets = new List<Transform>();
        var seen = new HashSet<Transform>();
        if (contexts == null)
        {
            return targets;
        }

        for (int i = 0; i < contexts.Count; i++)
        {
            DamageContext context = contexts[i];
            Transform targetTransform = context?.Role == DamageRole.Additional && context.Target != null
                ? context.Target.transform
                : null;
            if (targetTransform != null && seen.Add(targetTransform))
            {
                targets.Add(targetTransform);
            }
        }

        return targets;
    }

    private IEnumerator WaitForPresentationImpactRoutine(BattleCharactor target, SkillPresentationData presentation, HitDeliveryGate deliveryGate = null)
    {
        if (target == null || _chainLightningActionInstanceId <= 0)
        {
            yield break;
        }

        ImpactKey key = new ImpactKey(_chainLightningActionInstanceId, target.transform.GetInstanceID());
        yield return ASB.Work.Battle.Sequence.CustomEffectImpactAction.WaitForImpactKeyRoutine(presentation, key, deliveryGate, false);
    }

    private static string ResolveAdditionalChainTargetAnimationTrigger(SkillPresentationData presentation, SkillData skill)
    {
        if (presentation != null && !string.IsNullOrWhiteSpace(presentation.TargetAnimationTriggerOverride))
        {
            return presentation.TargetAnimationTriggerOverride.Trim();
        }

        return skill != null ? skill.ResolvedTargetAnimationTrigger : null;
    }

    private bool PlayChainLightningEffect(
        SkillPresentationData presentation,
        BattleCharactor actor,
        Transform primaryTargetTransform,
        IReadOnlyList<Transform> chainTargets)
    {
        if (presentation?.ChainLightningEffectPrefab == null
            || presentation.ProjectileVisual?.DeliveryMode != ProjectileDeliveryMode.ChainAdditionalTargets
            || actor == null
            || primaryTargetTransform == null)
        {
            return false;
        }

        GameObject effectPrefab = presentation.ChainLightningEffectPrefab;
        if (!_chainLightningEffects.TryGetValue(effectPrefab, out JC.VFX.ChainLightningVfx effect)
            || effect == null)
        {
            GameObject instance = Instantiate(effectPrefab);
            effect = instance.GetComponent<JC.VFX.ChainLightningVfx>();
            if (effect == null)
            {
                Debug.LogWarning($"[BattleManager] {effectPrefab.name}에 ChainLightningVfx가 없습니다.", effectPrefab);
                Destroy(instance);
                return false;
            }

            _chainLightningEffects[effectPrefab] = effect;
        }

        _chainLightningImpactEffect = effect;
        ChainLightningImpactProbe probe = effect.GetComponent<ChainLightningImpactProbe>();
        if (probe == null)
        {
            probe = effect.gameObject.AddComponent<ChainLightningImpactProbe>();
        }

        probe.Configure(_chainLightningActionInstanceId, primaryTargetTransform, chainTargets);
        effect.Play(actor.transform, primaryTargetTransform, chainTargets);
        return true;
    }

    private IEnumerator PlayChainLightningEffectRoutine(
        SkillPresentationData presentation,
        BattleCharactor actor,
        BattleCharactor primaryTarget,
        List<DamageContext> contexts,
        int pairCount)
    {
        var chainTargets = new List<Transform>();
        var seen = new HashSet<Transform>();
        for (int i = 1; i < pairCount; i++)
        {
            Transform targetTransform = contexts[i]?.Target != null ? contexts[i].Target.transform : null;
            if (targetTransform != null && targetTransform != primaryTarget.transform && seen.Add(targetTransform))
            {
                chainTargets.Add(targetTransform);
            }
        }

        PlayChainLightningEffect(presentation, actor, primaryTarget != null ? primaryTarget.transform : null, chainTargets);
        yield break;
    }

    private static void ReturnToIdleIfAlive(BattleCharactor unit)
    {
        if (unit == null || unit.IsDead)
        {
            return;
        }

        unit.EnsureAnimationController();
        unit.Anim?.PlayIdleAnimation();
    }

    public float CalculateSkillDamage(BattleCharactor actor, BattleCharactor target, SkillDataAsset skillData)
    {
        if (actor == null || target == null)
        {
            return 1f;
        }

        if (skillData == null)
        {
            return 1f;
        }

        float atk = actor.FinalStats.Atk;
        float def = target.FinalStats.DEF;

        float multiplier = Mathf.Max(0.01f, skillData.Power / 100f);
        float damage = (atk * multiplier) - def;
        return Mathf.Max(1.0f, damage);
    }

    private float CalculateGridSkillDamage(BattleCharactor actor, BattleCharactor target, float skillPercent)
    {
        if (actor == null || target == null)
        {
            return 1f;
        }

        float atk = actor.FinalStats.Atk;
        float def = target.FinalStats.DEF;
        float multiplier = Mathf.Max(0.01f, skillPercent / 100f);
        float damage = (atk * multiplier) - def;
        return Mathf.Max(1.0f, damage);
    }

    private float ApplyCriticalModifier(BattleCharactor actor, float baseDmg, out bool isCrit)
    {
        isCrit = false;
        if (actor == null)
        {
            return baseDmg;
        }

        float critRate = Mathf.Clamp01(actor.FinalStats.CritRate);
        isCrit = UnityEngine.Random.value < critRate;
        if (!isCrit)
        {
            return baseDmg;
        }

        float critMultiplier = Mathf.Max(1f, actor.FinalStats.CritMultiplier);
        return baseDmg * critMultiplier;
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

    private static bool IsMeleeSkillRange(BattleCharactor actor, SkillData skillData)
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

    private bool CheckEvade(BattleCharactor target)
    {
        if (target == null)
        {
            return false;
        }

        float evadeRate = Mathf.Clamp01(target.FinalStats.EvadeRate);
        return UnityEngine.Random.value < evadeRate;
    }

    private bool CheckCounter(BattleCharactor actor, BattleCharactor target, bool isCurrentlyCountering)
    {
        if (isCurrentlyCountering)
        {
            return false;
        }

        if (target == null || target.IsDead)
        {
            return false;
        }

        if (actor == null || actor.IsDead)
        {
            return false;
        }

        float counterRate = Mathf.Clamp01(target.FinalStats.CounterRate);
        return UnityEngine.Random.value < counterRate;
        // (선택) 사거리 체크: actor/target의 GridCell 거리 등이 필요하면 여기에 추가
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
