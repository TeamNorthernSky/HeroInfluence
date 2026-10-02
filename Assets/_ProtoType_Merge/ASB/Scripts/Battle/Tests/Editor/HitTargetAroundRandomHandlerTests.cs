using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HitTargetAroundRandomHandlerTests
{
    private GameObject _casterObject;
    private GameObject _targetObject;

    [TearDown]
    public void TearDown()
    {
        if (_casterObject != null) UnityEngine.Object.DestroyImmediate(_casterObject);
        if (_targetObject != null) UnityEngine.Object.DestroyImmediate(_targetObject);
    }

    [Test]
    public void MainAndAdditionalDamage_UseValueAndSubValue()
    {
        object handler = NewHandler(out object caster, out object target);
        object skill = NewSkill("FV20005_2", 200052, 1f, 0.8f);
        object result = NewResult();

        InvokeProtected(handler.GetType().BaseType, handler, "ApplyMainEffect", caster, target, skill, result);
        InvokeProtected(handler.GetType(), handler, "ApplyAdditionaDamage", caster, target, skill, result);

        IList contexts = DamageContexts(result);
        Assert.That(contexts.Count, Is.EqualTo(2));
        FieldInfo value = contexts[0].GetType().GetField("SkillValue");
        Assert.That((float)value.GetValue(contexts[0]), Is.EqualTo(1f));
        Assert.That((float)value.GetValue(contexts[1]), Is.EqualTo(0.8f));
    }

    [Test]
    public void MissingSubValue_WarnsOnceAndFallsBackToValue()
    {
        object handler = NewHandler(out object caster, out object target);
        object skill = NewSkill("FV_TEST_MISSING_SUB", 991234, 0.75f, 0f);
        object result = NewResult();
        int warningCount = 0;
        void CountWarning(string condition, string _, LogType type)
        {
            if (type == LogType.Warning && condition.Contains("Missing skillSubValue") &&
                condition.Contains("991234")) warningCount++;
        }
        Application.logMessageReceived += CountWarning;
        try
        {
            InvokeProtected(handler.GetType(), handler, "ApplyAdditionaDamage", caster, target, skill, result);
            InvokeProtected(handler.GetType(), handler, "ApplyAdditionaDamage", caster, target, skill, result);
        }
        finally
        {
            Application.logMessageReceived -= CountWarning;
        }

        IList contexts = DamageContexts(result);
        FieldInfo value = contexts[0].GetType().GetField("SkillValue");
        Assert.That(contexts.Count, Is.EqualTo(2));
        Assert.That(warningCount, Is.EqualTo(1));
        Assert.That((float)value.GetValue(contexts[0]), Is.EqualTo(0.75f));
        Assert.That((float)value.GetValue(contexts[1]), Is.EqualTo(0.75f));
    }

    private object NewHandler(out object caster, out object target)
    {
        Type characterType = FindType("BattleCharactor");
        _casterObject = new GameObject("DamageRuleCaster");
        _targetObject = new GameObject("DamageRuleTarget");
        caster = _casterObject.AddComponent(characterType);
        target = _targetObject.AddComponent(characterType);
        return Activator.CreateInstance(FindType("ASB.Work.Battle.SkillExecution.HitTargetAroundRandomHandler"));
    }

    private static object NewSkill(string key, int index, float value, float subValue)
    {
        Type type = FindType("SkillData");
        object skill = Activator.CreateInstance(type);
        type.GetField("skillKey").SetValue(skill, key);
        type.GetField("skillIndex").SetValue(skill, index);
        type.GetField("skillValue").SetValue(skill, value);
        type.GetField("skillSubValue").SetValue(skill, subValue);
        return skill;
    }

    private static object NewResult()
    {
        Type type = FindType("ASB.Work.Battle.SkillExecution.SkillExecutionResult");
        return type.GetMethod("SuccessResult", Type.EmptyTypes).Invoke(null, null);
    }

    private static IList DamageContexts(object result)
    {
        return (IList)result.GetType().GetProperty("DamageContexts").GetValue(result);
    }

    private static void InvokeProtected(Type owner, object instance, string name, params object[] args)
    {
        owner.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, args);
    }

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false))
            .FirstOrDefault(found => found != null);
        Assert.That(type, Is.Not.Null, $"타입을 찾지 못했습니다: {name}");
        return type;
    }
}
