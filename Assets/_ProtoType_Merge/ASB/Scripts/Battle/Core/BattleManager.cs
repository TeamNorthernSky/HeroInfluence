using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ASB.Work.Battle.SkillExecution;
using ASB.Work.Battle.Core;
using ASB.Work.Battle.Sequence;

/// <summary>
/// BattleAction 및 플레이어 입력에 의한 전투 실행. 데미지는 항상 target.TakeDamage로 적용합니다.
/// </summary>
[DisallowMultipleComponent]
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }
    public event Action<string> OnActionExecuted;

    private const int ClassSkillEffect_Heal = 1;
    private const int ClassSkillEffect_Revive = 2;
    private const int ClassSkillEffect_Buff = 3;

    private const float AnimEventTimeoutSeconds = 2f;

    [Header("Battle Speed")]
    [SerializeField] private float _currentBattleSpeed = 1.0f;
    [SerializeField] private BattleFlowManager battleFlowManager;

    [Header("Visual")]
    [SerializeField] private BattleVisualDirector _visualDirector;

    public float CurrentBattleSpeed => _currentBattleSpeed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BattleManager] 중복 인스턴스가 감지되었습니다.");
        }

        Instance = this;

        if (battleFlowManager == null)
        {
            battleFlowManager = FindObjectOfType<BattleFlowManager>();
        }
    }

    public void ChangeBattleSpeed(float newSpeed)
    {
        _currentBattleSpeed = Mathf.Max(0.01f, newSpeed);
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

        float finalDamage = CombatCalculator.CalculateDamage(context);
        context.Target.TakeDamage(finalDamage);
        Debug.Log($"[Combat] {context.Caster.UnitName} -> {context.Target.UnitName} dmg={finalDamage:F1} (Crit: {context.IsCritical})");

        return new BattleHitResult
        {
            Target = context.Target,
            Damage = finalDamage,
            IsCritical = context.IsCritical,
            TargetDied = context.Target.IsDead,
            SkillIndex = context.SkillIndex
        };
    }

    public IEnumerator ApplySkillExecutionResultRoutine(SkillExecutionResult result, Action<bool> onCompleted = null, bool isCounter = false)
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

                    aoeContexts.Add(damageContext);
                    hitCallbacks.Add(() =>
                    {
                        BattleHitResult r = ApplyDamage(damageContext);
                        totalDamageDealt += r?.Damage ?? 0f;
                        return r;
                    });
                }

                if (aoeContexts.Count > 0)
                {
                    yield return RunAoESkillSequence(aoeContexts, hitCallbacks);
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

                    SkillData hitAnimSkill = ResolveSkillAnimationData(TryGetSkillDataForDamageContext(damageContext));
                    yield return RunSkillSequenceCore(
                        damageContext.Caster,
                        damageContext.Target,
                        hitAnimSkill,
                        playBasicAttackAnimation: false,
                        playTargetHitAnimation: true,
                        () =>
                        {
                            BattleHitResult r = ApplyDamage(damageContext);
                            totalDamageDealt += r?.Damage ?? 0f;
                            return r;
                        });

                    float delay = Mathf.Max(0f, damageContext.DelayAfter);
                    if (delay > 0f)
                    {
                        yield return WaitForBattleSeconds(delay);
                    }
                }
            }
        }

        if (!isCounter && result.DamageContexts != null && result.DamageContexts.Any(ctx => ctx != null && ctx.CanTriggerCounter))
        {
            BattleCharactor originalCaster = result.DamageContexts[0].Caster;
            var counterCandidates = result.DamageContexts
                .Where(ctx => ctx != null && ctx.CanTriggerCounter)
                .Select(ctx => ctx.Target)
                .Distinct()
                .Where(t => t != null && !t.IsDead && originalCaster != null && t.IsPlayer != originalCaster.IsPlayer)
                .ToList();

            foreach (BattleCharactor defender in counterCandidates)
            {
                if (originalCaster == null || originalCaster.IsDead)
                {
                    break;
                }

                if (!CombatCalculator.RollCounter(defender))
                {
                    continue;
                }

                Debug.Log($"[Combat] {defender.UnitName} 근접 반격 발동! (계수 0.5)");
                bool counterDone = false;
                yield return StartCoroutine(ExecuteBasicAttack(defender, originalCaster, success => counterDone = success, true));
            }
        }

        result.OnPostExecution?.Invoke(totalDamageDealt);
        onCompleted?.Invoke(true);
    }

    public void ApplyStatusEffect(StatusEffectContext context)
    {
        if (context.Caster == null || context.Target == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(context.EffectType))
        {
            return;
        }

        if (context.EffectType.Equals("Taunt", StringComparison.OrdinalIgnoreCase))
        {
            SkillEffectHelper.SetTaunt(context.Caster, context.Target, context.DurationTurn);
            return;
        }

        Debug.LogWarning($"[BattleManager] ApplyStatusEffect: 지원하지 않는 EffectType={context.EffectType}");
    }

    /// <summary>ClassSkillSheet 행의 skillValue(예: 1.2 = 120%)로 그리드 스킬 피해를 계산합니다.</summary>
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

        if (SkillExecutionRegistry.TryGetHandler(classSkillRow.skillIndex, out ISkillEffectHandler custom))
        {
            SkillExecutionResult result = custom.Execute(actor, target, classSkillRow, null);
            result.Handler = custom;
            bool executedByCustom = false;
            yield return StartCoroutine(ApplySkillExecutionResultRoutine(result, success => executedByCustom = success));
            if (executedByCustom)
            {
                OnActionExecuted?.Invoke(GetSkillDisplayName(classSkillRow));
            }
            onCompleted?.Invoke(executedByCustom);
            yield break;
        }

        bool executedByDefault = false;
        yield return StartCoroutine(ExecuteDefaultSkill(actor, target, classSkillRow, success => executedByDefault = success));
        if (executedByDefault)
        {
            OnActionExecuted?.Invoke(GetSkillDisplayName(classSkillRow));
        }
        onCompleted?.Invoke(executedByDefault);
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

    /// <summary>레지스트리에 없는 일반 스킬: 데이터만으로 힐/딜 처리.</summary>
    private IEnumerator ExecuteDefaultSkill(BattleCharactor actor, BattleCharactor target, SkillData skillData, Action<bool> onCompleted = null)
    {
        if (skillData == null || actor == null || target == null)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (actor.IsDead)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        if (target.IsDead && skillData.classSkillEffect != ClassSkillEffect_Revive)
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        float multiplier = Mathf.Max(0.01f, skillData.skillValue);

        if (skillData.classSkillEffect == ClassSkillEffect_Heal)
        {
            float heal = Mathf.Max(0f, actor.FinalStats.Atk * multiplier);
            SkillData healAnimSkill = ResolveSkillAnimationData(skillData);
            yield return RunSkillSequenceCore(
                actor,
                target,
                healAnimSkill,
                playBasicAttackAnimation: false,
                playTargetHitAnimation: false,
                () =>
                {
                    target.ApplyHeal(heal);
                    return new BattleHitResult { Target = target, Damage = heal, IsHeal = true };
                });
            Debug.Log($"[Battle] GridHeal: {GetLabel(actor)} -> {GetLabel(target)} heal={heal:F1} (×{multiplier:0.##})");
            onCompleted?.Invoke(true);
            yield break;
        }

        if (skillData.classSkillEffect == ClassSkillEffect_Buff)
        {
            SkillData buffAnimSkill = ResolveSkillAnimationData(skillData);
            yield return RunSkillSequenceCore(
                actor,
                target,
                buffAnimSkill,
                playBasicAttackAnimation: false,
                playTargetHitAnimation: false,
                () =>
                {
                    ApplyBuff(actor, target, skillData);
                    return BattleHitResult.Empty(target);
                });
            onCompleted?.Invoke(true);
            yield break;
        }

        var context = new DamageContext
        {
            Caster = actor,
            Target = target,
            SkillMultiplier = multiplier,
            SkillIndex = skillData.skillIndex,
            IsRangedAttack = IsRangedSkill(actor, skillData),
            CanTriggerCounter = IsMeleeSkillRange(actor, skillData) && target.IsInFrontRow,
            IsCounterAttack = false
        };
        context.IsCritical = CombatCalculator.RollCritical(context);

        float dealt = 0f;
        SkillData damageAnimSkill = ResolveSkillAnimationData(skillData);
        yield return RunSkillSequenceCore(
            actor,
            target,
            damageAnimSkill,
            playBasicAttackAnimation: false,
            playTargetHitAnimation: true,
            () => { BattleHitResult r = ApplyDamage(context); dealt = r?.Damage ?? 0f; return r; });

        if (context.DelayAfter > 0f)
        {
            yield return WaitForBattleSeconds(context.DelayAfter);
        }
        Debug.Log($"[Battle] GridSkill: {GetLabel(actor)} -> {GetLabel(target)} dmg={dealt:F1} (×{multiplier:0.##})");
        onCompleted?.Invoke(true);
    }

    private static void ApplyBuff(BattleCharactor actor, BattleCharactor target, SkillData skillData)
    {
        if (actor == null || target == null || skillData == null)
        {
            return;
        }

        Debug.Log(
            $"[Battle] GridBuff: {GetLabel(actor)} -> {GetLabel(target)} (×{Mathf.Max(0.01f, skillData.skillValue):0.##})");
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
        float dealt = ApplyDamage(context);
        if (context.DelayAfter > 0f)
        {
            yield return WaitForBattleSeconds(context.DelayAfter);
        }
        Debug.Log($"[Battle] GridSkill: {GetLabel(actor)} -> {GetLabel(target)} dmg={dealt:F1} ({skillPercent:0.##}%)");
        onCompleted?.Invoke(true);
    }

    /// <summary>일반 공격: 피해 = max(1, 공격력×배율 - 방어력) 후 HP 감소.</summary>
    public IEnumerator ExecuteBasicAttack(BattleCharactor actor, BattleCharactor target, Action<bool> onCompleted = null, bool isCounterAttack = false)
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

        var context = new DamageContext
        {
            Caster = actor,
            Target = target,
            SkillMultiplier = isCounterAttack ? 0.5f : 1.0f,
            SkillIndex = -1,
            IsRangedAttack = false,
            CanTriggerCounter = !isCounterAttack && target.IsInFrontRow,
            IsCounterAttack = isCounterAttack
        };
        context.IsCritical = CombatCalculator.RollCritical(context);

        float dmg = 0f;
        SkillData basicAttackAnim = ResolveSkillAnimationData(null);

        if (isCounterAttack)
        {
            // skillIndex=10 → (10/10)%10=1 → ClassSkill_1 CrossFade, WaitForSkillClipEnd 정상 동작
            var counterSkillData = new SkillData
            {
                skillIndex = 10,
                HitDelay = basicAttackAnim?.HitDelay ?? 0.25f,
                TotalDelay = basicAttackAnim?.TotalDelay ?? 0.5f,
                UseAnimEvent = basicAttackAnim?.UseAnimEvent ?? false
            };

            yield return RunSkillSequenceCore(
                actor,
                target,
                counterSkillData,
                playBasicAttackAnimation: false,
                playTargetHitAnimation: true,
                () => { BattleHitResult r = ApplyDamage(context); dmg = r?.Damage ?? 0f; return r; });
        }
        else
        {
            yield return RunSkillSequenceCore(
                actor,
                target,
                basicAttackAnim,
                playBasicAttackAnimation: true,
                playTargetHitAnimation: true,
                () => { BattleHitResult r = ApplyDamage(context); dmg = r?.Damage ?? 0f; return r; });
        }

        if (context.DelayAfter > 0f)
        {
            yield return WaitForBattleSeconds(context.DelayAfter);
        }
        string actorName = actor.UnitName;
        string targetName = target.UnitName;

        Debug.Log($"{actorName}이 {targetName}에게 {dmg:F1}만큼 피해를 입혔습니다.");

        if (target.IsDead)
        {
            Debug.Log($"[Battle] 처치: {GetLabel(target)}");
        }

        OnActionExecuted?.Invoke("기본 공격");
        onCompleted?.Invoke(true);
    }

    private static string GetSkillDisplayName(SkillData skillData)
    {
        if (skillData == null)
        {
            return "스킬 사용";
        }

        if (!string.IsNullOrWhiteSpace(skillData.skillName))
        {
            return skillData.skillName;
        }

        return $"스킬 사용 (ID: {skillData.skillIndex})";
    }

    public void ExecuteAction(BattleAction action)
    {
        if (action == null)
        {
            Debug.LogWarning("[BattleManager] ExecuteAction: action이 null입니다.");
            return;
        }

        if (action.Actor == null)
        {
            Debug.LogWarning("[BattleManager] ExecuteAction: Actor가 null입니다.");
            return;
        }

        if (action.Actor.IsDead)
        {
            Debug.Log($"[BattleManager] 행동 취소: Actor가 사망 상태입니다. ({GetLabel(action.Actor)})");
            return;
        }

        switch (action.ActionType)
        {
            case BattleActionType.BasicAttack:
                StartCoroutine(ExecuteBasicAttack(action.Actor, action.Target));
                break;

            case BattleActionType.Skill:
                StartCoroutine(ExecuteSkill(action.Actor, action.Target, action.SkillData));
                break;

            default:
                Debug.LogWarning($"[BattleManager] 알 수 없는 ActionType: {action.ActionType}");
                break;
        }
    }

    private IEnumerator ExecuteSkill(BattleCharactor actor, BattleCharactor target, SkillDataAsset skillData)
    {
        if (skillData == null)
        {
            Debug.LogWarning($"[Battle] 스킬 사용 실패: SkillDataAsset이 null입니다. actor={GetLabel(actor)}");
            yield break;
        }

        float multiplier = Mathf.Max(0.01f, skillData.Power / 100f);
        var context = new DamageContext
        {
            Caster = actor,
            Target = target,
            SkillMultiplier = multiplier,
            SkillIndex = -3,
            IsRangedAttack = false,
            CanTriggerCounter = false,
            IsCounterAttack = false
        };
        context.IsCritical = CombatCalculator.RollCritical(context);

        float dealt = 0f;
        SkillData assetAnimSkill = ResolveSkillAnimationData(SkillDataFromAsset(skillData));
        yield return RunSkillSequenceCore(
            actor,
            target,
            assetAnimSkill,
            playBasicAttackAnimation: false,
            playTargetHitAnimation: true,
            () => { BattleHitResult r = ApplyDamage(context); dealt = r?.Damage ?? 0f; return r; });

        if (context.DelayAfter > 0f)
        {
            yield return WaitForBattleSeconds(context.DelayAfter);
        }

        Debug.Log($"[Battle] 스킬({skillData.DisplayName}): {GetLabel(actor)} -> {GetLabel(target)} dmg={dealt:F1}");

        if (target.IsDead)
        {
            Debug.Log($"[Battle] 처치: {GetLabel(target)}");
        }
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

    private static SkillData SkillDataFromAsset(SkillDataAsset asset)
    {
        if (asset == null)
        {
            return null;
        }

        return new SkillData
        {
            AnimationTrigger = asset.AnimationTrigger,
            StateName = asset.StateName,
            UseAnimEvent = asset.UseAnimEvent,
            HitDelay = asset.HitDelay,
            TotalDelay = asset.TotalDelay
        };
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

    private IEnumerator RunSkillSequenceCore(
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
        actor.EnsureAnimationController();
        CharactorAnimationController actorAnim = actor.Anim;
        actor.Anim?.SetAnimationSpeed(_currentBattleSpeed);

        string targetState = actorAnim != null
            ? actorAnim.GetTargetStateName(playBasicAttackAnimation ? null : skill)
            : string.Empty;

        Vector3 originPosition = actor.transform.position;
        float originRotationY = actor.transform.eulerAngles.y;
        UnitMovementProfile movement = actor.GetComponent<UnitMovementProfile>();
        bool shouldMove   = movement != null && !movement.RotateOnly && target != null && IsMeleeSkillRange(actor, skill);
        bool shouldRotate = movement != null && movement.RotateOnly  && target != null;

        string targetAnimTrigger = playTargetHitAnimation
            ? (skill?.ResolvedTargetAnimationTrigger ?? "Hit")
            : null;

        float sequenceBattleElapsed = 0f;
        var runner = new ActionSequenceRunner();

        if (shouldMove)
            runner.Enqueue(new MoveToTargetAction(actorAnim, target.transform, movement.ApproachDistance, movement.MoveDuration / _currentBattleSpeed));
        else if (shouldRotate)
            runner.Enqueue(new RotateToTargetAction(actor.transform, target.transform, movement.RotateDuration / _currentBattleSpeed));

        if (_visualDirector != null && skill != null)
            runner.Enqueue(new SpawnAttackEffectAction(actor, skill.skillIndex, _visualDirector));

        runner.Enqueue(new PlaySkillAnimAction(actorAnim, skill, playBasicAttackAnimation));
        runner.Enqueue(new WaitHitAction(actorAnim, skill, _currentBattleSpeed, elapsed => sequenceBattleElapsed += elapsed, AnimEventTimeoutSeconds));
        runner.Enqueue(new ResolveHitAction(target, onHitCallback, targetAnimTrigger, _currentBattleSpeed, _visualDirector));

        if (!string.IsNullOrEmpty(targetState))
            runner.Enqueue(new WaitClipEndAction(actorAnim, targetState, elapsed => sequenceBattleElapsed += elapsed));

        if (shouldMove)
            runner.Enqueue(new MoveToOriginAction(actorAnim, originPosition, Quaternion.Euler(0f, originRotationY, 0f), movement.ReturnDuration / _currentBattleSpeed));
        else if (shouldRotate)
            runner.Enqueue(new MoveToOriginAction(actorAnim, originPosition, Quaternion.Euler(0f, originRotationY, 0f), movement.RotateReturnDuration / _currentBattleSpeed));

        runner.Enqueue(new ReturnToIdleAction(actor));

        yield return StartCoroutine(runner.RunAll(this));

        float remainingTotal = Mathf.Max(0f, skill.TotalDelay - sequenceBattleElapsed);
        yield return WaitForBattleSeconds(Mathf.Max(remainingTotal, 0.2f));

        ReturnToIdleIfAlive(target);
    }

    /// <summary>
    /// 광역 스킬: 시전 애니 1회, HitDelay 시점에 전 타겟 동시 피격·데미지, 이후 전원 Idle 복귀.
    /// </summary>
    private IEnumerator RunAoESkillSequence(List<DamageContext> contexts, List<Func<BattleHitResult>> hitCallbacks)
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

        SkillData skill = ResolveSkillAnimationData(TryGetSkillDataForDamageContext(leadContext));
        actor.EnsureAnimationController();
        CharactorAnimationController actorAnim = actor.Anim;
        actor.Anim?.SetAnimationSpeed(_currentBattleSpeed);

        string targetState = actorAnim != null
            ? actorAnim.GetTargetStateName(skill)
            : string.Empty;

        float sequenceBattleElapsed = 0f;
        var runner = new ActionSequenceRunner();

        if (_visualDirector != null && skill != null)
            runner.Enqueue(new SpawnAttackEffectAction(actor, skill.skillIndex, _visualDirector));

        runner.Enqueue(new PlaySkillAnimAction(actorAnim, skill, false));
        runner.Enqueue(new WaitHitAction(actorAnim, skill, _currentBattleSpeed, elapsed => sequenceBattleElapsed += elapsed, AnimEventTimeoutSeconds));
        runner.Enqueue(new AoEApplyDamageAction(contexts, hitCallbacks, pairCount, _currentBattleSpeed, _visualDirector));

        if (!string.IsNullOrEmpty(targetState))
            runner.Enqueue(new WaitClipEndAction(actorAnim, targetState, elapsed => sequenceBattleElapsed += elapsed));

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

    public float CalculateBasicAttackDamage(BattleCharactor actor, BattleCharactor target)
    {
        if (actor == null || target == null)
        {
            return 1f;
        }

        float atk = actor.FinalStats.Atk;
        float def = target.FinalStats.DEF;
        float multiplier = 1f;
        float damage = (atk * multiplier) - def;
        return Mathf.Max(1.0f, damage);
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
