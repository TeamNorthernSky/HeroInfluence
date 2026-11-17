// JC | 2026-11-16 | 원경 편집 창을 닫아도 Play 전환 시 작업 씬 대신 원본 전투 씬으로 진입합니다.
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BackgroundEditing
{
    [InitializeOnLoad]
    static class BackgroundPlayMode
    {
        const string StateKey = "JC.BackgroundEditing.PlayRoots";
        [Serializable] sealed class RootState { public string folder; public bool active; }
        [Serializable] sealed class Snapshot { public RootState[] roots; }

        static BackgroundPlayMode()
        {
            EditorApplication.playModeStateChanged += Changed;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        // JC | 2026-11-16 | 저장·씬 닫기 없이 활성 씬만 복원합니다. 일반 전투/부트 씬에서 시작한 Play는 그대로 둡니다.
        internal static bool RestoreBattleScene()
        {
            var active = SceneManager.GetActiveScene();
            var document = BackgroundOps.OpenDocuments().FirstOrDefault(d => d.gameObject.scene == active);
            if (!document) return false;
            var original = BackgroundOps.FindOriginalCity(document.originalScenePath);
            if (!SceneManager.SetActiveScene(original.gameObject.scene))
                throw new InvalidOperationException("원경 편집의 원본 전투 씬을 활성화하지 못했습니다.");
            return true;
        }

        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // JC | 2026-11-16 | 미리보기 전용 씬을 먼저 정리하여 임시 씬이 Play 시작 씬으로 남지 않게 합니다.
                foreach (var window in Resources.FindObjectsOfTypeAll<BackgroundWindow>()) window.PrepareForPlay();
                try
                {
                    RestoreBattleScene();
                    var snapshot = new Snapshot { roots = BackgroundOps.OpenDocuments().Select(d =>
                        new RootState { folder = d.assetFolder, active = d.gameObject.activeSelf }).ToArray() };
                    SessionState.SetString(StateKey, JsonUtility.ToJson(snapshot));
                }
                catch (Exception error)
                {
                    // JC | 2026-11-16 | 원본이 없거나 여러 개이면 임의 씬으로 실행하지 않고 작업 내용을 보존한 채 안내합니다.
                    Debug.LogError("[원경 편집] Play 시작을 취소합니다. " + error.Message);
                    EditorApplication.isPlaying = false;
                }
            }
            else if (state == PlayModeStateChange.EnteredPlayMode) HideWorkRoots();
            else if (state == PlayModeStateChange.EnteredEditMode) RestoreWorkRoots();
        }

        // JC | 2026-11-16 | 작업용 복제 메시·광원이 실전 렌더에 섞이지 않게 합니다. 씬 재로드를 끈 Play도 같은 처리를 합니다.
        static void SceneLoaded(Scene scene, LoadSceneMode mode) { if (EditorApplication.isPlaying) HideWorkRoots(); }
        static void HideWorkRoots()
        {
            foreach (var document in BackgroundOps.OpenDocuments())
                if (document.gameObject.activeSelf) document.gameObject.SetActive(false);
        }
        static void RestoreWorkRoots()
        {
            string json = SessionState.GetString(StateKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var snapshot = JsonUtility.FromJson<Snapshot>(json);
            foreach (var document in BackgroundOps.OpenDocuments())
            {
                var before = snapshot.roots.FirstOrDefault(r => r.folder == document.assetFolder);
                if (before != null && document.gameObject.activeSelf != before.active) document.gameObject.SetActive(before.active);
            }
            SessionState.EraseString(StateKey);
        }
    }
}
