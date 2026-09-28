using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// 지그 프리뷰의 Hold 마커 판정(JigHoldPreview)과, Hold 중 이펙트 수명 누적(JigCuePreview.DriveOnly)을 고정한다.
/// Hold 판정 경계는 런타임 SkillPresentationDirector 재생 루프와 같아야 한다.
///
/// 지그 코드는 Assembly-CSharp-Editor, 마커는 Assembly-CSharp에 있어 리플렉션으로 접근한다.
/// </summary>
public sealed class PresentationJigHoldPreviewTests
{
    private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

    private static Type HoldPreviewType => FindType("ASB.Work.EditorTools.Jig.JigHoldPreview");
    private static Type CuePreviewType => FindType("ASB.Work.EditorTools.Jig.JigCuePreview");
    private static Type HoldMarkerType => FindType("PresentationHoldMarker");

    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [TearDown]
    public void TearDown()
    {
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) UnityEngine.Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    // ── FindCrossedHold ─────────────────────────────────────────

    [Test]
    public void FindCrossedHold_UsesRuntimeBoundaries()
    {
        var holds = Holds((0.5d, 0.3f));

        Assert.That(Find(holds, new bool[1], 0.4d, 0.6d), Is.EqualTo(0), "구간을 지나는 tick에 발동해야 합니다.");
        Assert.That(Find(holds, new bool[1], 0.4d, 0.5d), Is.EqualTo(0), "구간 끝(now==마커)은 포함해야 합니다.");
        Assert.That(Find(holds, new bool[1], 0.5d, 0.7d), Is.EqualTo(-1), "구간 시작(prev==마커)은 제외해야 합니다.");
        Assert.That(Find(holds, new bool[1], 0.2d, 0.4d), Is.EqualTo(-1), "아직 도달하지 않았으면 발동하지 않습니다.");
        Assert.That(Find(holds, new bool[1], 0.6d, 0.8d), Is.EqualTo(-1), "이미 지난 마커는 발동하지 않습니다.");
    }

    [Test]
    public void FindCrossedHold_MarkerAtRangeStart_NeverFires()
    {
        // 런타임은 holdPrevTime = 구간 시작으로 출발하므로, 시작 시각과 같은 Hold는 발동하지 않는다.
        var holds = Holds((0d, 0.5f));
        Assert.That(Find(holds, new bool[1], 0d, 0.1d), Is.EqualTo(-1));
    }

    [Test]
    public void FindCrossedHold_ReturnsEarliest_ThenSkipsConsumed()
    {
        var holds = Holds((0.3d, 0.2f), (0.4d, 0.2f));
        var consumed = new bool[2];

        Assert.That(Find(holds, consumed, 0.2d, 0.5d), Is.EqualTo(0), "한 tick에 둘이 걸리면 앞의 것부터 발동해야 합니다.");
        consumed[0] = true;
        Assert.That(Find(holds, consumed, 0.2d, 0.5d), Is.EqualTo(1), "소비된 Hold는 건너뛰어야 합니다.");
        consumed[1] = true;
        Assert.That(Find(holds, consumed, 0.2d, 0.5d), Is.EqualTo(-1));
    }

    // ── CollectHolds ────────────────────────────────────────────

    [Test]
    public void CollectHolds_ExcludesZeroDuration_AndSortsByTime()
    {
        TimelineAsset timeline = NewTimeline();
        CreateHoldMarker(timeline, 0.8d, 0.2f);
        CreateHoldMarker(timeline, 0.2d, 0.5f);
        CreateHoldMarker(timeline, 0.5d, 0f);   // 정지 없음 → 제외

        var holds = (List<KeyValuePair<double, float>>)HoldPreviewType.GetMethod("CollectHolds", AnyStatic)
            .Invoke(null, new object[] { timeline });

        Assert.That(holds.Count, Is.EqualTo(2));
        Assert.That(holds[0].Key, Is.EqualTo(0.2d).Within(1e-9));
        Assert.That(holds[0].Value, Is.EqualTo(0.5f).Within(1e-6f));
        Assert.That(holds[1].Key, Is.EqualTo(0.8d).Within(1e-9));
    }

    [Test]
    public void CollectHolds_NullOrNoMarkerTrack_ReturnsEmpty()
    {
        MethodInfo collect = HoldPreviewType.GetMethod("CollectHolds", AnyStatic);
        Assert.That(((ICollection)collect.Invoke(null, new object[] { null })).Count, Is.EqualTo(0));

        TimelineAsset noMarkers = Track(ScriptableObject.CreateInstance<TimelineAsset>());
        Assert.That(((ICollection)collect.Invoke(null, new object[] { noMarkers })).Count, Is.EqualTo(0));
    }

