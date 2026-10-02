using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>
    /// Timeline 편집 지그 창.
    ///
    /// 지그는 <b>다음 스킬 실행부터 반영되는 편집 도구</b>다. SetupPresentationContext가 페이즈 진입 시점에
    /// CueBinding → RuntimeCue 스냅샷을 만들므로(Director:229), 재생 중인 연출을 실시간으로 갈아끼우지 않는다.
    /// </summary>
    public class SkillPresentationJigWindow : EditorWindow
    {
        [MenuItem("Battle/Skill Presentation Timeline Jig")]
        public static void Open()
        {
            GetWindow<SkillPresentationJigWindow>("Cue Timeline Jig");
        }

        public static void Open(TimelineAsset timeline)
        {
            SkillPresentationJigWindow window =
                GetWindow<SkillPresentationJigWindow>("Cue Timeline Jig");
            window.SetRuntimeTimeline(timeline);
            window.Show();
        }

        [MenuItem("Assets/Skill Presentation Jig에서 열기", false, 2100)]
        private static void OpenSelectedTimeline()
        {
            if (Selection.activeObject is TimelineAsset timeline) Open(timeline);
        }

        [MenuItem("Assets/Skill Presentation Jig에서 열기", true)]
        private static bool ValidateOpenSelectedTimeline() =>
            Selection.activeObject is TimelineAsset;

        private SkillPresentationData _presentation;
        private GameObject _characterPrefab;
        private TimelineAsset _selectedTimelineAsset;

        private JigBuildResult _build;
        private TimelineAsset _runtimeTimeline;
        private readonly List<string> _runtimeSections = new List<string>();
        private int _selectedSection;
        private readonly List<string> _report = new List<string>();
        private Vector2 _scroll;

        private void OnDisable()
        {
            // ── 정리 훅 4개 중 마지막: 창 닫기 ──
            JigPreviewInstance.DestroyInstance();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            bool runtimeMode = _selectedTimelineAsset != null;
            EditorGUILayout.HelpBox(runtimeMode
                    ? "TimelineAsset 직접 편집 모드입니다. 선택한 하나의 Timeline 안에서 전신 트랙과 " +
                      "상·하체 Override 트랙을 함께 편집하고 미리봅니다."
                    : "Legacy Animator Rail 모드입니다. Editor/ 아래 임시 근사 Timeline으로 Phase Cue 시간을 편집합니다.\n" +
                      "이 Timeline은 런타임 자산이 아니며 Phase/local time이 원본입니다.",
                runtimeMode ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space();
            DrawInputs();

            EditorGUILayout.Space();
            DrawActions();

            if (JigPreviewInstance.IsAlive)
            {
                EditorGUILayout.Space();
                DrawPlayback();
                if (runtimeMode)
                {
                    EditorGUILayout.Space();
                    DrawRuntimeSections();
                }
                EditorGUILayout.Space();
                DrawCuePreview();
                EditorGUILayout.Space();
                DrawPreviewCamera();
            }

            if (_build != null)
            {
                EditorGUILayout.Space();
                DrawDiagnostics();
                EditorGUILayout.Space();
                DrawIntervals();
            }

            if (runtimeMode)
            {
                EditorGUILayout.Space();
                DrawRuntimeDiagnostics();
            }

            if (_report.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("실행 결과", EditorStyles.boldLabel);
                for (int i = 0; i < _report.Count; i++)
                {
                    EditorGUILayout.LabelField("• " + _report[i], EditorStyles.wordWrappedMiniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawInputs()
        {
            EditorGUILayout.LabelField("입력", EditorStyles.boldLabel);

            TimelineAsset nextTimeline = (TimelineAsset)EditorGUILayout.ObjectField(
                "Runtime Timeline", _selectedTimelineAsset, typeof(TimelineAsset), false);
            if (nextTimeline != _selectedTimelineAsset) SetRuntimeTimeline(nextTimeline);
            if (GUILayout.Button("Timeline 창의 현재 에셋 가져오기"))
            {
                TimelineAsset inspected = TimelineEditor.inspectedAsset;
                if (inspected != null) SetRuntimeTimeline(inspected);
                else _report.Add("Timeline 창에서 검사 중인 TimelineAsset이 없습니다.");
            }

            EditorGUI.BeginChangeCheck();
            _presentation = (SkillPresentationData)EditorGUILayout.ObjectField(
                "연출 자산", _presentation, typeof(SkillPresentationData), false);
            _characterPrefab = (GameObject)EditorGUILayout.ObjectField(
                "캐릭터 프리팹", _characterPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                // 입력이 바뀌면 기존 지그는 무효다.
                _build = null;
                JigPreviewPlayback.SetFullRange();
                _report.Clear();
            }

            if (_presentation != null && !_presentation.IsPhaseCue)
            {
                EditorGUILayout.HelpBox(
                    $"'{_presentation.name}'은 Schema=0(Legacy)입니다. 'Phase Cue 사용'을 켜고 페이즈·Cue를 채운 뒤 사용하세요.",
                    MessageType.Error);
            }

            if (_selectedTimelineAsset != null && _characterPrefab == null)
                EditorGUILayout.HelpBox(
                    "프리팹 없이도 Timeline 에셋을 열고 편집할 수 있습니다. 포즈 미리보기만 비활성화됩니다.",
                    MessageType.Info);
        }

        private void DrawActions()
        {
            bool canBuild = _presentation != null && _characterPrefab != null
                            && _presentation.IsPhaseCue && !_presentation.IsTimelineRail;

            using (new EditorGUI.DisabledScope(!canBuild))
            {
                if (GUILayout.Button(_build == null ? "Legacy Phase 지그 생성" : "Legacy Phase 지그 재생성", GUILayout.Height(26)))
                {
                    Generate();
                }
            }

            using (new EditorGUI.DisabledScope(_presentation == null || _characterPrefab == null))
            {
                if (GUILayout.Button("Runtime Timeline 에셋 생성 (자동 연결 안 함)", GUILayout.Height(26)))
                {
                    BakeStandaloneTimeline();
                }
            }

            using (new EditorGUI.DisabledScope(_selectedTimelineAsset == null))
            {
                if (GUILayout.Button(_characterPrefab != null
                        ? "선택 Timeline 열기 (상·하체 트랙 프리뷰)"
                        : "선택 Timeline 에셋 열기", GUILayout.Height(26)))
                {
                    OpenVariantForEditing(_selectedTimelineAsset);
                }
            }

            using (new EditorGUI.DisabledScope(_build == null || !_build.CanWriteBack))
            {
                if (GUILayout.Button("마커 → 자산 역기입", GUILayout.Height(24)))
                {
                    WriteBack();
                }
            }

            using (new EditorGUI.DisabledScope(_build == null || !_build.CanWriteBack))
            {
                if (GUILayout.Button("클립 이벤트 시각을 Time에 복사 (동작 변화 없음)"))
                {
                    CopyClipEvents();
                }
            }

            using (new EditorGUI.DisabledScope(_build == null && _selectedTimelineAsset == null))
            {
                if (GUILayout.Button("Timeline 창 열기 · 프리뷰 다시 선택"))
                {
                    if (_selectedTimelineAsset != null)
                        OpenVariantForEditing(_selectedTimelineAsset);
                    else
                    {
                        if (JigPreviewInstance.Current != null)
                            Selection.activeGameObject = JigPreviewInstance.Current;
                        OpenTimelineWindow();
                    }
                }
            }

            if (GUILayout.Button("지그 삭제 · 정리"))
            {
                JigTimelineBuilder.DeleteJigAssets();
                _build = null;
                _report.Clear();
                _report.Add("임시 에셋과 프리뷰 인스턴스를 정리했습니다.");
            }
        }

        /// <summary>
        /// 에디트 모드 슬로우 재생. <b>재생 속도만</b> 바꾼다 — 클립·마커·Cue 값은 무변경(감상용).
        /// director.time을 배속만큼 밀고 Evaluate로 포즈를 갱신한다(<see cref="JigPreviewPlayback"/>).
        /// </summary>
        private void DrawPlayback()
        {
            EditorGUILayout.LabelField("슬로우 재생 (감상용 — 데이터 무변경)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "재생 속도만 조절합니다. 클립·마커·Cue 값은 바뀌지 않습니다.\n" +
                "포즈는 Scene/Game 뷰에 갱신됩니다. 정밀 확인은 Timeline에서 플레이헤드를 직접 끌어(스크럽) 보세요.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 0.25x")) JigPreviewPlayback.Play(0.25f);
                if (GUILayout.Button("▶ 0.5x")) JigPreviewPlayback.Play(0.5f);
                if (GUILayout.Button("▶ 1x")) JigPreviewPlayback.Play(1f);
                if (GUILayout.Button("▶ 2x")) JigPreviewPlayback.Play(2f);
                using (new EditorGUI.DisabledScope(!JigPreviewPlayback.IsPlaying))
                {
                    if (GUILayout.Button("⏸ 정지")) JigPreviewPlayback.Stop();
                }
            }

            if (JigPreviewPlayback.IsPlaying)
            {
                float preview = JigPreviewPlayback.Speed;
                float region = JigPreviewPlayback.CurrentRegionSpeed();
                string hold = JigPreviewPlayback.IsHolding
                    ? $" · Hold 중 (남은 {JigPreviewPlayback.HoldRemaining:0.00}s)"
                    : string.Empty;
                EditorGUILayout.LabelField(
                    $"재생 중 · 프리뷰 {preview:0.##}x × 구간 {region:0.##}x = 현재 {preview * region:0.##}x{hold}",
                    EditorStyles.miniLabel);
                Repaint();   // 재생 중에는 창을 계속 갱신해 상태 표시가 살아 있게 한다.
            }
        }

        private void DrawRuntimeSections()
        {
            EditorGUILayout.LabelField("Timeline Section", EditorStyles.boldLabel);
            if (_runtimeTimeline == null)
            {
                EditorGUILayout.HelpBox("Runtime Timeline을 직접 선택해 여세요.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("전체 Timeline 범위 사용"))
            {
                JigPreviewPlayback.SetFullRange();
                PlayableDirector director = JigPreviewInstance.Director;
                if (director != null)
                {
                    director.time = 0d;
                    director.Evaluate();
                }
            }

            if (_runtimeSections.Count == 0)
            {
                EditorGUILayout.HelpBox("PresentationSectionMarker가 없습니다. 전체 Timeline만 재생할 수 있습니다.", MessageType.None);
                return;
            }

            _selectedSection = Mathf.Clamp(_selectedSection, 0, _runtimeSections.Count - 1);
            _selectedSection = EditorGUILayout.Popup("Section", _selectedSection, _runtimeSections.ToArray());
            if (GUILayout.Button("선택 Section 범위 사용"))
            {
                string id = _runtimeSections[_selectedSection];
                if (PresentationTimelineSections.TryResolve(_runtimeTimeline, id,
                        out PresentationTimelineRange range, out string error))
                {
                    JigPreviewPlayback.SetRange(range);
                    _report.Add($"Section '{id}' 선택: {range.Start:F3}s ~ {range.End:F3}s");
                }
                else
                {
                    _report.Add("Section 선택 실패: " + error);
                }
            }
        }

        private void DrawRuntimeDiagnostics()
        {
            if (_presentation == null || _selectedTimelineAsset == null) return;

            string key = ResolveCharacterKey();
            List<SkillTimelineValidationMessage> messages =
                SkillTimelineValidator.Validate(_presentation, key, _selectedTimelineAsset);

            EditorGUILayout.LabelField("Runtime Timeline 검증", EditorStyles.boldLabel);
            if (messages.Count == 0)
            {
                EditorGUILayout.HelpBox("검증 오류가 없습니다.", MessageType.Info);
                return;
            }

            for (int i = 0; i < messages.Count; i++)
            {
                MessageType type = messages[i].Severity == SkillTimelineValidationSeverity.Error
                    ? MessageType.Error
                    : messages[i].Severity == SkillTimelineValidationSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(messages[i].Message, type);
            }
        }

        private void SetRuntimeTimeline(TimelineAsset timeline)
        {
            _selectedTimelineAsset = timeline;
            _runtimeTimeline = timeline;
            _runtimeSections.Clear();
            if (timeline != null)
                PresentationTimelineSections.CollectSectionIds(timeline, _runtimeSections);
            _selectedSection = 0;
            JigPreviewPlayback.SetFullRange();
            JigPreviewInstance.DestroyInstance();
            Repaint();
        }

        /// <summary>
        /// 이펙트·사운드 미리보기 토글 + 레지스트리 선택. 슬로우 재생 중 Cue 시각에 근사로 발화한다(<see cref="JigCuePreview"/>).
        /// 원본 데이터 무영향 — 재생/스폰만 한다.
        /// </summary>
        private void DrawCuePreview()
        {
            EditorGUILayout.LabelField("이펙트·사운드 미리보기 (근사)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "슬로우 재생 중 Cue 시각에 사운드·소켓 이펙트를 근사로 발화합니다.\n" +
                "투사체·타깃 이펙트와 정확한 발화·믹싱은 PreviewScene에서 확인하세요.",
                MessageType.None);

            bool sound = EditorGUILayout.ToggleLeft("사운드 미리보기", JigCuePreview.SoundEnabled);
            if (sound != JigCuePreview.SoundEnabled)
            {
                JigCuePreview.SoundEnabled = sound;
                if (!sound) JigCuePreview.OnSoundToggledOff();
            }

            bool eff = EditorGUILayout.ToggleLeft("이펙트 미리보기 (근사)", JigCuePreview.EffectEnabled);
            if (eff != JigCuePreview.EffectEnabled)
            {
                JigCuePreview.EffectEnabled = eff;
                if (!eff) JigCuePreview.OnEffectToggledOff();
            }

            JigCuePreview.EffectRegistry = (EffectRegistry)EditorGUILayout.ObjectField(
                "Effect Registry", JigCuePreview.EffectRegistry, typeof(EffectRegistry), false);

            if (JigCuePreview.EffectEnabled && JigCuePreview.EffectRegistry == null)
            {
                EditorGUILayout.HelpBox(
                    "레지스트리 자산을 지정하세요(프로젝트에 정확히 1개면 자동 선택됩니다).",
                    MessageType.Warning);
            }
        }

        /// <summary>
        /// 프리뷰 카메라 보조. 뷰포트는 이 창에 두지 않는다 — <b>Game View가 직접 그린다.</b>
        /// 각도를 잡을 때는 하이어라키에서 카메라를 선택해 Scene View 기즈모로 옮기면 된다.
        /// </summary>
        private void DrawPreviewCamera()
        {
            GameObject preview = JigPreviewInstance.Current;
            Camera cam = JigPreviewCamera.Ensure(preview);

            EditorGUILayout.HelpBox(
                "Game View가 프리뷰 카메라로 그립니다(기존 카메라는 끄지 않고 위에 겹칩니다).\n" +
                "실행하면 프리뷰가 자동으로 사라지고 원래 게임 카메라가 잡습니다.\n" +
                "각도는 카메라를 선택해 Scene View에서 직접 옮기면 됩니다 — 자산에 저장되지 않습니다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("카메라 선택") && cam != null)
                {
                    Selection.activeGameObject = cam.gameObject;
                }
                if (GUILayout.Button("다시 프레이밍"))
                {
                    JigPreviewCamera.Frame(preview);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("정면")) JigPreviewCamera.ApplyPreset(JigPreviewCamera.Angle.Front);
                if (GUILayout.Button("3/4")) JigPreviewCamera.ApplyPreset(JigPreviewCamera.Angle.ThreeQuarter);
                if (GUILayout.Button("측면")) JigPreviewCamera.ApplyPreset(JigPreviewCamera.Angle.Right);
                if (GUILayout.Button("후면")) JigPreviewCamera.ApplyPreset(JigPreviewCamera.Angle.Back);
                if (GUILayout.Button("위")) JigPreviewCamera.ApplyPreset(JigPreviewCamera.Angle.Top);
            }
        }

        private void DrawDiagnostics()
        {
            if (_build.HasResolutionFailure)
            {
                EditorGUILayout.HelpBox(
                    "해석 실패 구간이 있어 편집 결과를 저장할 수 없습니다(역기입 비활성화).\n" +
                    "구간 위치를 신뢰할 수 없으므로 아래 실패를 먼저 해결하세요.",
                    MessageType.Error);

                for (int i = 0; i < _build.Failures.Count; i++)
                {
                    EditorGUILayout.LabelField("✗ " + _build.Failures[i], EditorStyles.wordWrappedMiniLabel);
                }
            }

            if (_build.Warnings.Count > 0)
            {
                EditorGUILayout.HelpBox("경고 " + _build.Warnings.Count + "건", MessageType.Warning);
                for (int i = 0; i < _build.Warnings.Count; i++)
                {
                    EditorGUILayout.LabelField("! " + _build.Warnings[i], EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawIntervals()
        {
            EditorGUILayout.LabelField("구간 (근사 프리뷰)", EditorStyles.boldLabel);

            for (int i = 0; i < _build.Intervals.Count; i++)
            {
                JigPhaseInterval iv = _build.Intervals[i];
                string cueCount = iv.Cues != null ? iv.Cues.Count.ToString() : "-";
                string state = iv.Clip != null ? iv.Clip.name : "(빈 구간)";
                string flag = iv.Resolved ? (iv.CanHostCues ? "" : "  [Cue 불가]") : "  ✗ 실패";

                EditorGUILayout.LabelField(
                    $"{iv.Label,-22} {iv.Start,7:F3}s ~ {iv.Start + iv.Duration,7:F3}s   " +
                    $"clip={state}  cue={cueCount}{flag}",
                    EditorStyles.miniLabel);
            }

            if (_presentation.Post != null && _presentation.Post.Enabled && _presentation.Post.ExtraDelay > 0f)
            {
                EditorGUILayout.HelpBox(
                    $"Post.ExtraDelay {_presentation.Post.ExtraDelay:F2}s 구간에는 Cue를 배치할 수 없습니다 — " +
                    "클립이 끝난 뒤라 재생 중인 state가 없어 드라이버가 시각을 해석할 근거가 없습니다.",
                    MessageType.Warning);
            }
        }

        // ──────────────────────────────────────────────────────────────

        private void Generate()
        {
            _report.Clear();

            // ★자산을 건드리는 첫 시점. CueId가 없으면 부여한다(§2.5.3).
            Undo.RecordObject(_presentation, "CueId 부여");
            if (_presentation.EnsureCueIds())
            {
                EditorUtility.SetDirty(_presentation);
                AssetDatabase.SaveAssets();
                _report.Add("이 자산에 CueId를 부여했습니다(자산이 수정되었습니다). 마커 역기입 대상을 특정하기 위해 필요합니다.");
            }

            _build = JigTimelineBuilder.Build(_presentation, _characterPrefab, out string error);
            if (_build == null)
            {
                _report.Add("생성 실패: " + error);
                EditorUtility.DisplayDialog("지그 생성 실패", error, "확인");
                return;
            }

            _report.Add($"생성 완료: {_build.AssetPath}");

            // ★TimelineAsset을 선택하면 창이 '에셋 모드'로 열려 바인딩이 없다 → 스크러빙해도 포즈가 안 움직인다.
            //   PlayableDirector가 붙은 프리뷰 인스턴스를 선택해야 Animation Track이 Animator를 구동한다.
            if (JigPreviewInstance.Current != null)
            {
                Selection.activeGameObject = JigPreviewInstance.Current;
                _report.Add("프리뷰 인스턴스를 선택했습니다. Timeline 창에서 플레이헤드를 끌면 포즈가 움직입니다.");
            }
            else
            {
                Selection.activeObject = _build.Timeline;
                _report.Add("프리뷰 인스턴스가 없어 에셋만 선택했습니다(포즈 미리보기 불가).");
            }

            OpenTimelineWindow();
        }

        private void BakeStandaloneTimeline()
        {
            _report.Clear();
            TimelineAsset timeline = JigPathABaker.Bake(_presentation, _characterPrefab, out string error);
            if (timeline == null)
            {
                _report.Add("Timeline 생성 실패: " + error);
                EditorUtility.DisplayDialog("Timeline 생성 실패", error, "확인");
                return;
            }

            SetRuntimeTimeline(timeline);
            _report.Add($"독립 Timeline 생성 완료: {AssetDatabase.GetAssetPath(timeline)}");
            _report.Add("SkillPresentationData에는 자동 연결하지 않았습니다.");
            OpenVariantForEditing(timeline);
        }

        /// <summary>Timeline 창을 띄운다. Window ▸ Sequencing ▸ Timeline 을 찾아 들어가지 않아도 되게 한다.</summary>
        private static void OpenTimelineWindow()
        {
            if (!EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline"))
            {
                Debug.LogWarning("[Jig] Timeline 창을 자동으로 열지 못했습니다. " +
                                 "Window ▸ Sequencing ▸ Timeline 을 직접 열어주세요.");
            }
        }

        /// <summary>
        /// TimelineAsset을 캐릭터 프리뷰 인스턴스(Director+Animator 바인딩)에 물려 Timeline 창으로 연다.
        /// 이렇게 해야 스크러빙 시 캐릭터 포즈가 실제로 움직인다(에셋만 선택하면 바인딩이 없어 정지).
        /// </summary>
        private void OpenVariantForEditing(TimelineAsset timeline)
        {
            if (timeline == null) return;

            _runtimeTimeline = timeline;
            PresentationTimelineSections.CollectSectionIds(timeline, _runtimeSections);
            _selectedSection = 0;
            JigPreviewPlayback.SetFullRange();
            int cueCount = JigCuePreview.SetFiresFromTimeline(_presentation, timeline, _report);
            _report.Add($"Runtime Timeline Cue 프리뷰 {cueCount}개를 불러왔습니다.");

            GameObject preview = JigPreviewInstance.Create(_characterPrefab, out string err);
            if (preview == null)
            {
                Debug.LogWarning($"[Jig] 프리뷰 인스턴스 생성 실패({err}) — 에셋만 엽니다(포즈 미리보기 불가).");
                Selection.activeObject = timeline;
                OpenTimelineWindow();
                return;
            }

            PlayableDirector director = JigPreviewInstance.Director;
            Animator animator = JigPreviewInstance.ResolveAnimator();
            director.playableAsset = timeline;

            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is AnimationTrack && animator != null)
                {
                    director.SetGenericBinding(track, animator);
                }
            }

            Selection.activeGameObject = preview;
            OpenTimelineWindow();
        }

        // 표시·검증 메시지용 키. 굽기와 같은 규칙(템플릿 Index 우선, 없으면 unitName).
        private string ResolveCharacterKey()
        {
            string index = JigPathABaker.ResolveTemplateIndex(_characterPrefab);
            return !string.IsNullOrEmpty(index) ? index : JigPathABaker.ResolveUnitName(_characterPrefab);
        }

        private void WriteBack()
        {
            _report.Clear();
            int changed = JigTimelineBuilder.WriteBack(_presentation, _build.Timeline, _report);
            _report.Insert(0, changed > 0
                ? $"{changed}개 Cue의 Time을 갱신했습니다."
                : "변경된 Cue가 없습니다.");

            if (IsDerivedSourceAsset(_presentation))
            {
                _report.Add("※ 이 자산은 Merged 파생 대상입니다. JcMergedAssetBuilder의 staleness 해시는 " +
                            "Cue 시각 변경을 감지하지 못하므로 Merged를 수동으로 재빌드하세요.");
            }
        }

        private void CopyClipEvents()
        {
            _report.Clear();
            int changed = JigTimelineBuilder.CopyClipEventTimes(_presentation, _build, _report);
            _report.Insert(0, changed > 0
                ? $"{changed}개 Cue에 클립 이벤트 시각을 복사했습니다(Timing은 ClipEvent 그대로 — 동작 변화 없음)."
                : "복사할 클립 이벤트가 없습니다.");
        }

        private static bool IsDerivedSourceAsset(SkillPresentationData data)
        {
            string path = AssetDatabase.GetAssetPath(data);
            return !string.IsNullOrEmpty(path) && path.Contains("/RenderFX/_Seam/Data/");
        }
    }
}
