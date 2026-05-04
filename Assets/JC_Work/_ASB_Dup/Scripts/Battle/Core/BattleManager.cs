using UnityEngine;
using ASB.Work.Battle.SkillExecution;

/// <summary>
/// BattleAction 및 플레이어 입력에 의한 전투 실행. 데미지는 항상 target.TakeDamage로 적용합니다.
/// </summary>
[DisallowMultipleComponent]
public class BattleManager : MonoBehaviour
{
    private const int ClassSkillEffect_Heal = 1;
    private const int ClassSkillEffect_Revive = 2;

    /// <summary>ClassSkillSheet 행의 skillValue(예: 1.2 = 120%)로 그리드 스킬 피해를 계산합니다.</summary>
    public bool ExecuteGridSkill(BattleCharactor actor, BattleCharactor target, SkillData classSkillRow)
    {
        if (classSkillRow == null || actor == null || target == null)
        {
            return false;
        }

        if (actor.IsDead)
        {
            return false;
        }

        bool reviveSkill = classSkillRow.classSkillEffect == ClassSkillEffect_Revive;
        if (target.IsDead != reviveSkill)
        {
            return false;
        }

        if (SkillExecutionRegistry.TryGetHandler(classSkillRow.skillIndex, out ISkillEffectHandler custom))
        {
            return custom.Execute(actor, target, classSkillRow, null);
        }

        return ExecuteDefaultSkill(actor, target, classSkillRow);
    }

    /// <summary>레지스트리에 없는 일반 스킬: 데이터만으로 힐/딜 처리.</summary>
    private bool ExecuteDefaultSkill(BattleCharactor actor, BattleCharactor target, SkillData skillData)
    {
        if (skillData == null || actor == null || target == null)
        {
            return false;
        }

        if (actor.IsDead || target.IsDead)
        {
            return false;
        }

        float multiplier = Mathf.Max(0.01f, skillData.skillValue);

        if (skillData.classSkillEffect == ClassSkillEffect_Heal)
        {
            float heal = Mathf.Max(0f, actor.FinalStats.Atk * multiplier);
            target.ApplyHeal(heal);
            Debug.Log($"[Battle] GridHeal: {GetLabel(actor)} -> {GetLabel(target)} heal={heal:F1} (×{multiplier:0.##})");
            return true;
        }

        float raw = (actor.FinalStats.Atk * multiplier) - target.FinalStats.DEF;
        float dmg = Mathf.Max(1f, raw);
        target.TakeDamage(dmg);
        Debug.Log($"[Battle] GridSkill: {GetLabel(actor)} -> {GetLabel(target)} dmg={dmg:F1} (×{multiplier:0.##})");
        return true;
    }

    public void ExecuteGridSkill(BattleCharactor actor, BattleCharactor target, float skillPercent)
    {
        ExecuteGridSkill(actor, target, skillPercent, isHeal: false);
    }

    private void ExecuteGridSkill(BattleCharactor actor, BattleCharactor target, float skillPercent, bool isHeal)
    {
        if (actor == null || target == null)
        {
            return;
        }

        if (actor.IsDead || target.IsDead)
        {
            return;
        }

        float multiplier = Mathf.Max(0.01f, skillPercent / 100f);
        if (isHeal)
        {
            float heal = Mathf.Max(0f, actor.FinalStats.Atk * multiplier);
            target.ApplyHeal(heal);
            Debug.Log($"[Battle] GridHeal: {GetLabel(actor)} -> {GetLabel(target)} heal={heal:F1} ({skillPercent:0.##}%)");
            return;
        }

        float dmg = CalculateGridSkillDamage(actor, target, skillPercent);
        target.TakeDamage(dmg);

        Debug.Log($"[Battle] GridSkill: {GetLabel(actor)} -> {GetLabel(target)} dmg={dmg:F1} ({skillPercent:0.##}%)");
    }

    /// <summary>일반 공격: 피해 = max(1, 공격력×배율 - 방어력) 후 HP 감소.</summary>
    public bool ExecuteBasicAttack(BattleCharactor actor, BattleCharactor target)
    {
        if (actor == null || target == null)
        {
            return false;
        }

        if (actor.IsDead || target.IsDead)
        {
            return false;
        }

        float dmg = CalculateBasicAttackDamage(actor, target);
        string actorName = actor.UnitName;
        string targetName = target.UnitName;

        Debug.Log($"{actorName}이 {targetName}에게 {dmg:F1}만큼 피해를 입혔습니다.");
        target.TakeDamage(dmg);

        if (target.IsDead)
        {
            Debug.Log($"[Battle] 처치: {GetLabel(target)}");
        }

        return true;
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
                ExecuteBasicAttack(action.Actor, action.Target);
                break;

            case BattleActionType.Skill:
                ExecuteSkill(action.Actor, action.Target, action.SkillData);
                break;

            default:
                Debug.LogWarning($"[BattleManager] 알 수 없는 ActionType: {action.ActionType}");
                break;
        }
    }

    private void ExecuteSkill(BattleCharactor actor, BattleCharactor target, SkillDataAsset skillData)
    {
        if (skillData == null)
        {
            Debug.LogWarning($"[Battle] 스킬 사용 실패: SkillDataAsset이 null입니다. actor={GetLabel(actor)}");
            return;
        }

        float dmg = CalculateSkillDamage(actor, target, skillData);

        Debug.Log($"[Battle] 스킬({skillData.DisplayName}): {GetLabel(actor)} -> {GetLabel(target)} dmg={dmg:F1}");
        target.TakeDamage(dmg);

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
