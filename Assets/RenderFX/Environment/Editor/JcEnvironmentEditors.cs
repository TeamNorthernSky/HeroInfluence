using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JC.VFX;

namespace JC.Env.EditorTools
{
    /// <summary>
    /// 환경 컨트롤러 인스펙터 — 적용(프로파일→씬) / 캡처(씬→프로파일, 메모리만) / 💾(프로파일 디스크 확정).
    /// 튜닝 저장 공식 규격([[reference-unity-so-live-tuning]])과 동일 문법.
    /// </summary>
    [CustomEditor(typeof(JcEnvironmentController))]
    public class JcEnvironmentControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var c = (JcEnvironmentController)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "튜닝 흐름: 씬에서 라이팅을 직접 스윕 → [캡처]로 프로파일에 담기(메모리) → [💾]로 파일 확정.\n" +
                "[적용]은 프로파일 값을 씬에 반영(플레이 시작 시에도 1회 자동).", MessageType.None);

            using (new EditorGUI.DisabledScope(c.Profile == null))
            {
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
                        Debug.Log($"[JcEnvironment] 씬 라이팅을 '{c.Profile.name}' 에 캡처(메모리). 확정은 💾.", c.Profile);
                    }
                }
                JcPresetEditorUtil.DrawSaveButton(c.Profile, wide: true);
            }
            if (c.Profile == null)
                EditorGUILayout.HelpBox("Profile 슬롯에 환경 프로파일(ENV_*)을 꽂아 주세요.", MessageType.Warning);
        }

        /// <summary>
        /// ★스크립트가 바꾼 씬 값(RenderSettings·라이트)은 Unity 가 dirty 로 자동 인식하지 못한다 —
        /// dirty 가 아니면 Ctrl+S 가 「변경 없음」으로 저장을 건너뛰어, 재로드 시 값이 되돌아간다(260811 실증).
        /// 에디트 모드의 적용 버튼은 반드시 씬을 명시 dirty 마킹한다.
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
