using System.Collections.Generic;
using UnityEngine.Timeline;

/// <summary>
/// 에디터 도구용 마커 조회(제네릭 편의 함수).
/// 런타임 쪽 <see cref="PresentationTimelineMarkers"/>는 Mono 첫 컴파일 지연을 피하려고 제네릭을 쓰지 않는다.
/// 에디터 도구는 전투 중에 돌지 않으므로 여기서만 제네릭을 쓴다. 런타임 코드에서는 쓰지 말 것.
/// </summary>
public static class PresentationTimelineMarkersEditor
{
    /// <summary>순서는 <see cref="PresentationTimelineMarkers.Collect"/>와 같다.</summary>
    public static List<T> OfType<T>(TimelineAsset timeline) where T : class, IMarker
    {
        var found = new List<IMarker>();
        PresentationTimelineMarkers.Collect(timeline, typeof(T), found);
        var result = new List<T>(found.Count);
        foreach (IMarker marker in found) result.Add((T)marker);
        return result;
    }
}
