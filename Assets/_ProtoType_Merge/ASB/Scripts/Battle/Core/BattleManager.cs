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
/// ?꾪닾 ?ㅽ뻾怨??ㅽ궗 寃곌낵 ?곸슜???대떦?⑸땲?? ?곕?吏????긽 target.TakeDamage濡??곸슜?⑸땲??
/// </summary>
[DisallowMultipleComponent]
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }
    public event Action<string> OnActionExecuted;

    /// <summary>UI ???紐??癒?퐣 獄쏄퀣??癰궰野껋럩???遺욧퍕????獄쏆뮇源??쀪땁??덈뼄. AutoBattleController.OnAutoBattleToggleRequested?? ??덉뵬?????쉘.</summary>
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

    // [CSV 沃섎챷????袁⑸뻻] CSV??UseAnimEvent / HitDelay ?뚎됱쓥????곷선 Presentation 燁삳똾源됪에?볥젃?癒?퐣 ??堉?怨룸빍??
    // CSV ??쎄텕筌??곕떽? ?????袁⑤굡?? ApplyPresentationOverride 筌롫뗄苑??? ??볤탢??뤾쉭??
    [Header("Hit Timing Override (CSV 沃섎챷????袁⑸뻻)")]
    [SerializeField] private SkillPresentationCatalog _presentationCatalog;

    public float CurrentBattleSpeed => _currentBattleSpeed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BattleManager] 餓λ쵎???紐꾨뮞??곷뮞揶쎛 揶쏅Ŋ???뤿???щ빍??");
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

    // TODO: ?袁る떮 VFX Instantiate 野껋럥以덂첎? ?곕떽???롢늺 ??밴쉐 筌욊낱??ApplyBattleSpeedToVfx(vfxInstance)???紐꾪뀱??뤾쉭??
    // ?袁⑹삺 TmpBattleScene ?袁る떮 ??쎄쾿?깆???癒?뮉 ParticleSystem ??쎄텢 ??꾨읃??Instantiate ?꾨뗀諭뜹첎? ??곷뮸??덈뼄.
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

        // ??삳뼊 ??딅뱜 筌욊쑵六?餓???彛???野껋옕肉??????袁⑸꺗 ??野꺿뫗? ?얜똻???몃빍??
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
        if (context?.Target == null || predicted == null)
        {
            return predicted;
        }

        if (context.Target.IsDead)
        {
            return predicted;
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
        if (result.DamageContexts != null)
        {
            bool isAoE = result.Handler is BaseAoESkillHandler;

            if (isAoE)
            {
                var aoeContexts = new List<DamageContext>();
                var hitCallbacks = new List<Func<BattleHitResult>>();

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
                    result.RecordDamageResult(damageContext, predicted);

                    aoeContexts.Add(damageContext);
                    hitCallbacks.Add(() =>
                    {
                        BattleHitResult r = CommitDamage(damageContext, predicted);
                        totalDamageDealt += r?.Damage ?? 0f;
                        return r;
                    });
                }

                if (aoeContexts.Count > 0)
                {
                    var aoeQueue = new ASB.Work.Battle.Command.BattleActionQueue();
                    aoeQueue.Enqueue(new ASB.Work.Battle.Command.SkillActionCommand(aoeContexts, hitCallbacks));
                    yield return StartCoroutine(aoeQueue.RunAll(this));
                }
            }
            else
            {
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
                    result.RecordDamageResult(damageContext, predicted);

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
                            totalDamageDealt += r?.Damage ?? 0f;
                            return r;
                        }));
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
                    }));
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

    /// <summary>ClassSkillSheet ??깆벥 skillValue(?? 1.2 = 120%)嚥?域밸챶?????쎄텢 ??노퉸???④쑴沅??몃빍??</summary>
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
            Debug.LogWarning("[BattleManager] Influence揶쎛 ?봔鈺곌퉲釉????쎄텢???????????곷뮸??덈뼄.");
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

    private List<CounterAttackRequest> CollectCounterAttackRequests(SkillExecutionResult result)
    {
        var requests = new List<CounterAttackRequest>();
        if (result.DamageContexts == null) return requests;

        BattleCharactor originalCaster = result.DamageContexts
            .FirstOrDefault(ctx => ctx?.Caster != null)
            ?.Caster;
        if (originalCaster == null) return requests;

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
                Debug.Log($"[Combat] {defender.UnitName} 獄쏆꼵爰???쎄텢({skill.skillIndex})???怨?筌왖 ??쎄텢???袁⑤빍??곴퐣 獄쏆꼵爰???뽰뇚");
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

        Debug.Log($"[Combat] {req.Defender.UnitName} 域뱀눘??獄쏆꼵爰?獄쏆뮆猷? (?④쑴??0.5)");

        // 獄쏆꼵爰?? ?뚣끉??? ?紐껊굶??? ?얜똻???랁?疫꿸퀡???怨?筌왖 野껋럥以덌쭕??????몃빍??
        // Influence ???걟 ??곸벉, ?怨?筌왖 ?④쑴??0.5, 獄쏆꼵爰?? 獄쏆꼵爰???醫딆뻣??? ??녿뮸??덈뼄.
        SkillExecutionResult result = BuildDefaultSkillResult(
            req.Defender,
            req.OriginalCaster,
            req.Skill,
            SkillExecutionOptions.CounterDefault);

        bool executed = false;
        yield return StartCoroutine(ApplySkillExecutionResultRoutine(result, success => executed = success));

        if (!executed)
        {
            Debug.LogWarning($"[BattleManager] {req.Defender.UnitName} 獄쏆꼵爰???쎈뻬 ??쎈솭.");
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

        // 疫꿸퀡???⑤벀爰?? ExecuteBasicAttack 野껋럥以덃에??브쑬???뤿선 ??됰선 ??由경에???쇰선??? ??녿뮸??덈뼄.
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

    // [CSV 沃섎챷????袁⑸뻻] SkillPresentationCatalog?癒?퐣 UseAnimEvent / HitDelay????뚮선 SkillData????堉?怨룸빍??
    // CSV ??쎄텕筌띾뜆肉??뚎됱쓥???곕떽???롢늺 ??筌롫뗄苑???紐꾪뀱?봔?? 筌롫뗄苑???癒?퍥????볤탢??뤾쉭??
    private void ApplyPresentationOverride(SkillData skill)
    {
        if (skill == null || _presentationCatalog == null) return;
        SkillPresentationData presentation = _presentationCatalog.Get(skill.skillIndex);
        if (presentation == null) return;

        skill.UseAnimEvent = presentation.UseAnimEvent;
        skill.HitDelay     = presentation.HitDelay;

        if (!string.IsNullOrWhiteSpace(presentation.AnimationTriggerOverride))
            skill.AnimationTrigger = presentation.AnimationTriggerOverride;
        if (!string.IsNullOrWhiteSpace(presentation.TargetAnimationTriggerOverride))
            skill.TargetAnimationTrigger = presentation.TargetAnimationTriggerOverride;
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

        return source;
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

    internal IEnumerator RunSkillSequenceCore(
        BattleCharactor actor,
        BattleCharactor target,
        SkillData skill,
        bool playBasicAttackAnimation,
        bool playTargetHitAnimation,
        Func<BattleHitResult> onHitCallback)
    {
        if (actor == null)
        {
            onHitCallback?.Invoke();
            yield break;
        }

        skill = ResolveSkillAnimationData(skill);
        ApplyPresentationOverride(skill); // [CSV 沃섎챷????袁⑸뻻]
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

        float sequenceBattleElapsed = 0f;
        var runner = new ActionSequenceRunner();

        EnqueueSkillApproach(runner, actor, target, actorAnim, movement, shouldMove, shouldRotate);

        if (_visualDirector != null && skill != null)
            runner.Enqueue(new SpawnAttackEffectAction(actor, skill.skillIndex, _visualDirector));

        runner.Enqueue(new PlaySkillAnimAction(actorAnim, skill, playBasicAttackAnimation, actor));
        runner.Enqueue(new WaitHitAction(actorAnim, skill, _currentBattleSpeed, elapsed => sequenceBattleElapsed += elapsed, AnimEventTimeoutSeconds));

        SkillPresentationData presentation = _visualDirector?.GetPresentation(skill?.skillIndex ?? 0);
        if (presentation?.ProjectilePrefab != null && target != null)
        {
            runner.Enqueue(new SpawnProjectileAction(actor, target, skill.skillIndex, onHitCallback, targetAnimTrigger, _currentBattleSpeed, _visualDirector, presentation));
        }
        else
        {
            runner.Enqueue(new ResolveHitAction(actor, target, onHitCallback, targetAnimTrigger, _currentBattleSpeed, _visualDirector));
        }

        if (!string.IsNullOrEmpty(targetState))
            runner.Enqueue(new WaitClipEndAction(actorAnim, targetState, elapsed => sequenceBattleElapsed += elapsed));

        EnqueueSkillReturn(runner, actorAnim, movement, shouldMove, shouldRotate, originPosition, originRotationY);

        runner.Enqueue(new ReturnToIdleAction(actor));

        yield return StartCoroutine(runner.RunAll(this));

        float remainingTotal = Mathf.Max(0f, skill.TotalDelay - sequenceBattleElapsed);
        yield return WaitForBattleSeconds(Mathf.Max(remainingTotal, 0.2f));

        if (target != null)
            yield return StartCoroutine(new WaitTargetReactionAction(target, _currentBattleSpeed).ExecuteRoutine(this));
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
        bool shouldRotate)
    {
        if (target == null || movement == null)
        {
            return;
        }

        if (shouldMove)
        {
            runner.Enqueue(new MoveToTargetAction(actorAnim, target.transform, movement.ApproachDistance, movement.MoveDuration / _currentBattleSpeed));
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
        float originRotationY)
    {
        if (movement == null)
        {
            return;
        }

        Quaternion originRotation = Quaternion.Euler(0f, originRotationY, 0f);
        if (shouldMove)
        {
            runner.Enqueue(new MoveToOriginAction(actorAnim, originPosition, originRotation, movement.ReturnDuration / _currentBattleSpeed));
        }
        else if (shouldRotate)
        {
            runner.Enqueue(new MoveToOriginAction(actorAnim, originPosition, originRotation, movement.RotateReturnDuration / _currentBattleSpeed));
        }
    }

    /// <summary>
    /// ?용쵐肉???쎄텢: ??뽰읈 ?醫딅빍 1?? HitDelay ??뽰젎??????野???덈뻻 ??④봄夷?怨?筌왖, ??꾩뜎 ?袁⑹뜚 Idle 癰귣벀?.
    /// </summary>
    internal IEnumerator RunAoESkillSequence(List<DamageContext> contexts, List<Func<BattleHitResult>> hitCallbacks)
    {
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
        ApplyPresentationOverride(skill); // [CSV 沃섎챷????袁⑸뻻]
        actor.EnsureAnimationController();
        CharactorAnimationController actorAnim = actor.Anim;
        actor.Anim?.SetAnimationSpeed(_currentBattleSpeed);

        string targetState = actorAnim != null
            ? actorAnim.GetTargetStateName(skill)
            : string.Empty;

        ResolveSkillMovement(actor, primaryTarget, skill, out UnitMovementProfile movement, out bool shouldMove, out bool shouldRotate, out Vector3 originPosition, out float originRotationY);

        float sequenceBattleElapsed = 0f;
        var runner = new ActionSequenceRunner();

        EnqueueSkillApproach(runner, actor, primaryTarget, actorAnim, movement, shouldMove, shouldRotate);

        if (_visualDirector != null && skill != null)
            runner.Enqueue(new SpawnAttackEffectAction(actor, skill.skillIndex, _visualDirector));

        runner.Enqueue(new PlaySkillAnimAction(actorAnim, skill, false));
        runner.Enqueue(new WaitHitAction(actorAnim, skill, _currentBattleSpeed, elapsed => sequenceBattleElapsed += elapsed, AnimEventTimeoutSeconds));
        runner.Enqueue(new AoEApplyDamageAction(contexts, hitCallbacks, pairCount, _currentBattleSpeed, _visualDirector));

        if (!string.IsNullOrEmpty(targetState))
            runner.Enqueue(new WaitClipEndAction(actorAnim, targetState, elapsed => sequenceBattleElapsed += elapsed));

        EnqueueSkillReturn(runner, actorAnim, movement, shouldMove, shouldRotate, originPosition, originRotationY);

        runner.Enqueue(new ReturnToIdleAction(actor));

        yield return StartCoroutine(runner.RunAll(this));

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
        // (?醫뤾문) ??援끿뵳?筌ｋ똾寃? actor/target??GridCell 椰꾧퀡???源놁뵠 ?袁⑹뒄??롢늺 ??由???곕떽?
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
