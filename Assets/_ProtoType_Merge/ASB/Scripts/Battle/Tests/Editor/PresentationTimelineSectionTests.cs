using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// 프로젝트 런타임은 Assembly-CSharp에 있어 asmdef 테스트가 직접 참조할 수 없다.
/// 기존 Presentation 테스트와 동일하게 프로젝트 타입만 reflection으로 접근한다.
/// </summary>
public class PresentationTimelineSectionTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    private static Type DataType => FindRuntimeType("SkillPresentationData");
    private static Type RailType => FindRuntimeType("AnimationRail");
    private static Type SectionMarkerType => FindRuntimeType("PresentationSectionMarker");
    private static Type SectionBoundaryType => FindRuntimeType("PresentationSectionBoundary");
    private static Type SectionsType => FindRuntimeType("PresentationTimelineSections");

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void SchemaAndAnimationRail_AreIndependentAxes()
    {
        ScriptableObject data = Track(ScriptableObject.CreateInstance(DataType));
        FieldInfo schema = DataType.GetField("PresentationSchemaVersion");
        FieldInfo rail = DataType.GetField("AnimationRail");
        PropertyInfo isPhase = DataType.GetProperty("IsPhaseCue");
        PropertyInfo isTimeline = DataType.GetProperty("IsTimelineRail");

        schema.SetValue(data, 0);
        rail.SetValue(data, Enum.Parse(RailType, "Timeline"));
        Assert.That((bool)isPhase.GetValue(data), Is.False);
        Assert.That((bool)isTimeline.GetValue(data), Is.True);

        schema.SetValue(data, 1);
        rail.SetValue(data, Enum.Parse(RailType, "Animator"));
        Assert.That((bool)isPhase.GetValue(data), Is.True);
        Assert.That((bool)isTimeline.GetValue(data), Is.False);
    }

    [Test]
    public void TryResolve_UsesTimelineMarkersAsOnlyTimeSource()
    {
        TimelineAsset timeline = NewTimeline();
        CreateSectionMarker(timeline, 0.25d, "Attack", "Start");
        CreateSectionMarker(timeline, 0.9d, "Attack", "End");

        object[] args = { timeline, "attack", null, null };
        bool ok = (bool)SectionsType.GetMethod("TryResolve").Invoke(null, args);

        Assert.That(ok, Is.True, args[3] as string);
        object range = args[2];
        Assert.That((double)range.GetType().GetProperty("Start").GetValue(range),
            Is.EqualTo(0.25d).Within(0.0001d));
        Assert.That((double)range.GetType().GetProperty("End").GetValue(range),
            Is.EqualTo(0.9d).Within(0.0001d));
    }

    [Test]
    public void TryResolve_RejectsDuplicateBoundary()
    {
        TimelineAsset timeline = NewTimeline();
        CreateSectionMarker(timeline, 0d, "Attack", "Start");
        CreateSectionMarker(timeline, 0.1d, "Attack", "Start");
        CreateSectionMarker(timeline, 0.5d, "Attack", "End");

        object[] args = { timeline, "Attack", null, null };
        bool ok = (bool)SectionsType.GetMethod("TryResolve").Invoke(null, args);

        Assert.That(ok, Is.False);
        StringAssert.Contains("중복", args[3] as string);
    }

    [Test]
    public void CollectSectionIds_IsUniqueAndCaseInsensitive()
    {
        TimelineAsset timeline = NewTimeline();
        CreateSectionMarker(timeline, 0d, "Attack", "Start");
        CreateSectionMarker(timeline, 0.5d, "attack", "End");

        var ids = new List<string>();
        SectionsType.GetMethod("CollectSectionIds").Invoke(null, new object[] { timeline, ids });

        Assert.That(ids, Has.Count.EqualTo(1));
        Assert.That(ids[0], Is.EqualTo("Attack"));
    }

    private TimelineAsset NewTimeline()
    {
        TimelineAsset timeline = Track(ScriptableObject.CreateInstance<TimelineAsset>());
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 1d;
        timeline.CreateMarkerTrack();
        return timeline;
    }

    private static void CreateSectionMarker(TimelineAsset timeline, double time, string id, string boundary)
    {
        MethodInfo create = typeof(TrackAsset).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m => m.Name == "CreateMarker" && !m.IsGenericMethod
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(Type));
        object marker = create.Invoke(timeline.markerTrack, new object[] { SectionMarkerType, time });
        SectionMarkerType.GetMethod("Configure")
            .Invoke(marker, new[] { id, Enum.Parse(SectionBoundaryType, boundary) });
    }

    private static Type FindRuntimeType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, false))
            .FirstOrDefault(t => t != null);
        Assert.That(type, Is.Not.Null, $"런타임 타입 '{fullName}'을 찾지 못했습니다.");
        return type;
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        _created.Add(value);
        return value;
    }
}