    // ── 이펙트 수명 누적(Hold 중 DriveOnly) ─────────────────────

    [Test]
    public void EffectAge_AccumulatesAcrossDriveOnlyAndAdvance_ThenReaps()
    {
        // 사용자가 연 지그의 Cue 목록을 건드리지 않도록 보관 후 복원한다.
        IList fires = (IList)CuePreviewType.GetField("_fires", AnyStatic).GetValue(null);
        var saved = fires.Cast<object>().ToList();
        MethodInfo setFires = CuePreviewType.GetMethod("SetFires", AnyStatic);
        Type fireType = FindType("ASB.Work.EditorTools.Jig.JigCueFire");
        Type fireListType = typeof(List<>).MakeGenericType(fireType);

        try
        {
            setFires.Invoke(null, new[] { Activator.CreateInstance(fireListType) });   // 발화 없음 + 스폰물 정리

            var go = new GameObject("JigHoldPreviewTest_Fx") { hideFlags = HideFlags.HideAndDontSave };
            CuePreviewType.GetMethod("AddSpawnedForTest", AnyStatic).Invoke(null, new object[] { go });
            Assert.That(SpawnedCount(), Is.EqualTo(1));

            DriveOnly(0.1d);
            DriveOnly(0.1d);
            Assert.That(AgeOf(0), Is.EqualTo(0.2d).Within(1e-9), "Hold 중(DriveOnly)에도 나이가 누적되어야 합니다.");

            Advance(1.0d, 1.02d);
            Assert.That(AgeOf(0), Is.EqualTo(0.22d).Within(1e-9),
                "일반 진행(Advance)도 Timeline 시각이 아니라 dt 합으로 누적되어야 합니다.");

            // 파티클이 없는(IsAlive=false) 이펙트는 유예(0.25s)를 넘기면 정리된다.
            DriveOnly(0.05d);
            Assert.That(SpawnedCount(), Is.EqualTo(0), "유예를 넘긴 죽은 이펙트는 정리되어야 합니다.");
            Assert.That(go == null, Is.True, "정리 시 GameObject가 파괴되어야 합니다.");
        }
        finally
        {
            var restore = (IList)Activator.CreateInstance(fireListType);
            foreach (object f in saved) restore.Add(f);
            setFires.Invoke(null, new object[] { restore });
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────

    private static List<KeyValuePair<double, float>> Holds(params (double time, float duration)[] items)
    {
        return items.Select(i => new KeyValuePair<double, float>(i.time, i.duration)).ToList();
    }

    private static int Find(List<KeyValuePair<double, float>> holds, bool[] consumed, double prev, double now)
    {
        return (int)HoldPreviewType.GetMethod("FindCrossedHold", AnyStatic)
            .Invoke(null, new object[] { holds, consumed, prev, now });
    }

    private static void DriveOnly(double dt) =>
        CuePreviewType.GetMethod("DriveOnly", AnyStatic).Invoke(null, new object[] { dt });

    private static void Advance(double prev, double now) =>
        CuePreviewType.GetMethod("Advance", AnyStatic).Invoke(null, new object[] { prev, now });

    private static int SpawnedCount() =>
        (int)CuePreviewType.GetProperty("SpawnedCountForTest", AnyStatic).GetValue(null);

    private static double AgeOf(int index) =>
        (double)CuePreviewType.GetMethod("AgeOfSpawnedForTest", AnyStatic).Invoke(null, new object[] { index });

    private TimelineAsset NewTimeline()
    {
        TimelineAsset timeline = Track(ScriptableObject.CreateInstance<TimelineAsset>());
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 2d;
        timeline.CreateMarkerTrack();
        return timeline;
    }

    private static void CreateHoldMarker(TimelineAsset timeline, double time, float duration)
    {
        MethodInfo create = typeof(TrackAsset).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m => m.Name == "CreateMarker" && !m.IsGenericMethod
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(Type));
        object marker = create.Invoke(timeline.markerTrack, new object[] { HoldMarkerType, time });
        HoldMarkerType.GetMethod("Configure").Invoke(marker, new object[] { duration });
    }

    private static Type FindType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullName, false))
            .FirstOrDefault(t => t != null);
        Assert.That(type, Is.Not.Null, $"타입 '{fullName}'을 찾지 못했습니다.");
        return type;
    }

    private T Track<T>(T value) where T : UnityEngine.Object
    {
        _created.Add(value);
        return value;
    }
}
