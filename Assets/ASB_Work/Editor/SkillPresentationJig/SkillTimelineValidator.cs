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
            return ValidateTimeline(data, characterKey, timeline, true);
        }

        private static List<SkillTimelineValidationMessage> ValidateTimeline(
            SkillPresentationData data, string characterKey, TimelineAsset timeline, bool validateBindings)
        {
            var result = new List<SkillTimelineValidationMessage>();
            if (data == null)
            {
                Add(result, SkillTimelineValidationSeverity.Error, "SkillPresentationData가 없습니다.");
                return result;
            }

            if (!data.IsTimelineRail && validateBindings)
            {
                // §4: Animator Rail이라도 즉시 반환하지 않고 남은 SkillTimelines 구조를 먼저 검증한다.
                // dangling/null 바인딩은 정리 대상(Error), 모두 유효하면 Migration Data(Info), 비면 정상(Info).
                ValidateAnimatorRailBindings(data, result);
                return result;
            }

            if (validateBindings) ValidateBindings(data, result);
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

        public static List<SkillTimelineValidationMessage> ValidatePlan(
            SkillPresentationData data, string characterKey, int targetCount)
        {
            var result = new List<SkillTimelineValidationMessage>();
            if (data == null)
            {
                Add(result, SkillTimelineValidationSeverity.Error, "SkillPresentationData가 없습니다.");
                return result;
            }
            ValidateBindings(data, result);
            var segments = new List<SkillTimelineSegment>();
            if (!data.TryResolvePlan(characterKey, null, targetCount, segments))
            {
                Add(result, SkillTimelineValidationSeverity.Error,
                    $"'{characterKey}' 대상 {targetCount}명 플랜을 찾지 못했습니다.");
                return result;
            }

            Append(result, ValidateSegmentsDirect(data, characterKey, targetCount, segments));
            return result;
        }

        public static List<SkillTimelineValidationMessage> ValidateBindingUsage(
            SkillPresentationData data, int bindingIndex, int segmentIndex)
        {
            var result = new List<SkillTimelineValidationMessage>();
            if (!TryGetBinding(data, bindingIndex, result, out SkillTimelineBinding binding))
                return result;

            if (segmentIndex == 0)
            {
                if (binding.Segments != null && binding.Segments.Count > 0)
                {
                    Add(result, SkillTimelineValidationSeverity.Info,
                        "Segments가 있어 binding.Timeline은 런타임 플랜에서 무시됩니다.");
                    Append(result, ValidateTimeline(
                        data, binding.CharacterKey, binding.Timeline, false));
                    return result;
                }
            }
            else
            {
                int index = segmentIndex - 1;
                if (binding.Segments == null || index < 0 || index >= binding.Segments.Count
                    || binding.Segments[index] == null)
                {
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"바인딩 {bindingIndex}의 세그먼트 {segmentIndex}가 더 이상 존재하지 않습니다.");
                    return result;
                }
            }

            Append(result, ValidateExactBindingPlan(data, bindingIndex));
            return result;
        }

        public static List<SkillTimelineValidationMessage> ValidateExactBindingPlan(
            SkillPresentationData data, int bindingIndex)
        {
            var result = new List<SkillTimelineValidationMessage>();
            if (!TryGetBinding(data, bindingIndex, result, out SkillTimelineBinding binding))
                return result;

            var segments = new List<SkillTimelineSegment>();
            if (binding.Segments != null && binding.Segments.Count > 0)
            {
                segments.AddRange(binding.Segments);
                if (binding.Timeline != null)
                    Add(result, SkillTimelineValidationSeverity.Info,
                        "Segments가 있어 binding.Timeline은 런타임 플랜에서 무시됩니다.");
            }
            else
            {
                segments.Add(new SkillTimelineSegment { Timeline = binding.Timeline, TargetSlot = 1 });
            }

            Append(result, ValidateSegmentsDirect(data, binding.CharacterKey, binding.TargetCount, segments));
            return result;
        }

        public static List<SkillTimelineValidationMessage> ValidateProvisionalUsage(
            SkillPresentationData data, string characterKey, int targetCount,
            int segmentIndex, int targetSlot, TimelineAsset timeline)
        {
            var segments = new List<SkillTimelineSegment>
            {
                new SkillTimelineSegment { Timeline = timeline, TargetSlot = targetSlot }
            };
            List<SkillTimelineValidationMessage> result =
                ValidateSegmentsDirect(data, characterKey, targetCount, segments);
            for (int i = 0; i < result.Count; i++)
                result[i].Message = $"세그먼트 {segmentIndex}: " + result[i].Message;
            return result;
        }

        public static List<SkillTimelineValidationMessage> ValidateProvisionalPlan(
            SkillPresentationData data, string characterKey, int targetCount,
            IReadOnlyList<SkillTimelineSegment> draftSegments)
        {
            return ValidateSegmentsDirect(data, characterKey, targetCount, draftSegments);
        }

        private static List<SkillTimelineValidationMessage> ValidateSegmentsDirect(
            SkillPresentationData data, string characterKey, int targetCount,
            IReadOnlyList<SkillTimelineSegment> segments)
        {
            var result = new List<SkillTimelineValidationMessage>();
            if (data == null)
            {
                Add(result, SkillTimelineValidationSeverity.Error, "SkillPresentationData가 없습니다.");
                return result;
            }
            if (segments == null || segments.Count == 0)
            {
                Add(result, SkillTimelineValidationSeverity.Error, "플랜에 세그먼트가 없습니다.");
                return result;
            }

            var impacts = new Dictionary<int, int>();
            var heldInstanceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cueBindings = new List<CueBinding>();
            data.CollectAllCues(cueBindings);

            for (int i = 0; i < segments.Count; i++)
            {
                SkillTimelineSegment segment = segments[i];
                if (segment == null)
                {
                    Add(result, SkillTimelineValidationSeverity.Error, $"세그먼트 {i + 1}이 null입니다.");
                    continue;
                }
                if (segment.TargetSlot < 1)
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"세그먼트 {i + 1}의 TargetSlot은 1 이상이어야 합니다.");
                else if (targetCount > 0 && segment.TargetSlot > targetCount)
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"세그먼트 {i + 1}의 슬롯 {segment.TargetSlot}이 대상 수 {targetCount}을 넘습니다.");

                TimelineAsset timeline = segment.Timeline;
                List<SkillTimelineValidationMessage> timelineMessages =
                    ValidateTimeline(data, characterKey, timeline, false);
                for (int m = 0; m < timelineMessages.Count; m++)
                    Add(result, timelineMessages[m].Severity,
                        $"세그먼트 {i + 1}: {timelineMessages[m].Message}");
                if (timeline == null || timeline.markerTrack == null) continue;

                var ordered = new List<IMarker>();
                foreach (IMarker raw in timeline.markerTrack.GetMarkers()) ordered.Add(raw);
                ordered.Sort((a, b) => a.time.CompareTo(b.time));

                var moves = new List<PresentationMoveMarker>();
                for (int markerIndex = 0; markerIndex < ordered.Count; markerIndex++)
                {
                    IMarker raw = ordered[markerIndex];
                    if (raw is PresentationMoveMarker move) moves.Add(move);
                    if (!(raw is PresentationSignalMarker signal)) continue;
                    if (signal.Kind == PresentationSignalKind.Cue)
                        ValidateHeldCueFlow(signal, cueBindings, heldInstanceKeys, i + 1, result);
                    if (signal.Kind == PresentationSignalKind.Projectile)
                        Add(result, SkillTimelineValidationSeverity.Error,
                            $"세그먼트 {i + 1}: 다중 대상 플랜의 Projectile 마커는 지원하지 않습니다.");
                    if (signal.Kind != PresentationSignalKind.Impact) continue;
                    int slot = signal.TargetSlot > 0 ? signal.TargetSlot : segment.TargetSlot;
                    if (targetCount > 0 && slot > targetCount)
                        Add(result, SkillTimelineValidationSeverity.Warning,
                            $"세그먼트 {i + 1}: {signal.time:F3}s Impact 슬롯 {slot}이 대상 수를 넘어서 무시됩니다.");
                    impacts[slot] = impacts.TryGetValue(slot, out int count) ? count + 1 : 1;
                }

                ValidateMovePairs(data, targetCount, segment, i + 1, moves, result);
            }

            if (targetCount > 0)
                for (int slot = 1; slot <= targetCount; slot++)
                    if (!impacts.ContainsKey(slot))
                        Add(result, SkillTimelineValidationSeverity.Warning,
                            $"슬롯 {slot}에 Impact가 없어 플랜 종료 시 확정합니다.");
            foreach (KeyValuePair<int, int> impact in impacts)
                if (impact.Value > 1)
                    Add(result, SkillTimelineValidationSeverity.Warning,
                        $"슬롯 {impact.Key}의 Impact가 {impact.Value}개입니다(첫 번째만 적용).");
            foreach (string key in heldInstanceKeys)
                Add(result, SkillTimelineValidationSeverity.Error,
                    $"플랜 종료 시 held InstanceKey '{key}'가 남습니다. 이 플랜 안에 Stop Cue가 필요합니다.");
            return result;
        }

        private static void ValidateMovePairs(SkillPresentationData data, int targetCount,
            SkillTimelineSegment segment, int segmentNumber, List<PresentationMoveMarker> moves,
            List<SkillTimelineValidationMessage> result)
        {
            if (moves.Count > 0 && data.PresentationArchetype != PresentationArchetype.Melee)
                Add(result, SkillTimelineValidationSeverity.Error,
                    $"세그먼트 {segmentNumber}: Move 마커는 Melee에서만 사용할 수 있습니다.");
            moves.Sort((a, b) => a.time.CompareTo(b.time));
            PresentationMoveMarker start = null;
            for (int i = 0; i < moves.Count; i++)
            {
                PresentationMoveMarker move = moves[i];
                int slot = move.TargetSlot > 0 ? move.TargetSlot : segment.TargetSlot;
                if (targetCount > 0 && slot > targetCount)
                    Add(result, SkillTimelineValidationSeverity.Warning,
                        $"세그먼트 {segmentNumber}: {move.time:F3}s Move 슬롯 {slot}이 대상 수를 넘어서 무시됩니다.");
                if (move.Boundary == PresentationSectionBoundary.Start)
                {
                    if (start != null)
                        Add(result, SkillTimelineValidationSeverity.Error,
                            $"세그먼트 {segmentNumber}: Move Start가 이전 End 전에 나왔습니다(구간 겹침).");
                    start = move;
                }
                else
                {
                    int startSlot = start != null
                        ? (start.TargetSlot > 0 ? start.TargetSlot : segment.TargetSlot)
                        : -1;
                    if (start == null || move.time <= start.time || startSlot != slot)
                        Add(result, SkillTimelineValidationSeverity.Error,
                            $"세그먼트 {segmentNumber}: Move Start/End 시간 또는 슬롯이 맞지 않습니다.");
                    start = null;
                }
            }
            if (start != null)
                Add(result, SkillTimelineValidationSeverity.Error,
                    $"세그먼트 {segmentNumber}: Move Start에 대응하는 End가 없습니다.");
        }

        private static void ValidateHeldCueFlow(PresentationSignalMarker marker,
            List<CueBinding> cueBindings, HashSet<string> heldInstanceKeys, int segmentNumber,
            List<SkillTimelineValidationMessage> result)
        {
            CueBinding cue = ResolveCueBinding(marker, cueBindings);
            if (cue == null) return;
            string key = cue.NormalizedInstanceKey;
            if (cue.Operation == CueOperation.Spawn)
            {
                if (string.IsNullOrEmpty(key)) return;
                if (!heldInstanceKeys.Add(key))
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"세그먼트 {segmentNumber} {marker.time:F3}s: held InstanceKey '{key}'가 활성 상태에서 다시 Spawn됩니다.");
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                Add(result, SkillTimelineValidationSeverity.Error,
                    $"세그먼트 {segmentNumber} {marker.time:F3}s: {cue.Operation} Cue에 InstanceKey가 없습니다.");
                return;
            }
            if (!heldInstanceKeys.Contains(key))
                Add(result, SkillTimelineValidationSeverity.Error,
                    $"세그먼트 {segmentNumber} {marker.time:F3}s: Spawn되지 않은 InstanceKey '{key}'에 {cue.Operation}을 실행합니다.");
            if (cue.Operation == CueOperation.Stop) heldInstanceKeys.Remove(key);
        }

        private static CueBinding ResolveCueBinding(PresentationSignalMarker marker, List<CueBinding> cues)
        {
            if (!string.IsNullOrWhiteSpace(marker.CueId))
            {
                for (int i = 0; i < cues.Count; i++)
                    if (cues[i] != null && string.Equals(cues[i].CueId, marker.CueId, StringComparison.Ordinal))
                        return cues[i];
                return null;
            }

            if (string.IsNullOrWhiteSpace(marker.CueName)) return null;
            CueBinding match = null;
            int count = 0;
            for (int i = 0; i < cues.Count; i++)
            {
                CueBinding cue = cues[i];
                if (cue == null || !string.Equals(cue.NormalizedCueName, marker.CueName,
                        StringComparison.OrdinalIgnoreCase)) continue;
                match = cue;
                count++;
            }
            return count == 1 ? match : null;
        }

        private static bool TryGetBinding(SkillPresentationData data, int bindingIndex,
            List<SkillTimelineValidationMessage> result, out SkillTimelineBinding binding)
        {
            binding = null;
            if (data == null || data.SkillTimelines == null
                || bindingIndex < 0 || bindingIndex >= data.SkillTimelines.Count)
            {
                Add(result, SkillTimelineValidationSeverity.Error,
                    "선택한 바인딩이 더 이상 존재하지 않습니다. 사용처를 다시 스캔하세요.");
                return false;
            }
            binding = data.SkillTimelines[bindingIndex];
            if (binding != null) return true;
            Add(result, SkillTimelineValidationSeverity.Error, $"바인딩 {bindingIndex}가 null입니다.");
            return false;
        }

        private static void Append(List<SkillTimelineValidationMessage> destination,
            IReadOnlyList<SkillTimelineValidationMessage> source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++) destination.Add(source[i]);
        }

        private static void ValidateBindings(SkillPresentationData data,
            List<SkillTimelineValidationMessage> result)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                string identity = key + "\u001f" + binding.TargetCount;
                if (!keys.Add(identity))
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"CharacterKey '{(string.IsNullOrEmpty(key) ? "(wildcard)" : key)}', TargetCount {binding.TargetCount}가 중복되었습니다.");

                bool hasSegments = binding.Segments != null && binding.Segments.Count > 0;
                if (binding.Timeline == null && !hasSegments)
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"CharacterKey '{(string.IsNullOrEmpty(key) ? "(wildcard)" : key)}'의 Timeline 참조가 비어 있습니다.");
                if (hasSegments && binding.Timeline != null)
                    Add(result, SkillTimelineValidationSeverity.Info,
                        $"바인딩 {i}: Segments가 있어 binding.Timeline은 런타임 플랜에서 무시됩니다.");
                if (hasSegments)
                    for (int s = 0; s < binding.Segments.Count; s++)
                    {
                        SkillTimelineSegment segment = binding.Segments[s];
                        if (segment == null || segment.Timeline == null || segment.TargetSlot < 1)
                            Add(result, SkillTimelineValidationSeverity.Error,
                                $"바인딩 {i} 세그먼트 {s + 1}의 Timeline/TargetSlot이 유효하지 않습니다.");
                        else if (binding.TargetCount > 0 && segment.TargetSlot > binding.TargetCount)
                            Add(result, SkillTimelineValidationSeverity.Error,
                                $"바인딩 {i} 세그먼트 {s + 1} 슬롯 {segment.TargetSlot}이 대상 수 {binding.TargetCount}를 넘습니다.");
                    }
            }
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
            var firstCueByName = new Dictionary<string, CueBinding>(StringComparer.OrdinalIgnoreCase);
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
                {
                    definedCueNames[cueName] = definedCueNames.TryGetValue(cueName, out int count) ? count + 1 : 1;
                    if (!firstCueByName.ContainsKey(cueName)) firstCueByName.Add(cueName, cue);
                }
            }

            foreach (IMarker raw in timeline.markerTrack.GetMarkers())
            {
                if (!(raw is PresentationSignalMarker marker)) continue;
                switch (marker.Kind)
                {
                    case PresentationSignalKind.Cue:
                        if (!string.IsNullOrWhiteSpace(marker.CueId) && !cueIds.Add(marker.CueId))
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
                        else if (firstCueByName.TryGetValue(marker.CueName, out CueBinding matchedCue)
                                 && string.IsNullOrWhiteSpace(matchedCue.CueId))
                        {
                            Add(result, SkillTimelineValidationSeverity.Error,
                                $"{marker.time:F3}s Cue Marker 이름 '{marker.CueName}'은 유일하지만 데이터 CueId가 비어 " +
                                "런타임 RegisterTimelineRailCues에서 등록되지 않습니다. CueId 수리를 실행하세요.");
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

                bool hasSegments = binding.Segments != null && binding.Segments.Count > 0;
                bool hasValidTimeline = binding.Timeline != null;
                if (hasSegments)
                {
                    hasValidTimeline = true;
                    for (int s = 0; s < binding.Segments.Count; s++)
                    {
                        SkillTimelineSegment segment = binding.Segments[s];
                        if (segment != null && segment.Timeline != null && segment.TargetSlot >= 1) continue;
                        Add(result, SkillTimelineValidationSeverity.Error,
                            $"Animator Rail의 SkillTimelines[{i}].Segments[{s}]가 유효하지 않습니다. 정리하세요.");
                        hasValidTimeline = false;
                        anyError = true;
                    }
                }

                if (!hasValidTimeline)
                {
                    string key = binding.CharacterKey != null ? binding.CharacterKey.Trim() : string.Empty;
                    Add(result, SkillTimelineValidationSeverity.Error,
                        $"Animator Rail이지만 CharacterKey '{(string.IsNullOrEmpty(key) ? "(wildcard)" : key)}'의 " +
                        "Timeline 참조가 비어 있고 Segments도 유효하지 않습니다(미설정 또는 dangling GUID). " +
                        "SkillTimelines를 정리하세요.");
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
                if (!(track is AnimationTrack animation)) continue;
                if (track.muted) continue;
                if (track.parent is AnimationTrack) continue;
                if (animation.applyAvatarMask && animation.avatarMask != null) continue;
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
                        $"{label} 범위에 전신 기본 Animation Track이 없습니다.");
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
