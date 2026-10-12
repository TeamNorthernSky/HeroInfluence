using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 근접 접근/복귀 이동 효과음(키 결정 + PlaySfxAction). Assembly-CSharp 런타임 타입은 reflection으로 접근한다.
/// </summary>
public class MovementSfxTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
    private readonly List<string> _calls = new List<string>();
    private FieldInfo _sinkField;
    private object _previousSink;

    private static Type ResolverType => FindType("ASB.Work.Battle.Sequence.MovementSfxKeyResolver");
    private static Type ActionType => FindType("ASB.Work.Battle.Sequence.PlaySfxAction");

    [SetUp]
    public void SetUp()
    {
        _sinkField = FindType("PresentationSfxPlayer").GetField(
            "SinkOverride", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(_sinkField, Is.Not.Null);
        _previousSink = _sinkField.GetValue(null);
        _sinkField.SetValue(null, new Action<string>(key => _calls.Add(key)));
    }

    [TearDown]
    public void TearDown()
    {
        if (_sinkField != null) _sinkField.SetValue(null, _previousSink);
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
        _calls.Clear();
    }

    [Test]
    public void ResolveApproach_PhaseKeyWinsOverUnitKey()
    {
        Component movement = CreateMovement(approachKey: "Unit_Approach", returnKey: "Unit_Return");

        Assert.That(ResolveApproach(CreatePhase("MovePhase", " Skill_Approach "), movement),
            Is.EqualTo("Skill_Approach"));
        Assert.That(ResolveReturn(CreatePhase("ReturnPhase", "Skill_Return"), movement),
            Is.EqualTo("Skill_Return"));
    }

    [Test]
    public void Resolve_FallsBackToUnitKeyWhenPhaseKeyEmptyOrPhaseMissing()
    {
        Component movement = CreateMovement(approachKey: " Unit_Approach ", returnKey: "Unit_Return");

        Assert.That(ResolveApproach(CreatePhase("MovePhase", "  "), movement), Is.EqualTo("Unit_Approach"));
        Assert.That(ResolveApproach(null, movement), Is.EqualTo("Unit_Approach"));
        Assert.That(ResolveReturn(CreatePhase("ReturnPhase", null), movement), Is.EqualTo("Unit_Return"));
        Assert.That(ResolveReturn(null, movement), Is.EqualTo("Unit_Return"));
    }

    [Test]
    public void Resolve_EmptyWhenNoKeyAnywhere()
    {
        Component movement = CreateMovement(approachKey: null, returnKey: "");

        Assert.That(ResolveApproach(CreatePhase("MovePhase", null), movement), Is.Empty);
        Assert.That(ResolveReturn(CreatePhase("ReturnPhase", null), movement), Is.Empty);
        Assert.That(ResolveApproach(null, null), Is.Empty);
        Assert.That(ResolveReturn(null, null), Is.Empty);
    }

    [Test]
    public void PlaySfxAction_PlaysNormalizedKeyOnce()
    {
        RunToEnd(CreateAction(" Move_Dash01 "));

        Assert.That(_calls, Is.EqualTo(new[] { "Move_Dash01" }));
    }

    [Test]
    public void PlaySfxAction_EmptyKeyPlaysNothing()
    {
        RunToEnd(CreateAction("   "));
        RunToEnd(CreateAction(null));

        Assert.That(_calls, Is.Empty);
    }

    private static string ResolveApproach(object movePhase, Component movement)
    {
        return (string)ResolverType.GetMethod("ResolveApproach").Invoke(null, new[] { movePhase, (object)movement });
    }

    private static string ResolveReturn(object returnPhase, Component movement)
    {
        return (string)ResolverType.GetMethod("ResolveReturn").Invoke(null, new[] { returnPhase, (object)movement });
    }

    private static object CreatePhase(string typeName, string sfxKey)
    {
        object phase = Activator.CreateInstance(FindType(typeName));
        phase.GetType().GetField("SfxKey").SetValue(phase, sfxKey);
        return phase;
    }

    private Component CreateMovement(string approachKey, string returnKey)
    {
        GameObject go = Track(new GameObject("MovementSfxTest"));
        Component movement = go.AddComponent(FindType("UnitMovementProfile"));
        movement.GetType().GetField("ApproachSfxKey").SetValue(movement, approachKey);
        movement.GetType().GetField("ReturnSfxKey").SetValue(movement, returnKey);
        return movement;
    }

    private static object CreateAction(string key)
    {
        return Activator.CreateInstance(ActionType, key);
    }

    private static void RunToEnd(object action)
    {
        var routine = (IEnumerator)ActionType.GetMethod("ExecuteRoutine").Invoke(action, new object[] { null });
        int guard = 0;
        while (routine.MoveNext())
            Assert.That(++guard, Is.LessThan(100), "PlaySfxAction이 종료되지 않습니다.");
    }

    private static Type FindType(string name)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, $"타입 '{name}'을 찾지 못했습니다.");
        return type;
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        _created.Add(value);
        return value;
    }
}
