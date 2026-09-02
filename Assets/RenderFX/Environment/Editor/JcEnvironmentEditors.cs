using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JC.VFX;

namespace JC.Env.EditorTools
{
    /// <summary>
    /// 환경 컨트롤러 인스펙터 — 프로파일을 <b>인라인으로 편집</b>하고 값이 바뀌면 즉시 씬에 반영한다(라이브 프리뷰).
    ///
    /// ★260902 구조 개편: 하늘 파라미터를 프로젝트 창의 재질에서 찾아 만지는 대신
    ///   씬 하이어라키에서 JC_Environment 하나만 선택하면 전부 조절된다.
    ///   재질은 출력물이므로 직접 열 일이 없다(이중 정본 제거).
    ///
    /// 저장 규격은 [[reference-unity-so-live-tuning]] 그대로 — 조절값은 메모리, 디스크 확정은 명시 💾.
    /// 단 하늘 재질도 함께 갱신되므로 💾 는 <b>프로파일 + 하늘 재질</b>을 같이 저장한다.
    /// </summary>
    [CustomEditor(typeof(JcEnvironmentController))]
    public class JcEnvironmentControllerEditor : Editor
    {
        private const string FoldKey = "JC.Env.InlineProfileFold";
        private SerializedObject _profileSO;
        private Object _cachedProfile;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var c = (JcEnvironmentController)target;

            EditorGUILayout.Space(6);

            if (c.Profile == null)
            {
                EditorGUILayout.HelpBox("Profile 슬롯에 환경 프로파일(ENV_*)을 꽂아 주세요.", MessageType.Warning);
                return;
            }

            // ── 프로파일 인라인 편집 (라이브 반영) ──
            if (_profileSO == null || _cachedProfile != c.Profile)
            {
                _profileSO = new SerializedObject(c.Profile);
                _cachedProfile = c.Profile;
            }

            bool fold = EditorPrefs.GetBool(FoldKey, true);
            bool newFold = EditorGUILayout.Foldout(fold,
                $"환경 프로파일 — {c.Profile.name}  (값 변경 시 즉시 씬 반영)", true, EditorStyles.foldoutHeader);
            if (newFold != fold) EditorPrefs.SetBool(FoldKey, newFold);

            if (newFold)
            {
                _profileSO.Update();
                EditorGUI.BeginChangeCheck();

                using (new EditorGUI.IndentLevelScope())
                {
                    var it = _profileSO.GetIterator();
                    bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false;
                        if (it.propertyPath == "m_Script") continue;
                        EditorGUILayout.PropertyField(it, true);
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    _profileSO.ApplyModifiedProperties();
                    ApplyLive(c);   // ★슬라이더를 움직이는 즉시 씬이 따라온다
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "튜닝 흐름: 위 값을 직접 조절(즉시 반영) → [💾]로 파일 확정.\n" +
                "씬에서 라이트를 직접 스윕했다면 [● 캡처]로 프로파일에 담고 저장한다.\n" +
                "★순서 권장: 태양 강도를 0으로 내리고 하늘(①)만으로 기저 밝기를 잡은 뒤, 태양(③)을 올린다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 적용 — 프로파일 → 씬", GUILayout.Height(28)))
                {
                    c.ApplyNow();
                    MarkSceneDirty(c);
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("● 캡처 — 씬 → 프로파일", GUILayout.Height(28)))
                {
                    Undo.RecordObject(c.Profile, "Capture Environment");
                    c.CaptureToProfile();
                    EditorUtility.SetDirty(c.Profile);
                    _profileSO = null;   // 인라인 표시 갱신
                    Debug.Log($"[JcEnvironment] 씬 라이팅을 '{c.Profile.name}' 에 캡처(메모리). 확정은 💾.", c.Profile);
                }
            }

            // 💾 — 프로파일 + 하늘 재질을 함께 확정
            if (GUILayout.Button("💾 디스크 저장 (프로파일 + 하늘 재질)", GUILayout.Height(30)))
                SaveProfileAndSky(c);
        }

        /// <summary>인라인 편집값을 즉시 씬에 반영. 재질도 갱신되므로 dirty 마킹까지 함께.</summary>
        private static void ApplyLive(JcEnvironmentController c)
        {
            c.ApplyNow();

            var sky = RenderSettings.skybox;
            if (sky != null && !Application.isPlaying && AssetDatabase.Contains(sky))
                EditorUtility.SetDirty(sky);   // 디스크 확정은 💾 에서

            MarkSceneDirty(c);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();   // 게임뷰도 즉시 갱신
        }

        private static void SaveProfileAndSky(JcEnvironmentController c)
        {
            if (c.Profile != null)
            {
                EditorUtility.SetDirty(c.Profile);
                AssetDatabase.SaveAssetIfDirty(c.Profile);
            }

            var sky = c.Profile != null ? c.Profile.skyboxMaterial : null;
            if (sky != null && AssetDatabase.Contains(sky))
            {
                EditorUtility.SetDirty(sky);
                AssetDatabase.SaveAssetIfDirty(sky);
            }

            Debug.Log($"[JcEnvironment] 디스크 저장 완료 — 프로파일 '{(c.Profile ? c.Profile.name : "없음")}'" +
                      $" / 하늘 재질 '{(sky ? sky.name : "없음")}'", c.Profile);
        }

        /// <summary>
        /// ★스크립트가 바꾼 씬 값(RenderSettings·라이트)은 Unity 가 dirty 로 자동 인식하지 못한다 —
        /// dirty 가 아니면 Ctrl+S 가 「변경 없음」으로 저장을 건너뛰어, 재로드 시 값이 되돌아간다(260811 실증).
        /// 에디트 모드의 적용 경로는 반드시 씬을 명시 dirty 마킹한다.
        /// </summary>
        internal static void MarkSceneDirty(Component ctx)
        {
            if (Application.isPlaying || ctx == null) return;
            EditorSceneManager.MarkSceneDirty(ctx.gameObject.scene);
        }
    }

    /// <summary>프로파일 자산 인스펙터 — 열린 씬의 컨트롤러를 찾아 적용 + 💾.</summary>
    [CustomEditor(typeof(JcEnvironmentProfile))]
    public class JcEnvironmentProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (JcEnvironmentProfile)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "이 프로파일은 씬의 JC_Environment(컨트롤러) 인스펙터에서 인라인으로 편집하는 것이 기본 흐름입니다 — " +
                "거기서는 값 변경이 즉시 씬에 반영됩니다.", MessageType.None);

            if (GUILayout.Button("▶ 적용 — 열린 씬에 반영", GUILayout.Height(26)))
            {
                var c = Object.FindFirstObjectByType<JcEnvironmentController>(FindObjectsInactive.Include);
                if (c != null)
                {
                    c.Apply(p);
                    JcEnvironmentControllerEditor.MarkSceneDirty(c);
                    SceneView.RepaintAll();
                }
                else Debug.LogWarning("[JcEnvironment] 씬에 JcEnvironmentController 가 없습니다 — GO 에 붙여 주세요.");
            }
            JcPresetEditorUtil.DrawSaveButton(p, wide: true);
        }
    }
}
