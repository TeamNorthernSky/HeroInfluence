using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class TutorialBattlePlayModeTests
{
    private const BindingFlags AllInstance =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    [UnityTest]
    public IEnumerator EntryFlowLock_BlocksFirstTurnUntilReleased()
    {
        Type flowType = FindRuntimeType("BattleFlowManager");
        Type managerType = FindRuntimeType("BattleManager");
        Type characterType = FindRuntimeType("BattleCharactor");

        var systemObject = new GameObject("TutorialFlowGatePlayMode");
        var playerObject = new GameObject("TutorialPlayer");
        var enemyObject = new GameObject("TutorialEnemy");
        try
        {
            Component flow = systemObject.AddComponent(flowType);
            Component manager = systemObject.AddComponent(managerType);
            flowType.GetField("battleManager", AllInstance).SetValue(flow, manager);

            Component player = CreateInitializedUnit(playerObject, characterType, true, 10f);
            Component enemy = CreateInitializedUnit(enemyObject, characterType, false, 1f);
            object participants = CreateRuntimeList(characterType, player, enemy);

            flowType.GetMethod(
                    "Initialize",
                    new[] { participants.GetType(), typeof(bool) })
                .Invoke(flow, new[] { participants, (object)false });

            IDisposable flowLock = (IDisposable)flowType
                .GetMethod("AcquireFlowLock")
                .Invoke(flow, new object[] { this, "entry-ui" });

            int turnStartedCount = 0;
            EventInfo turnStarted = flowType.GetEvent("OnTurnStarted");
            Delegate listener = CreateForwarder(
                turnStarted.EventHandlerType,
                _ => turnStartedCount++);
            turnStarted.AddEventHandler(flow, listener);

            flowType.GetMethod("StartBattleLoop").Invoke(flow, null);
            yield return null;
            yield return null;
            Assert.That(turnStartedCount, Is.Zero,
                "입장 UI 잠금이 남아 있는 동안 첫 턴이 시작되면 안 됩니다.");

            flowLock.Dispose();
            for (int i = 0; i < 20 && turnStartedCount == 0; i++)
            {
                yield return null;
            }

            Assert.That(turnStartedCount, Is.EqualTo(1));
            flowType.GetMethod("StopBattleLoop").Invoke(flow, null);
            turnStarted.RemoveEventHandler(flow, listener);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(systemObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
            UnityEngine.Object.DestroyImmediate(enemyObject);
        }
    }

    [UnityTest]
    public IEnumerator SkillResolved_FiresForPlayerAndEnemyTopLevelActions()
    {
        Type managerType = FindRuntimeType("BattleManager");
        Type characterType = FindRuntimeType("BattleCharactor");
        Type skillType = FindRuntimeType("SkillData");

        var managerObject = new GameObject("TutorialSkillResolvedPlayMode");
        var playerObject = new GameObject("TutorialPlayer");
        var enemyObject = new GameObject("TutorialEnemy");
        try
        {
            Component manager = managerObject.AddComponent(managerType);
            Component player = CreateInitializedUnit(playerObject, characterType, true, 10f);
            Component enemy = CreateInitializedUnit(enemyObject, characterType, false, 10f);
            object skill = Activator.CreateInstance(skillType);
            skillType.GetField("skillIndex").SetValue(skill, 900001);
            skillType.GetField("skillKey").SetValue(skill, "TutorialTestSkill");
            skillType.GetField("skillName").SetValue(skill, "Tutorial Test Skill");
            skillType.GetField("classSkillEffect").SetValue(skill, 0);
            skillType.GetField("skillValue").SetValue(skill, 1f);
            skillType.GetField("HitDelay").SetValue(skill, 0f);
            skillType.GetField("TotalDelay").SetValue(skill, 0f);
            skillType.GetField("UseAnimEvent").SetValue(skill, false);

            var resolutions = new List<object>();
            EventInfo resolvedEvent = managerType.GetEvent("OnSkillResolved");
            Delegate listener = CreateForwarder(
                resolvedEvent.EventHandlerType,
                args => resolutions.Add(args[0]));
            resolvedEvent.AddEventHandler(manager, listener);

            MethodInfo execute = managerType.GetMethods()
                .Single(method =>
                {
                    if (method.Name != "ExecuteGridSkill")
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 4 &&
                           parameters[0].ParameterType == characterType &&
                           parameters[1].ParameterType == characterType &&
                           parameters[2].ParameterType == skillType;
                });

            IEnumerator playerRoutine = (IEnumerator)execute.Invoke(
                manager,
                new[] { player, enemy, skill, null });
            yield return ((MonoBehaviour)manager).StartCoroutine(playerRoutine);

            IEnumerator enemyRoutine = (IEnumerator)execute.Invoke(
                manager,
                new[] { enemy, player, skill, null });
            yield return ((MonoBehaviour)manager).StartCoroutine(enemyRoutine);

            Assert.That(resolutions.Count, Is.EqualTo(2),
                "각 최상위 행동은 SkillResolved를 정확히 한 번씩 발행해야 합니다.");

            Type resolutionType = resolutions[0].GetType();
            Assert.That(
                (bool)resolutionType.GetProperty("WasPlayerAction").GetValue(resolutions[0]),
                Is.True);
            Assert.That(
                (bool)resolutionType.GetProperty("WasPlayerAction").GetValue(resolutions[1]),
                Is.False);

            resolvedEvent.RemoveEventHandler(manager, listener);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
            UnityEngine.Object.DestroyImmediate(enemyObject);
        }
    }

    private static Type FindRuntimeType(string fullName)
    {
        Type found = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(type => type.FullName == fullName);
        Assert.That(found, Is.Not.Null, $"런타임 타입을 찾지 못했습니다: {fullName}");
        return found;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
    }

    private static Component CreateInitializedUnit(
        GameObject gameObject,
        Type characterType,
        bool isPlayer,
        float speed)
    {
        Type statType = FindRuntimeType("StatBlock");
        Component unit = gameObject.AddComponent(characterType);
        characterType.GetProperty("IsPlayer").SetValue(unit, isPlayer);
        characterType.GetMethod("SetLevelScaling").Invoke(unit, new object[] { false });

        object stats = Activator.CreateInstance(
            statType,
            new object[]
            {
                100f, 10f, 0f, 0f, speed,
                0f, 1f, 0f, 0f, 100f
            });
        characterType.GetMethod("SetBaseStats").Invoke(unit, new[] { stats });
        characterType.GetMethod("RecalculateStats").Invoke(unit, new object[] { false });
        characterType.GetMethod("InitializeCurrentHpToMax").Invoke(unit, null);
        return unit;
    }

    private static object CreateRuntimeList(
        Type elementType,
        params object[] entries)
    {
        Type listType = typeof(List<>).MakeGenericType(elementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType);
        for (int i = 0; i < entries.Length; i++)
        {
            list.Add(entries[i]);
        }
        return list;
    }

    private static Delegate CreateForwarder(
        Type delegateType,
        Action<object[]> callback)
    {
        MethodInfo invoke = delegateType.GetMethod("Invoke");
        ParameterExpression[] parameters = invoke.GetParameters()
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
            .ToArray();
        NewArrayExpression boxedArguments = Expression.NewArrayInit(
            typeof(object),
            parameters.Select(parameter => Expression.Convert(parameter, typeof(object))));
        InvocationExpression body = Expression.Invoke(
            Expression.Constant(callback),
            boxedArguments);
        return Expression.Lambda(delegateType, body, parameters).Compile();
    }
}
