using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 절망의 굴렁쇠(EAI_40005) 대상 선택: 전열/후열 무관하게 자기 행(같은 Coords.y)만 노리고, 행이 비면 사라진다.
/// 게임 코드는 Assembly-CSharp에 있어 리플렉션으로 호출한다(EventBattlePlanBuilderTests와 동일 패턴).
/// </summary>
public sealed class HoopSameRowTargetingTests
{
    private const int HoopSkillIndex = 400051;

    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < created.Count; i++)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [Test]
    public void PicksCloserHeroInSameRow_IgnoringLowerHpElsewhere()
    {
        Component hoop = CreateUnit("Hoop", isPlayer: false, x: 2, y: 1, hp: 30f, withHoopSkill: true);
        Component otherRowLowHp = CreateUnit("OtherRow", isPlayer: true, x: 1, y: 0, hp: 1f);
        Component sameRowBack = CreateUnit("SameRowBack", isPlayer: true, x: 0, y: 1, hp: 50f);
        Component sameRowFront = CreateUnit("SameRowFront", isPlayer: true, x: 1, y: 1, hp: 80f);

        object decision = Decide(hoop, otherRowLowHp, sameRowBack, sameRowFront);

        Assert.That(GetField<bool>(decision, "Skip"), Is.False);
        Assert.That(GetField<object>(decision, "Target"), Is.SameAs(sameRowFront));
    }

    [Test]
    public void TargetsBackRowWhenSameRowFrontIsEmpty()
    {
        Component hoop = CreateUnit("Hoop", isPlayer: false, x: 2, y: 2, hp: 30f, withHoopSkill: true);
        Component otherRowFront = CreateUnit("OtherRowFront", isPlayer: true, x: 1, y: 1, hp: 1f);
        Component sameRowBack = CreateUnit("SameRowBack", isPlayer: true, x: 0, y: 2, hp: 50f);

        object decision = Decide(hoop, otherRowFront, sameRowBack);

        Assert.That(GetField<bool>(decision, "Skip"), Is.False);
        Assert.That(GetField<object>(decision, "Target"), Is.SameAs(sameRowBack));
    }

    [Test]
    public void VanishesWhenNoHeroInSameRow()
    {
        Component hoop = CreateUnit("Hoop", isPlayer: false, x: 2, y: 0, hp: 30f, withHoopSkill: true);
        Component otherRowA = CreateUnit("OtherRowA", isPlayer: true, x: 1, y: 1, hp: 10f);
        Component otherRowB = CreateUnit("OtherRowB", isPlayer: true, x: 0, y: 2, hp: 10f);

        object decision = Decide(hoop, otherRowA, otherRowB);

        Assert.That(GetField<bool>(decision, "Skip"), Is.False, "같은 행이 비어도 턴을 스킵하면 안 된다.");
        Assert.That(GetField<bool>(decision, "IsSelfAction"), Is.True);
        Assert.That(GetField<object>(decision, "Target"), Is.Null);

        // 커밋 전에는 판정만 한다(부작용 없음). 커밋하면 사라진다.
        PropertyInfo isDead = hoop.GetType().GetProperty("IsDead");
        Assert.That((bool)isDead.GetValue(hoop), Is.False);
        GetField<Action>(decision, "Commit").Invoke();
        Assert.That((bool)isDead.GetValue(hoop), Is.True);
        Assert.That((bool)isDead.GetValue(otherRowA), Is.False);
        Assert.That((bool)isDead.GetValue(otherRowB), Is.False);
    }

    // ----- helpers -----

    private object Decide(Component self, params Component[] targets)
    {
        Type aiType = FindRuntimeType("EnemyAI.EAI_40005");
        object ai = Activator.CreateInstance(aiType);
        Type unitType = FindRuntimeType("BattleCharactor");
        IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(unitType));
        foreach (Component t in targets) list.Add(t);

        MethodInfo decide = aiType.GetMethod("DecideAction", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(decide, Is.Not.Null);
        object decision = decide.Invoke(ai, new object[] { self, list });
        Assert.That(decision, Is.Not.Null);
        return decision;
    }

    private Component CreateUnit(string name, bool isPlayer, int x, int y, float hp, bool withHoopSkill = false)
    {
        var cellObject = new GameObject($"Grid_{x}_{y}");
        created.Add(cellObject);
        Component cell = cellObject.AddComponent(FindRuntimeType("ASB.Work.BattleGrid.GridCell"));
        SetField(cell, "coords", new Vector2Int(x, y));

        var unitObject = new GameObject(name);
        created.Add(unitObject);
        Type unitType = FindRuntimeType("BattleCharactor");
        Component unit = unitObject.AddComponent(unitType);
        unitType.GetProperty("IsPlayer").SetValue(unit, isPlayer);
        SetField(unit, "currentHp", hp);
        unitType.GetMethod("AssignToCell", BindingFlags.Public | BindingFlags.Instance).Invoke(unit, new object[] { cell });

        if (withHoopSkill)
        {
            Type skillType = FindRuntimeType("SkillData");
            object skill = Activator.CreateInstance(skillType);
            skillType.GetField("skillIndex").SetValue(skill, HoopSkillIndex);
            IList skills = (IList)unitType.GetField("availableSkills").GetValue(unit);
            skills.Add(skill);
        }

        return unit;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} 필드를 찾지 못했습니다.");
        field.SetValue(target, value);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} 필드를 찾지 못했습니다.");
        return (T)field.GetValue(target);
    }

    private static Type FindRuntimeType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        throw new InvalidOperationException($"Runtime type '{typeName}' was not found in loaded assemblies.");
    }
}
