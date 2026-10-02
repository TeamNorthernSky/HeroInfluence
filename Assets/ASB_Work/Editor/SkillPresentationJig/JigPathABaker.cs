using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>굽기 계획의 클립 1개(Attack Beat 1개).</summary>
    public sealed class JigBakeClip
    {
        public string Label;
        public string StateName;
        public AnimationClip Clip;
        public double Start;
        public double Duration;     // 실효 길이(state speed 반영)
        public double TimeScale;    // clip.length / 실효 길이 = state speed
    }

    /// <summary>굽기 계획의 Cue 마커 1개.</summary>
    public sealed class JigBakeCue
    {
        public double Time;
        public CueBinding Cue;
        public string Label;
        public string Source;       // "NormalizedTime" / "Seconds" / "ClipEvent" / "분산(ClipEvent 없음)"
    }

    /// <summary>
    /// 굽기 계획 — <see cref="JigPathABaker.Plan"/>의 순수 계산 결과. 에셋을 만들지 않는다.
    /// Errors가 하나라도 있으면 굽기를 중단한다(파일·SO 변경 없음).
    /// </summary>
    public sealed class JigBakePlan
    {
        public readonly List<JigBakeClip> Clips = new List<JigBakeClip>();
        public readonly List<JigBakeCue> Cues = new List<JigBakeCue>();
        public PresentationSignalKind DeliveryKind = PresentationSignalKind.Impact;
        public double DeliveryTime;
        public string DeliveryRule;
        public double SectionEnd;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Infos = new List<string>();

        public bool Succeeded => Errors.Count == 0 && Clips.Count > 0;
    }

    /// <summary>
    /// 스킬 연출 + 캐릭터 프리팹 → 독립 Runtime TimelineAsset을 굽는다.
    ///
    /// - <b>Attack-only 굽기</b>: Attack.Beats를 순서대로 클립으로 넣고 BlendInSeconds만큼 겹친다
    ///   (배치는 Legacy 지그와 같은 <see cref="JigPhaseLayout.ComputeBlendBoundaries"/>).
    ///   Timeline 레일은 MovePrepare/AttackPrepare/Post를 실행하지 않으므로 거기에 내용이 있으면 중단한다.
    /// - Cue 마커는 Timing별(NormalizedTime/Seconds/ClipEvent)로 자기 Beat 기준 시각에 놓는다.
    /// - Delivery(Impact/Projectile) 마커는 Animator 레일에서 첫 히트가 처리되는 시점과 같은 규칙으로 놓는다.
    /// - 계산(<see cref="Plan"/>)이 전부 성공한 뒤에만 에셋을 만든다.
    /// - 결과 Timeline은 SkillPresentationData에 자동 연결하지 않는다. 연결은 런타임 데이터 저작 단계에서 직접 한다.
    ///
    /// ★런타임 타입(PresentationSignalMarker)만 쓴다 — 에디터 전용 CueMarker는 빌드에서 깨지므로 굽지 않는다.
    /// ★결과물은 런타임 폴더(Editor 아님)에 저장해 빌드에 포함되게 한다.
    /// </summary>
    public static class JigPathABaker
    {
        private const string RuntimeFolderParent = "Assets/ASB_Work/Skills";
        private const string RuntimeFolderName = "Timelines";
        private const string RuntimeFolder = RuntimeFolderParent + "/" + RuntimeFolderName;

        private const string OnHitFunctionName = "AniEvent_OnHit";
        private const double FallbackDeliveryRatio = 0.6d;

        public static TimelineAsset Bake(SkillPresentationData data, GameObject characterPrefab, out string error)
        {
            error = null;
            if (data == null) { error = "연출 자산이 비어 있습니다."; return null; }
            if (characterPrefab == null) { error = "캐릭터 프리팹이 비어 있습니다."; return null; }

            // 1) 캐릭터 키(템플릿 Index, 없으면 unitName) + Animator
            string characterKey = ResolveBakeKey(characterPrefab);
            Animator animator = characterPrefab.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                error = "캐릭터 프리팹에 Animator/AnimatorController가 없습니다.";
                return null;
            }

            // 2) 계획(순수 계산). 실패하면 아무것도 만들거나 바꾸지 않는다.
            JigBakePlan plan = Plan(data, animator.runtimeAnimatorController);
            if (!plan.Succeeded)
            {
                error = "굽기를 중단했습니다(파일·연출 자산 변경 없음).\n- " +
                        (plan.Errors.Count > 0 ? string.Join("\n- ", plan.Errors) : "굽을 클립이 없습니다.");
                return null;
            }

            // 3) 런타임 폴더 + 결정적 경로. 기존 Variant는 덮어쓰지 않는다(저작 내용 보호).
            string safeKey = string.IsNullOrEmpty(characterKey) ? "Any" : SanitizeFileName(characterKey);
            string path = $"{RuntimeFolder}/Skill{data.SkillIndex}_{safeKey}.playable";
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(path) != null)
            {
                error = $"이미 Runtime Timeline Variant가 있습니다: {path}\n기존 Variant를 열어 편집하거나 명시적으로 이름을 바꾼 뒤 다시 시도하세요.";
                return null;
            }
            EnsureFolder();

            // 4) 생성. 도중 예외가 나면 만든 파일을 지운다.
            if (data.EnsureCueIds()) EditorUtility.SetDirty(data);

            TimelineAsset timeline;
            try
            {
                timeline = CreateTimelineAsset(plan, path);
            }
            catch (System.Exception e)
            {
                if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(path) != null) AssetDatabase.DeleteAsset(path);
                error = $"Timeline 생성 중 오류로 중단했습니다(만든 파일은 삭제): {e.Message}";
                Debug.LogException(e);
                return null;
            }

            if (plan.DeliveryKind == PresentationSignalKind.Projectile)
            {
                data.PresentationArchetype = PresentationArchetype.Projectile;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            LogBakeResult(plan, path, characterKey, timeline);
            return timeline;
        }

        /// <summary>
        /// 굽기 계획을 계산한다. 에셋 생성·SO 변경이 없는 순수 계산이다(테스트 대상).
        /// </summary>
        public static JigBakePlan Plan(SkillPresentationData data, RuntimeAnimatorController controller)
        {
            var plan = new JigBakePlan();
            if (data == null) { plan.Errors.Add("연출 자산이 비어 있습니다."); return plan; }
            if (controller == null) { plan.Errors.Add("AnimatorController가 없습니다."); return plan; }

            CheckSupportedScope(data, plan);

            List<JigPhaseInterval> intervals = BuildAttackIntervals(data, controller, plan,
                out List<AttackBeat> beats, out List<string> states, out bool useBeats);
            if (plan.Errors.Count > 0 || intervals.Count == 0) return plan;

            // 배치 — Legacy 지그와 같은 경계 블렌드 계산(Beat→Beat는 EarlyOverlap).
            List<JigBlendBoundary> boundaries = JigPhaseLayout.ComputeBlendBoundaries(intervals);
            for (int i = 0; i < boundaries.Count; i++)
            {
                if (!string.IsNullOrEmpty(boundaries[i].Warning)) plan.Warnings.Add(boundaries[i].Warning);
                if (boundaries[i].IsEventGated)
                {
                    plan.Warnings.Add(
                        $"{boundaries[i].FromLabel}→{boundaries[i].ToLabel}: AdvanceOnEvent — 실제 겹침은 이벤트 시점에 따라 다를 수 있습니다.");
                }
            }
            if (intervals[0].RequestedBlendSeconds > 0f)
            {
                plan.Infos.Add(
                    $"{intervals[0].Label}의 BlendIn {intervals[0].RequestedBlendSeconds:F2}s는 Timeline에 반영되지 않습니다(재생 시작 시 즉시 전환).");
            }

            for (int i = 0; i < intervals.Count; i++)
            {
                JigPhaseInterval iv = intervals[i];
                double speed = SpeedOf(iv);
                plan.Clips.Add(new JigBakeClip
                {
                    Label = iv.Label,
                    StateName = states[i],
                    Clip = iv.Clip,
                    Start = iv.Start,
                    Duration = iv.EffectiveLength,
                    TimeScale = speed,
                });
                plan.SectionEnd = System.Math.Max(plan.SectionEnd, iv.Start + iv.EffectiveLength);
                PlanCues(iv, speed, plan);
            }

            PlanDelivery(data, intervals, beats, useBeats, plan);
            return plan;
        }

        // ──────────────────────────────────────────────────────────────
        // 계획 단계
        // ──────────────────────────────────────────────────────────────

        /// <summary>Attack-only 굽기 지원 범위 검사. Timeline 레일이 실행하지 않는 페이즈에 내용이 있으면 Error.</summary>
        private static void CheckSupportedScope(SkillPresentationData data, JigBakePlan plan)
        {
            if (!data.IsPhaseCue) return;   // Schema 0은 페이즈 데이터를 런타임이 읽지 않는다.

            CheckCuePhase("MovePrepare", data.MovePrepare, 0f, plan);
            CheckCuePhase("AttackPrepare", data.AttackPrepare, 0f, plan);
            CheckCuePhase("Post", data.Post, data.Post != null ? data.Post.ExtraDelay : 0f, plan);

            if (data.MovingAttack != null && data.MovingAttack.Enabled)
            {
                plan.Errors.Add("MovingAttack이 켜져 있습니다 — Timeline 레일은 이동공격을 지원하지 않습니다.");
            }

            bool moves = (data.Move != null && data.Move.Enabled) || (data.Return != null && data.Return.Enabled);
            if (moves && data.PresentationArchetype != PresentationArchetype.Melee)
            {
                plan.Warnings.Add(
                    $"Archetype={data.PresentationArchetype}인데 Move/Return이 켜져 있습니다 — Timeline 레일에서는 이동하지 않습니다. " +
                    "근접 이동형이면 Archetype=Melee로 바꾸세요.");
            }
        }

        private static void CheckCuePhase(string label, CuePhase phase, float extraDelay, JigBakePlan plan)
        {
            if (phase == null || !phase.Enabled) return;

            bool hasState = !string.IsNullOrWhiteSpace(phase.AnimationStateName);
            bool hasCues = phase.Cues != null && phase.Cues.Count > 0;
            bool hasDelay = extraDelay > 0f;
            if (!hasState && !hasCues && !hasDelay) return;

            plan.Errors.Add(
                $"{label}에 내용이 있습니다(" +
                (hasState ? "애니 " : "") + (hasCues ? "Cue " : "") + (hasDelay ? "ExtraDelay " : "") +
                ") — Timeline 레일은 이 페이즈를 실행하지 않아 Attack-only 굽기에서 지원하지 않습니다. 비우거나 끈 뒤 다시 굽으세요.");
        }

        /// <summary>
        /// Attack Beat 구간 목록(엄격 모드). 상태명 규칙은 런타임 ResolveAttackBeatState와 같다:
        /// Beat 자체 값 → (index 0만) 최상위 상태명. 후속 Beat가 비면 Error.
        /// </summary>
        private static List<JigPhaseInterval> BuildAttackIntervals(SkillPresentationData data,
            RuntimeAnimatorController controller, JigBakePlan plan,
            out List<AttackBeat> beats, out List<string> states, out bool useBeats)
        {
            var intervals = new List<JigPhaseInterval>();
            beats = new List<AttackBeat>();
            states = new List<string>();

            // 런타임 useCombo와 같은 조건(Schema 1 + Attack 켜짐 + Beat 1개 이상).
            useBeats = data.IsPhaseCue && data.Attack != null && data.Attack.Enabled
                       && data.Attack.Beats != null && data.Attack.Beats.Count > 0;
            if (useBeats)
            {
                beats.AddRange(data.Attack.Beats);
            }
            else
            {
                beats.Add(null);   // 가상의 Beat[0] — 최상위 상태명 하나(기존 단일 클립 굽기와 같음)
                if (!data.IsPhaseCue && data.Attack?.Beats != null && data.Attack.Beats.Count > 0)
                {
                    plan.Infos.Add("Schema 0이라 Attack.Beats를 쓰지 않고 최상위 AnimationStateName으로 굽습니다.");
                }
                else if (data.IsPhaseCue && data.Attack != null && !data.Attack.Enabled
                         && data.Attack.Beats != null && data.Attack.Beats.Count > 0)
                {
                    plan.Warnings.Add("Attack이 꺼져 있어 Beat와 Beat Cue를 굽지 않고 최상위 AnimationStateName으로 굽습니다.");
                }
            }

            for (int i = 0; i < beats.Count; i++)
            {
                AttackBeat beat = beats[i];
                string label = useBeats ? $"Attack.Beats[{i}]" : "Attack(최상위 상태)";

                string state = beat != null && !string.IsNullOrWhiteSpace(beat.AnimationStateName)
                    ? beat.AnimationStateName.Trim()
                    : null;
                if (state == null)
                {
                    if (i > 0)
                    {
                        plan.Errors.Add($"{label}의 AnimationStateName이 비어 있습니다 — 런타임도 이 Beat를 재생하지 못합니다.");
                        continue;
                    }
                    state = data.ResolvedAnimationStateName;
                    if (string.IsNullOrWhiteSpace(state))
                    {
                        plan.Errors.Add($"{label}의 AnimationStateName이 비어 있고 최상위 AnimationStateName도 없습니다.");
                        continue;
                    }
                    if (useBeats) plan.Infos.Add($"{label}: 상태명이 비어 최상위 '{state}'를 사용합니다.");
                }

                JigStateResolution r = JigStateResolver.Resolve(controller, state);
                if (!r.Success || r.Clip == null || r.EffectiveLength <= 0d)
                {
                    plan.Errors.Add($"{label} '{state}' 클립 해석 실패: {(r.Success ? "클립 길이가 0입니다." : r.FailureReason)}");
                    continue;
                }

                intervals.Add(new JigPhaseInterval
                {
                    Label = label,
                    Start = 0d,
                    Duration = r.EffectiveLength,
                    EffectiveLength = r.EffectiveLength,
                    Clip = r.Clip,
                    Cues = beat?.Cues,
                    Resolved = true,
                    RequestedBlendSeconds = beat != null ? Mathf.Max(0f, beat.BlendInSeconds) : 0f,
                    AdvanceOnEvent = beat != null && beat.AdvanceOnEvent,
                    CueLimit = r.EffectiveLength * JigPhaseLayout.CueFireGuaranteedLimit,
                });
                states.Add(state);
            }

            if (plan.Errors.Count > 0)
            {
                intervals.Clear();
                states.Clear();
            }
            return intervals;
        }

        /// <summary>state speed = 클립 길이 / 실효 길이. Timeline 클립 timeScale과 클립 이벤트 시각 환산에 쓴다.</summary>
        private static double SpeedOf(JigPhaseInterval iv)
        {
            if (iv.Clip == null || iv.EffectiveLength <= 0d || iv.Clip.length <= 0f) return 1d;
            return iv.Clip.length / iv.EffectiveLength;
        }

        private static void PlanCues(JigPhaseInterval iv, double speed, JigBakePlan plan)
        {
            if (iv.Cues == null) return;

            Dictionary<string, List<float>> clipEvents = JigTimelineBuilder.CollectClipEventTimes(iv.Clip);
            int spread = 0;

            for (int c = 0; c < iv.Cues.Count; c++)
            {
                CueBinding cue = iv.Cues[c];
                if (cue == null) continue;

                string norm = cue.NormalizedCueName;
                string cueLabel = $"{iv.Label}.Cues[{c}] '{norm}'";
                if (string.IsNullOrEmpty(norm))
                {
                    plan.Warnings.Add($"{iv.Label}.Cues[{c}]: CueName이 비어 마커를 만들지 않습니다.");
                    continue;
                }

                if (cue.IsDataTimed)
                {
                    // Time=0은 유효값이다 — BeatStart에 놓는다.
                    double local = cue.ResolveFireSeconds((float)iv.EffectiveLength);
                    if (local < 0d)
                    {
                        plan.Warnings.Add($"{cueLabel}: 시각을 해석할 수 없어(길이 불명) 마커를 만들지 않습니다.");
                        continue;
                    }
                    if (local >= iv.CueLimit)
                    {
                        plan.Warnings.Add(
                            $"{cueLabel}: {local:F3}s가 발화 보장 한계 {iv.CueLimit:F3}s 이후입니다 — 더 이른 시각으로 옮기세요.");
                    }
                    plan.Cues.Add(new JigBakeCue
                    {
                        Time = iv.Start + local, Cue = cue, Label = cueLabel, Source = cue.Timing.ToString(),
                    });
                    continue;
                }

                // ClipEvent — 클립의 실제 AniEvent_PresentationCue 시각(state speed로 환산).
                if (clipEvents.TryGetValue(norm, out List<float> times) && times.Count > 0)
                {
                    for (int t = 0; t < times.Count; t++)
                    {
                        plan.Cues.Add(new JigBakeCue
                        {
                            Time = iv.Start + times[t] / speed, Cue = cue, Label = cueLabel, Source = "ClipEvent",
                        });
                    }
                    continue;
                }

                double ratio = System.Math.Min(0.25d + 0.15d * spread, JigPhaseLayout.CueFireGuaranteedLimit);
                spread++;
                plan.Cues.Add(new JigBakeCue
                {
                    Time = iv.Start + iv.EffectiveLength * ratio, Cue = cue, Label = cueLabel, Source = "분산(ClipEvent 없음)",
                });
                plan.Warnings.Add(
                    $"{cueLabel}: Timing=ClipEvent인데 클립 '{iv.Clip?.name}'에 같은 이름의 AniEvent_PresentationCue가 없어 " +
                    $"{ratio:P0} 지점에 임시로 놓았습니다 — 위치를 확정하세요.");
            }
        }

        /// <summary>
        /// Delivery 마커 시각. Animator 레일에서 첫 히트(대미지 + 투사체 발사)가 처리되는 시점과 같은 규칙:
        /// - Beats(ComboSkillAction): WaitForHitEvent=true인 첫 Beat의 첫 AniEvent_OnHit, 모두 false면 첫 Beat 시작.
        /// - Beats 없음(WaitHitAction): 클립의 첫 AniEvent_OnHit.
        /// - OnHit 이벤트가 필요한데 없으면 그 Beat의 60% + 경고(Animator 레일은 타임아웃까지 대기).
        /// </summary>
        private static void PlanDelivery(SkillPresentationData data, List<JigPhaseInterval> intervals,
            List<AttackBeat> beats, bool useBeats, JigBakePlan plan)
        {
            plan.DeliveryKind = data.GetProjectileVisual() != null
                ? PresentationSignalKind.Projectile
                : PresentationSignalKind.Impact;

            int hitIndex = -1;
            if (useBeats)
            {
                for (int i = 0; i < beats.Count && i < intervals.Count; i++)
                {
                    if (beats[i] != null && beats[i].WaitForHitEvent) { hitIndex = i; break; }
                }
                if (hitIndex < 0)
                {
                    plan.DeliveryTime = intervals[0].Start;
                    plan.DeliveryRule = "모든 Beat WaitForHitEvent=false → 첫 Beat 시작(Animator 레일과 같음)";
                    plan.Infos.Add(
                        $"{plan.DeliveryKind} 마커를 첫 Beat 시작({plan.DeliveryTime:F3}s)에 놓았습니다 — Animator 레일과 같은 시점입니다. " +
                        "실제 타격 시점으로 옮기세요.");
                    return;
                }
            }
            else
            {
                hitIndex = 0;
            }

            JigPhaseInterval hit = intervals[hitIndex];
            float onHit = FirstEventTime(hit.Clip, OnHitFunctionName);
            if (onHit >= 0f)
            {
                plan.DeliveryTime = hit.Start + onHit / SpeedOf(hit);
                plan.DeliveryRule = $"{hit.Label}의 첫 {OnHitFunctionName}";
                return;
            }

            plan.DeliveryTime = hit.Start + hit.EffectiveLength * FallbackDeliveryRatio;
            plan.DeliveryRule = $"{hit.Label}의 {FallbackDeliveryRatio:P0}(OnHit 이벤트 없음)";
            plan.Warnings.Add(
                $"{hit.Label} 클립 '{hit.Clip?.name}'에 {OnHitFunctionName}이 없어 {plan.DeliveryKind} 마커를 " +
                $"{FallbackDeliveryRatio:P0} 지점에 놓았습니다 — Animator 레일은 이 경우 타임아웃까지 대기합니다.");
        }

        private static float FirstEventTime(AnimationClip clip, string functionName)
        {
            if (clip == null) return -1f;
            float best = -1f;
            AnimationEvent[] events = clip.events;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].functionName != functionName) continue;
                if (best < 0f || events[i].time < best) best = events[i].time;
            }
            return best;
        }

        // ──────────────────────────────────────────────────────────────
        // 생성 단계
        // ──────────────────────────────────────────────────────────────

        private static TimelineAsset CreateTimelineAsset(JigBakePlan plan, string path)
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, path);

            var animTrack = timeline.CreateTrack<AnimationTrack>(null, "Animation");
            for (int i = 0; i < plan.Clips.Count; i++)
            {
                JigBakeClip c = plan.Clips[i];
                TimelineClip tlClip = animTrack.CreateClip(c.Clip);
                tlClip.start = c.Start;
                tlClip.duration = c.Duration;
                tlClip.timeScale = c.TimeScale;
                tlClip.displayName = $"{c.Label} ({c.StateName})";
            }

            timeline.CreateMarkerTrack();
            MarkerTrack markerTrack = timeline.markerTrack;

            // 전체 Timeline의 기본 공격 구간. 구간 시간의 원본은 이 Marker 둘뿐이다.
            var sectionStart = markerTrack.CreateMarker<PresentationSectionMarker>(0d);
            sectionStart.Configure("Attack", PresentationSectionBoundary.Start);
            var sectionEnd = markerTrack.CreateMarker<PresentationSectionMarker>(plan.SectionEnd);
            sectionEnd.Configure("Attack", PresentationSectionBoundary.End);

            for (int i = 0; i < plan.Cues.Count; i++)
            {
                JigBakeCue c = plan.Cues[i];
                var marker = markerTrack.CreateMarker<PresentationSignalMarker>(c.Time);
                marker.Configure(PresentationSignalKind.Cue, c.Cue.NormalizedCueName, c.Cue.CueId);
                EditorUtility.SetDirty(marker);
            }

            var delivery = markerTrack.CreateMarker<PresentationSignalMarker>(plan.DeliveryTime);
            delivery.Configure(plan.DeliveryKind, null, null);
            EditorUtility.SetDirty(delivery);

            EditorUtility.SetDirty(markerTrack);
            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void LogBakeResult(JigBakePlan plan, string path, string characterKey, TimelineAsset timeline)
        {
            var sb = new StringBuilder();
            sb.Append($"[JigPathABaker] 구움: {path}\n  캐릭터: '{characterKey}' / 클립 {plan.Clips.Count}개, 구간 0~{plan.SectionEnd:F3}s\n");
            for (int i = 0; i < plan.Clips.Count; i++)
            {
                JigBakeClip c = plan.Clips[i];
                sb.Append($"  클립 {c.Label} '{c.StateName}' → {c.Clip.name} @ {c.Start:F3}~{c.Start + c.Duration:F3}s (speed {c.TimeScale:0.##})\n");
            }
            for (int i = 0; i < plan.Cues.Count; i++)
            {
                sb.Append($"  Cue {plan.Cues[i].Label} @ {plan.Cues[i].Time:F3}s [{plan.Cues[i].Source}]\n");
            }
            sb.Append($"  {plan.DeliveryKind} @ {plan.DeliveryTime:F3}s [{plan.DeliveryRule}]\n");
            for (int i = 0; i < plan.Infos.Count; i++) sb.Append($"  ⓘ {plan.Infos[i]}\n");
            for (int i = 0; i < plan.Warnings.Count; i++) sb.Append($"  ⚠ {plan.Warnings[i]}\n");
            sb.Append("  → 저작자: 클립 블렌드, 마커 위치, (투사체면) 발사 시점을 Timeline에서 다듬으세요.");

            if (plan.Warnings.Count > 0) Debug.LogWarning(sb.ToString(), timeline);
            else Debug.Log(sb.ToString(), timeline);
        }

        /// <summary>
        /// 프리팹 이름의 마지막 '_' 뒤 숫자를 템플릿 Index로 읽는다(예: Unit_VillanGun_20002 → "20002").
        /// 런타임 스포너가 같은 `_{Index}` 규칙으로 프리팹을 찾으므로 BattleCharactor.TemplateIndex와 일치한다. 없으면 null.
        /// </summary>
        public static string ResolveTemplateIndex(GameObject characterPrefab)
        {
            if (characterPrefab == null) return null;

            string name = characterPrefab.name;
            int underscore = name.LastIndexOf('_');
            if (underscore < 0 || underscore == name.Length - 1) return null;

            string suffix = name.Substring(underscore + 1).Trim();
            for (int i = 0; i < suffix.Length; i++)
            {
                if (!char.IsDigit(suffix[i])) return null;
            }
            return suffix;
        }

        /// <summary>과도기 폴백용 unitName(프리팹 BattleCharactor 직렬화 값, 없으면 프리팹 이름).</summary>
        public static string ResolveUnitName(GameObject characterPrefab)
        {
            if (characterPrefab == null) return null;
            var bc = characterPrefab.GetComponentInChildren<BattleCharactor>();
            return bc != null ? bc.UnitName : characterPrefab.name;
        }

        /// <summary>굽기 시 기록할 CharacterKey. 템플릿 Index 우선, 없으면 unitName으로 폴백(경고).</summary>
        public static string ResolveBakeKey(GameObject characterPrefab)
        {
            string index = ResolveTemplateIndex(characterPrefab);
            if (!string.IsNullOrEmpty(index)) return index;

            string unitName = ResolveUnitName(characterPrefab);
            Debug.LogWarning(
                $"[JigPathABaker] 프리팹 '{characterPrefab?.name}' 이름에서 템플릿 Index(_숫자)를 찾지 못해 " +
                $"unitName '{unitName}'을 CharacterKey로 사용합니다.",
                characterPrefab);
            return unitName;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(RuntimeFolder))
            {
                AssetDatabase.CreateFolder(RuntimeFolderParent, RuntimeFolderName);
            }
        }

        private static string SanitizeFileName(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return sb.ToString();
        }
    }
}
