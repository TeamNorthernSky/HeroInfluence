using System;
using System.Collections;
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
        Debug.Log($"[Combat] {context.Caster.UnitName} -> {context.Target.UnitName} dmg={finalDamage:F1}");
        return finalDamage;
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
            for (int i = 0; i < result.DamageContexts.Count; i++)
            {
                DamageContext damageContext = result.DamageContexts[i];
                totalDamageDealt += ApplyDamage(damageContext);

                float delay = damageContext != null ? Mathf.Max(0f, damageContext.DelayAfter) : 0f;
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }
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
            target.ApplyHeal(heal);
            Debug.Log($"[Battle] GridHeal: {GetLabel(actor)} -> {GetLabel(target)} heal={heal:F1} (×{multiplier:0.##})");
            onCompleted?.Invoke(true);
            yield break;
        }

        var context = new DamageContext
        {
            Caster = actor,
            Target = target,
            SkillMultiplier = multiplier,
            SkillIndex = skillData.skillIndex,
            IsCritical = false
        };
        float dealt = ApplyDamage(context);
        if (context.DelayAfter > 0f)
        {
            yield return new WaitForSeconds(context.DelayAfter);
        }
        Debug.Log($"[Battle] GridSkill: {GetLabel(actor)} -> {GetLabel(target)} dmg={dealt:F1} (×{multiplier:0.##})");
        onCompleted?.Invoke(true);
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
            IsCritical = false
        };
        float dealt = ApplyDamage(context);
        if (context.DelayAfter > 0f)
        {
            yield return new WaitForSeconds(context.DelayAfter);
        }
        Debug.Log($"[Battle] GridSkill: {GetLabel(actor)} -> {GetLabel(target)} dmg={dealt:F1} ({skillPercent:0.##}%)");
        onCompleted?.Invoke(true);
    }

    /// <summary>일반 공격: 피해 = max(1, 공격력×배율 - 방어력) 후 HP 감소.</summary>
    public IEnumerator ExecuteBasicAttack(BattleCharactor actor, BattleCharactor target, Action<bool> onCompleted = null)
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
            SkillMultiplier = 1.0f,
            SkillIndex = -1,
            IsCritical = false
        };
        float dmg = ApplyDamage(context);
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
            IsCritical = false
        };
        float dealt = ApplyDamage(context);
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
