using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    public enum SkillTimelineValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class SkillTimelineValidationMessage
    {
        public SkillTimelineValidationSeverity Severity;
        public string Message;
    }

    /// <summary>실제 Runtime Timeline Variant와 SkillPresentationData 연결을 정적으로 검증한다.</summary>
    public static class SkillTimelineValidator
    {
        public static List<SkillTimelineValidationMessage> Validate(
            SkillPresentationData data, string characterKey, TimelineAsset timeline)
        {
            var result = new List<SkillTimelineValidationMessage>();
            if (data == null)
            {
                Add(result, SkillTimelineValidationSeverity.Error, "SkillPresentationData가 없습니다.");
                return result;
            }

            if (!data.IsTimelineRail)
            {
                // §4: Animator Rail이라도 즉시 반환하지 않고 남은 SkillTimelines 구조를 먼저 검증한다.
                // dangling/null 바인딩은 정리 대상(Error), 모두 유효하면 Migration Data(Info), 비면 정상(Info).
                ValidateAnimatorRailBindings(data, result);
                return result;
            }

            ValidateBindings(data, result);
            if (timeline == null)
            {
                Add(result, SkillTimelineValidationSeverity.Error,
                    $"CharacterKey '{characterKey}'에 연결된 Runtime Timeline이 없습니다.");
                return result;
            }

            string path = AssetDatabase.GetAssetPath(timeline);
            if (string.IsNullOrEmpty(path))
                Add(result, SkillTimelineValidationSeverity.Error, "Timeline이 AssetDatabase 에셋이 아닙니다.");
            else if (path.Replace('\\', '/').Contains("/Editor/"))
                Add(result, SkillTimelineValidationSeverity.Error,
                    $"런타임 바인딩이 Editor 전용 Timeline을 참조합니다: {path}");

            int animationTracks = 0;
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is AnimationTrack) animationTracks++;
            }
            if (animationTracks == 0)
                Add(result, SkillTimelineValidationSeverity.Error, "Animation Track이 없습니다.");
            else if (animationTracks > 1)
                Add(result, SkillTimelineValidationSeverity.Warning,
                    $"Animation Track이 {animationTracks}개입니다. 동일 Animator 중복 바인딩 의도를 확인하세요.");

            ValidateMarkers(data, timeline, result);
            ValidateSections(timeline, result);
            ValidateSpeedMarkers(timeline, result);
            ValidateAnimationCoverage(timeline, result);
            return result;
        }

        public static bool HasErrors(IReadOnlyList<SkillTimelineValidationMessage> messages)
        {
            if (messages == null) return false;
            for (int i = 0; i < messages.Count; i++)
                if (messages[i].Severity == SkillTimelineValidationSeverity.Error) return true;
            return false;
        }

        private static void ValidateBindings(SkillPresentationData data,
            List<SkillTimelineValidationMessage> result)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int wildcardCount = 0;
            if (data.SkillTimelines == null || data.SkillTimelines.Count == 0)
            {
                Add(result, SkillTimelineValidationSeverity.Error, "SkillTimelines 바인딩이 비어 있습니다.");
                return;
            }

            for (int i = 0; i < data.SkillTimelines.Count; i++)
            {
                SkillTimelineBinding binding = data.SkillTimelines[i];
                if (binding == null)
                {
                    Add(result, SkillTimelineValidationSeverity.Warning, $"SkillTimelines[{i}]가 null입니다.");
                    continue;
                }

                string key = binding.CharacterKey != null ? binding.CharacterKey.Trim() : string.Empty;
                if (string.IsNullOrEmpty(key)) wildcardCount++;
                else if (!keys.Add(key))
                    Add(result, SkillTimelineValidationSeverity.Error, $"CharacterKey '{key}'가 중복되었습니다.");

                if (binding.Timeline == null)
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"CharacterKey '{(string.IsNullOrEmpty(key) ? "(wildcard)" : key)}'의 Timeline 참조가 비어 있습니다.");
            }

            if (wildcardCount > 1)
                Add(result, SkillTimelineValidationSeverity.Error, "Wildcard CharacterKey 바인딩이 둘 이상입니다.");
        }

        private static void ValidateMarkers(SkillPresentationData data, TimelineAsset timeline,
            List<SkillTimelineValidationMessage> result)
        {
            if (timeline.markerTrack == null)
            {
                Add(result, SkillTimelineValidationSeverity.Error, "Marker Track이 없습니다.");
                return;
            }

            bool hasImpact = false;
            bool hasProjectile = false;
            int projectileCount = 0;
            var cueIds = new HashSet<string>(StringComparer.Ordinal);
            var definedCueIds = new HashSet<string>(StringComparer.Ordinal);
            var definedCueNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var cueBindings = new List<CueBinding>();
            data.CollectAllCues(cueBindings);
            for (int i = 0; i < cueBindings.Count; i++)
            {
                CueBinding cue = cueBindings[i];
                if (cue == null) continue;

                if (!string.IsNullOrWhiteSpace(cue.CueId) && !definedCueIds.Add(cue.CueId))
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"SkillPresentationData의 CueId '{cue.CueId}'가 중복되었습니다.");

                string cueName = cue.NormalizedCueName;
                if (!string.IsNullOrEmpty(cueName))
                    definedCueNames[cueName] = definedCueNames.TryGetValue(cueName, out int count) ? count + 1 : 1;
            }

            foreach (IMarker raw in timeline.markerTrack.GetMarkers())
            {
                if (!(raw is PresentationSignalMarker marker)) continue;
                switch (marker.Kind)
                {
                    case PresentationSignalKind.Cue:
                        if (string.IsNullOrWhiteSpace(marker.CueId))
                            Add(result, SkillTimelineValidationSeverity.Warning,
                                $"{marker.time:F3}s Cue Marker에 CueId가 없습니다. 이름 폴백은 동명 Cue에 안전하지 않습니다.");
                        else if (!cueIds.Add(marker.CueId))
                            Add(result, SkillTimelineValidationSeverity.Error,
                                $"CueId '{marker.CueId}' Marker가 중복되었습니다.");

                        if (!string.IsNullOrWhiteSpace(marker.CueId))
                        {
                            if (!definedCueIds.Contains(marker.CueId))
                                Add(result, SkillTimelineValidationSeverity.Error,
                                    $"{marker.time:F3}s Cue Marker의 CueId '{marker.CueId}'가 데이터 Cue에 없습니다.");
                        }
                        else if (string.IsNullOrWhiteSpace(marker.CueName))
                        {
                            Add(result, SkillTimelineValidationSeverity.Error,
                                $"{marker.time:F3}s Cue Marker에 CueId와 CueName이 모두 없습니다.");
                        }
                        else if (!definedCueNames.TryGetValue(marker.CueName, out int nameCount))
                        {
                            Add(result, SkillTimelineValidationSeverity.Error,
                                $"{marker.time:F3}s Cue Marker 이름 '{marker.CueName}'이 데이터 Cue에 없습니다.");
                        }
                        else if (nameCount > 1)
                        {
                            Add(result, SkillTimelineValidationSeverity.Error,
                                $"{marker.time:F3}s Cue Marker 이름 '{marker.CueName}'이 둘 이상의 데이터 Cue와 일치합니다. CueId를 사용하세요.");
                        }
                        break;
                    case PresentationSignalKind.Impact:
                        hasImpact = true;
                        break;
                    case PresentationSignalKind.Projectile:
                        hasProjectile = true;
                        projectileCount++;
                        break;
                }
            }

            // ChainLightning: 일반 Projectile Prefab이 없어도(GetProjectileVisual null) 번개 시작 시점을
            // Projectile 마커로 표시해야 한다. 대미지는 도달 신호(Probe)가 소유하므로 Impact 마커(즉발)는 금지.
            bool expectsChainDelivery =
                data.ChainLightningEffectPrefab != null
                && data.ProjectileVisual?.DeliveryMode == ProjectileDeliveryMode.ChainAdditionalTargets;
            bool expectsProjectile = data.PresentationArchetype == PresentationArchetype.Projectile
                                     || data.GetProjectileVisual() != null
                                     || expectsChainDelivery;
            if (expectsProjectile && !hasProjectile)
                Add(result, SkillTimelineValidationSeverity.Error, expectsChainDelivery
                    ? "ChainLightning 스킬에 Projectile Marker가 없습니다(번개 시작 시점)."
                    : "Projectile 스킬에 Projectile Marker가 없습니다.");
            if (expectsChainDelivery && projectileCount > 1)
                Add(result, SkillTimelineValidationSeverity.Error,
                    $"ChainLightning Timeline에 Projectile Marker가 {projectileCount}개입니다(번개는 1회 시작 — 1개만 두세요).");
            if (expectsChainDelivery && hasImpact)
                Add(result, SkillTimelineValidationSeverity.Error,
                    "ChainLightning Timeline에 Impact Marker가 있습니다. 대미지는 번개 도달 신호가 소유하므로 " +
                    "Impact 마커(즉발)를 제거하세요.");
            if (!expectsProjectile && !hasImpact)
                Add(result, SkillTimelineValidationSeverity.Warning,
                    "Impact Marker가 없습니다. 현재 런타임은 Timeline 종료 시 1회 폴백하지만 명시 Marker를 권장합니다.");
        }

        private static void ValidateSections(TimelineAsset timeline,
            List<SkillTimelineValidationMessage> result)
        {
            var ids = new List<string>();
            PresentationTimelineSections.CollectSectionIds(timeline, ids);
            for (int i = 0; i < ids.Count; i++)
            {
                if (!PresentationTimelineSections.TryResolve(timeline, ids[i], out PresentationTimelineRange range,
                        out string error))
                {
                    Add(result, SkillTimelineValidationSeverity.Error, error);
                    continue;
                }

                foreach (TrackAsset track in timeline.GetOutputTracks())
                {
                    if (!(track is AnimationTrack)) continue;
                    foreach (TimelineClip clip in track.GetClips())
                    {
                        if (CutsClip(range.Start, clip) || CutsClip(range.End, clip))
                        {
                            Add(result, SkillTimelineValidationSeverity.Warning,
                                $"Section '{ids[i]}' 경계가 AnimationClip '{clip.displayName}' 중간을 자릅니다. " +
                                "의도된 포즈 절단인지 확인하세요.");
                        }
                    }
                }
            }
        }

        private static bool CutsClip(double boundary, TimelineClip clip)
        {
            const double epsilon = 0.0001d;
            return boundary > clip.start + epsilon && boundary < clip.end - epsilon;
        }

        /// <summary>SpeedRegion 마커 검증: 유한값·허용범위·동일시각 중복·범위 밖·과도한 예상 재생시간.</summary>
        private const float SpeedWarnTotalSeconds = 30f;

        private static void ValidateSpeedMarkers(TimelineAsset timeline,
            List<SkillTimelineValidationMessage> result)
        {
            if (timeline.markerTrack == null) return;   // Marker Track 부재는 ValidateMarkers가 이미 처리

            var speedMarkers = new List<PresentationSpeedMarker>();
            foreach (IMarker raw in timeline.markerTrack.GetMarkers())
                if (raw is PresentationSpeedMarker sm) speedMarkers.Add(sm);
            if (speedMarkers.Count == 0) return;   // 속도 마커는 선택 기능

            double duration = timeline.duration;
            const double eps = 0.0001d;
            var timeGroups = new Dictionary<long, int>();

            for (int i = 0; i < speedMarkers.Count; i++)
            {
                PresentationSpeedMarker sm = speedMarkers[i];
                float raw = sm.RawSpeed;

                if (float.IsNaN(raw) || float.IsInfinity(raw))
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"{sm.time:F3}s Speed Marker 값이 유한하지 않습니다(NaN/Infinity).");
                else if (raw < PresentationSpeedMarker.MinSpeed - 1e-6f
                         || raw > PresentationSpeedMarker.MaxSpeed + 1e-6f)
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"{sm.time:F3}s Speed Marker 값 {raw}가 허용 범위 " +
                        $"[{PresentationSpeedMarker.MinSpeed}, {PresentationSpeedMarker.MaxSpeed}] 밖입니다.");

                if (sm.time < -eps || sm.time > duration + eps)
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"{sm.time:F3}s Speed Marker가 Timeline 범위(0~{duration:F3}s) 밖입니다.");

                long bucket = (long)System.Math.Round(sm.time / eps);
                timeGroups[bucket] = timeGroups.TryGetValue(bucket, out int c) ? c + 1 : 1;
            }

            foreach (KeyValuePair<long, int> g in timeGroups)
                if (g.Value > 1)
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"{g.Key * eps:F3}s에 Speed Marker가 {g.Value}개 겹칩니다(같은 시각 중복은 " +
                        "비결정적이라 금지 — 서로 다른 시각의 '가장 늦은 마커 우선'은 정상).");

            // 예상 실시간 재생 시간 경고(경고만, 하드 가드 없음): Σ(구간길이 / 구간속도).
            // 너무 느린 속도값 저작 실수로 한 턴이 사실상 정지하는 것을 방지한다.
            double estimated = EstimatePlaybackSeconds(speedMarkers, duration);
            if (estimated > SpeedWarnTotalSeconds)
                Add(result, SkillTimelineValidationSeverity.Warning,
                    $"Speed Marker 적용 시 예상 실시간 재생이 약 {estimated:F1}s입니다(> {SpeedWarnTotalSeconds:F0}s). " +
                    "너무 느린 구간속도가 있는지 확인하세요(재생 루프에 타임아웃이 없어 한 턴이 멈출 수 있음).");
        }

        private static double EstimatePlaybackSeconds(List<PresentationSpeedMarker> markers, double duration)
        {
            if (duration <= 0d) return 0d;

            var ordered = new List<PresentationSpeedMarker>(markers);
            ordered.Sort((a, b) => a.time.CompareTo(b.time));

            double total = 0d;
            double cursor = 0d;
            float currentSpeed = PresentationTimelineSpeed.DefaultSpeed;   // 첫 마커 이전 = 1.0
            for (int i = 0; i < ordered.Count; i++)
            {
                double markerTime = System.Math.Min(System.Math.Max(ordered[i].time, 0d), duration);
                if (markerTime > cursor)
                {
                    total += (markerTime - cursor) / System.Math.Max(currentSpeed, PresentationSpeedMarker.MinSpeed);
                    cursor = markerTime;
                }
                currentSpeed = ordered[i].Speed;
            }
            if (duration > cursor)
                total += (duration - cursor) / System.Math.Max(currentSpeed, PresentationSpeedMarker.MinSpeed);
            return total;
        }

        /// <summary>§4: Animator Rail 에셋의 SkillTimelines 구조 검증. 비면 정상, dangling/null은 정리 대상 Error.</summary>
        private static void ValidateAnimatorRailBindings(SkillPresentationData data,
            List<SkillTimelineValidationMessage> result)
        {
            if (data.SkillTimelines == null || data.SkillTimelines.Count == 0)
            {
                Add(result, SkillTimelineValidationSeverity.Info,
                    "Animator Rail: SkillTimelines가 비어 있습니다(정상, Phase 기반 Legacy Jig 사용).");
                return;
            }

            bool anyError = false;
            for (int i = 0; i < data.SkillTimelines.Count; i++)
            {
                SkillTimelineBinding binding = data.SkillTimelines[i];
                if (binding == null)
                {
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"Animator Rail이지만 SkillTimelines[{i}]가 null입니다. 정리하세요.");
                    anyError = true;
                    continue;
                }

                if (binding.Timeline == null)
                {
                    string key = binding.CharacterKey != null ? binding.CharacterKey.Trim() : string.Empty;
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"Animator Rail이지만 CharacterKey '{(string.IsNullOrEmpty(key) ? "(wildcard)" : key)}'의 " +
                        "Timeline 참조가 비어 있습니다(미설정 또는 dangling GUID). SkillTimelines를 정리하세요.");
                    anyError = true;
                }
            }

            if (!anyError)
                Add(result, SkillTimelineValidationSeverity.Info,
                    "Animator Rail: SkillTimelines에 유효한 마이그레이션 데이터가 남아 있습니다(런타임 미사용).");
        }

        /// <summary>
        /// §3: 선택된 Section(없으면 Full) 범위를 활성 Animation Track의 AnimationClip이 빈틈없이 덮는지 검사한다.
        /// 클립의 pre/post extrapolation(Hold/Loop 등)을 반영해 정상적인 held-tail을 오탐하지 않는다.
        /// </summary>
        private static void ValidateAnimationCoverage(TimelineAsset timeline,
            List<SkillTimelineValidationMessage> result)
        {
            var ranges = new List<PresentationTimelineRange>();
            var ids = new List<string>();
            PresentationTimelineSections.CollectSectionIds(timeline, ids);
            if (ids.Count == 0)
            {
                ranges.Add(PresentationTimelineRange.Full(timeline));
            }
            else
            {
                for (int i = 0; i < ids.Count; i++)
                    if (PresentationTimelineSections.TryResolve(timeline, ids[i],
                            out PresentationTimelineRange r, out _))
                        ranges.Add(r);
            }

            var clips = new List<TimelineClip>();
            bool hasActiveAnimationTrack = false;
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (!(track is AnimationTrack)) continue;
                if (track.muted) continue;
                hasActiveAnimationTrack = true;
                foreach (TimelineClip clip in track.GetClips()) clips.Add(clip);
            }

            const double eps = 0.0001d;
            foreach (PresentationTimelineRange range in ranges)
            {
                if (!range.IsValid) continue;
                string label = string.IsNullOrEmpty(range.SectionId) ? "Timeline" : $"Section '{range.SectionId}'";

                if (!hasActiveAnimationTrack)
                {
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"{label} 범위에 활성 Animation Track이 없습니다(트랙 없음 또는 전부 muted).");
                    continue;
                }

                var ordered = new List<TimelineClip>();
                foreach (TimelineClip clip in clips)
                {
                    if (!(clip.asset is AnimationPlayableAsset apa) || apa.clip == null)
                    {
                        Add(result, SkillTimelineValidationSeverity.Error,
                            $"{label}: AnimationClip이 비어 있는 클립('{clip.displayName}')이 있습니다.");
                        continue;
                    }
                    ordered.Add(clip);
                }
                ordered.Sort((a, b) => a.start.CompareTo(b.start));

                double cursor = range.Start;
                bool covered = true;
                for (int i = 0; i < ordered.Count && cursor < range.End - eps; i++)
                {
                    TimelineClip clip = ordered[i];
                    double effStart = clip.preExtrapolationMode != TimelineClip.ClipExtrapolation.None
                        ? double.NegativeInfinity : clip.start;
                    double effEnd = clip.postExtrapolationMode != TimelineClip.ClipExtrapolation.None
                        ? double.PositiveInfinity : clip.end;

                    if (effEnd <= cursor + eps) continue;   // 이미 지난 클립
                    if (effStart > cursor + eps)            // cursor와 이 클립 사이에 공백
                    {
                        covered = false;
                        break;
                    }
                    cursor = Math.Max(cursor, effEnd);
                }

                if (covered && cursor < range.End - eps) covered = false; // 마지막 클립 이후 공백
                if (!covered)
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"{label}: Animation Track에 빈 구간이 있습니다(구간 시작/클립 사이/끝 공백). " +
                        "빈 애니가 노출되므로 클립을 채우거나 Extrapolation(Hold)로 덮으세요.");
            }
        }

        private static void Add(List<SkillTimelineValidationMessage> result,
            SkillTimelineValidationSeverity severity, string message)
        {
            result.Add(new SkillTimelineValidationMessage { Severity = severity, Message = message });
        }
    }
}
