using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>
    /// Timeline에서 같은 표시 프레임에 놓인 마커 묶음.
    /// 실제 marker.time은 건드리지 않고 저작 UI에서만 한 묶음으로 보여준다.
    /// </summary>
    public sealed class TimelineMarkerStackGroup
    {
        public int Frame { get; }
        public double FrameTime { get; }
        public IReadOnlyList<IMarker> Markers { get; }

        public TimelineMarkerStackGroup(int frame, double frameTime, List<IMarker> markers)
        {
            Frame = frame;
            FrameTime = frameTime;
            Markers = markers;
        }
    }

    /// <summary>겹친 마커 탐색과 표시 문자열을 한곳에서 관리한다.</summary>
    public static class TimelineMarkerStackUtility
    {
        public static List<TimelineMarkerStackGroup> CollectCollisions(TimelineAsset timeline, double frameRate = 0d)
        {
            var result = new List<TimelineMarkerStackGroup>();
            if (timeline == null) return result;

            double fps = ResolveFrameRate(timeline, frameRate);
            var groups = new Dictionary<int, List<IMarker>>();

            foreach (IMarker marker in EnumerateMarkers(timeline))
            {
                if (marker == null) continue;
                int frame = TimelineMarkerStackUtility.ToFrame(marker.time, fps);
                if (!groups.TryGetValue(frame, out List<IMarker> list))
                {
                    list = new List<IMarker>();
                    groups.Add(frame, list);
                }
                list.Add(marker);
            }

            foreach (KeyValuePair<int, List<IMarker>> pair in groups.OrderBy(x => x.Key))
            {
                if (pair.Value.Count < 2) continue;
                pair.Value.Sort(CompareMarkers);
                result.Add(new TimelineMarkerStackGroup(pair.Key, pair.Key / fps, pair.Value));
            }

            return result;
        }

        public static List<IMarker> GetSameFrameMarkers(IMarker marker)
        {
            var result = new List<IMarker>();
            if (marker == null || marker.parent == null) return result;

            double fps = ResolveFrameRate(marker.parent.timelineAsset, 0d);
            int frame = TimelineMarkerStackUtility.ToFrame(marker.time, fps);
            foreach (IMarker candidate in marker.parent.GetMarkers())
            {
                if (candidate != null && TimelineMarkerStackUtility.ToFrame(candidate.time, fps) == frame)
                    result.Add(candidate);
            }
            result.Sort(CompareMarkers);
            return result;
        }

        public static string Describe(IMarker marker)
        {
            switch (marker)
            {
                case PresentationSignalMarker signal:
                    if (signal.Kind == PresentationSignalKind.Cue)
                    {
                        string cue = string.IsNullOrWhiteSpace(signal.CueName) ? "(이름 없음)" : signal.CueName;
                        return "Signal · Cue · " + cue;
                    }
                    return "Signal · " + signal.Kind;

                case PresentationSpeedMarker speed:
                    return $"Speed · {speed.Speed:0.##}x";

                case PresentationSectionMarker section:
                    return $"Section · {section.SectionId} · {section.Boundary}";

                case CueMarker cue:
                    string phase = string.IsNullOrWhiteSpace(cue.PhaseLabel) ? string.Empty : " · " + cue.PhaseLabel;
                    return "Jig Cue · " + (string.IsNullOrWhiteSpace(cue.CueName) ? "(이름 없음)" : cue.CueName) + phase;

                default:
                    return marker != null ? marker.GetType().Name : "(null)";
            }
        }

        public static Color ColorFor(IMarker marker)
        {
            if (marker is PresentationSignalMarker signal)
            {
                switch (signal.Kind)
                {
                    case PresentationSignalKind.Impact: return new Color(0.95f, 0.30f, 0.28f);
                    case PresentationSignalKind.Projectile: return new Color(1.00f, 0.58f, 0.18f);
                    default: return new Color(0.20f, 0.75f, 0.95f);
                }
            }
            if (marker is PresentationSpeedMarker) return new Color(0.35f, 0.85f, 0.40f);
            if (marker is PresentationSectionMarker section)
                return section.Boundary == PresentationSectionBoundary.Start
                    ? new Color(0.35f, 0.55f, 1.00f)
                    : new Color(0.72f, 0.42f, 0.95f);
            if (marker is CueMarker) return new Color(1.00f, 0.82f, 0.25f);
            return Color.gray;
        }

        private const double FrameRateEpsilon = 1e-6d;
        private const double DefaultFrameRate = 60d;
        private const double MaxTimelineDurationSeconds = 9e6d;

        /// <summary>Unity Timeline 1.7 TimeUtility.ToFrames와 같은 양수 시간 프레임 환산.</summary>
        public static int ToFrame(double time, double frameRate)
        {
            double fps = frameRate > FrameRateEpsilon ? frameRate : DefaultFrameRate;
            double clamped = Math.Min(Math.Max(time, -MaxTimelineDurationSeconds), MaxTimelineDurationSeconds);
            double tolerance = Math.Max(Math.Abs(clamped), 1d) * 1e-9d;
            return clamped < 0d
                ? (int)Math.Ceiling(clamped * fps - tolerance)
                : (int)Math.Floor(clamped * fps + tolerance);
        }

        private static double ResolveFrameRate(TimelineAsset timeline, double requested)
        {
            if (requested > FrameRateEpsilon) return requested;
            if (timeline != null && timeline.editorSettings.frameRate > FrameRateEpsilon)
                return timeline.editorSettings.frameRate;
            return DefaultFrameRate;
        }

        private static IEnumerable<IMarker> EnumerateMarkers(TimelineAsset timeline)
        {
            var seenMarkers = new HashSet<IMarker>();
            var seenTracks = new HashSet<TrackAsset>();

            if (timeline.markerTrack != null)
            {
                foreach (IMarker marker in timeline.markerTrack.GetMarkers())
                    if (marker != null && seenMarkers.Add(marker))
                        yield return marker;
                seenTracks.Add(timeline.markerTrack);
            }

            foreach (TrackAsset root in timeline.GetRootTracks())
            {
                foreach (TrackAsset track in EnumerateTrackTree(root))
                {
                    if (track == null || !seenTracks.Add(track)) continue;
                    foreach (IMarker marker in track.GetMarkers())
                        if (marker != null && seenMarkers.Add(marker))
                            yield return marker;
                }
            }
        }

        private static IEnumerable<TrackAsset> EnumerateTrackTree(TrackAsset track)
        {
            if (track == null) yield break;
            yield return track;
            foreach (TrackAsset child in track.GetChildTracks())
                foreach (TrackAsset nested in EnumerateTrackTree(child))
                    yield return nested;
        }

        private static int CompareMarkers(IMarker a, IMarker b)
        {
            int rank = MarkerRank(a).CompareTo(MarkerRank(b));
            if (rank != 0) return rank;
            int type = string.Compare(a?.GetType().Name, b?.GetType().Name, StringComparison.Ordinal);
            if (type != 0) return type;
            return a.time.CompareTo(b.time);
        }

        private static int MarkerRank(IMarker marker)
        {
            if (marker is PresentationSectionMarker) return 0;
            if (marker is PresentationSignalMarker signal)
            {
                switch (signal.Kind)
                {
                    case PresentationSignalKind.Cue: return 10;
                    case PresentationSignalKind.Projectile: return 11;
                    case PresentationSignalKind.Impact: return 12;
                }
            }
            if (marker is CueMarker) return 20;
            if (marker is PresentationSpeedMarker) return 30;
            return 100;
        }
    }

    /// <summary>
    /// 현재 Timeline의 같은 프레임 마커를 모두 노출하고 개별 선택할 수 있는 도킹 창.
    /// 마커 시간을 이동하지 않으므로 런타임 결과에는 영향을 주지 않는다.
    /// </summary>
    public sealed class TimelineMarkerStackWindow : EditorWindow
    {
        private TimelineAsset _manualTimeline;
        private bool _followTimelineWindow = true;
        private Vector2 _scroll;

        [MenuItem("Window/Sequencing/Timeline Marker Stack")]
        public static void Open()
        {
            GetWindow<TimelineMarkerStackWindow>("Marker Stack");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            TimelineAsset timeline = ResolveTimeline();
            if (timeline == null)
            {
                EditorGUILayout.HelpBox(
                    "Timeline 창에서 Timeline을 열거나, 위의 Follow를 끄고 TimelineAsset을 지정하세요.",
                    MessageType.Info);
                return;
            }

            double fps = timeline.editorSettings.frameRate;
            List<TimelineMarkerStackGroup> groups =
                TimelineMarkerStackUtility.CollectCollisions(timeline, fps);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(timeline.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"{fps:0.###} fps · 겹친 프레임 {groups.Count}개",
                EditorStyles.miniLabel);

            if (groups.Count == 0)
            {
                EditorGUILayout.HelpBox("같은 표시 프레임에 겹친 마커가 없습니다.", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                "마커 시간은 그대로 유지됩니다. '선택'을 누르면 가려진 마커를 Timeline과 Inspector에서 바로 편집할 수 있습니다.",
                MessageType.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < groups.Count; i++)
                DrawGroup(groups[i], fps);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _followTimelineWindow = GUILayout.Toggle(
                    _followTimelineWindow, "Follow Timeline", EditorStyles.toolbarButton,
                    GUILayout.Width(105f));

                using (new EditorGUI.DisabledScope(_followTimelineWindow))
                {
                    _manualTimeline = (TimelineAsset)EditorGUILayout.ObjectField(
                        _manualTimeline, typeof(TimelineAsset), false);
                }
            }
        }

        private TimelineAsset ResolveTimeline()
        {
            TimelineAsset inspected = TimelineEditor.inspectedAsset;
            if (_followTimelineWindow && inspected != null) return inspected;
            return _manualTimeline;
        }

        private static void DrawGroup(TimelineMarkerStackGroup group, double fps)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        $"Frame {group.Frame} · {group.FrameTime:F3}s · {group.Markers.Count}개",
                        EditorStyles.boldLabel);

                    if (GUILayout.Button("모두 선택", GUILayout.Width(72f)))
                        SelectAll(group.Markers);
                }

                for (int i = 0; i < group.Markers.Count; i++)
                {
                    IMarker marker = group.Markers[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect swatch = GUILayoutUtility.GetRect(
                            7f, 16f, GUILayout.Width(7f), GUILayout.Height(16f));
                        EditorGUI.DrawRect(swatch, TimelineMarkerStackUtility.ColorFor(marker));

                        bool selected = marker is Object markerObject && Selection.Contains(markerObject);
                        GUIStyle style = selected ? EditorStyles.boldLabel : EditorStyles.label;
                        EditorGUILayout.LabelField(
                            TimelineMarkerStackUtility.Describe(marker),
                            style, GUILayout.MinWidth(150f));

                        EditorGUILayout.LabelField(
                            $"{marker.time:F4}s",
                            EditorStyles.miniLabel, GUILayout.Width(70f));

                        int exactFrame = TimelineMarkerStackUtility.ToFrame(marker.time, fps);
                        EditorGUILayout.LabelField(
                            $"F{exactFrame}",
                            EditorStyles.miniLabel, GUILayout.Width(36f));

                        if (GUILayout.Button("선택", GUILayout.Width(48f)))
                            SelectOne(marker);
                    }
                }
            }
        }

        private static void SelectOne(IMarker marker)
        {
            Object markerObject = marker as Object;
            if (markerObject == null) return;

            if (TimelineEditor.inspectedDirector != null)
                Selection.SetActiveObjectWithContext(markerObject, TimelineEditor.inspectedDirector);
            else
                Selection.activeObject = markerObject;

            TimelineEditor.Refresh(RefreshReason.WindowNeedsRedraw);
        }

        private static void SelectAll(IReadOnlyList<IMarker> markers)
        {
            Selection.objects = markers
                .Select(marker => marker as Object)
                .Where(marker => marker != null)
                .ToArray();
            TimelineEditor.Refresh(RefreshReason.WindowNeedsRedraw);
        }
    }

    internal static class TimelineMarkerStackOverlay
    {
        private static GUIStyle s_BadgeStyle;

        public static MarkerDrawOptions Options(IMarker marker)
        {
            List<IMarker> stack = TimelineMarkerStackUtility.GetSameFrameMarkers(marker);
            string tooltip = TimelineMarkerStackUtility.Describe(marker);
            if (stack.Count > 1)
                tooltip += $"\n같은 프레임에 {stack.Count}개 마커가 있습니다. Window > Sequencing > Timeline Marker Stack에서 개별 선택하세요.";
            return new MarkerDrawOptions { tooltip = tooltip };
        }

        public static void Draw(IMarker marker, MarkerOverlayRegion region)
        {
            List<IMarker> stack = TimelineMarkerStackUtility.GetSameFrameMarkers(marker);
            if (stack.Count < 2) return;

            const float chipWidth = 5f;
            float totalWidth = stack.Count * chipWidth;
            float startX = region.markerRegion.center.x - totalWidth * 0.5f;
            float y = region.markerRegion.yMax - 4f;

            for (int i = 0; i < stack.Count; i++)
            {
                var chip = new Rect(startX + i * chipWidth, y, chipWidth - 1f, 3f);
                EditorGUI.DrawRect(chip, TimelineMarkerStackUtility.ColorFor(stack[i]));
            }

            Rect badge = new Rect(region.markerRegion.xMax - 1f, region.markerRegion.yMin - 2f, 18f, 13f);
            EditorGUI.DrawRect(badge, new Color(0.08f, 0.08f, 0.08f, 0.92f));
            GUI.Label(badge, stack.Count.ToString(), BadgeStyle);
        }

        private static GUIStyle BadgeStyle
        {
            get
            {
                if (s_BadgeStyle != null) return s_BadgeStyle;
                s_BadgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9,
                    normal = { textColor = Color.white }
                };
                return s_BadgeStyle;
            }
        }
    }

    [CustomTimelineEditor(typeof(PresentationSignalMarker))]
    public sealed class PresentationSignalMarkerStackEditor : MarkerEditor
    {
        public override MarkerDrawOptions GetMarkerOptions(IMarker marker) =>
            TimelineMarkerStackOverlay.Options(marker);

        public override void DrawOverlay(IMarker marker, MarkerUIStates uiState, MarkerOverlayRegion region) =>
            TimelineMarkerStackOverlay.Draw(marker, region);
    }

    [CustomTimelineEditor(typeof(PresentationSpeedMarker))]
    public sealed class PresentationSpeedMarkerStackEditor : MarkerEditor
    {
        public override MarkerDrawOptions GetMarkerOptions(IMarker marker) =>
            TimelineMarkerStackOverlay.Options(marker);

        public override void DrawOverlay(IMarker marker, MarkerUIStates uiState, MarkerOverlayRegion region) =>
            TimelineMarkerStackOverlay.Draw(marker, region);
    }

    [CustomTimelineEditor(typeof(PresentationSectionMarker))]
    public sealed class PresentationSectionMarkerStackEditor : MarkerEditor
    {
        public override MarkerDrawOptions GetMarkerOptions(IMarker marker) =>
            TimelineMarkerStackOverlay.Options(marker);

        public override void DrawOverlay(IMarker marker, MarkerUIStates uiState, MarkerOverlayRegion region) =>
            TimelineMarkerStackOverlay.Draw(marker, region);
    }

    [CustomTimelineEditor(typeof(CueMarker))]
    public sealed class CueMarkerStackEditor : MarkerEditor
    {
        public override MarkerDrawOptions GetMarkerOptions(IMarker marker) =>
            TimelineMarkerStackOverlay.Options(marker);

        public override void DrawOverlay(IMarker marker, MarkerUIStates uiState, MarkerOverlayRegion region) =>
            TimelineMarkerStackOverlay.Draw(marker, region);
    }
}