using System.Collections.Generic;
using UnityEngine.Timeline;

/// <summary>기본 Markers 트랙과 Presentation 전용 트랙의 마커를 함께 읽는다.</summary>
public static class PresentationTimelineMarkers
{
    public static IEnumerable<T> OfType<T>(TimelineAsset timeline) where T : class, IMarker
    {
        if (timeline == null) yield break;

        if (timeline.markerTrack != null)
            foreach (IMarker marker in timeline.markerTrack.GetMarkers())
                if (marker is T typed) yield return typed;

        foreach (TrackAsset root in timeline.GetRootTracks())
            foreach (T marker in OfTypeInTrack<T>(root, timeline.markerTrack))
                yield return marker;
    }

    private static IEnumerable<T> OfTypeInTrack<T>(TrackAsset track, TrackAsset defaultTrack)
        where T : class, IMarker
    {
        if (track == null || track == defaultTrack) yield break;

        if (track is PresentationMarkerTrack)
            foreach (IMarker marker in track.GetMarkers())
                if (marker is T typed) yield return typed;

        foreach (TrackAsset child in track.GetChildTracks())
            foreach (T marker in OfTypeInTrack<T>(child, defaultTrack))
                yield return marker;
    }
}
