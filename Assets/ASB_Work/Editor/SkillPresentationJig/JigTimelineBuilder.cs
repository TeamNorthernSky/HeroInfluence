using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>빌드 결과와 편집자에게 보여줄 진단.</summary>
    public sealed class JigBuildResult
    {
        public TimelineAsset Timeline;
        public string AssetPath;
        public List<JigPhaseInterval> Intervals = new List<JigPhaseInterval>();

        /// <summary>경계 블렌드(겹침/연장) 계산 결과. 표시·진단용.</summary>
        public List<JigBlendBoundary> Boundaries = new List<JigBlendBoundary>();

        /// <summary>Cue 미리보기(사운드·이펙트) 발화 목록. 마커 시각과 동일 계산을 재사용해 수집한다.</summary>
        public List<JigCueFire> CueFires = new List<JigCueFire>();

        /// <summary>해석 실패가 하나라도 있으면 역기입을 허용하지 않는다(§2.5.1).</summary>
        public bool HasResolutionFailure;

        public readonly List<string> Failures = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool CanWriteBack => Timeline != null && !HasResolutionFailure;
    }

    /// <summary>
    /// 연출 SO → 임시 TimelineAsset 생성, 그리고 마커 → SO 역기입.
    ///
    /// 트랙은 <b>SO로부터 생성된 표시물</b>이다. TimelineClip의 duration/clipIn/timeScale을 진실로 두지 않는다.
    /// 편집 결과는 반드시 SO로 역기입한 뒤 트랙을 재생성한다.
    /// </summary>
    public static class JigTimelineBuilder
    {
        /// <summary>임시 에셋 폴더. Editor/ 아래이므로 플레이어 빌드에 포함되지 않는다.</summary>
        public const string TempFolderParent = "Assets/ASB_Work/Editor";
        public const string TempFolderName = "SkillPresentationJigTemp";
        public const string TempFolder = TempFolderParent + "/" + TempFolderName;

        private const string CueFunctionName = "AniEvent_PresentationCue";

        // ──────────────────────────────────────────────────────────────
        // 생성
        // ──────────────────────────────────────────────────────────────

        public static JigBuildResult Build(SkillPresentationData data, GameObject characterPrefab, out string error)
        {
            error = null;
            var result = new JigBuildResult();

            if (data == null)
            {
                error = "연출 자산이 비어 있습니다.";
                return null;
            }

            if (!data.IsPhaseCue)
            {
                error = $"'{data.name}'은 Schema=0(Legacy)입니다. 'Phase Cue 사용'을 켜고 페이즈·Cue를 채운 뒤 다시 시도하세요.\n" +
                        "(5010/5020/5030은 한 세대 더 오래된 형식이라 EffectRegistry 등록부터 필요합니다.)";
                return null;
            }

            GameObject preview = JigPreviewInstance.Create(characterPrefab, out error);
            if (preview == null)
            {
                return null;
            }

            Animator animator = JigPreviewInstance.ResolveAnimator();
            RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
            if (controller == null)
            {
                JigPreviewInstance.DestroyInstance();
                error = $"프리팹 '{characterPrefab.name}'의 Animator에 Controller가 없습니다.";
                return null;
            }

            var movement = preview.GetComponent<UnitMovementProfile>();
            result.Intervals = JigPhaseLayout.Build(data, controller, movement, out bool anyFailure);
            result.HasResolutionFailure = anyFailure;

            // 경계 블렌드(겹침/연장) 계산 — 인터벌의 Start/DisplayDuration을 겹침 반영으로 갱신한다.
            // 아래 클립·마커 생성이 이 갱신된 Start를 쓰므로 반드시 클립 생성 전에 부른다.
            result.Boundaries = JigPhaseLayout.ComputeBlendBoundaries(result.Intervals);
            for (int b = 0; b < result.Boundaries.Count; b++)
            {
                if (!string.IsNullOrEmpty(result.Boundaries[b].Warning))
                {
                    result.Warnings.Add(result.Boundaries[b].Warning);
                }
            }

            for (int i = 0; i < result.Intervals.Count; i++)
            {
                JigPhaseInterval iv = result.Intervals[i];
                if (!iv.Resolved)
                {
                    result.Failures.Add($"{iv.Label}: {iv.FailureReason}");
                }
            }

            if (result.Intervals.Count == 0)
            {
                JigPreviewInstance.DestroyInstance();
                error = "구간이 하나도 없습니다. 활성화된 페이즈와 AnimationStateName을 확인하세요.";
                return null;
            }

            // ── 임시 에셋 ──
            EnsureTempFolder();
            string path = $"{TempFolder}/Jig_{data.name}_{characterPrefab.name}.playable";
            AssetDatabase.DeleteAsset(path);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, path);
            result.Timeline = timeline;
            result.AssetPath = path;

            // 프레임 레이트 정렬 — 마커가 클립 프레임에 스냅되게 한다.
            // 맞추지 않으면 드래그 결과가 클립 프레임과 어긋나 왕복 변환이 스냅 오차만큼 깨진다.
            float frameRate = ResolveFrameRate(result.Intervals);
            if (frameRate > 0f)
            {
                timeline.editorSettings.frameRate = frameRate;
            }

            var animTrack = timeline.CreateTrack<AnimationTrack>(null, "Animation (근사 프리뷰)");
            timeline.CreateMarkerTrack();
            TrackAsset markerTrack = timeline.markerTrack;

            for (int i = 0; i < result.Intervals.Count; i++)
            {
                JigPhaseInterval iv = result.Intervals[i];

                if (iv.Clip != null)
                {
                    TimelineClip clip = animTrack.CreateClip(iv.Clip);
                    clip.start = iv.Start;   // ★ComputeBlendBoundaries가 겹침 반영해 갱신한 Start
                    clip.displayName = iv.Label;

                    // 클립 길이 = DisplayDuration(BaseClipDuration + PostBoundary 연장분).
                    //   BaseClipDuration: 로코모션=이동 지속시간 / 그 외=실효 길이(Post ExtraDelay 제외)
                    double clipDuration = iv.DisplayDuration > 0d ? iv.DisplayDuration : iv.BaseClipDuration;
                    if (clipDuration > 0d)
                    {
                        clip.duration = clipDuration;
                    }

                    // 연장 정책(§4.2): 로코모션은 루프. 비루프 애니 클립은 끝 포즈 Hold —
                    // Timeline은 clip.duration이 클립 길이보다 길면 기본이 마지막 프레임 Hold이므로 별도 처리 없이 성립한다.
                    // (TimelineClip.postExtrapolationMode는 읽기 전용이라 AnimationPlayableAsset.loop만 켠다.)
                    if (iv.IsLocomotion && iv.EffectiveLength > 0d && clipDuration > iv.EffectiveLength + 1e-4d
                        && clip.asset is AnimationPlayableAsset playable)
                    {
                        playable.loop = AnimationPlayableAsset.LoopMode.On;
                    }
                }

                // 로코모션 해석 실패는 치명적이지 않으므로 경고로만 알린다(프리뷰만 비어 보임).
                if (iv.IsLocomotion && !string.IsNullOrEmpty(iv.FailureReason))
                {
                    result.Warnings.Add($"{iv.Label}: {iv.FailureReason}");
                }

                AddMarkersForInterval(markerTrack, iv, result);
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            // Animator 바인딩 — 실제 씬 캐릭터가 아니라 프리뷰 인스턴스에만 연결한다.
            PlayableDirector director = JigPreviewInstance.Director;
            if (director != null)
            {
                director.playableAsset = timeline;
                director.SetGenericBinding(animTrack, animator);
            }

            // Cue 미리보기(사운드·이펙트) 준비 — 레지스트리 자동 해석 + 발화 목록 설정(빌드 교체 시 이전 상태 정리).
            JigCuePreview.AutoResolveRegistries();
            JigCuePreview.SetFires(result.CueFires);

            return result;
        }

        private static void AddMarkersForInterval(TrackAsset markerTrack, JigPhaseInterval iv, JigBuildResult result)
        {
            if (markerTrack == null || iv.Cues == null)
            {
                return;
            }

            // 클립이 가진 이벤트 시각(정규화된 이름 → 시각 목록). 런타임과 같은 정규화를 쓴다.
            Dictionary<string, List<float>> clipEvents = CollectClipEventTimes(iv.Clip);

            for (int c = 0; c < iv.Cues.Count; c++)
            {
                CueBinding cue = iv.Cues[c];
                if (cue == null) continue;

                string norm = cue.NormalizedCueName;
                if (string.IsNullOrEmpty(norm))
                {
                    result.Warnings.Add($"{iv.Label}.Cues[{c}]: CueName이 비어 실행되지 않습니다.");
                    continue;
                }

                if (!iv.Resolved)
                {
                    // 실패 구간의 Cue는 위치를 신뢰할 수 없으므로 마커를 만들지 않는다.
                    continue;
                }

                if (cue.IsDataTimed)
                {
                    double local = cue.ResolveFireSeconds((float)iv.EffectiveLength);

                    // ★발화 가능 범위 검증. SO의 OnValidate는 NormalizedTime 상한만 보고,
                    //   Seconds는 실제 클립 길이를 알아야 검증할 수 있어 여기까지 미뤄둔 항목이다.
                    if (local < 0d)
                    {
                        result.Warnings.Add(
                            $"{iv.Label}.Cues[{c}] '{norm}': 시각을 해석할 수 없습니다(길이 불명).");
                    }
                    else if (local >= iv.CueLimit)
                    {
                        result.Warnings.Add(
                            $"{iv.Label}.Cues[{c}] '{norm}': 발화 시각 {local:F3}s가 발화 보장 한계 " +
                            $"{iv.CueLimit:F3}s(클립 {iv.EffectiveLength:F3}s × {JigPhaseLayout.CueFireGuaranteedLimit:F2}) " +
                            "이후입니다 → 발화하지 않을 수 있습니다. 더 이른 시각으로 옮기세요.");
                    }

                    double dataFire = iv.Start + Mathf.Max(0f, (float)local);
                    CreateMarker(markerTrack, dataFire, cue, iv, fromClipEvent: false);
                    if (local >= 0d) TryCollectCueFire(result, dataFire, cue);
                    continue;
                }

                // ClipEvent — 클립의 실제 이벤트 시각에 읽기 전용 마커
                if (clipEvents.TryGetValue(norm, out List<float> times) && times.Count > 0)
                {
                    for (int t = 0; t < times.Count; t++)
                    {
                        double clipFire = iv.Start + times[t];
                        CreateMarker(markerTrack, clipFire, cue, iv, fromClipEvent: true);
                        TryCollectCueFire(result, clipFire, cue);
                    }
                }
                else
                {
                    result.Warnings.Add(
                        $"{iv.Label}.Cues[{c}] '{norm}': Timing=ClipEvent인데 클립 '{iv.Clip?.name}'에 " +
                        "같은 이름의 AniEvent_PresentationCue가 없습니다 → 발화하지 않습니다.");
                }
            }
        }

        /// <summary>
        /// 미리보기 발화 목록에 수집한다(§4 술어). Held(InstanceKey 있거나 Signal/Stop)와 발화할 게 없는 Cue는 제외.
        /// Target 계열 앵커도 수집한다 — 사운드는 재생하고 이펙트만 스킵하는 판정은 JigCuePreview가 한다.
        /// </summary>
        private static void TryCollectCueFire(JigBuildResult result, double fireTime, CueBinding cue)
        {
            if (cue == null || cue.Operation != CueOperation.Spawn || !string.IsNullOrWhiteSpace(cue.InstanceKey))
            {
                return;
            }

            bool hasEffect = cue.EffectIds != null && cue.EffectIds.Count > 0;
            bool hasSound = cue.SoundIds != null && cue.SoundIds.Count > 0;
            if (!hasEffect && !hasSound)
            {
                return;
            }

            result.CueFires.Add(new JigCueFire
            {
                FireTime = fireTime,
                EffectIds = cue.EffectIds,
                SoundIds = cue.SoundIds,
                Anchor = cue.Anchor,
                Socket = cue.Socket,
                Label = cue.NormalizedCueName,
            });
        }

        private static void CreateMarker(TrackAsset markerTrack, double time, CueBinding cue,
            JigPhaseInterval iv, bool fromClipEvent)
        {
            var marker = markerTrack.CreateMarker<CueMarker>(time);
            marker.CueId = cue.CueId;
            marker.CueName = cue.NormalizedCueName;
            marker.PhaseLabel = iv.Label;
            marker.FromClipEvent = fromClipEvent;
            marker.PhaseStart = iv.Start;
            marker.EffectiveLength = iv.EffectiveLength;
            marker.TimingSource = (int)cue.Timing;
        }

        /// <summary>클립의 Cue 이벤트 시각. 이름은 런타임과 같이 trim + 소문자로 정규화한다.</summary>
        public static Dictionary<string, List<float>> CollectClipEventTimes(AnimationClip clip)
        {
            var map = new Dictionary<string, List<float>>();
            if (clip == null)
            {
                return map;
            }

            AnimationEvent[] events = clip.events;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].functionName != CueFunctionName) continue;
                string param = events[i].stringParameter;
                if (string.IsNullOrWhiteSpace(param)) continue;

                string norm = param.Trim().ToLowerInvariant();
                if (!map.TryGetValue(norm, out List<float> list))
                {
                    list = new List<float>();
                    map[norm] = list;
                }
                list.Add(events[i].time);
            }
            return map;
        }

        private static float ResolveFrameRate(List<JigPhaseInterval> intervals)
        {
            for (int i = 0; i < intervals.Count; i++)
            {
                AnimationClip clip = intervals[i].Clip;
                if (clip != null && clip.frameRate > 0f)
                {
                    return clip.frameRate;
                }
            }
            return 0f;
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(TempFolderParent, TempFolderName);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 역기입
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 마커 위치를 SO의 <c>CueBinding.Time</c>에 쓴다.
        /// phaseStart/실효 길이는 <b>재계산하지 않고 마커에 저장된 생성 시점 스냅샷</b>을 쓴다 —
        /// 재계산 결과가 생성 당시와 달라지면 조용히 틀린 값이 들어간다.
        /// </summary>
        public static int WriteBack(SkillPresentationData data, TimelineAsset timeline, List<string> report)
        {
            if (data == null || timeline == null || timeline.markerTrack == null)
            {
                return 0;
            }

            Dictionary<string, CueBinding> byId = IndexCuesById(data);
            Undo.RecordObject(data, "Cue 시각 역기입");

            int changed = 0;
            foreach (IMarker m in timeline.markerTrack.GetMarkers())
            {
                if (!(m is CueMarker marker) || marker.FromClipEvent)
                {
                    continue;   // 클립 이벤트 마커는 시각이 클립 안에 있다 → 역기입 대상 아님
                }

                if (string.IsNullOrEmpty(marker.CueId) || !byId.TryGetValue(marker.CueId, out CueBinding cue))
                {
                    report?.Add($"'{marker.CueName}': 대상 Cue를 찾지 못했습니다(자산이 그사이 편집됨). 건너뜁니다.");
                    continue;
                }

                if ((int)cue.Timing != marker.TimingSource)
                {
                    report?.Add($"'{marker.CueName}': Timing이 생성 이후 바뀌었습니다({(CueTimingSource)marker.TimingSource} → {cue.Timing}). 건너뜁니다.");
                    continue;
                }

                float next = JigPhaseLayout.ToCueTime(cue.Timing, marker.time, marker.PhaseStart,
                    marker.EffectiveLength, out bool clamped);

                if (clamped)
                {
                    report?.Add($"'{marker.CueName}': 발화 보장 범위를 벗어나 {next:F3}으로 클램프했습니다.");
                }

                if (!Mathf.Approximately(cue.Time, next))
                {
                    cue.Time = next;
                    changed++;
                }
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
            return changed;
        }

        /// <summary>
        /// 클립 이벤트 시각을 <c>CueBinding.Time</c>에 복사한다(§2.7).
        /// 기본을 NormalizedTime으로 고정한다 — 캐릭터별 오버라이드로 클립 길이가 달라지기 때문이다.
        /// 동작은 바뀌지 않는다(억제 규칙으로 클립 이벤트가 계속 이김).
        /// </summary>
        public static int CopyClipEventTimes(SkillPresentationData data, JigBuildResult build, List<string> report)
        {
            if (data == null || build == null)
            {
                return 0;
            }

            Undo.RecordObject(data, "클립 이벤트 시각 복사");
            int changed = 0;

            for (int i = 0; i < build.Intervals.Count; i++)
            {
                JigPhaseInterval iv = build.Intervals[i];
                if (!iv.CanHostCues || iv.Cues == null) continue;

                Dictionary<string, List<float>> events = CollectClipEventTimes(iv.Clip);

                for (int c = 0; c < iv.Cues.Count; c++)
                {
                    CueBinding cue = iv.Cues[c];
                    if (cue == null || cue.IsDataTimed) continue;

                    string norm = cue.NormalizedCueName;
                    if (string.IsNullOrEmpty(norm)) continue;
                    if (!events.TryGetValue(norm, out List<float> times) || times.Count == 0) continue;

                    if (times.Count > 1)
                    {
                        report?.Add($"{iv.Label} '{norm}': 클립에 이벤트가 {times.Count}개 있어 첫 번째({times[0]:F3}s)만 복사합니다.");
                    }

                    float normalized = (float)(times[0] / iv.EffectiveLength);
                    normalized = Mathf.Clamp(normalized, 0f, JigPhaseLayout.MaxWritableNormalizedTime);

                    cue.Time = normalized;
                    // Timing은 바꾸지 않는다 — ClipEvent로 남겨 동작을 유지한다.
                    changed++;
                    report?.Add($"{iv.Label} '{norm}': Time = {normalized:F4} (클립 {times[0]:F3}s / 길이 {iv.EffectiveLength:F3}s)");
                }
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
            return changed;
        }

        private static Dictionary<string, CueBinding> IndexCuesById(SkillPresentationData data)
        {
            var map = new Dictionary<string, CueBinding>();
            foreach (CueBinding cue in EnumerateAllCues(data))
            {
                if (cue != null && !string.IsNullOrEmpty(cue.CueId) && !map.ContainsKey(cue.CueId))
                {
                    map[cue.CueId] = cue;
                }
            }
            return map;
        }

        public static IEnumerable<CueBinding> EnumerateAllCues(SkillPresentationData data)
        {
            if (data == null) yield break;

            foreach (PhaseBase phase in data.GetPhases())
            {
                if (phase is CuePhase cuePhase && cuePhase.Cues != null)
                {
                    for (int i = 0; i < cuePhase.Cues.Count; i++) yield return cuePhase.Cues[i];
                }
            }

            if (data.MovingAttack?.Cues != null)
            {
                for (int i = 0; i < data.MovingAttack.Cues.Count; i++) yield return data.MovingAttack.Cues[i];
            }

            if (data.Attack?.Beats != null)
            {
                for (int b = 0; b < data.Attack.Beats.Count; b++)
                {
                    List<CueBinding> cues = data.Attack.Beats[b]?.Cues;
                    if (cues == null) continue;
                    for (int i = 0; i < cues.Count; i++) yield return cues[i];
                }
            }
        }

        public static void DeleteJigAssets()
        {
            JigPreviewInstance.DestroyInstance();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
                AssetDatabase.Refresh();
            }
        }
    }
}
