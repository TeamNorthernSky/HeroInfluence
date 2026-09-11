using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum RuntimeStatMask
{
    None = 0,
    HP = 1 << 0,
    Atk = 1 << 1,
    DEF = 1 << 2,
    Luck = 1 << 3,
    Speed = 1 << 4,
    CriticalRate = 1 << 5,
    CritMultiplier = 1 << 6,
    CounterRate = 1 << 7,
    AvoidRate = 1 << 8,
    Influence = 1 << 9,
    All = ~0
}

public enum RuntimeStatOperation
{
    Add,
    Multiply,
    Override
}

[Serializable]
public struct RuntimeStatModifier
{
    [SerializeField] private RuntimeStatMask mask;
    [SerializeField] private RuntimeStatOperation operation;
    [SerializeField] private StatBlock values;

    public RuntimeStatMask Mask => mask;
    public RuntimeStatOperation Operation => operation;
    public StatBlock Values => values;

    public RuntimeStatModifier(RuntimeStatMask mask, RuntimeStatOperation operation, StatBlock values)
    {
        this.mask = mask;
        this.operation = operation;
        this.values = values;
    }

    internal void Apply(ref StatBlock stats)
    {
        if ((mask & RuntimeStatMask.HP) != 0) stats.HP = ApplyValue(stats.HP, values.HP);
        if ((mask & RuntimeStatMask.Atk) != 0) stats.Atk = ApplyValue(stats.Atk, values.Atk);
        if ((mask & RuntimeStatMask.DEF) != 0) stats.DEF = ApplyValue(stats.DEF, values.DEF);
        if ((mask & RuntimeStatMask.Luck) != 0) stats.Luck = ApplyValue(stats.Luck, values.Luck);
        if ((mask & RuntimeStatMask.Speed) != 0) stats.Speed = ApplyValue(stats.Speed, values.Speed);
        if ((mask & RuntimeStatMask.CriticalRate) != 0) stats.CriticalRate = ApplyValue(stats.CriticalRate, values.CriticalRate);
        if ((mask & RuntimeStatMask.CritMultiplier) != 0) stats.CritMultiplier = ApplyValue(stats.CritMultiplier, values.CritMultiplier);
        if ((mask & RuntimeStatMask.CounterRate) != 0) stats.CounterRate = ApplyValue(stats.CounterRate, values.CounterRate);
        if ((mask & RuntimeStatMask.AvoidRate) != 0) stats.AvoidRate = ApplyValue(stats.AvoidRate, values.AvoidRate);
        if ((mask & RuntimeStatMask.Influence) != 0) stats.Influence = ApplyValue(stats.Influence, values.Influence);
    }

    private float ApplyValue(float current, float value)
    {
        switch (operation)
        {
            case RuntimeStatOperation.Multiply:
                return current * value;
            case RuntimeStatOperation.Override:
                return value;
            default:
                return current + value;
        }
    }
}

public partial class BattleCharactor
{
    private readonly List<MinimumHpConstraintEntry> minimumHpConstraints =
        new List<MinimumHpConstraintEntry>();
    private readonly List<RuntimeStatModifierEntry> runtimeStatModifiers =
        new List<RuntimeStatModifierEntry>();
    private int nextRuntimeConstraintId = 1;

    public float MinimumHpAfterDamage => ResolveMinimumHpAfterDamage();
    public int MinimumHpConstraintCount => minimumHpConstraints.Count;
    public int RuntimeStatModifierCount => runtimeStatModifiers.Count;

    public IDisposable AddMinimumHpConstraint(float minimumHp, object owner = null)
    {
        int id = nextRuntimeConstraintId++;
        float clampedMinimum = Mathf.Clamp(minimumHp, 0f, MaxHp);
        minimumHpConstraints.Add(new MinimumHpConstraintEntry(id, clampedMinimum, owner));

        float resolvedMinimum = ResolveMinimumHpAfterDamage();
        if (!IsDead && currentHp > 0f && currentHp < resolvedMinimum)
        {
            currentHp = resolvedMinimum;
            OnHpChanged?.Invoke(CurrentHp, MaxHp);
        }

        return new RuntimeConstraintHandle(() => RemoveMinimumHpConstraint(id));
    }

    public IDisposable AddRuntimeStatModifier(RuntimeStatModifier modifier, object owner = null)
    {
        int id = nextRuntimeConstraintId++;
        runtimeStatModifiers.Add(new RuntimeStatModifierEntry(id, modifier, owner));
        RecalculateStats(applyCurrentHpClamp: true);
        return new RuntimeConstraintHandle(() => RemoveRuntimeStatModifier(id));
    }

    private float ResolveMinimumHpAfterDamage()
    {
        float minimum = 0f;
        for (int i = 0; i < minimumHpConstraints.Count; i++)
        {
            minimum = Mathf.Max(minimum, minimumHpConstraints[i].MinimumHp);
        }

        return Mathf.Clamp(minimum, 0f, MaxHp);
    }

    private void ApplyRuntimeStatModifiers()
    {
        for (int i = 0; i < runtimeStatModifiers.Count; i++)
        {
            RuntimeStatModifierEntry entry = runtimeStatModifiers[i];
            entry.Modifier.Apply(ref finalStats);
        }

        finalStats.HP = Mathf.Max(1f, finalStats.HP);
        finalStats.Atk = Mathf.Max(0f, finalStats.Atk);
        finalStats.DEF = Mathf.Max(0f, finalStats.DEF);
        finalStats.Luck = Mathf.Max(0f, finalStats.Luck);
        finalStats.Speed = Mathf.Max(0f, finalStats.Speed);
        finalStats.CriticalRate = Mathf.Max(0f, finalStats.CriticalRate);
        finalStats.CritMultiplier = Mathf.Max(0f, finalStats.CritMultiplier);
        finalStats.CounterRate = Mathf.Max(0f, finalStats.CounterRate);
        finalStats.AvoidRate = Mathf.Max(0f, finalStats.AvoidRate);
        finalStats.Influence = Mathf.Clamp(finalStats.Influence, 0f, 200f);
    }

    private void RemoveMinimumHpConstraint(int id)
    {
        minimumHpConstraints.RemoveAll(entry => entry.Id == id);
    }

    private void RemoveRuntimeStatModifier(int id)
    {
        int removed = runtimeStatModifiers.RemoveAll(entry => entry.Id == id);
        if (removed > 0 && this != null)
        {
            RecalculateStats(applyCurrentHpClamp: true);
        }
    }

    private void ClearRuntimeConstraints()
    {
        minimumHpConstraints.Clear();
        runtimeStatModifiers.Clear();
    }

    private sealed class MinimumHpConstraintEntry
    {
        public readonly int Id;
        public readonly float MinimumHp;
        public readonly object Owner;

        public MinimumHpConstraintEntry(int id, float minimumHp, object owner)
        {
            Id = id;
            MinimumHp = minimumHp;
            Owner = owner;
        }
    }

    private sealed class RuntimeStatModifierEntry
    {
        public readonly int Id;
        public readonly RuntimeStatModifier Modifier;
        public readonly object Owner;

        public RuntimeStatModifierEntry(int id, RuntimeStatModifier modifier, object owner)
        {
            Id = id;
            Modifier = modifier;
            Owner = owner;
        }
    }

    private sealed class RuntimeConstraintHandle : IDisposable
    {
        private Action release;

        public RuntimeConstraintHandle(Action releaseAction)
        {
            release = releaseAction;
        }

        public void Dispose()
        {
            Action action = release;
            release = null;
            action?.Invoke();
        }
    }
}
