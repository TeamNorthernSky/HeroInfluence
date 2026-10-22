using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class DamageReductionRulesTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
            if (go != null) Object.DestroyImmediate(go);
        created.Clear();
    }

    [Test]
    public void ReductionRoll_UsesReduceRateAsChance()
    {
        Component caster = Unit("Caster", 110f, 0f, 0f);
        Component never = Unit("Never", 0f, 10f, 0f);
        Component always = Unit("Always", 0f, 10f, 1f);

        for (int i = 0; i < 200; i++)
        {
            Assert.That(RollReduction(Hit(caster, never)), Is.False);
            Assert.That(RollReduction(Hit(caster, always)), Is.True);
        }

        // 확률 감소가 경감 확률을 전부 지우면 발동하지 않는다.
        object pierced = Hit(caster, always);
        pierced.GetType().GetField("TargetAvoidRateReduction").SetValue(pierced, 1f);
        Assert.That(RollReduction(pierced), Is.False);
    }

    [Test]
    public void ReducedHit_TakesHalfDamage_AndReduceRateNoLongerScalesDamage()
    {
        Component caster = Unit("Caster", 110f, 0f, 0f);
        Component target = Unit("Target", 0f, 10f, 0.3f); // Atk - DEF = 100

        Assert.That(Damage(Hit(caster, target, null), out bool notRolled), Is.EqualTo(100f));
        Assert.That(notRolled, Is.False);
        Assert.That(Damage(Hit(caster, target, false), out bool notReduced), Is.EqualTo(100f));
        Assert.That(notReduced, Is.False);
        Assert.That(Damage(Hit(caster, target, true), out bool reduced), Is.EqualTo(50f));
        Assert.That(reduced, Is.True);
        Assert.That(Damage(Hit(caster, target, true, true), out _), Is.EqualTo(75f));
    }

    private Component Unit(string name, float atk, float def, float reduceRate)
    {
        GameObject unitObject = new GameObject(name);
        created.Add(unitObject);
        Type unitType = FindType("BattleCharactor");
        Component unit = unitObject.AddComponent(unitType);

        Type statType = FindType("StatBlock");
        object stats = Activator.CreateInstance(statType);
        statType.GetField("HP").SetValue(stats, 1000f);
        statType.GetField("Atk").SetValue(stats, atk);
        statType.GetField("DEF").SetValue(stats, def);
        statType.GetField("AvoidRate").SetValue(stats, reduceRate);
        unitType.GetField("finalStats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(unit, stats);
        return unit;
    }

    private static object Hit(Component caster, Component target, bool? reduced = null, bool critical = false)
    {
        Type type = FindType("ASB.Work.Battle.Core.DamageContext");
        object context = Activator.CreateInstance(type);
        type.GetField("Caster").SetValue(context, caster);
        type.GetField("Target").SetValue(context, target);
        type.GetField("SkillMultiplier").SetValue(context, 1f);
        type.GetField("IsCritical").SetValue(context, critical);
        type.GetField("IsDamageReduced").SetValue(context, reduced);
        return context;
    }

    private static bool RollReduction(object context)
    {
        MethodInfo roll = FindType("ASB.Work.Battle.Core.CombatCalculator").GetMethod("RollDamageReduction");
        return (bool)roll.Invoke(null, new[] { context });
    }

    private static float Damage(object context, out bool damageReduced)
    {
        MethodInfo calculate = FindType("ASB.Work.Battle.Core.CombatCalculator").GetMethod(
            "CalculateDamage", new[] { context.GetType(), typeof(bool).MakeByRefType() });
        object[] args = { context, false };
        float damage = (float)calculate.Invoke(null, args);
        damageReduced = (bool)args[1];
        return damage;
    }

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(found => found != null);
        Assert.That(type, Is.Not.Null, name);
        return type;
    }
}
