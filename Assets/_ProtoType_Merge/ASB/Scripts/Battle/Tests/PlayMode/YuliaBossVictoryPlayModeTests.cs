using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class YuliaBossVictoryPlayModeTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator Simulation_BossDeathWinsWhileAmplifierAndWheelAreAlive()
    {
        yield return RunBossDeathScenario(eventBattle: false);
    }

    [UnityTest]
    public IEnumerator EventBattle_BossDeathWinsWhileAmplifierAndWheelAreAlive()
    {
        yield return RunBossDeathScenario(eventBattle: true);
    }

    [UnityTest]
    public IEnumerator DeadBossStateWinsEvenWhenDeathNotificationWasMissed()
    {
        Type flowType = FindRuntimeType("BattleFlowManager");
        Type unitType = FindRuntimeType("BattleCharactor");
        GameObject flowObject = new GameObject("YuliaVictoryFlow");
        GameObject playerObject = new GameObject("YuliaVictoryPlayer");
        GameObject bossObject = new GameObject("YuliaVictoryBoss");
        GameObject wheelObject = new GameObject("YuliaVictoryWheel");
        try
        {
            RegisterContext(eventBattle: false);
            Component flow = flowObject.AddComponent(flowType);
            Component player = CreateUnit(playerObject, unitType, true, "10001");
            Component boss = CreateUnit(bossObject, unitType, false, "40001");
            Component wheel = CreateUnit(wheelObject, unitType, false, "40005");
            InitializeFlow(flow, flowType, unitType, player, boss, wheel);

            // 사망 이벤트 구독이 끊긴 경우를 재현한다. 남은 굴렁쇠는 살아 있다.
            unitType.GetField("<IsDead>k__BackingField", InstanceMembers).SetValue(boss, true);
            Assert.That((bool)flowType.GetProperty("IsEndingBattle").GetValue(flow), Is.True);
            Assert.That((bool)unitType.GetProperty("IsDead").GetValue(wheel), Is.False);
            object[] resultArgs = { null };
            Assert.That((bool)flowType.GetMethod("TryEvaluateBattleResult", InstanceMembers)
                .Invoke(flow, resultArgs), Is.True);
            Assert.That(resultArgs[0].ToString(), Is.EqualTo("Victory"));

            flowType.GetMethod("StartBattleLoop").Invoke(flow, null);
            yield return null;
            Assert.That(GetField<bool>(flow, "battleEnded"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(flowObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
            UnityEngine.Object.DestroyImmediate(bossObject);
            UnityEngine.Object.DestroyImmediate(wheelObject);
            ClearContext();
        }
    }

    private static IEnumerator RunBossDeathScenario(bool eventBattle)
    {
        Type flowType = FindRuntimeType("BattleFlowManager");
        Type unitType = FindRuntimeType("BattleCharactor");
        GameObject flowObject = new GameObject("YuliaVictoryFlow");
        GameObject playerObject = new GameObject("YuliaVictoryPlayer");
        GameObject bossObject = new GameObject("YuliaVictoryBoss");
        GameObject amplifierObject = new GameObject("YuliaVictoryAmplifier");
        GameObject wheelObject = new GameObject("YuliaVictoryWheel");
        try
        {
            RegisterContext(eventBattle);
            Component flow = flowObject.AddComponent(flowType);
            Component player = CreateUnit(playerObject, unitType, true, "10001");
            Component boss = CreateUnit(bossObject, unitType, false, "40001");
            Component amplifier = CreateUnit(amplifierObject, unitType, false, "40002");
            Component wheel = CreateUnit(wheelObject, unitType, false, "40005");
            InitializeFlow(flow, flowType, unitType, player, boss, amplifier, wheel);

            Assert.That(GetField<string>(flow, "enemyGroupKey"), Is.EqualTo("BE490"));
            flowType.GetMethod("StartBattleLoop").Invoke(flow, null);
            yield return null;

            unitType.GetMethod("TakeDamage").Invoke(boss, new object[] { 1000f });
            for (int frame = 0; frame < 5 && !GetField<bool>(flow, "battleEnded"); frame++)
                yield return null;

            Assert.That(GetField<bool>(flow, "battleEnded"), Is.True);
            Assert.That(GetField<object>(flow, "requestedBattleResult").ToString(), Is.EqualTo("Victory"));
            Assert.That((bool)unitType.GetProperty("IsDead").GetValue(amplifier), Is.False);
            Assert.That((bool)unitType.GetProperty("IsDead").GetValue(wheel), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(flowObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
            UnityEngine.Object.DestroyImmediate(bossObject);
            UnityEngine.Object.DestroyImmediate(amplifierObject);
            UnityEngine.Object.DestroyImmediate(wheelObject);
            ClearContext();
        }
    }

    private static void RegisterContext(bool eventBattle)
    {
        Type contextType = FindRuntimeType("CombatContext");
        Component context = (Component)contextType.GetMethod("EnsureInstance").Invoke(null, null);
        contextType.GetMethod("Clear").Invoke(context, null);
        if (eventBattle)
        {
            Type eventDataType = FindRuntimeType("CombatEventBattleData");
            object eventData = Activator.CreateInstance(eventDataType, 2, "BE490", 0, 1);
            contextType.GetMethod("RegisterEventBattle").Invoke(context, new[] { eventData });
        }
        else
        {
            MethodInfo registerEnemy = contextType.GetMethod("RegisterCombatEnemy");
            Type sourceType = registerEnemy.GetParameters()[4].ParameterType;
            object source = Enum.Parse(sourceType, "Simulation");
            registerEnemy.Invoke(context, new[] { "test-enemy", "", "BE490", (object)1, source });
        }
    }

    private static void ClearContext()
    {
        Type contextType = FindRuntimeType("CombatContext");
        Component context = contextType.GetProperty("Instance").GetValue(null) as Component;
        if (context != null) contextType.GetMethod("Clear").Invoke(context, null);
    }

    private static Component CreateUnit(GameObject gameObject, Type unitType, bool isPlayer, string templateIndex)
    {
        Component unit = gameObject.AddComponent(unitType);
        unitType.GetProperty("IsPlayer").SetValue(unit, isPlayer);
        unitType.GetMethod("SetTemplateIndex").Invoke(unit, new object[] { templateIndex });
        unitType.GetField("currentHp", InstanceMembers).SetValue(unit, 100f);
        return unit;
    }

    private static void InitializeFlow(Component flow, Type flowType, Type unitType, params Component[] units)
    {
        Type listType = typeof(List<>).MakeGenericType(unitType);
        var participants = (IList)Activator.CreateInstance(listType);
        foreach (Component unit in units) participants.Add(unit);
        flowType.GetMethod("Initialize", new[] { listType, typeof(bool) })
            .Invoke(flow, new object[] { participants, false });
    }

    private static T GetField<T>(object instance, string name)
        => (T)instance.GetType().GetField(name, InstanceMembers).GetValue(instance);

    private static Type FindRuntimeType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Runtime type '{name}' was not found.");
        return type;
    }
}
