using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// 프로젝트 런타임은 Assembly-CSharp에 있어 asmdef 테스트가 직접 참조할 수 없다.
/// PresentationTimelineSectionTests와 동일하게 프로젝트 타입만 reflection으로 접근한다.
/// SpeedRegion 구간 속도 조회(PresentationTimelineSpeed.SpeedAt) 검증.
/// </summary>
public class PresentationTimelineSpeedTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    private static Type SpeedMarkerType => FindRuntimeType("PresentationSpeedMarker");
    private static Type SpeedType => FindRuntimeType("PresentationTimelineSpeed");

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void SpeedAt_NullTimeline_ReturnsDefault()
    {
        Assert.That(SpeedAt(null, 0.5d), Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void SpeedAt_NoMarkerTrack_ReturnsDefault()
    {
        TimelineAsset timeline = Track(ScriptableObject.CreateInstance<TimelineAsset>());
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 1d;
        // markerTrack 생성 안 함
        Assert.That(SpeedAt(timeline, 0.5d), Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void SpeedAt_NoSpeedMarkers_ReturnsDefault()
    {
        TimelineAsset timeline = NewTimeline();
        Assert.That(SpeedAt(timeline, 0.5d), Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void SpeedAt_BeforeFirstMarker_ReturnsDefault()
    {
        TimelineAsset timeline = NewTimeline();
        CreateSpeedMarker(timeline, 0.5d, 0.25f);
        Assert.That(SpeedAt(timeline, 0.2d), Is.EqualTo(1f).Within(1e-5f));
    }

    [Test]
    public void SpeedAt_ExactMarkerTime_ReturnsMarkerSpeed()
    {
        TimelineAsset timeline = NewTimeline();
        CreateSpeedMarker(timeline, 0.5d, 0.25f);
        Assert.That(SpeedAt(timeline, 0.5d), Is.EqualTo(0.25f).Within(1e-5f));
    }

    [Test]
    public void SpeedAt_JustBeforeAndAfter_SwitchesAtBoundary()
    {
        TimelineAsset timeline = NewTimeline();
        CreateSpeedMarker(timeline, 0d, 0.25f);
        CreateSpeedMarker(timeline, 1.0d, 0.5f);

        Assert.That(SpeedAt(timeline, 0.999d), Is.EqualTo(0.25f).Within(1e-5f), "경계 직전");
        Assert.That(SpeedAt(timeline, 1.001d), Is.EqualTo(0.5f).Within(1e-5f), "경계 직후");
    }

    [Test]
    public void SpeedAt_NonChronologicalCreation_PicksLatestByTime()
    {
        TimelineAsset timeline = NewTimeline();
        // 생성 순서를 시간순과 반대로
        CreateSpeedMarker(timeline, 1.0d, 0.5f);
        CreateSpeedMarker(timeline, 0.2d, 0.25f);

        Assert.That(SpeedAt(timeline, 1.5d), Is.EqualTo(0.5f).Within(1e-5f));
        Assert.That(SpeedAt(timeline, 0.5d), Is.EqualTo(0.25f).Within(1e-5f));
    }

    [Test]
    public void SpeedAt_SanitizesNaNInfinityAndClampsRange()
    {
        TimelineAsset nan = NewTimeline();
        CreateSpeedMarker(nan, 0d, float.NaN);
        Assert.That(SpeedAt(nan, 0.5d), Is.EqualTo(1f).Within(1e-5f), "NaN→1.0");

        TimelineAsset inf = NewTimeline();
        CreateSpeedMarker(inf, 0d, float.PositiveInfinity);
        Assert.That(SpeedAt(inf, 0.5d), Is.EqualTo(1f).Within(1e-5f), "Infinity→1.0");

        TimelineAsset low = NewTimeline();
        CreateSpeedMarker(low, 0d, -5f);
        Assert.That(SpeedAt(low, 0.5d), Is.EqualTo(0.01f).Within(1e-5f), "음수→MinSpeed clamp");

        TimelineAsset zero = NewTimeline();
        CreateSpeedMarker(zero, 0d, 0f);
        Assert.That(SpeedAt(zero, 0.5d), Is.EqualTo(0.01f).Within(1e-5f), "0→MinSpeed clamp");

        TimelineAsset high = NewTimeline();
        CreateSpeedMarker(high, 0d, 100f);
        Assert.That(SpeedAt(high, 0.5d), Is.EqualTo(8f).Within(1e-5f), ">Max→MaxSpeed clamp");
    }

    [Test]
    public void SpeedAt_MultipleRegions_InheritsPreviousUntilNext()
    {
        TimelineAsset timeline = NewTimeline();
        CreateSpeedMarker(timeline, 0.0d, 0.25f);
        CreateSpeedMarker(timeline, 1.0d, 0.5f);
        CreateSpeedMarker(timeline, 2.0d, 1.0f);

        Assert.That(SpeedAt(timeline, 0.5d), Is.EqualTo(0.25f).Within(1e-5f));
        Assert.That(SpeedAt(timeline, 1.5d), Is.EqualTo(0.5f).Within(1e-5f));
        Assert.That(SpeedAt(timeline, 2.5d), Is.EqualTo(1.0f).Within(1e-5f), "Section 중간재생도 이전 마커 상속");
    }

    private float SpeedAt(TimelineAsset timeline, double time)
    {
        object r = SpeedType.GetMethod("SpeedAt").Invoke(null, new object[] { timeline, time });
        return (float)r;
    }

    private TimelineAsset NewTimeline()
    {
        TimelineAsset timeline = Track(ScriptableObject.CreateInstance<TimelineAsset>());
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 3d;
        timeline.CreateMarkerTrack();
        return timeline;
    }

    private static void CreateSpeedMarker(TimelineAsset timeline, double time, float speed)
    {
        MethodInfo create = typeof(TrackAsset).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m => m.Name == "CreateMarker" && !m.IsGenericMethod
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(Type));
        object marker = create.Invoke(timeline.markerTrack, new object[] { SpeedMarkerType, time });
        SpeedMarkerType.GetMethod("Configure").Invoke(marker, new object[] { speed });
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
