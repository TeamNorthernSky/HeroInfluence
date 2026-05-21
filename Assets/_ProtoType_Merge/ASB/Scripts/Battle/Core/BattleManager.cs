using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using ASB.Work.Battle.SkillExecution;
using ASB.Work.Battle.Core;

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BattleManager] 중복 인스턴스가 감지되었습니다.");
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private float ApplyDamage(DamageContext context)
    {
        if (context.Caster == null || context.Target == null)
        {
            return 0f;
        }

        // 다단 히트 진행 중 사망 타겟에 대한 후속 타격은 무시합니다.
        if (context.Target.IsDead)
        {
            return 0f;
        }

        float finalDamage = CombatCalculator.CalculateDamage(context);
        context.Target.TakeDamage(finalDamage);
        Debug.Log($"[Combat] {context.Caster.UnitName} -> {context.Target.UnitName} dmg={finalDamage:F1} (Crit: {context.IsCritical})");
        return finalDamage;
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
                    () => { totalDamageDealt += ApplyDamage(damageContext); });

                float delay = Mathf.Max(0f, damageContext.DelayAfter);
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
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
                () => target.ApplyHeal(heal));
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
                () => ApplyBuff(actor, target, skillData));
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
            () => { dealt = ApplyDamage(context); });

        if (context.DelayAfter > 0f)
        {
            yield return new WaitForSeconds(context.DelayAfter);
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
            yield return new WaitForSeconds(context.DelayAfter);
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
                () => { dmg = ApplyDamage(context); });
        }
        else
        {
            yield return RunSkillSequenceCore(
                actor,
                target,
                basicAttackAnim,
                playBasicAttackAnimation: true,
                playTargetHitAnimation: true,
                () => { dmg = ApplyDamage(context); });
        }

        if (context.DelayAfter > 0f)
        {
            yield return new WaitForSeconds(context.DelayAfter);
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
            () => { dealt = ApplyDamage(context); });

        if (context.DelayAfter > 0f)
        {
            yield return new WaitForSeconds(context.DelayAfter);
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

        SkillDataLoader loader = UnityEngine.Object.FindFirstObjectByType<SkillDataLoader>(FindObjectsInactive.Include);
        if (loader != null && loader.TryGetSkill(damageContext.SkillIndex, out SkillData loaded) && loaded != null)
        {
            return loaded;
        }

        return null;
    }

    private IEnumerator RunSkillSequenceCore(
        BattleCharactor actor,
        BattleCharactor target,
        SkillData skill,
        bool playBasicAttackAnimation,
        bool playTargetHitAnimation,
        Action onHitCallback)
    {
        if (actor == null)
        {
            onHitCallback?.Invoke();
            yield break;
        }

        skill = ResolveSkillAnimationData(skill);
        actor.EnsureAnimationController();
        CharactorAnimationController actorAnim = actor.Anim;

        float startTime = Time.time;
        string targetState = actorAnim != null
            ? actorAnim.GetTargetStateName(playBasicAttackAnimation ? null : skill)
            : string.Empty;

        actorAnim?.ResetHitEvent();
        actorAnim?.PlaySkillAnimation(playBasicAttackAnimation ? null : skill);

        float elapsedSoFar = Time.time - startTime;
        if (skill.UseAnimEvent)
        {
            yield return new WaitUntil(() =>
                actorAnim != null && actorAnim.IsHitEventReached
                || Time.time - startTime > AnimEventTimeoutSeconds);
        }
        else
        {
            float remainingHitDelay = Mathf.Max(0f, skill.HitDelay - elapsedSoFar);
            yield return new WaitForSeconds(remainingHitDelay);
        }

        onHitCallback?.Invoke();

        if (playTargetHitAnimation && target != null)
        {
            target.EnsureAnimationController();
            target.Anim?.PlayGenericAnimation("Hit");
        }

        if (actorAnim != null && !string.IsNullOrEmpty(targetState))
        {
            yield return StartCoroutine(actorAnim.WaitForSkillClipEnd(targetState));
        }

        ReturnToIdleIfAlive(actor);

        float currentElapsed = Time.time - startTime;
        float remainingTotal = Mathf.Max(0f, skill.TotalDelay - currentElapsed);
        float targetIdleDelay = Mathf.Max(remainingTotal, 0.2f);
        yield return new WaitForSeconds(targetIdleDelay);

        ReturnToIdleIfAlive(target);
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
