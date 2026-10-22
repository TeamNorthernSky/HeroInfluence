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
        Component caster = Unit("Caster", 110f, 0f, 0f, false, 2, 0);
        Component never = Unit("Never", 0f, 10f, 0f, true, 0, 0);
        Component always = Unit("Always", 0f, 10f, 1f, true, 0, 1);

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
        Component caster = Unit("Caster", 110f, 0f, 0f, false, 2, 0);
        Unit("FormationFront", 0f, 10f, 0f, true, 1, 0);
        Component target = Unit("Target", 0f, 10f, 0.3f, true, 0, 0); // Atk - DEF = 100

        Assert.That(Damage(Hit(caster, target, null), out bool notRolled), Is.EqualTo(100f));
        Assert.That(notRolled, Is.False);
        Assert.That(Damage(Hit(caster, target, false), out bool notReduced), Is.EqualTo(100f));
        Assert.That(notReduced, Is.False);
        Assert.That(Damage(Hit(caster, target, true), out bool reduced), Is.EqualTo(50f));
        Assert.That(reduced, Is.True);
        Assert.That(Damage(Hit(caster, target, true, true), out _), Is.EqualTo(50f));
    }

    [TestCase("ASB.Work.Battle.Core.CombatCalculator")]
    [TestCase("ASB.Work.Battle.Core.JcCombatCalculator")]
    public void ReducedBackHit_NeverCrits_AndResolutionIsStable(string calculator)
    {
        Component caster = Unit("Caster", 110f, 0f, 0f, false, 2, 0);
        Unit("FormationFront", 0f, 10f, 0f, true, 1, 0);
        Component back = Unit("Back", 0f, 10f, 1f, true, 0, 0);
        object hit = Hit(caster, back);
        hit.GetType().GetField("IsCritical").SetValue(hit, true);
        hit.GetType().GetField("IsCriticalResolved").SetValue(hit, true);

        Resolve(hit, calculator);
        Assert.That(hit.GetType().GetField("IsDamageReduced").GetValue(hit), Is.EqualTo(true));
        Assert.That(hit.GetType().GetField("IsCritical").GetValue(hit), Is.EqualTo(false));
        Assert.That(hit.GetType().GetField("IsCriticalResolved").GetValue(hit), Is.EqualTo(true));
        Assert.That(CalculatedDamage(hit, calculator), Is.EqualTo(50f));

        Resolve(hit, calculator);
        Assert.That(hit.GetType().GetField("IsCritical").GetValue(hit), Is.EqualTo(false));
    }

    [TestCase("ASB.Work.Battle.Core.CombatCalculator")]
    [TestCase("ASB.Work.Battle.Core.JcCombatCalculator")]
    public void SharedCrit_SkipsReducedTarget_AndUsesOneResultForOtherTargets(string calculator)
    {
        Component caster = Unit("Caster", 110f, 0f, 0f, false, 2, 0);
        Component front = Unit("Front", 0f, 10f, 0f, true, 1, 0);
        Component reducedBack = Unit("ReducedBack", 0f, 10f, 1f, true, 0, 0);
        Component clearBack = Unit("ClearBack", 0f, 10f, 0f, true, 0, 1);
        Type groupType = FindType("ASB.Work.Battle.Core.SharedCriticalRoll");
        object group = Activator.CreateInstance(groupType);

        object reduced = Hit(caster, reducedBack);
        object clear = Hit(caster, clearBack);
        object redirected = Hit(caster, reducedBack);
        foreach (object hit in new[] { reduced, clear, redirected })
            hit.GetType().GetField("SharedCriticalRoll").SetValue(hit, group);
        // 대신 맞기로 최종 대상이 전열이 된 경우는 그 전열의 규칙을 따른다.
        redirected.GetType().GetField("Target").SetValue(redirected, front);

        Resolve(reduced, calculator);
        Assert.That(groupType.GetField("Result").GetValue(group), Is.Null);
        groupType.GetField("Result").SetValue(group, true);
        Resolve(clear, calculator);
        Resolve(redirected, calculator);
        Assert.That(reduced.GetType().GetField("IsCritical").GetValue(reduced), Is.EqualTo(false));
        Assert.That(reduced.GetType().GetField("IsDamageReduced").GetValue(reduced), Is.EqualTo(true));
        Assert.That(clear.GetType().GetField("IsCritical").GetValue(clear), Is.EqualTo(true));
        Assert.That(redirected.GetType().GetField("IsCritical").GetValue(redirected), Is.EqualTo(true));
        Assert.That(redirected.GetType().GetField("IsDamageReduced").GetValue(redirected), Is.EqualTo(false));
        Assert.That(groupType.GetField("Result").GetValue(group), Is.EqualTo(true));
    }

    [Test]
    public void FrontCannotReduceDamage_EvenWithFullRateOrPresetReduction()
    {
        Component caster = Unit("Caster", 110f, 0f, 0f, false, 2, 0);
        Component front = Unit("Front", 0f, 10f, 1f, true, 1, 0);
        Component back = Unit("Back", 0f, 10f, 1f, true, 0, 0);

        Assert.That(RollReduction(Hit(caster, front)), Is.False);
        Assert.That(RollReduction(Hit(caster, back)), Is.True);
        Assert.That(Damage(Hit(caster, front, true), out bool reduced), Is.EqualTo(90f));
        Assert.That(reduced, Is.False);

        const string jcCalculator = "ASB.Work.Battle.Core.JcCombatCalculator";
        Assert.That(RollReduction(Hit(caster, front), jcCalculator), Is.False);
        Assert.That(RollReduction(Hit(caster, back), jcCalculator), Is.True);
        object jcFrontHit = Hit(caster, front, true);
        float jcFrontDamage = (float)FindType(jcCalculator).GetMethod("CalculateDamage")
            .Invoke(null, new[] { jcFrontHit });
        Assert.That(jcFrontDamage, Is.EqualTo(90f));
    }

    private Component Unit(string name, float atk, float def, float reduceRate, bool isPlayer, int x, int y)
    {
        GameObject cellObject = new GameObject($"Grid_{x}_{y}");
        created.Add(cellObject);
        Component cell = cellObject.AddComponent(FindType("ASB.Work.BattleGrid.GridCell"));
        cell.GetType().GetMethod("SetCoords").Invoke(cell, new object[] { new Vector2Int(x, y) });

        GameObject unitObject = new GameObject(name);
        created.Add(unitObject);
        Type unitType = FindType("BattleCharactor");
        Component unit = unitObject.AddComponent(unitType);
        unitType.GetProperty("IsPlayer").SetValue(unit, isPlayer);
        cell.GetType().GetMethod("SetOccupyingUnit").Invoke(cell, new object[] { unit });
        unitType.GetField("occupiedCell", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(unit, cell);

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
        type.GetField("IsRangedAttack").SetValue(context, true);
        type.GetField("IsCritical").SetValue(context, critical);
        type.GetField("IsDamageReduced").SetValue(context, reduced);
        return context;
    }

    private static bool RollReduction(object context, string calculator = "ASB.Work.Battle.Core.CombatCalculator")
    {
        MethodInfo roll = FindType(calculator).GetMethod("RollDamageReduction");
        return (bool)roll.Invoke(null, new[] { context });
    }

    private static void Resolve(object context, string calculator)
    {
        FindType(calculator).GetMethod("ResolveHitRolls").Invoke(null, new[] { context });
    }

    private static float CalculatedDamage(object context, string calculator)
    {
        return (float)FindType(calculator).GetMethod("CalculateDamage", new[] { context.GetType() })
            .Invoke(null, new[] { context });
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
