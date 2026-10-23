using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>율리아 피해 무효(증폭기가 하나라도 살아 있으면 피해 0) 계약. 코어 걸쇠·피해 계산·프리팹 배선.</summary>
public sealed class EncounterParticipantImmunityTests
{
    private const string MadonnaUnitPath = "Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanMadonna_40001.prefab";
    private readonly List<GameObject> created = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in created)
            if (go != null) Object.DestroyImmediate(go);
        created.Clear();
    }

    [Test]
    public void DamageImmunity_KeepsHpUntilReleased()
    {
        Component unit = Unit("Yulia", 0f, 0f, true, 0, 0);
        Type type = unit.GetType();
        type.GetField("currentHp", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(unit, 1000f);
        MethodInfo takeDamage = type.GetMethod("TakeDamage", new[] { typeof(float) });

        var immunity = (IDisposable)type.GetMethod("AddDamageImmunity").Invoke(unit, null);
        takeDamage.Invoke(unit, new object[] { 100f });
        Assert.That(type.GetProperty("IsDamageImmune").GetValue(unit), Is.EqualTo(true));
        Assert.That(type.GetProperty("CurrentHp").GetValue(unit), Is.EqualTo(1000f), "걸쇠가 있으면 HP가 줄지 않는다.");

        immunity.Dispose();
        immunity.Dispose();   // 두 번 풀어도 안전
        takeDamage.Invoke(unit, new object[] { 100f });
        Assert.That(type.GetProperty("IsDamageImmune").GetValue(unit), Is.EqualTo(false));
        Assert.That(type.GetProperty("CurrentHp").GetValue(unit), Is.EqualTo(900f));
    }

    [Test]
    public void ImmuneTarget_CalculatesZeroDamage_WithoutCritOrReduction()
    {
        Component caster = Unit("Caster", 110f, 0f, false, 2, 0);
        Unit("FormationFront", 0f, 10f, true, 1, 0);
        Component target = Unit("Target", 0f, 10f, true, 0, 0); // Atk - DEF = 100
        var immunity = (IDisposable)target.GetType().GetMethod("AddDamageImmunity").Invoke(target, null);

        object hit = Hit(caster, target);
        hit.GetType().GetField("IsCritical").SetValue(hit, true);
        FindType("ASB.Work.Battle.Core.CombatCalculator").GetMethod("ResolveHitRolls").Invoke(null, new[] { hit });
        Assert.That(hit.GetType().GetField("IsCritical").GetValue(hit), Is.EqualTo(false), "0 피해에 치명타 표시를 내지 않는다.");
        Assert.That(hit.GetType().GetField("IsDamageReduced").GetValue(hit), Is.EqualTo(false));
        Assert.That(Damage(hit, out bool reduced), Is.EqualTo(0f), "최소 피해(2)도 없이 0.");
        Assert.That(reduced, Is.False);

        immunity.Dispose();
        object normal = Hit(caster, target);
        normal.GetType().GetField("IsDamageReduced").SetValue(normal, false);
        Assert.That(Damage(normal, out _), Is.EqualTo(100f), "걸쇠를 풀면 원래 피해.");
    }

    [Test]
    public void ShouldBeImmune_OnlyWhileAnyParticipantAlive()
    {
        MethodInfo should = FindType("EncounterParticipantImmunity").GetMethod("ShouldBeImmune", BindingFlags.Public | BindingFlags.Static);
        Assert.That((bool)should.Invoke(null, new object[] { 2 }), Is.True);
        Assert.That((bool)should.Invoke(null, new object[] { 1 }), Is.True, "하나라도 살아 있으면 무효.");
        Assert.That((bool)should.Invoke(null, new object[] { 0 }), Is.False, "전부 쓰러져 부활 대기 중이면 피해를 받는다.");
    }

    [TestCase("Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40002.prefab")]
    [TestCase("Assets/Resources/prefab/BattlePrefab/EnemyUnit/Unit_VillanAmplifier_40003.prefab")]
    public void YuliaImmunity_IsWiredToAmplifiers(string amplifierPath)
    {
        GameObject madonna = AssetDatabase.LoadAssetAtPath<GameObject>(MadonnaUnitPath);
        Assert.That(madonna.GetComponent(FindType("EncounterParticipantImmunity")), Is.Not.Null, "율리아 루트에 피해 무효 컴포넌트가 있어야 한다.");
        GameObject amplifier = AssetDatabase.LoadAssetAtPath<GameObject>(amplifierPath);
        Assert.That(amplifier.GetComponent(FindType("EncounterParticipant")), Is.Not.Null, "증폭기가 참여자여야 율리아 무효 조건에 들어간다.");
    }

    private Component Unit(string name, float atk, float def, bool isPlayer, int x, int y)
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
        unitType.GetField("finalStats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(unit, stats);
        return unit;
    }

    private static object Hit(Component caster, Component target)
    {
        Type type = FindType("ASB.Work.Battle.Core.DamageContext");
        object context = Activator.CreateInstance(type);
        type.GetField("Caster").SetValue(context, caster);
        type.GetField("Target").SetValue(context, target);
        type.GetField("SkillMultiplier").SetValue(context, 1f);
        type.GetField("IsRangedAttack").SetValue(context, true);
        return context;
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
