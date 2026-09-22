using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

public sealed class TimelineMarkerStackTestMarkerA : Marker
{
}

public sealed class TimelineMarkerStackTestMarkerB : Marker
{
}

public class TimelineMarkerStackWindowTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null)
                UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void CollectCollisions_GroupsDifferentMarkerTypesOnSameDisplayedFrame()
    {
        TimelineAsset timeline = Track(ScriptableObject.CreateInstance<TimelineAsset>());
        timeline.editorSettings.frameRate = 60d;
        timeline.CreateMarkerTrack();
        Track(timeline.markerTrack);

        Track(timeline.markerTrack.CreateMarker<TimelineMarkerStackTestMarkerA>(0.1000d) as UnityEngine.Object);
        Track(timeline.markerTrack.CreateMarker<TimelineMarkerStackTestMarkerB>(0.1050d) as UnityEngine.Object);
        Track(timeline.markerTrack.CreateMarker<TimelineMarkerStackTestMarkerA>(0.1200d) as UnityEngine.Object);

        Type utilityType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.FullName == "ASB.Work.EditorTools.Jig.TimelineMarkerStackUtility");
        Assert.That(utilityType, Is.Not.Null, "TimelineMarkerStackUtility를 찾지 못했습니다.");

        MethodInfo collect = utilityType.GetMethod(
            "CollectCollisions", BindingFlags.Public | BindingFlags.Static);
        Assert.That(collect, Is.Not.Null);

        object raw = collect.Invoke(null, new object[] { timeline, 60d });
        List<object> groups = ((IEnumerable)raw).Cast<object>().ToList();

        Assert.That(groups, Has.Count.EqualTo(1));

        object group = groups[0];
        int frame = (int)group.GetType().GetProperty("Frame").GetValue(group);
        var markers = ((IEnumerable)group.GetType().GetProperty("Markers").GetValue(group))
            .Cast<object>()
            .ToList();

        Assert.That(frame, Is.EqualTo(6));
        Assert.That(markers, Has.Count.EqualTo(2));
        Assert.That(markers.Any(m => m is TimelineMarkerStackTestMarkerA), Is.True);
        Assert.That(markers.Any(m => m is TimelineMarkerStackTestMarkerB), Is.True);
    }

    [Test]
    public void ToFrame_MatchesTimelineDisplayedFrameFlooring()
    {
        Type utilityType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.FullName == "ASB.Work.EditorTools.Jig.TimelineMarkerStackUtility");
        MethodInfo toFrame = utilityType?.GetMethod("ToFrame", BindingFlags.Public | BindingFlags.Static);

        Assert.That(toFrame, Is.Not.Null);
        Assert.That((int)toFrame.Invoke(null, new object[] { 0.1000d, 60d }), Is.EqualTo(6));
        Assert.That((int)toFrame.Invoke(null, new object[] { 0.1166d, 60d }), Is.EqualTo(6));
        Assert.That((int)toFrame.Invoke(null, new object[] { 0.1167d, 60d }), Is.EqualTo(7));
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        if (value != null) _created.Add(value);
        return value;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null);
        }
    }
}
