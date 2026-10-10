using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CounterattackRulesTests
{
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
            if (go != null) Object.DestroyImmediate(go);
        created.Clear();
    }

    [TestCase("FV40002_1", 400021)]
    [TestCase("FV40002_2", 400022)]
    [TestCase("FV40003_1", 400031)]
    [TestCase("FV40003_2", 400032)]
    public void AmplifierSelfAction_CannotBeCounterSkill(string key, int index)
    {
        Component defender = Unit("Amplifier", false, 2, 0);
        foreach (float value in new[] { 0f, 1f })
        {
            object skill = Skill(key, index, 0, value);
            Assert.That(CanDefenderCounter(defender, skill), Is.False, $"{key}, value={value}");
        }
    }

    [Test]
    public void CounterSkill_RequiresFrontRowAndActualDamage_ButNotMeleeRange()
    {
        Component front = Unit("Front", false, 2, 0);
        Component back = Unit("Back", false, 3, 0);
        Component allyFront = Unit("AllyFront", true, 1, 0);
        Component allyBack = Unit("AllyBack", true, 0, 0);
        object melee = Skill("FV20003_1", 200031, 0, 0.8f);
        object ranged = Skill("FV20002_1", 200021, 1, 1f);
        object heal = Skill("HS4010", 4010, 1, 1f, 1);

        Assert.That(CanDefenderCounter(front, melee), Is.True);
        Assert.That(CanDefenderCounter(front, ranged), Is.True);
        Assert.That(CanDefenderCounter(back, melee), Is.False);
        Assert.That(CanDefenderCounter(allyFront, melee), Is.True);
        Assert.That(CanDefenderCounter(allyBack, melee), Is.False);
        Assert.That(CanDefenderCounter(front, heal), Is.False);
        Assert.That(CounterRate(front) - CounterRate(back), Is.EqualTo(0.3f).Within(0.001f));
        Assert.That(CounterRate(allyFront) - CounterRate(allyBack), Is.EqualTo(0.3f).Within(0.001f));
    }

    [Test]
    public void EmptyFixedFrontRow_DoesNotPromoteBackRowIntoCounterPosition()
    {
        Component enemyBack = Unit("EnemyBackOnly", false, 3, 0);
        Component allyBack = Unit("AllyBackOnly", true, 0, 0);
        object attack = Skill("FV20002_1", 200021, 1, 1f);

        Assert.That(CanDefenderCounter(enemyBack, attack), Is.False);
        Assert.That(CanDefenderCounter(allyBack, attack), Is.False);

        foreach (string managerName in new[] { "BattleManager", "JcBattleManager" })
        {
            MethodInfo canTrigger = FindType(managerName).GetMethod(
                "CanTriggerCounterattack", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(canTrigger, Is.Not.Null);
            Assert.That(canTrigger.Invoke(null, new[] { Hit(allyBack, enemyBack, -1) }), Is.False);
            Assert.That(canTrigger.Invoke(null, new[] { Hit(enemyBack, allyBack, -1) }), Is.False);
        }
    }

    [TestCase("BattleManager")]
    [TestCase("JcBattleManager")]
    public void OnlyMeleeHitsOnFixedFrontRow_CanTriggerCounter(string managerName)
    {
        Component heroFront = Unit("HeroFront", true, 1, 0);
        Component heroBack = Unit("HeroBack", true, 0, 0);
        Component enemyFront = Unit("EnemyFront", false, 2, 0);
        Component enemyBack = Unit("EnemyBack", false, 3, 0);
        Type manager = FindType(managerName);
        MethodInfo canTrigger = manager.GetMethod("CanTriggerCounterattack", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(canTrigger, Is.Not.Null);

        AddSkill(heroFront, Skill("HS1010", 1010, 0, 1f));
        AddSkill(heroFront, Skill("HS1020", 1020, 1, 1f));
        AddSkill(enemyFront, Skill("FV20003_1", 200031, 0, 1f));
        AddSkill(enemyFront, Skill("FV20002_1", 200021, 1, 1f));

        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyFront, 1010) }), Is.True);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyBack, 1010) }), Is.False);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyFront, 1020, true) }), Is.False);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(enemyFront, heroFront, 200031) }), Is.True);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(enemyFront, heroBack, 200031) }), Is.False);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(enemyFront, heroFront, 200021, true) }), Is.False);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyFront, -1) }), Is.True);
        Assert.That(canTrigger.Invoke(null, new[] { Hit(heroFront, enemyFront, -1, true) }), Is.False);
    }

    private static void AddSkill(Component unit, object skill)
    {
        ((IList)unit.GetType().GetField("availableSkills").GetValue(unit)).Add(skill);
    }

    private Component Unit(string name, bool isPlayer, int x, int y)
    {
        GameObject cellObject = new GameObject($"Grid_{x}_{y}_{name}");
        created.Add(cellObject);
        Component cell = cellObject.AddComponent(FindType("ASB.Work.BattleGrid.GridCell"));
        cell.GetType().GetMethod("SetCoords").Invoke(cell, new object[] { new Vector2Int(x, y) });

        GameObject unitObject = new GameObject(name);
        created.Add(unitObject);
        Component unit = unitObject.AddComponent(FindType("BattleCharactor"));
        unit.GetType().GetProperty("IsPlayer").SetValue(unit, isPlayer);
        unit.GetType().GetMethod("AssignToCell").Invoke(unit, new object[] { cell });
        return unit;
    }

    private static object Skill(string key, int index, int range, float value, int effect = 0)
    {
        Type type = FindType("SkillData");
        object skill = Activator.CreateInstance(type);
        type.GetField("skillKey").SetValue(skill, key);
        type.GetField("skillIndex").SetValue(skill, index);
        type.GetField("classSkillRange").SetValue(skill, range);
        type.GetField("classSkillEffect").SetValue(skill, effect);
        type.GetField("skillValue").SetValue(skill, value);
        return skill;
    }

    private static object Hit(Component caster, Component target, int skillIndex, bool isRanged = false)
    {
        Type type = FindType("ASB.Work.Battle.Core.DamageContext");
        object context = Activator.CreateInstance(type);
        type.GetField("Caster").SetValue(context, caster);
        type.GetField("Target").SetValue(context, target);
        type.GetField("SkillIndex").SetValue(context, skillIndex);
        type.GetField("IsRangedAttack").SetValue(context, isRanged);
        return context;
    }

    private static bool CanDefenderCounter(Component defender, object skill)
    {
        Type rules = FindType("ASB.Work.Battle.Core.CounterattackRules");
        return (bool)rules.GetMethod("CanDefenderCounter").Invoke(null, new[] { defender, skill });
    }

    private static float CounterRate(Component unit)
    {
        object stats = unit.GetType().GetProperty("FinalStats").GetValue(unit);
        return (float)stats.GetType().GetField("CounterRate").GetValue(stats);
    }

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(found => found != null);
        Assert.That(type, Is.Not.Null, name);
        return type;
    }
}
