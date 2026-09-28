using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// 런타임 코드는 Assembly-CSharp에 있고 이 asmdef 테스트 어셈블리는 직접 참조할 수 없으므로
/// 프로젝트의 다른 프레젠테이션 계약 테스트와 동일하게 리플렉션으로 검증합니다.
/// </summary>
public sealed class EnemySkillExecutionPreparerTests
{
    private static Type SkillDataType => FindRuntimeType("SkillData");
    private static Type PreparerType => FindRuntimeType("EnemySkillExecutionPreparer");

    [Test]
    public void Prepare_FillsEnemyFallbackWithoutMutatingSource()
    {
        object source = NewSkill();
        Set(source, "skillIndex", 200011);
        Set(source, "skillKey", "FV20001_1");
        Set(source, "AnimationTrigger", string.Empty);
        Set(source, "TargetAnimationTrigger", string.Empty);
        Set(source, "HitDelay", 0f);
        Set(source, "TotalDelay", 0f);
        Set(source, "UseAnimEvent", true);
        Set(source, "boundary", null);

        object prepared = Prepare(source);

        Assert.That(prepared, Is.Not.Null.And.Not.SameAs(source));
        Assert.That(Get<string>(prepared, "AnimationTrigger"), Is.EqualTo("Attack"));
        Assert.That(Get<string>(prepared, "TargetAnimationTrigger"), Is.EqualTo("Hit"));
        Assert.That(Get<float>(prepared, "HitDelay"), Is.EqualTo(0.25f));
        Assert.That(Get<float>(prepared, "TotalDelay"), Is.EqualTo(0.5f));
        Assert.That(Get<bool>(prepared, "UseAnimEvent"), Is.False);
        Assert.That((IList)Get<object>(prepared, "boundary"), Is.Empty);

        Assert.That(Get<string>(source, "AnimationTrigger"), Is.Empty);
        Assert.That(Get<string>(source, "TargetAnimationTrigger"), Is.Empty);
        Assert.That(Get<float>(source, "HitDelay"), Is.Zero);
        Assert.That(Get<float>(source, "TotalDelay"), Is.Zero);
        Assert.That(Get<bool>(source, "UseAnimEvent"), Is.True);
        Assert.That(Get<object>(source, "boundary"), Is.Null);
    }

    [Test]
    public void Prepare_PreservesCompleteAnimationTiming()
    {
        object source = NewSkill();
        Set(source, "AnimationTrigger", "DroneAttack");
        Set(source, "TargetAnimationTrigger", "DroneHit");
        Set(source, "HitDelay", 0.4f);
        Set(source, "TotalDelay", 0.9f);
        Set(source, "UseAnimEvent", true);

        object prepared = Prepare(source);

        Assert.That(Get<string>(prepared, "AnimationTrigger"), Is.EqualTo("DroneAttack"));
        Assert.That(Get<string>(prepared, "TargetAnimationTrigger"), Is.EqualTo("DroneHit"));
        Assert.That(Get<float>(prepared, "HitDelay"), Is.EqualTo(0.4f));
        Assert.That(Get<float>(prepared, "TotalDelay"), Is.EqualTo(0.9f));
        Assert.That(Get<bool>(prepared, "UseAnimEvent"), Is.True);
    }

    [Test]
    public void Prepare_ClonesBoundaryList()
    {
        object source = NewSkill();
        Set(source, "AnimationTrigger", "Attack");
        Set(source, "TargetAnimationTrigger", "Hit");
        Set(source, "HitDelay", 0.25f);
        Set(source, "TotalDelay", 0.5f);

        Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(int));
        var sourceBoundary = (IList)Activator.CreateInstance(listType);
        sourceBoundary.Add(1);
        sourceBoundary.Add(2);
        Set(source, "boundary", sourceBoundary);

        object prepared = Prepare(source);
        var preparedBoundary = (IList)Get<object>(prepared, "boundary");
        preparedBoundary[0] = 99;

        Assert.That(sourceBoundary[0], Is.EqualTo(1));
    }

    private static object NewSkill()
    {
        return Activator.CreateInstance(SkillDataType);
    }

    private static object Prepare(object source)
    {
        MethodInfo method = PreparerType.GetMethod(
            "Prepare",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { SkillDataType },
            null);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new[] { source });
    }

    private static void Set(object instance, string fieldName, object value)
    {
        FieldInfo field = SkillDataType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"SkillData.{fieldName} field is missing.");
        field.SetValue(instance, value);
    }

    private static T Get<T>(object instance, string fieldName)
    {
        FieldInfo field = SkillDataType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"SkillData.{fieldName} field is missing.");
        return (T)field.GetValue(instance);
    }

    private static Type FindRuntimeType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }

        throw new InvalidOperationException($"Runtime type '{fullName}' was not found.");
    }
}
