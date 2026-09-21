using UnityEngine.Timeline;

/// <summary>
/// Timeline의 <see cref="PresentationSpeedMarker"/>에서 특정 시각의 구간 속도를 조회한다.
///
/// 이벤트가 아니라 <b>데이터 조회</b> 방식이며, 시간 원본은 Marker다(SkillPresentationData에 복제하지 않는다).
/// 인자 <paramref name="time"/>은 director.time·marker.time과 동일한 "절대 Timeline 좌표"다 —
/// Section-상대 오프셋으로 변환해 넘기지 말 것(상속이 깨진다).
///
/// markerTrack이 muted여도 데이터로 읽는다(PresentationTimelineSections와 동일 정책).
/// </summary>
public static class PresentationTimelineSpeed
{
    public const float DefaultSpeed = 1f;

    /// <summary>
    /// <paramref name="time"/> 이전(포함)의 가장 늦은 <see cref="PresentationSpeedMarker"/> 배속.
    /// 해당 마커가 없거나 timeline/markerTrack이 null이면 <see cref="DefaultSpeed"/>(1.0).
    /// </summary>
    public static float SpeedAt(TimelineAsset timeline, double time)
    {
        if (timeline == null) return DefaultSpeed;
        MarkerTrack markerTrack = timeline.markerTrack;
        if (markerTrack == null) return DefaultSpeed;

        bool found = false;
        double bestTime = double.NegativeInfinity;
        float best = DefaultSpeed;

        foreach (IMarker marker in markerTrack.GetMarkers())
        {
            if (!(marker is PresentationSpeedMarker speed)) continue;   // 다른 마커 타입은 무시
            double t = marker.time;
            if (t > time + 1e-9d) continue;                            // 아직 도달하지 않은 마커
            if (!found || t >= bestTime)                               // 시각이 가장 늦은 것 채택
            {
                found = true;
                bestTime = t;
                best = speed.Speed;
            }
        }

        return best;
    }
}
