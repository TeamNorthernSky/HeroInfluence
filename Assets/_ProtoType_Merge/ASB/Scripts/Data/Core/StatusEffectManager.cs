using System.Collections.Generic;
using UnityEngine;

public sealed class StatusEffectManager
{
    private readonly BattleCharactor owner;
    private readonly List<StatusEffectInstance> activeEffects;

    public StatusEffectManager(BattleCharactor owner, List<StatusEffectInstance> activeEffects)
    {
        this.owner = owner;
        this.activeEffects = activeEffects ?? new List<StatusEffectInstance>();
    }

    public IReadOnlyList<StatusEffectInstance> ActiveEffects => activeEffects;

    public bool IsStunned
    {
        get
        {
            if (owner == null || owner.IsDead)
            {
                return false;
            }

            for (int i = 0; i < activeEffects.Count; i++)
            {
                StatusEffectInstance effect = activeEffects[i];
                if (effect != null
                    && effect.effectType == StatusEffectType.stun
                    && effect.remainingTurns > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool IsHealBanned
    {
        get
        {
            if (owner == null || owner.IsDead)
            {
                return false;
            }

            for (int i = 0; i < activeEffects.Count; i++)
            {
                StatusEffectInstance effect = activeEffects[i];
                if (effect != null
                    && effect.effectType == StatusEffectType.healBan
                    && effect.remainingTurns > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public float DamageTakenMultiplier
    {
        get
        {
            var shield = activeEffects.Find(e => e != null && e.effectType == StatusEffectType.damage_taken_down && e.remainingTurns > 0);
            return shield != null ? 1f - Mathf.Clamp01(shield.value) : 1f;
        }
    }

    public bool ApplyStatusEffect(StatusEffectInstance effect)
    {
        if (owner == null || effect == null || effect.effectType == StatusEffectType.none)
        {
            return false;
        }

        StatusEffectInstance existing = activeEffects.Find(x => x != null && x.effectType == effect.effectType);
        if (existing != null)
        {
            existing.category = effect.category;
            existing.value = effect.value;
            existing.remainingTurns = Mathf.Max(1, effect.remainingTurns);
            existing.source = effect.source;
            Debug.Log($"[Status] 갱신: {owner.UnitName} effect={effect.effectType} turns={existing.remainingTurns}");
        }
        else
        {
            activeEffects.Add(new StatusEffectInstance
            {
                effectType = effect.effectType,
                category = effect.category,
                value = effect.value,
                remainingTurns = Mathf.Max(1, effect.remainingTurns),
                source = effect.source
            });
            Debug.Log($"[Status] 적용: {owner.UnitName} effect={effect.effectType} turns={Mathf.Max(1, effect.remainingTurns)}");
        }

        return RequiresStatRecalculation(effect.effectType);
    }

    public bool RemoveStatusEffect(StatusEffectType effectType)
    {
        if (activeEffects.Count == 0)
        {
            return false;
        }

        bool removed = activeEffects.RemoveAll(x => x != null && x.effectType == effectType) > 0;
        return removed && RequiresStatRecalculation(effectType);
    }

    public bool HasStatusEffect(StatusEffectType effectType)
    {
        if (activeEffects.Count == 0)
        {
            return false;
        }

        return activeEffects.Exists(x => x != null && x.effectType == effectType);
    }

    public BattleCharactor GetTauntSource()
    {
        if (activeEffects.Count == 0)
        {
            return null;
        }

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffectInstance effect = activeEffects[i];
            if (effect == null || effect.effectType != StatusEffectType.taunt)
            {
                continue;
            }

            if (effect.remainingTurns > 0 && effect.source != null && !effect.source.IsDead)
            {
                return effect.source;
            }

            activeEffects.RemoveAt(i);
        }

        return null;
    }

    public void ProcessTurnStartStatusEffects()
    {
        // 방어 코어는 시전한 턴 종료에 사라지지 않고 다음 자기 순서가 시작될 때 만료됩니다.
        RemoveStatusEffect(StatusEffectType.damage_taken_down);
        if (owner == null || owner.IsDead || activeEffects.Count == 0)
        {
            return;
        }

        for (int i = 0; i < activeEffects.Count; i++)
        {
            StatusEffectInstance effect = activeEffects[i];
            if (effect == null || effect.remainingTurns <= 0)
            {
                continue;
            }

            if (effect.effectType != StatusEffectType.poison && effect.effectType != StatusEffectType.bleed)
            {
                continue;
            }

            float dotDamage = Mathf.Max(0f, effect.value);
            if (dotDamage <= 0f)
            {
                continue;
            }

            float before = owner.CurrentHp;
            owner.TakeDamage(dotDamage);
            float applied = Mathf.Max(0f, before - owner.CurrentHp);
            Debug.Log($"[Status] DOT tick: {owner.UnitName} effect={effect.effectType} dmg={applied:F1} turnsLeft={effect.remainingTurns}");

            if (owner.IsDead)
            {
                return;
            }
        }
    }

    public bool AdvanceStatusEffectDuration()
    {
        if (activeEffects.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < activeEffects.Count; i++)
        {
            StatusEffectInstance effect = activeEffects[i];
            if (effect == null)
            {
                continue;
            }

            if (effect.effectType != StatusEffectType.damage_taken_down) effect.remainingTurns -= 1;
        }

        bool removedStatModifier = false;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffectInstance effect = activeEffects[i];
            if (effect == null || effect.remainingTurns > 0)
            {
                continue;
            }

            if (RequiresStatRecalculation(effect.effectType))
            {
                removedStatModifier = true;
            }

            activeEffects.RemoveAt(i);
        }

        return removedStatModifier;
    }

    public void ApplyStatusEffectStatModifiers(ref StatBlock finalStats)
    {
        if (activeEffects.Count == 0)
        {
            return;
        }

        float atkMultiplier = 1f;
        float defMultiplier = 1f;

        for (int i = 0; i < activeEffects.Count; i++)
        {
            StatusEffectInstance effect = activeEffects[i];
            if (effect == null || effect.remainingTurns <= 0)
            {
                continue;
            }

            float amount = Mathf.Max(0f, effect.value);
            switch (effect.effectType)
            {
                case StatusEffectType.attack_up:
                    atkMultiplier += amount;
                    break;
                case StatusEffectType.attack_down:
                    atkMultiplier -= amount;
                    break;
                case StatusEffectType.defense_up:
                    defMultiplier += amount;
                    break;
                case StatusEffectType.defense_down:
                    defMultiplier -= amount;
                    break;
            }
        }

        atkMultiplier = Mathf.Max(0.1f, atkMultiplier);
        defMultiplier = Mathf.Max(0.1f, defMultiplier);

        finalStats.Atk = Mathf.Max(1f, finalStats.Atk * atkMultiplier);
        finalStats.DEF = Mathf.Max(0f, finalStats.DEF * defMultiplier);
    }

    public static bool RequiresStatRecalculation(StatusEffectType effectType)
    {
        if (effectType == StatusEffectType.healBan)
        {
            return false;
        }

        return effectType == StatusEffectType.attack_up
               || effectType == StatusEffectType.attack_down
               || effectType == StatusEffectType.defense_up
               || effectType == StatusEffectType.defense_down;
    }
}
