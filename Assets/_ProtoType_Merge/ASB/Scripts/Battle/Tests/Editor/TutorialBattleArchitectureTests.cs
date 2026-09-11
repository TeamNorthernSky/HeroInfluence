using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TutorialBattleArchitectureTests
{
    private const BindingFlags AllInstance =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

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

    [Test]
    public void FlowLock_RequiresEveryOwnerHandleToRelease()
    {
        Type flowType = FindRuntimeType("BattleFlowManager");
        var gameObject = new GameObject("TutorialFlowLockTest");
        try
        {
            Component flow = gameObject.AddComponent(flowType);
            MethodInfo acquire = flowType.GetMethod("AcquireFlowLock");
            PropertyInfo blocked = flowType.GetProperty("IsFlowBlocked");

            var first = (IDisposable)acquire.Invoke(flow, new object[] { this, "first" });
            var second = (IDisposable)acquire.Invoke(flow, new object[] { new object(), "second" });

            Assert.That(blocked.GetValue(flow), Is.True);
            first.Dispose();
            first.Dispose();
            Assert.That(blocked.GetValue(flow), Is.True,
                "첫 handle을 중복 해제해도 다른 소유자의 잠금은 남아야 합니다.");

            second.Dispose();
            Assert.That(blocked.GetValue(flow), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void MinimumHpConstraints_UseHighestFloor_AndReleaseIndependently()
    {
        Type characterType = FindRuntimeType("BattleCharactor");
        var gameObject = new GameObject("TutorialMinimumHpTest");
        try
        {
            Component unit = CreateInitializedUnit(gameObject, characterType, hp: 100f, atk: 10f);
            MethodInfo add = characterType.GetMethod("AddMinimumHpConstraint");
            MethodInfo takeDamage = characterType.GetMethod("TakeDamage");
            PropertyInfo currentHp = characterType.GetProperty("CurrentHp");
            PropertyInfo isDead = characterType.GetProperty("IsDead");

            var low = (IDisposable)add.Invoke(unit, new object[] { 1f, this });
            var high = (IDisposable)add.Invoke(unit, new object[] { 10f, new object() });

            takeDamage.Invoke(unit, new object[] { 999f });
            Assert.That((float)currentHp.GetValue(unit), Is.EqualTo(10f).Within(0.001f));
            Assert.That((bool)isDead.GetValue(unit), Is.False);

            high.Dispose();
            takeDamage.Invoke(unit, new object[] { 999f });
            Assert.That((float)currentHp.GetValue(unit), Is.EqualTo(1f).Within(0.001f));
            Assert.That((bool)isDead.GetValue(unit), Is.False);

            low.Dispose();
            takeDamage.Invoke(unit, new object[] { 999f });
            Assert.That((bool)isDead.GetValue(unit), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void RuntimeStatModifier_SurvivesRecalculate_AndRestoresOnDispose()
    {
        Type characterType = FindRuntimeType("BattleCharactor");
        Type statType = FindRuntimeType("StatBlock");
        Type maskType = FindRuntimeType("RuntimeStatMask");
        Type operationType = FindRuntimeType("RuntimeStatOperation");
        Type modifierType = FindRuntimeType("RuntimeStatModifier");

        var gameObject = new GameObject("TutorialRuntimeStatTest");
        try
        {
            Component unit = CreateInitializedUnit(gameObject, characterType, hp: 100f, atk: 10f);
            object values = CreateStats(statType, hp: 0f, atk: 5f);
            object mask = Enum.Parse(maskType, "Atk");
            object operation = Enum.Parse(operationType, "Add");
            object modifier = Activator.CreateInstance(
                modifierType,
                new[] { mask, operation, values });

            MethodInfo add = characterType.GetMethod("AddRuntimeStatModifier");
            var handle = (IDisposable)add.Invoke(unit, new[] { modifier, (object)this });

            Assert.That(ReadFinalStat(unit, characterType, "Atk"), Is.EqualTo(15f).Within(0.001f));
            characterType.GetMethod("RecalculateStats").Invoke(unit, new object[] { true });
            Assert.That(ReadFinalStat(unit, characterType, "Atk"), Is.EqualTo(15f).Within(0.001f));

            handle.Dispose();
            handle.Dispose();
            Assert.That(ReadFinalStat(unit, characterType, "Atk"), Is.EqualTo(10f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void PlayerActionConstraint_BlocksSkipAndFleeWithoutAFlowLock()
    {
        Type flowType = FindRuntimeType("BattleFlowManager");
        Type actionType = FindRuntimeType("PendingActionType");
        var gameObject = new GameObject("TutorialActionConstraintTest");
        try
        {
            Component flow = gameObject.AddComponent(flowType);
            MethodInfo addConstraint = flowType.GetMethod("AddPlayerActionConstraint");
            Type predicateType = addConstraint.GetParameters()[1].ParameterType;
            MethodInfo invoke = predicateType.GetMethod("Invoke");
            ParameterExpression[] parameters = invoke.GetParameters()
                .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
                .ToArray();
            object classSkill = Enum.Parse(actionType, "ClassSkill");
            BinaryExpression body = Expression.Equal(
                parameters[1],
                Expression.Constant(classSkill, actionType));
            Delegate predicate = Expression.Lambda(predicateType, body, parameters).Compile();

            var handle = (IDisposable)addConstraint.Invoke(
                flow,
                new object[] { this, predicate, "class skill only" });
            MethodInfo isAllowed = flowType.GetMethod("IsPlayerActionAllowed");

            Assert.That(
                (bool)isAllowed.Invoke(flow, new[] { null, classSkill, null }),
                Is.True);
            Assert.That(
                (bool)isAllowed.Invoke(
                    flow,
                    new[] { null, Enum.Parse(actionType, "Skip"), null }),
                Is.False);
            Assert.That(
                (bool)isAllowed.Invoke(
                    flow,
                    new[] { null, Enum.Parse(actionType, "Flee"), null }),
                Is.False);

            handle.Dispose();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void SkillResolutionContext_SeparatesAttemptedAndAppliedDamage()
    {
        Type resultType = FindRuntimeType("ASB.Work.Battle.SkillExecution.SkillExecutionResult");
        Type damageContextType = FindRuntimeType("ASB.Work.Battle.Core.DamageContext");
        Type hitType = FindRuntimeType("ASB.Work.Battle.Core.BattleHitResult");
        Type resolutionType = FindRuntimeType("SkillResolutionContext");

        object result = resultType.GetMethod("SuccessResult", Type.EmptyTypes).Invoke(null, null);
        object damageContext = Activator.CreateInstance(damageContextType);
        object hit = Activator.CreateInstance(hitType);
        hitType.GetField("Damage").SetValue(hit, 25f);
        hitType.GetField("AppliedDamage").SetValue(hit, 4f);

        resultType.GetMethod("RecordDamageResult").Invoke(
            result,
            new[] { damageContext, hit });

        ConstructorInfo constructor = resolutionType.GetConstructors().Single();
        object resolution = constructor.Invoke(new object[] { null, null, null, true, result });

        Assert.That(
            (float)resolutionType.GetProperty("TotalAttemptedDamage").GetValue(resolution),
            Is.EqualTo(25f).Within(0.001f));
        Assert.That(
            (float)resolutionType.GetProperty("TotalAppliedDamage").GetValue(resolution),
            Is.EqualTo(4f).Within(0.001f));
    }

    // NOTE: TutorialBattleFlowRegistry 검증(§20)은 이 Editor 테스트 어셈블리가 게임플레이 런타임
    //       어셈블리를 직접 참조하지 않으므로(다른 테스트는 FindRuntimeType 리플렉션 사용) 여기서 생략.
    //       asmdef 참조 정리 또는 리플렉션 기반으로 별도 추가 예정.

    private static Component CreateInitializedUnit(
        GameObject gameObject,
        Type characterType,
        float hp,
        float atk)
    {
        Type statType = FindRuntimeType("StatBlock");
        Component unit = gameObject.AddComponent(characterType);
        characterType.GetMethod("SetLevelScaling").Invoke(unit, new object[] { false });
        characterType.GetMethod("SetBaseStats").Invoke(
            unit,
            new[] { CreateStats(statType, hp, atk) });
        characterType.GetMethod("RecalculateStats").Invoke(unit, new object[] { false });
        characterType.GetMethod("InitializeCurrentHpToMax").Invoke(unit, null);
        return unit;
    }

    private static object CreateStats(Type statType, float hp, float atk)
    {
        return Activator.CreateInstance(
            statType,
            new object[]
            {
                hp, atk, 0f, 0f, 1f,
                0f, 1f, 0f, 0f, 0f
            });
    }

    private static float ReadFinalStat(object unit, Type characterType, string fieldName)
    {
        object finalStats = characterType.GetProperty("FinalStats").GetValue(unit);
        return (float)finalStats.GetType().GetField(fieldName).GetValue(finalStats);
    }
}
