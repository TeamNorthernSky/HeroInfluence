using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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

        private SkillPresentationData _presentation;
        private GameObject _characterPrefab;

        private JigBuildResult _build;
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

            EditorGUILayout.HelpBox(
                "Cue 시각을 포즈를 보며 편집하는 도구입니다.\n" +
                "• 생성물은 Editor/ 아래 임시 에셋이며 빌드·씬에 남지 않습니다.\n" +
                "• 마커는 발화하지 않습니다 — 실제 발화·데미지·홀드 확인은 PreviewScene에서 하세요.\n" +
                "• 전체 타임라인은 근사 프리뷰입니다. 편집의 진실은 '해당 phase/state 진입 후 local time'입니다.",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawInputs();

            EditorGUILayout.Space();
            DrawActions();

            if (JigPreviewInstance.IsAlive)
            {
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

            EditorGUI.BeginChangeCheck();
            _presentation = (SkillPresentationData)EditorGUILayout.ObjectField(
                "연출 자산", _presentation, typeof(SkillPresentationData), false);
            _characterPrefab = (GameObject)EditorGUILayout.ObjectField(
                "캐릭터 프리팹", _characterPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                // 입력이 바뀌면 기존 지그는 무효다.
                _build = null;
                _report.Clear();
            }

            if (_presentation != null && !_presentation.IsPhaseCue)
            {
                EditorGUILayout.HelpBox(
                    $"'{_presentation.name}'은 Schema=0(Legacy)입니다. 'Phase Cue 사용'을 켜고 페이즈·Cue를 채운 뒤 사용하세요.",
                    MessageType.Error);
            }
        }

        private void DrawActions()
        {
            bool canBuild = _presentation != null && _characterPrefab != null && _presentation.IsPhaseCue;

            using (new EditorGUI.DisabledScope(!canBuild))
            {
                if (GUILayout.Button(_build == null ? "지그 생성" : "지그 재생성", GUILayout.Height(26)))
                {
                    Generate();
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

            using (new EditorGUI.DisabledScope(_build == null))
            {
                if (GUILayout.Button("Timeline 창 열기 · 프리뷰 다시 선택"))
                {
                    if (JigPreviewInstance.Current != null)
                    {
                        Selection.activeGameObject = JigPreviewInstance.Current;
                    }
                    OpenTimelineWindow();
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

        /// <summary>Timeline 창을 띄운다. Window ▸ Sequencing ▸ Timeline 을 찾아 들어가지 않아도 되게 한다.</summary>
        private static void OpenTimelineWindow()
        {
            if (!EditorApplication.ExecuteMenuItem("Window/Sequencing/Timeline"))
            {
                Debug.LogWarning("[Jig] Timeline 창을 자동으로 열지 못했습니다. " +
                                 "Window ▸ Sequencing ▸ Timeline 을 직접 열어주세요.");
            }
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
