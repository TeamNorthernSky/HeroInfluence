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
        var deliveryGates = new Dictionary<DamageContext, HitDeliveryGate>();
        _projectileChainState = null; // 캐스트마다 초기화(이전 체인 상태 누수 방지)
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
                    deliveryGates[damageContext] = aoeDeliveryGate;

                    aoeContexts.Add(damageContext);
                    hitCallbacks.Add(() =>
                    {
                        BattleHitResult r = CommitDamage(damageContext, predicted);
                        result.RecordDamageResult(damageContext, r);
                        totalDamageDealt += r?.Damage ?? 0f;
                        return r;
                    });
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
                    deliveryGates[damageContext] = deliveryGate;

                    SkillData hitAnimSkill = ResolveSkillAnimationData(TryGetSkillDataForDamageContext(damageContext));
                    var actionQueue = new ASB.Work.Battle.Command.BattleActionQueue();
                    actionQueue.Enqueue(new ASB.Work.Battle.Command.SkillActionCommand(
                        damageContext.Caster,
                        damageContext.Target,
                        hitAnimSkill,
                        playBasicAttackAnimation: false,
                        playTargetHitAnimation: true,
                        onHitCallback: () =>
                        {
                            BattleHitResult r = CommitDamage(damageContext, predicted);
                            result.RecordDamageResult(damageContext, r);
                            totalDamageDealt += r?.Damage ?? 0f;
                            return r;
                        }, deliveryGate));
                    yield return StartCoroutine(actionQueue.RunAll(this));

                    float delay = Mathf.Max(0f, damageContext.DelayAfter);
                    if (delay > 0f)
                    {
                        yield return WaitForBattleSeconds(delay);
                    }
                }
            }
        }

        if (result.HealContexts != null && result.HealContexts.Count > 0)
        {
            for (int i = 0; i < result.HealContexts.Count; i++)
            {
                HealContext healContext = result.HealContexts[i];
                if (healContext == null || healContext.Caster == null || healContext.Target == null || healContext.Target.IsDead)
                {
                    continue;
                }

                SkillData healAnimSkill = ResolveSkillAnimationData(TryGetSkillDataForHealContext(healContext));
                var healQueue = new ASB.Work.Battle.Command.BattleActionQueue();
                healQueue.Enqueue(new ASB.Work.Battle.Command.SkillActionCommand(
                    healContext.Caster,
                    healContext.Target,
                    healAnimSkill,
                    playBasicAttackAnimation: false,
                    playTargetHitAnimation: false,
                    onHitCallback: () =>
                    {
                        healContext.Target.ApplyHeal(healContext.HealAmount);
                        return new BattleHitResult
                        {
                            Target = healContext.Target,
                            Damage = healContext.HealAmount,
                            IsHeal = true,
                            SkillIndex = healContext.SkillIndex
                        };
                    }, new HitDeliveryGate()));
                yield return StartCoroutine(healQueue.RunAll(this));
            }
        }

        if (result.StatusEffectContexts != null && result.StatusEffectContexts.Count > 0)
        {
            for (int i = 0; i < result.StatusEffectContexts.Count; i++)
            {
                StatusEffectContext statusContext = result.StatusEffectContexts[i];
                if (statusContext.Target == null || statusContext.Target.IsDead)
                {
                    continue;
                }

                if (CanApplyStatusEffect(statusContext, deliveryGates))
                {
                    ApplyStatusEffect(statusContext);
                }
            }
        }

        var counterRequests = CollectCounterAttackRequests(result, deliveryGates);
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
            OnActionExecuted?.Invoke(GetSkillDisplayName(classSkillRow));
        }

        onCompleted?.Invoke(executed);
    }

    private List<CounterAttackRequest> CollectCounterAttackRequests(SkillExecutionResult result, IReadOnlyDictionary<DamageContext, HitDeliveryGate> deliveryGates)
    {
        var requests = new List<CounterAttackRequest>();
        if (result.DamageContexts == null) return requests;

        BattleCharactor originalCaster = result.DamageContexts
            .FirstOrDefault(ctx => ctx?.Caster != null)
            ?.Caster;
        if (originalCaster == null) return requests;

        var candidates = result.DamageContexts
            .Where(ctx => ctx != null && ctx.CanTriggerCounter && CanApplyDeliveryEffects(ctx, deliveryGates))
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

    private static bool CanApplyStatusEffect(
        StatusEffectContext statusContext,
        IReadOnlyDictionary<DamageContext, HitDeliveryGate> deliveryGates)
    {
        if (deliveryGates == null)
        {
            return true;
        }

        foreach (KeyValuePair<DamageContext, HitDeliveryGate> pair in deliveryGates)
        {
            DamageContext damageContext = pair.Key;
            if (damageContext != null
                && damageContext.Caster == statusContext.Caster
                && damageContext.Target == statusContext.Target
                && pair.Value != null
                && !pair.Value.CanApplyEffects)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanApplyDeliveryEffects(
        DamageContext damageContext,
        IReadOnlyDictionary<DamageContext, HitDeliveryGate> deliveryGates)
    {
        return damageContext != null
            && (deliveryGates == null
                || !deliveryGates.TryGetValue(damageContext, out HitDeliveryGate gate)
                || gate == null
                || gate.CanApplyEffects);
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
        IReadOnlyList<BattleCharactor> aoeTargets = null)
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
            TargetPosition = target != null ? target.transform.position : actor.transform.position,
            SocketTransform = actor.GetComponent<UnitVisualProfile>()?.AttackEffectSocket ?? actor.transform,
            PlaybackSpeed = _currentBattleSpeed,
            HitIndex = 0,
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
        SkillPresentationData presentation, IEnumerable<BattleCharactor> candidateTargets, out MovingAttackPlan plan)
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

        if (targets.Count == 0) return false;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < targets.Count; i++) center += targets[i].transform.position;
        center /= targets.Count;

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
        BattleCharactor target,
        SkillData skill,
        bool playBasicAttackAnimation,
        bool playTargetHitAnimation,
        Func<BattleHitResult> onHitCallback,
        HitDeliveryGate deliveryGate)
    {
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

        ResolveSkillMovement(actor, target, skill, out UnitMovementProfile movement, out bool shouldMove, out bool shouldRotate, out Vector3 originPosition, out float originRotationY);

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
                            && TryBuildMovingAttackPlan(actor, actorAnim, presentation, new[] { target }, out spinSweepPlan);

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
                                host => ResolveSkillHitRoutine(host, actor, target, onHitCallback, targetAnimTrigger,
                                    playTargetHitAnimation, skill, deliveryGate),
                                null,
                                ResolveMovingAttackOutgoingBlendInSeconds(presentation, returnEnabled && movement != null),
                                elapsed => sequenceBattleElapsed += elapsed);
                        }
                        
                else
                {
                    if (moveEnabled)
                        EnqueueSkillApproach(runner, actor, target, actorAnim, movement, shouldMove, shouldRotate, presentation?.Move);
        
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
                            (beat, stateName) => SetupPresentationContext(actor, target, skill, presentation, presentationActionInstanceId, beat?.Cues, stateName),
                            host => ResolveSkillHitRoutine(host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate),
                            _currentBattleSpeed,
                            AnimEventTimeoutSeconds,
                            postBlendAfterLastBeatSeconds,
                            elapsed => sequenceBattleElapsed += elapsed));
                    }
                    else
                    {
                        if (usePhaseCue)
                            runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => ActivatePresentationCueRoutine(
                                actor, target, skill, presentation, presentationActionInstanceId, GetPrimaryAttackBeat(presentation)?.Cues, targetState)));
                        runner.Enqueue(new PlaySkillAnimAction(actorAnim, skill, playBasicAttackAnimation, actor));
                        runner.Enqueue(new WaitHitAction(actorAnim, skill, _currentBattleSpeed, elapsed => sequenceBattleElapsed += elapsed, AnimEventTimeoutSeconds));
                        runner.Enqueue(new ASB.Work.Battle.Sequence.RunRoutineAction(host => ResolveSkillHitRoutine(
                            host, actor, target, onHitCallback, targetAnimTrigger, playTargetHitAnimation, skill, deliveryGate)));
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
        ProjectileVisualData projectileVisual = presentation?.GetProjectileVisual();
        if (projectileVisual != null && projectileVisual.DeliveryMode == ProjectileDeliveryMode.ChainAdditionalTargets)
        {
            return new ProjectileChainState();
        }
        return null;
    }

    private IEnumerator ResolveSkillHitRoutine(MonoBehaviour host, BattleCharactor actor, BattleCharactor target,
        Func<BattleHitResult> onHitCallback, string targetAnimTrigger, bool playTargetHitAnimation, SkillData skill,
        HitDeliveryGate deliveryGate)
    {
        SkillPresentationData presentation = skill != null ? _presentationCatalog?.Get(skill.skillIndex) : null;
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

            var projectile = new ProjectileImpactAction(actor, target, projectileVisual, _currentBattleSpeed, deliveryGate, skill.skillIndex, originOverride);
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
            if (!deliveryGate.CanApplyEffects)
            {
                yield break;
            }
            if (projectile.DeliveryResult == ProjectileDeliveryResult.Arrived)
            {
                targetAnimTrigger = null;
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
        projectileVisual = null;
        if (!playTargetHitAnimation || skill == null || skill.classSkillEffect != 0 || !IsRangedSkill(actor, skill))
        {
            return false;
        }

        projectileVisual = presentation?.GetProjectileVisual();
        return projectileVisual != null && projectileVisual.Prefab != null;
    }

    private ProjectileVisualData GetProjectileVisual(BattleCharactor actor, SkillData skill, SkillPresentationData presentation)
    {
        return TryGetProjectileVisual(actor, skill, presentation, true, out ProjectileVisualData projectileVisual)
            ? projectileVisual : null;
    }
    private static void ResolveSkillMovement(
        BattleCharactor actor,
        BattleCharactor target,
        SkillData skill,
        out UnitMovementProfile movement,
        out bool shouldMove,
        out bool shouldRotate,
        out Vector3 originPosition,
        out float originRotationY)
    {
        originPosition = actor != null ? actor.transform.position : Vector3.zero;
        originRotationY = actor != null ? actor.transform.eulerAngles.y : 0f;
        movement = actor != null ? actor.GetComponent<UnitMovementProfile>() : null;
        shouldMove = movement != null && !movement.RotateOnly && target != null && IsMeleeSkillRange(actor, skill);
        shouldRotate = movement != null && movement.RotateOnly && target != null;
    }

    private void EnqueueSkillApproach(
        ActionSequenceRunner runner,
        BattleCharactor actor,
        BattleCharactor target,
        CharactorAnimationController actorAnim,
        UnitMovementProfile movement,
        bool shouldMove,
        bool shouldRotate,
        MovePhase movePhase)
    {
        if (target == null || movement == null)
        {
            return;
        }

        string animationStateName = movePhase?.AnimationStateName;
        float blendInSeconds = movePhase != null ? Mathf.Max(0f, movePhase.BlendInSeconds) : 0.1f;

        if (shouldMove)
        {
            runner.Enqueue(new MoveToTargetAction(
                actorAnim, target.transform, movement.ApproachDistance, movement.MoveDuration / _currentBattleSpeed,
                animationStateName, blendInSeconds));
        }
        else if (shouldRotate)
        {
            runner.Enqueue(new RotateToTargetAction(actor.transform, target.transform, movement.RotateDuration / _currentBattleSpeed));
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
        if (movement == null)
        {
            return;
        }

        Quaternion originRotation = Quaternion.Euler(0f, originRotationY, 0f);
        string animationStateName = returnPhase?.AnimationStateName;
        float blendInSeconds = returnPhase != null ? Mathf.Max(0f, returnPhase.BlendInSeconds) : 0.1f;

        if (shouldMove)
        {
            runner.Enqueue(new MoveToOriginAction(
                actorAnim, originPosition, originRotation, movement.ReturnDuration / _currentBattleSpeed,
                animationStateName, blendInSeconds));
        }
        else if (shouldRotate)
        {
            runner.Enqueue(new MoveToOriginAction(
                actorAnim, originPosition, originRotation, movement.RotateReturnDuration / _currentBattleSpeed,
                animationStateName, blendInSeconds));
        }
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
                            host => new AoEApplyDamageAction(contexts, hitCallbacks, pairCount, _currentBattleSpeed, _visualDirector, projectileVisual, deliveryGate).ExecuteRoutine(host),
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
                        runner.Enqueue(new AoEApplyDamageAction(contexts, hitCallbacks, pairCount, _currentBattleSpeed, _visualDirector, projectileVisual, deliveryGate));
        
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
