using System;
using System.Collections.Generic;
using UnityEngine.Timeline;

/// <summary>Timeline 구간의 절대 시간 범위.</summary>
public readonly struct PresentationTimelineRange
{
    public PresentationTimelineRange(string sectionId, double start, double end)
    {
        SectionId = sectionId ?? string.Empty;
        Start = start;
        End = end;
    }

    public string SectionId { get; }
    public double Start { get; }
    public double End { get; }
    public double Duration => Math.Max(0d, End - Start);
    public bool IsValid => End > Start;

    public static PresentationTimelineRange Full(TimelineAsset timeline)
    {
        return new PresentationTimelineRange(string.Empty, 0d, timeline != null ? timeline.duration : 0d);
    }
}

/// <summary>
/// Timeline Marker에서 이름 기반 구간을 해석한다. 시간의 원본은 Marker이며 SkillPresentationData에
/// 시작/종료 시간을 복제하지 않는다.
/// </summary>
public static class PresentationTimelineSections
{
    public static bool TryResolve(TimelineAsset timeline, string sectionId,
        out PresentationTimelineRange range, out string error)
    {
        range = default;
        error = null;

        if (timeline == null)
        {
            error = "Timeline이 없습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sectionId))
        {
            range = PresentationTimelineRange.Full(timeline);
            return range.IsValid;
        }

        MarkerTrack markerTrack = timeline.markerTrack;
        if (markerTrack == null)
        {
            error = $"Section '{sectionId}'을 찾을 Marker Track이 없습니다.";
            return false;
        }

        string normalized = sectionId.Trim();
        double? start = null;
        double? end = null;
        foreach (IMarker marker in markerTrack.GetMarkers())
        {
            if (!(marker is PresentationSectionMarker section)
                || !string.Equals(section.SectionId, normalized, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (section.Boundary == PresentationSectionBoundary.Start)
            {
                if (start.HasValue)
                {
                    error = $"Section '{normalized}'의 Start Marker가 중복되었습니다.";
                    return false;
                }
                start = marker.time;
            }
            else
            {
                if (end.HasValue)
                {
                    error = $"Section '{normalized}'의 End Marker가 중복되었습니다.";
                    return false;
                }
                end = marker.time;
            }
        }

        if (!start.HasValue || !end.HasValue)
        {
            error = $"Section '{normalized}'에는 Start와 End Marker가 모두 필요합니다.";
            return false;
        }

        range = new PresentationTimelineRange(normalized, start.Value, end.Value);
        if (!range.IsValid)
        {
            error = $"Section '{normalized}'의 End는 Start보다 뒤여야 합니다.";
            range = default;
            return false;
        }

        if (range.Start < 0d || range.End > timeline.duration + 0.0001d)
        {
            error = $"Section '{normalized}' 범위가 Timeline 길이를 벗어났습니다.";
            range = default;
            return false;
        }

        return true;
    }

    public static void CollectSectionIds(TimelineAsset timeline, List<string> destination)
    {
        if (destination == null) throw new ArgumentNullException(nameof(destination));
        destination.Clear();
        if (timeline == null || timeline.markerTrack == null) return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IMarker marker in timeline.markerTrack.GetMarkers())
        {
            if (!(marker is PresentationSectionMarker section)
                || string.IsNullOrWhiteSpace(section.SectionId)
                || !seen.Add(section.SectionId))
            {
                continue;
            }
            destination.Add(section.SectionId);
        }
        destination.Sort(StringComparer.OrdinalIgnoreCase);
    }
}
