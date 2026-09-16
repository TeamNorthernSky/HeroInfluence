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

    // NOTE: 이 EditMode 어셈블리는 런타임(Assembly-CSharp)을 직접 참조하지 못하므로 리플렉션(FindRuntimeType) 사용.
    //       ShowBlockingUi fail-open의 실제 데드락 회피는 BattleFlowManager 실인스턴스가 필요해 PlayMode 테스트로 별도 예정.

    [Test]
    public void UiCatalog_Find_ExactThenWildcard_Ordinal()
    {
        Type sheetType = FindRuntimeType("TutorialUiSheet");
        Type catalogType = FindRuntimeType("TutorialUiCatalog");

        ScriptableObject exact = MakeSheet(sheetType, 5, "TUT_A");
        ScriptableObject wild = MakeSheet(sheetType, -1, "TUT_B");
        ScriptableObject catalog = ScriptableObject.CreateInstance(catalogType);
        try
        {
            var sheets = (System.Collections.IList)catalogType
                .GetField("sheets", AllInstance).GetValue(catalog);
            sheets.Add(exact);
            sheets.Add(wild);

            MethodInfo find = catalogType.GetMethod("Find");
            Assert.That(find.Invoke(catalog, new object[] { 5, "TUT_A" }), Is.SameAs(exact), "정확 (zone,key)");
            Assert.That(find.Invoke(catalog, new object[] { 9, "TUT_A" }), Is.Null, "다른 zone·와일드카드 없음");
            Assert.That(find.Invoke(catalog, new object[] { 9, "TUT_B" }), Is.SameAs(wild), "zone 와일드카드(-1)");
            Assert.That(find.Invoke(catalog, new object[] { 5, "tut_a" }), Is.Null, "Ordinal 대소문자 구분");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(exact);
            UnityEngine.Object.DestroyImmediate(wild);
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void UiSheet_CollectValidationIssues_DetectsDupEmptyAndBadView()
    {
        Type sheetType = FindRuntimeType("TutorialUiSheet");
        Type entryType = FindRuntimeType("TutorialUiEntry");
        ScriptableObject sheet = ScriptableObject.CreateInstance(sheetType);
        try
        {
            var entries = (System.Collections.IList)sheetType
                .GetField("entries", AllInstance).GetValue(sheet);
            entries.Add(MakeEntry(entryType, "dup", "guide"));
            entries.Add(MakeEntry(entryType, "dup", "guide"));   // 중복 key
            entries.Add(MakeEntry(entryType, "", "guide"));       // 빈 key
            entries.Add(MakeEntry(entryType, "x", "dialogue"));   // 미지원 viewId

            var issues = new List<string>();
            sheetType.GetMethod("CollectValidationIssues").Invoke(sheet, new object[] { issues });

            Assert.That(issues.Exists(s => s.Contains("중복")), Is.True, "중복 key 검출");
            Assert.That(issues.Exists(s => s.Contains("빈 key")), Is.True, "빈 key 검출");
            Assert.That(issues.Exists(s => s.Contains("viewId")), Is.True, "미지원 viewId 검출");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    private static ScriptableObject MakeSheet(Type sheetType, int zoneId, string battleKey)
    {
        ScriptableObject sheet = ScriptableObject.CreateInstance(sheetType);
        sheetType.GetField("zoneId", AllInstance).SetValue(sheet, zoneId);
        sheetType.GetField("battleKey", AllInstance).SetValue(sheet, battleKey);
        return sheet;
    }

    private static object MakeEntry(Type entryType, string key, string viewId)
    {
        object e = Activator.CreateInstance(entryType);
        entryType.GetField("key").SetValue(e, key);
        entryType.GetField("viewId").SetValue(e, viewId);
        return e;
    }

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
