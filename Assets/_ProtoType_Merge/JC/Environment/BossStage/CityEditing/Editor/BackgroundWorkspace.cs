using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BackgroundEditing
{
    public static partial class BackgroundOps
    {
        // JC | 2026-11-05 | 열기/저장/결과 생성은 작업 씬 하나를 참조합니다. 결과 프리팹의 배포 경로와는 별개입니다.
        public const string WorkspacePath = Local + "/World/Workspace.unity";
        public static BackgroundDocument[] OpenDocuments() => Resources.FindObjectsOfTypeAll<BackgroundDocument>()
            .Where(d => d.gameObject.scene.IsValid() && d.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(d.gameObject.scene)).ToArray();

        public static void Save(BackgroundDocument document)
        {
            Guard();
            if (!document || !document.gameObject.scene.IsValid()) throw new InvalidOperationException("열린 원경 작업본이 없습니다.");
            var scene = document.gameObject.scene;
            // JC | 2026-11-05 | 기존 테스트 씬이나 다른 열린 작업본을 덮어쓰지 않고, 현재 원경 편집 씬만 고정 경로로 저장합니다.
            if (scene.GetRootGameObjects().Length != 1 || document.transform.parent)
                throw new InvalidOperationException("원경 작업본만 있는 전용 씬에서 저장해 주세요. 다른 씬의 오브젝트는 저장하지 않습니다.");
            var existing = SceneManager.GetSceneByPath(WorkspacePath);
            if (existing.IsValid() && existing.isLoaded && existing != scene)
                throw new InvalidOperationException("고정 작업 씬이 이미 열려 있습니다. 해당 작업 씬에서 계속 편집해 주세요.");
            Folder(Local + "/World");
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, WorkspacePath)) throw new System.IO.IOException("고정 작업 파일 저장 실패");
        }
    }

    // JC | 2026-11-05 | 편집 화면과 선택에서만 다른 씬을 제외합니다. 활성 상태/렌더러/저장 내용과 게임 카메라는 변경하지 않습니다.
    public sealed class BackgroundSceneIsolation
    {
        readonly List<GameObject> hidden = new List<GameObject>();
        readonly List<GameObject> unpickable = new List<GameObject>();
        Scene workScene;
        public void Apply(Scene scene)
        {
            if (workScene != scene) { Restore(); workScene = scene; }
            var visibility = SceneVisibilityManager.instance;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var other = SceneManager.GetSceneAt(i);
                if (other == scene || !other.isLoaded) continue;
                foreach (var go in other.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
                {
                    if (!visibility.IsHidden(go, false)) { hidden.Add(go); visibility.Hide(go, false); }
                    if (!visibility.IsPickingDisabled(go, false)) { unpickable.Add(go); visibility.DisablePicking(go, false); }
                }
            }
        }
        public void Restore()
        {
            // JC | 2026-11-05 | 편집기 종료/재컴파일/Play 전환 때 이 도구가 바꾼 표시만 복원하여 기존 숨김 상태를 보존합니다.
            var visibility = SceneVisibilityManager.instance;
            foreach (var go in hidden) if (go) visibility.Show(go, false);
            foreach (var go in unpickable) if (go) visibility.EnablePicking(go, false);
            hidden.Clear(); unpickable.Clear(); workScene = default;
        }
    }

    public sealed partial class BackgroundWindow
    {
        readonly BackgroundSceneIsolation isolation = new BackgroundSceneIsolation();
        void RefreshIsolation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !document || !document.gameObject.scene.isLoaded) { RestoreIsolation(); return; }
            isolation.Apply(document.gameObject.scene);
        }
        void RestoreIsolation() => isolation.Restore();
        void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) { RestoreIsolation(); DisposePreview(); }
            else if (state == PlayModeStateChange.EnteredEditMode) RefreshIsolation();
        }
    }
}
