using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class SectorTwoEnemyAiTests
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
    public void Rifleman_UsesShotAndLowestHpInSameRow()
    {
        Component self = Unit("Rifleman", false, 2, 1, 50);
        AddSkill(self, 200021);
        AddSkill(self, 200022);
        Component near = Unit("Near", true, 1, 1, 2f);
        Component low = Unit("LowSameRow", true, 0, 1, 1f);
        Component lowestElsewhere = Unit("LowestElsewhere", true, 1, 0, 0.5f);

        Assert.That(low.GetType().GetProperty("CurrentHp").GetValue(low), Is.EqualTo(1f));
        Assert.That(low.GetType().GetProperty("OccupiedCell").GetValue(low), Is.Not.Null);

        object decision = Decide(20002, self, near, low, lowestElsewhere);
        Assert.That(Field<object>(decision, "Target"), Is.SameAs(low));
        Assert.That(Field<object>(decision, "SelectedSkill")?.GetType().GetField("skillIndex")
            ?.GetValue(Field<object>(decision, "SelectedSkill")), Is.EqualTo(200021));
    }

    [Test]
    public void Rifleman_WhenRowEmpty_UsesNearest()
    {
        Component self = Unit("Rifleman", false, 2, 1, 50);
        AddSkill(self, 200021);
        Component near = Unit("Near", true, 1, 0, 80);
        Component far = Unit("Far", true, 0, 2, 1);

        Assert.That(Field<object>(Decide(20002, self, near, far), "Target"), Is.SameAs(near));
    }

    [Test]
    public void ShieldSoldier_WithoutWoundedAlly_UsesNearest()
    {
        Component self = Unit("Shield", false, 2, 1, 50);
        AddSkill(self, 200031);
        Component near = Unit("Near", true, 1, 1, 80);
        Component farLowHp = Unit("FarLowHp", true, 0, 0, 1);

        Assert.That(Field<object>(Decide(20003, self, near, farLowHp), "Target"), Is.SameAs(near));
    }

    [Test]
    public void ReinforcedSoldier_ComboPrefersFrontForFirstAndBackForSecond()
    {
        Component self = Unit("Reinforced", false, 2, 1, 50);
        object combo = AddSkill(self, 200052);
        Component front = Unit("Front", true, 1, 1, 80);
        Component back = Unit("Back", true, 0, 1, 50);

        Assert.That(Field<object>(Decide(20005, self, front, back), "Target"), Is.SameAs(front));

        Type handlerType = FindType("ASB.Work.Battle.SkillExecution.HitTargetAroundRandomHandler");
        object handler = Activator.CreateInstance(handlerType);
        IList candidates = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(FindType("BattleCharactor")));
        candidates.Add(back);
        object selected = handlerType.GetMethod("SelectAdditionalTargets", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(handler, new[] { self, front, candidates, combo });
        Assert.That(((IList)selected)[0], Is.SameAs(back));
    }

    [Test]
    public void Factory_CreatesDedicatedAiForTurretAndReinforcedSoldier()
    {
        Type factory = FindType("EnemyAI.EnemyAIFactory");
        MethodInfo create = factory.GetMethod("CreateAI", new[] { typeof(int) });
        Assert.That(create.Invoke(null, new object[] { 20004 }).GetType().Name, Is.EqualTo("EAI_20004"));
        Assert.That(create.Invoke(null, new object[] { 20005 }).GetType().Name, Is.EqualTo("EAI_20005"));
    }

    [Test]
    public void Turret_PrefersTargetWithMoreAdjacentHeroes()
    {
        Component clustered = Unit("Clustered", true, 1, 1, 2f);
        Unit("Above", true, 1, 0, 2f);
        Unit("Left", true, 0, 1, 2f);
        Component isolated = Unit("Isolated", true, 0, 2, 2f);
        Type aiType = FindType("EnemyAI.EAI_20004");
        IList candidates = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(FindType("BattleCharactor")));
        candidates.Add(clustered);
        candidates.Add(isolated);
        object result = aiType.GetMethod("SelectMostClustered", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { candidates });
        Assert.That(result, Is.SameAs(clustered));
    }

    [Test]
    public void EventSkillKeys_UseTheDedicatedHandlers()
    {
        Type registry = FindType("ASB.Work.Battle.SkillExecution.SkillExecutionRegistry");
        MethodInfo lookup = registry.GetMethod("TryGetHandler");
        object[] comboArgs = { "20005_2", null };
        object[] turretArgs = { "20004_1", null };
        Assert.That(lookup.Invoke(null, comboArgs), Is.True);
        Assert.That(lookup.Invoke(null, turretArgs), Is.True);
        Assert.That(comboArgs[1].GetType().Name, Is.EqualTo("HitTargetAroundRandomHandler"));
        Assert.That(turretArgs[1].GetType().Name, Is.EqualTo("PiercingDashSkillHandler"));
    }

    private Component Unit(string name, bool isPlayer, int x, int y, float hp)
    {
        GameObject cellGo = new GameObject($"Cell_{name}");
        created.Add(cellGo);
        Component cell = cellGo.AddComponent(FindType("ASB.Work.BattleGrid.GridCell"));
        SetField(cell, "coords", new Vector2Int(x, y));

        GameObject unitGo = new GameObject(name);
        created.Add(unitGo);
        Component unit = unitGo.AddComponent(FindType("BattleCharactor"));
        unit.GetType().GetProperty("IsPlayer").SetValue(unit, isPlayer);
        unit.GetType().GetMethod("AssignToCell").Invoke(unit, new object[] { cell });
        SetField(unit, "currentHp", hp);
        return unit;
    }

    private static object AddSkill(Component unit, int index)
    {
        Type skillType = FindType("SkillData");
        object skill = Activator.CreateInstance(skillType);
        skillType.GetField("skillIndex").SetValue(skill, index);
        skillType.GetField("skillKey").SetValue(skill, $"{index / 10}_{index % 10}");
        skillType.GetField("classSkillRangeLine").SetValue(skill, 1);
        skillType.GetField("classSkillEffect").SetValue(skill, 0);
        ((IList)unit.GetType().GetField("availableSkills").GetValue(unit)).Add(skill);
        return skill;
    }

    private static object Decide(int index, Component self, params Component[] targets)
    {
        Type factory = FindType("EnemyAI.EnemyAIFactory");
        object ai = factory.GetMethod("CreateAI", new[] { typeof(int) }).Invoke(null, new object[] { index });
        IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(FindType("BattleCharactor")));
        foreach (Component target in targets) list.Add(target);
        return ai.GetType().GetMethod("DecideAction").Invoke(ai, new object[] { self, list });
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static T Field<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, name);
        return (T)field.GetValue(target);
    }

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(found => found != null);
        Assert.That(type, Is.Not.Null, name);
        return type;
    }
}
