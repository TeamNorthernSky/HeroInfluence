using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 코드 시점 효과음(Event SFX) 진입점 PresentationSfxEvents. Assembly-CSharp 런타임 타입은 reflection으로 접근한다.
/// </summary>
public class PresentationSfxEventsTests
{
    // 테스트마다 다른 프레임 번호를 써서 정적 중복 제거 상태가 테스트 사이에 새지 않게 한다.
    private static int s_NextFrame = 100000;

    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
    private readonly List<string> _calls = new List<string>();
    private FieldInfo _sinkField;
    private FieldInfo _timedSinkField;
    private FieldInfo _frameField;
    private object _previousSink;
    private object _previousTimedSink;
    private object _previousFrame;
    private int _frame;

    private static Type EventsType => FindType("PresentationSfxEvents");
    private static Type DataType => FindType("SkillPresentationData");
    private static Type EntryType => FindType("PresentationEventSfx");
    private static Type EventEnum => FindType("PresentationSfxEvent");
    private static Type RoleEnum => FindType("ASB.Work.Battle.Core.DamageRole");

    [SetUp]
    public void SetUp()
    {
        Type player = FindType("PresentationSfxPlayer");
        _sinkField = player.GetField("SinkOverride", BindingFlags.NonPublic | BindingFlags.Static);
        _timedSinkField = player.GetField("TimedSinkOverride", BindingFlags.NonPublic | BindingFlags.Static);
        _frameField = EventsType.GetField("FrameOverride", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(_sinkField, Is.Not.Null);
        Assert.That(_timedSinkField, Is.Not.Null);
        Assert.That(_frameField, Is.Not.Null);

        _previousSink = _sinkField.GetValue(null);
        _previousTimedSink = _timedSinkField.GetValue(null);
        _previousFrame = _frameField.GetValue(null);
        _sinkField.SetValue(null, new Action<string>(key => _calls.Add(key)));
        _timedSinkField.SetValue(null, new Action<string, float>((key, seconds) =>
            _calls.Add(key + "@" + seconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))));

        _frame = s_NextFrame;
        s_NextFrame += 100;
        _frameField.SetValue(null, new Func<int>(() => _frame));
    }

    [TearDown]
    public void TearDown()
    {
        if (_sinkField != null) _sinkField.SetValue(null, _previousSink);
        if (_timedSinkField != null) _timedSinkField.SetValue(null, _previousTimedSink);
        if (_frameField != null) _frameField.SetValue(null, _previousFrame);
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
        _calls.Clear();
    }

    [Test]
    public void PlayHit_PlaysEveryHitEntryOnly()
    {
        object data = CreatePresentation(("Hit", "Hit_A", -1), ("Hit", "Hit_B", -1), ("EffectImpact", "Impact_C", -1));

        PlayHit(data, CreateCaster(), "Primary");

        Assert.That(_calls, Is.EqualTo(new[] { "Hit_A", "Hit_B" }));
    }

    [Test]
    public void PlayHit_SameCasterSameFrame_PlaysOnce_NextFramePlaysAgain()
    {
        object data = CreatePresentation(("Hit", "Hit_A", -1));
        Component caster = CreateCaster();

        PlayHit(data, caster, "Primary");
        PlayHit(data, caster, "Primary");
        Assert.That(_calls, Is.EqualTo(new[] { "Hit_A" }));

        _frame++;
        PlayHit(data, caster, "Primary");
        Assert.That(_calls, Is.EqualTo(new[] { "Hit_A", "Hit_A" }));
    }

    [Test]
    public void PlayHit_DifferentCastersSameFrame_PlayEach()
    {
        object data = CreatePresentation(("Hit", "Hit_A", -1));

        PlayHit(data, CreateCaster(), "Primary");
        PlayHit(data, CreateCaster(), "Primary");

        Assert.That(_calls, Is.EqualTo(new[] { "Hit_A", "Hit_A" }));
    }

