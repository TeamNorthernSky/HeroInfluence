using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BattleActionCompletionPlayModeTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void CompletionAfterBattleEndIsNotPublished_ActiveMismatchStillWarns()
    {
        Type flowType = FindRuntimeType("BattleFlowManager");
        Type inputType = FindRuntimeType("InputHandler");
        Type unitType = FindRuntimeType("BattleCharactor");
        GameObject flowObject = new GameObject("ActionCompletionFlow");
        GameObject inputObject = new GameObject("ActionCompletionInput");
        GameObject playerObject = new GameObject("ActionCompletionPlayer");
        GameObject enemyObject = new GameObject("ActionCompletionEnemy");
        Delegate listener = null;
        EventInfo resolvedEvent = inputType.GetEvent("PlayerSkillActionResolved");
        try
        {
            Component flow = flowObject.AddComponent(flowType);
            Component input = inputObject.AddComponent(inputType);
            flowType.GetField("inputHandler", InstanceMembers).SetValue(flow, input);
            inputType.GetField("battleFlowManager", InstanceMembers).SetValue(input, flow);
            Component player = CreateUnit(playerObject, unitType, true);
            Component enemy = CreateUnit(enemyObject, unitType, false);
            Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(unitType);
            var participants = (IList)Activator.CreateInstance(listType);
            participants.Add(player);
            participants.Add(enemy);
            flowType.GetMethod("Initialize", new[] { listType, typeof(bool) })
                .Invoke(flow, new object[] { participants, false });

            int published = 0;
            listener = CreateForwarder(resolvedEvent.EventHandlerType, _ => published++);
            resolvedEvent.AddEventHandler(null, listener);
            MethodInfo notify = inputType.GetMethod("ResolveAutoBattleAction");
            MethodInfo receive = flowType.GetMethod("OnPlayerSkillActionResolved", InstanceMembers);
            FieldInfo ended = flowType.GetField("battleEnded", InstanceMembers);
            FieldInfo actionResolved = flowType.GetField("playerActionResolved", InstanceMembers);

            ended.SetValue(flow, true);
            notify.Invoke(input, new object[] { player, enemy });
            receive.Invoke(flow, new object[] { player, enemy }); // 이미 발행된 이벤트가 늦게 도착한 경우
            Assert.That(published, Is.Zero);
            Assert.That(actionResolved.GetValue(flow), Is.False);

            ended.SetValue(flow, false);
            unitType.GetField("<IsDead>k__BackingField", InstanceMembers).SetValue(enemy, true);
            Assert.That(flowType.GetProperty("IsEndingBattle").GetValue(flow), Is.True);
            flowType.GetField("<CurrentUnit>k__BackingField", InstanceMembers).SetValue(flow, player);
            notify.Invoke(input, new object[] { player, enemy }); // 마지막 타격 후에도 살아 있는 턴의 완료
            Assert.That(published, Is.EqualTo(1));
            Assert.That(actionResolved.GetValue(flow), Is.True);
            flowType.GetField("<CurrentUnit>k__BackingField", InstanceMembers).SetValue(flow, null);
            actionResolved.SetValue(flow, false);
            notify.Invoke(input, new object[] { player, enemy }); // 종료 루프가 턴을 정리한 뒤
            Assert.That(published, Is.EqualTo(1));
            unitType.GetField("<IsDead>k__BackingField", InstanceMembers).SetValue(enemy, false);
            Assert.That(flowType.GetProperty("IsEndingBattle").GetValue(flow), Is.False);
            string playerName = (string)unitType.GetProperty("UnitName").GetValue(player);
            LogAssert.Expect(LogType.Warning,
                $"[BattleFlow] PlayerSkillActionResolved 무시: CurrentUnit=null, actor={playerName}");
            notify.Invoke(input, new object[] { player, enemy });
            Assert.That(published, Is.EqualTo(2));
            Assert.That(actionResolved.GetValue(flow), Is.False);

            flowType.GetField("<CurrentUnit>k__BackingField", InstanceMembers).SetValue(flow, player);
            notify.Invoke(input, new object[] { player, enemy });
            Assert.That(published, Is.EqualTo(3));
            Assert.That(actionResolved.GetValue(flow), Is.True);
        }
        finally
        {
            if (listener != null) resolvedEvent.RemoveEventHandler(null, listener);
            UnityEngine.Object.DestroyImmediate(inputObject);
            UnityEngine.Object.DestroyImmediate(flowObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
            UnityEngine.Object.DestroyImmediate(enemyObject);
        }
    }

    private static Component CreateUnit(GameObject gameObject, Type unitType, bool isPlayer)
    {
        Component unit = gameObject.AddComponent(unitType);
        unitType.GetProperty("IsPlayer").SetValue(unit, isPlayer);
        unitType.GetField("currentHp", InstanceMembers).SetValue(unit, 100f);
        return unit;
    }

    private static Delegate CreateForwarder(Type delegateType, Action<object[]> callback)
    {
        ParameterExpression[] parameters = delegateType.GetMethod("Invoke").GetParameters()
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
            .ToArray();
        NewArrayExpression arguments = Expression.NewArrayInit(typeof(object),
            parameters.Select(parameter => Expression.Convert(parameter, typeof(object))));
        return Expression.Lambda(delegateType,
            Expression.Invoke(Expression.Constant(callback), arguments), parameters).Compile();
    }

    private static Type FindRuntimeType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"Runtime type '{name}' was not found.");
        return type;
    }
}
