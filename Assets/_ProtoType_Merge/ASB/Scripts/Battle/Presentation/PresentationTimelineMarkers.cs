using System;
using System.Collections.Generic;
using UnityEngine.Timeline;

/// <summary>
/// 기본 Markers 트랙과 Presentation 전용 트랙의 마커를 함께 읽는다.
///
/// 제네릭 메서드·반복자(yield)를 쓰지 않는다. Mono는 System.Object 직계 클래스(static 클래스,
/// 컴파일러가 만드는 반복자 클래스 포함)가 공유 제네릭 정보를 처음 쓸 때 부모 템플릿을
/// 제곱 시간으로 복사해 에디터에서 클래스마다 약 0.13초 멈춘다(첫 시전 Timeline 건너뜀의 원인).
/// 마커 종류는 Type으로 받는다.
/// </summary>
public static class PresentationTimelineMarkers
{
    /// <summary>
    /// <paramref name="markerType"/>에 해당하는 마커를 <paramref name="results"/>에 담는다(기존 내용은 지움).
    /// 순서: 기본 Markers 트랙 → 루트 트랙 순서대로 PresentationMarkerTrack(하위 트랙 포함).
    /// </summary>
    public static void Collect(TimelineAsset timeline, Type markerType, List<IMarker> results)
    {
        results.Clear();
        if (timeline == null || markerType == null) return;

        TrackAsset defaultTrack = timeline.markerTrack;
        if (defaultTrack != null)
            AddMatching(defaultTrack, markerType, results);

        int rootCount = timeline.rootTrackCount;
        for (int i = 0; i < rootCount; i++)
            CollectInTrack(timeline.GetRootTrack(i), defaultTrack, markerType, results);
    }

    /// <summary><paramref name="markerType"/> 마커가 하나라도 있으면 true(찾는 즉시 멈춘다).</summary>
    public static bool Contains(TimelineAsset timeline, Type markerType)
    {
        if (timeline == null || markerType == null) return false;

        TrackAsset defaultTrack = timeline.markerTrack;
        if (defaultTrack != null && HasMatching(defaultTrack, markerType)) return true;

        int rootCount = timeline.rootTrackCount;
        for (int i = 0; i < rootCount; i++)
            if (ContainsInTrack(timeline.GetRootTrack(i), defaultTrack, markerType)) return true;
        return false;
    }

    private static void CollectInTrack(TrackAsset track, TrackAsset defaultTrack, Type markerType,
        List<IMarker> results)
    {
        if (track == null || track == defaultTrack) return;

        if (track is PresentationMarkerTrack)
            AddMatching(track, markerType, results);

        foreach (TrackAsset child in track.GetChildTracks())
            CollectInTrack(child, defaultTrack, markerType, results);
    }

    private static bool ContainsInTrack(TrackAsset track, TrackAsset defaultTrack, Type markerType)
    {
        if (track == null || track == defaultTrack) return false;

        if (track is PresentationMarkerTrack && HasMatching(track, markerType)) return true;

        foreach (TrackAsset child in track.GetChildTracks())
            if (ContainsInTrack(child, defaultTrack, markerType)) return true;
        return false;
    }

    private static void AddMatching(TrackAsset track, Type markerType, List<IMarker> results)
    {
        int count = track.GetMarkerCount();
        for (int i = 0; i < count; i++)
        {
            IMarker marker = track.GetMarker(i);
            if (markerType.IsInstanceOfType(marker)) results.Add(marker);
        }
    }

    private static bool HasMatching(TrackAsset track, Type markerType)
    {
        int count = track.GetMarkerCount();
        for (int i = 0; i < count; i++)
            if (markerType.IsInstanceOfType(track.GetMarker(i))) return true;
        return false;
    }
}