    [Test]
    public void PlayHit_Additional_UsesAdditionalHitEntry_OrFallsBackToHit()
    {
        object withAdditional = CreatePresentation(("Hit", "Hit_A", -1), ("AdditionalHit", "Chain_B", -1));
        object hitOnly = CreatePresentation(("Hit", "Hit_A", -1));
        Component caster = CreateCaster();

        PlayHit(withAdditional, caster, "Primary");
        PlayHit(withAdditional, caster, "Additional");
        PlayHit(hitOnly, caster, "Additional");

        Assert.That(_calls, Is.EqualTo(new[] { "Hit_A", "Chain_B", "Hit_A" }));
    }

    [Test]
    public void PlayEffectImpact_FiltersByOccurrence()
    {
        object data = CreatePresentation(
            ("EffectImpact", "Every", -1), ("EffectImpact", "First", 0), ("EffectImpact", "Second", 1), ("Hit", "Hit_A", -1));

        PlayEffectImpact(data, 0);
        PlayEffectImpact(data, 1);
        PlayEffectImpact(data, 2);

        Assert.That(_calls, Is.EqualTo(new[] { "Every", "First", "Every", "Second", "Every" }));
    }

    [Test]
    public void PlayEffectImpact_IsNotDeduplicatedWithinFrame()
    {
        object data = CreatePresentation(("EffectImpact", "Impact", -1));

        PlayEffectImpact(data, 0);
        PlayEffectImpact(data, 0);

        Assert.That(_calls, Is.EqualTo(new[] { "Impact", "Impact" }));
    }

    [Test]
    public void EmptyKeyOrNullPresentation_PlaysNothing()
    {
        object data = CreatePresentation(("Hit", "   ", -1), ("EffectImpact", null, -1));

        PlayHit(data, CreateCaster(), "Primary");
        PlayHit(null, CreateCaster(), "Primary");
        PlayEffectImpact(data, 0);
        PlayEffectImpact(null, 0);

        Assert.That(_calls, Is.Empty);
    }

    [Test]
    public void DurationAndKeyWhitespace_AreApplied()
    {
        object data = CreatePresentation(("EffectImpact", " Timed ", -1, 0.5f), ("EffectImpact", " Plain ", -1, 0f));

        PlayEffectImpact(data, 0);

        Assert.That(_calls, Is.EqualTo(new[] { "Timed@0.5", "Plain" }));
    }

    private object CreatePresentation(params (string evt, string key, int occurrence)[] rows)
    {
        return CreatePresentation(rows.Select(row => (row.evt, row.key, row.occurrence, 0f)).ToArray());
    }

    private object CreatePresentation(params (string evt, string key, int occurrence, float duration)[] rows)
    {
        ScriptableObject data = Track(ScriptableObject.CreateInstance(DataType));
        var list = (IList)DataType.GetField("EventSfx").GetValue(data);
        foreach ((string evt, string key, int occurrence, float duration) in rows)
        {
            object entry = Activator.CreateInstance(EntryType);
            EntryType.GetField("Event").SetValue(entry, Enum.Parse(EventEnum, evt));
            EntryType.GetField("Key").SetValue(entry, key);
            EntryType.GetField("Occurrence").SetValue(entry, occurrence);
            EntryType.GetField("DurationSeconds").SetValue(entry, duration);
            list.Add(entry);
        }
        return data;
    }

    private Component CreateCaster()
    {
        GameObject go = Track(new GameObject("EventSfxCaster"));
        return go.AddComponent(FindType("BattleCharactor"));
    }

    private static void PlayHit(object presentation, Component caster, string role)
    {
        EventsType.GetMethod("PlayHit").Invoke(null, new[] { presentation, caster, Enum.Parse(RoleEnum, role) });
    }

    private static void PlayEffectImpact(object presentation, int occurrence)
    {
        EventsType.GetMethod("PlayEffectImpact").Invoke(null, new[] { presentation, (object)occurrence });
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
