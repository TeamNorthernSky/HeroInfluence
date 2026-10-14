using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>선택한 Timeline에 Presentation 마커용 레인을 추가한다.</summary>
    internal static class PresentationMarkerTrackSetup
    {
        private const string MenuPath = "Assets/Skill Presentation/Add Speed and Sound Tracks";
        private const string SplitAllMenuPath = "Tools/Skill Presentation/Split Speed and Sound Markers";
        private const string RuntimeTimelineFolder = "Assets/ASB_Work/Skills/Timelines";

        [MenuItem(SplitAllMenuPath)]
        private static void SplitAllFromMenu() => Debug.Log(SplitAllRuntimeTimelines());

        /// <summary>실제 스킬 Timeline 전체를 한 번에 변환한다. 다시 실행해도 중복 생성하지 않는다.</summary>
        public static string SplitAllRuntimeTimelines()
        {
            string[] guids = AssetDatabase.FindAssets("t:TimelineAsset", new[] { RuntimeTimelineFolder });
            Array.Sort(guids, StringComparer.Ordinal);
            int timelines = 0;
            int speedMarkers = 0;
            int soundMarkers = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".playable", StringComparison.OrdinalIgnoreCase)) continue;
                TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                if (timeline == null) continue;
                SplitTimeline(timeline, out int speed, out int sound);
                timelines++;
                speedMarkers += speed;
                soundMarkers += sound;
                AssetDatabase.SaveAssetIfDirty(timeline);
            }

            AssetDatabase.SaveAssets();
            return $"[Skill Timeline] {timelines}개 Timeline: Speed 마커 {speedMarkers}개, Sound 마커 {soundMarkers}개를 기존 시각 그대로 분리했습니다.";
        }

        private static void SplitTimeline(TimelineAsset timeline, out int speedCount, out int soundCount)
        {
            Undo.RegisterCompleteObjectUndo(timeline, "Split Presentation Markers");
            PresentationMarkerTrack speedTrack = GetOrCreateTrack(timeline, PresentationMarkerTrackRole.Speed, "Speed");
            PresentationMarkerTrack soundTrack = GetOrCreateTrack(timeline, PresentationMarkerTrackRole.Sfx, "Sound");

            speedCount = MoveMarkers<PresentationSpeedMarker>(timeline, speedTrack);
            soundCount = MoveMarkers<PresentationSfxMarker>(timeline, soundTrack);
            EditorUtility.SetDirty(timeline);
        }

        private static PresentationMarkerTrack GetOrCreateTrack(TimelineAsset timeline,
            PresentationMarkerTrackRole role, string name)
        {
            foreach (TrackAsset root in timeline.GetRootTracks())
            {
                PresentationMarkerTrack existing = FindTrack(root, role);
                if (existing == null) continue;
                if (existing.name != name)
                {
                    Undo.RecordObject(existing, "Rename Presentation Marker Track");
                    existing.name = name;
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            PresentationMarkerTrack track = timeline.CreateTrack<PresentationMarkerTrack>(null, name);
            Undo.RegisterCreatedObjectUndo(track, "Add Presentation Marker Track");
            track.SetRole(role);
            EditorUtility.SetDirty(track);
            return track;
        }

        private static PresentationMarkerTrack FindTrack(TrackAsset track, PresentationMarkerTrackRole role)
        {
            if (track is PresentationMarkerTrack presentationTrack && presentationTrack.Role == role)
                return presentationTrack;
            foreach (TrackAsset child in track.GetChildTracks())
            {
                PresentationMarkerTrack found = FindTrack(child, role);
                if (found != null) return found;
            }
            return null;
        }

        private static int MoveMarkers<T>(TimelineAsset timeline, PresentationMarkerTrack target)
            where T : ScriptableObject, IMarker
        {
            var sources = new List<T>(PresentationTimelineMarkers.OfType<T>(timeline));
            int moved = 0;
            foreach (T source in sources)
            {
                TrackAsset sourceTrack = source.parent;
                if (sourceTrack == target) continue;

                double originalTime = source.time;
                string originalData = EditorJsonUtility.ToJson(source);
                T copy = target.CreateMarker<T>(originalTime);
                EditorUtility.CopySerialized(source, copy);
                copy.name = source.name;
                if (copy.time != originalTime || EditorJsonUtility.ToJson(copy) != originalData)
                    throw new InvalidOperationException($"{timeline.name}: {typeof(T).Name} 마커 데이터가 복사 중 변경되었습니다.");

                EditorUtility.SetDirty(copy);
                if (sourceTrack == null || !sourceTrack.DeleteMarker(source))
                    throw new InvalidOperationException($"{timeline.name}: 원본 {typeof(T).Name} 마커를 제거하지 못했습니다.");
                EditorUtility.SetDirty(sourceTrack);
                EditorUtility.SetDirty(target);
                moved++;
            }
            return moved;
        }

        [MenuItem(MenuPath, false, 2101)]
        private static void AddTracks()
        {
            TimelineAsset timeline = Selection.activeObject as TimelineAsset;
            if (timeline == null) return;

            var existing = new HashSet<PresentationMarkerTrackRole>();
            foreach (TrackAsset root in timeline.GetRootTracks())
                CollectRoles(root, existing);

            int added = 0;
            Undo.RegisterCompleteObjectUndo(timeline, "Add Presentation Marker Tracks");
            added += AddIfMissing(timeline, existing, PresentationMarkerTrackRole.Speed, "Speed");
            added += AddIfMissing(timeline, existing, PresentationMarkerTrackRole.Sfx, "Sound");

            if (added == 0) return;
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssetIfDirty(timeline);
            UnityEngine.Debug.Log($"[Skill Timeline] {timeline.name}: 마커 트랙 {added}개를 추가했습니다.", timeline);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanAddTracks() => Selection.activeObject is TimelineAsset;

        private static int AddIfMissing(TimelineAsset timeline,
            HashSet<PresentationMarkerTrackRole> existing, PresentationMarkerTrackRole role, string name)
        {
            if (!existing.Add(role)) return 0;

            PresentationMarkerTrack track = timeline.CreateTrack<PresentationMarkerTrack>(null, name);
            Undo.RegisterCreatedObjectUndo(track, "Add Presentation Marker Track");
            track.SetRole(role);
            EditorUtility.SetDirty(track);
            return 1;
        }

        private static void CollectRoles(TrackAsset track, HashSet<PresentationMarkerTrackRole> roles)
        {
            if (track == null) return;
            if (track is PresentationMarkerTrack presentationTrack)
                roles.Add(presentationTrack.Role);

            foreach (TrackAsset child in track.GetChildTracks())
                CollectRoles(child, roles);
        }
    }
}
