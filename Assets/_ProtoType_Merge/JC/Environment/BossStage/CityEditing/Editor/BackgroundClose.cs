using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BackgroundEditing
{
    // JC | 2026-11-16 | 창 닫기와 컴파일/Play 전환을 구분하고, 사용자가 선택한 저장·폐기 이후에만 작업 씬을 닫습니다.
    public sealed partial class BackgroundWindow
    {
        [SerializeField] bool ownsWorkspaceSession;
        BackgroundDocument[] closingDocuments = Array.Empty<BackgroundDocument>();
        bool discardOnClose;
        static bool reloading, quitting;

        // JC | 2026-11-16 | Scene 탭에서 이동/칠하기 중이어도 닫기 보호를 갱신하며, 컴파일 후 캐시는 한 번만 재연결합니다.
        void EnableCloseTracking()
        {
            EditorSceneManager.sceneDirtied += WorkSceneChanged;
            EditorSceneManager.sceneSaved += WorkSceneChanged;
            EditorApplication.delayCall += ResumeCloseTracking;
        }
        void DisableCloseTracking()
        {
            EditorSceneManager.sceneDirtied -= WorkSceneChanged;
            EditorSceneManager.sceneSaved -= WorkSceneChanged;
            EditorApplication.delayCall -= ResumeCloseTracking;
        }
        void ResumeCloseTracking() { if (ownsWorkspaceSession) TrackCloseState(); }
        void WorkSceneChanged(Scene scene) { if (closingDocuments.Any(d => d && d.gameObject.scene == scene)) UpdateCloseState(); }

        [InitializeOnLoadMethod]
        static void RegisterCloseGuards()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => reloading = true;
            EditorApplication.quitting += () => quitting = true;
        }

        void TrackCloseState()
        {
            if (!document || EditorApplication.isPlayingOrWillChangePlaymode) return;
            ownsWorkspaceSession = true;
            // 현재 단일 원본 작업본과 이전 World 작업 씬만 소유하며 다른 편집 작업은 닫지 않습니다.
            closingDocuments = BackgroundOps.OpenDocuments().Where(d => d == document || d.gameObject.scene.path == BackgroundOps.WorkspacePath).ToArray();
            UpdateCloseState();
        }

        // JC | 2026-11-16 | 화면 갱신에서는 캐시된 씬의 변경 플래그만 읽고, 전체 문서 검색은 연결/계층 변경 때만 합니다.
        void UpdateCloseState()
        {
            if (!ownsWorkspaceSession || discardOnClose) return;
            hasUnsavedChanges = closingDocuments.Any(d => d && (d.gameObject.scene.isDirty || EditorUtility.IsDirty(d)));
            saveChangesMessage = "원경 편집에 미저장 내용이 있습니다.\n저장: 도시 원본에 적용·저장한 뒤 종료\n저장 안 함: 마지막 저장 이후 변경을 버리고 종료\n취소: 편집 계속하기\n이전 Workspace의 변경은 해당 작업 파일에만 보관합니다.";
        }

        // JC | 2026-11-16 | 탭 X/닫기 메뉴도 Unity의 저장·버리기·취소 절차를 사용합니다. 실패하면 미저장 상태를 유지합니다.
        public override void SaveChanges()
        {
            ValidateCloseDocuments();
            foreach (var item in closingDocuments.Where(d => d && (d.gameObject.scene.isDirty || EditorUtility.IsDirty(d))))
            {
                if (item.IsCityTile) BackgroundOps.SaveOriginalCity(item);
                else BackgroundOps.Save(item);
            }
            base.SaveChanges();
            discardOnClose = false;
        }
        public override void DiscardChanges()
        {
            ValidateCloseDocuments();
            discardOnClose = true;
            base.DiscardChanges();
        }
        void ValidateCloseDocuments()
        {
            BackgroundOps.Guard();
            if (!document) throw new InvalidOperationException("원경 작업본을 다시 연결한 뒤 종료해 주세요.");
            BackgroundOps.FindOriginalCity(document.originalScenePath);
            foreach (var item in closingDocuments.Where(d => d))
            {
                var roots = item.gameObject.scene.GetRootGameObjects();
                if (roots.Length != 1 || roots[0] != item.gameObject)
                    throw new InvalidOperationException("작업 씬에 원경 외 오브젝트가 있어 자동으로 닫지 않습니다: " + item.gameObject.scene.name);
            }
        }

        void ExitEditing()
        {
            TrackCloseState();
            ValidateCloseDocuments();
            if (hasUnsavedChanges)
            {
                int choice = EditorUtility.DisplayDialogComplex("원경 편집 종료", saveChangesMessage,
                    "원본에 저장 후 닫기", "편집 계속하기", "변경 버리고 닫기");
                if (choice == 1) return;
                if (choice == 0) SaveChanges(); else DiscardChanges();
            }
            Close();
        }

        // JC | 2026-11-16 | 씬 폐기만으로 남을 수 있는 작업 전용 메시/재질의 미저장 상태도
        // 디스크 저장본으로 되돌립니다. 재베이크·원본 씬 저장 없이 변경된 작업 에셋만 다시 읽습니다.
        static void RestoreDiscardedTerrain(BackgroundDocument current)
        {
            string generated = current.assetFolder + "/Generated";
            if (!current.IsCityTile || !AssetDatabase.IsValidFolder(generated)) return;
            foreach (var guid in AssetDatabase.FindAssets("", new[] { generated }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset || !EditorUtility.IsDirty(asset)) continue;
                // JC | 2026-11-16 | 원래 경로의 Import는 메모리의 변경된 메시를 유지할 수 있어 저장 파일을 별도 경로에서 읽습니다.
                string copyPath = BackgroundOps.Local + "/CloseRecovery_" + Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(path);
                try
                {
                    System.IO.File.Copy(path, copyPath);
                    AssetDatabase.ImportAsset(copyPath, ImportAssetOptions.ForceSynchronousImport);
                    var saved = AssetDatabase.LoadMainAssetAtPath(copyPath);
                    if (!saved || saved.GetType() != asset.GetType()) throw new InvalidOperationException("작업 에셋의 저장본을 읽지 못했습니다: " + path);
                    if (asset is Mesh mesh && saved is Mesh savedMesh) BackgroundOps.CopyMesh(savedMesh, mesh);
                    else EditorUtility.CopySerialized(saved, asset);
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
                finally { AssetDatabase.DeleteAsset(copyPath); }
            }
        }
        void OnDestroy()
        {
            // JC | 2026-11-16 | 리로드/Play/에디터 종료에는 작업 씬을 손대지 않습니다. 비표시 검증용 창도 대상이 아닙니다.
            if (!ownsWorkspaceSession || reloading || quitting || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                ValidateCloseDocuments();
                if (!discardOnClose && closingDocuments.Any(d => d && (d.gameObject.scene.isDirty || EditorUtility.IsDirty(d))))
                    throw new InvalidOperationException("미저장 원경 작업은 닫지 않았습니다. 편집기를 다시 열어 저장 또는 변경 버리기를 선택해 주세요.");
                var source = BackgroundOps.FindOriginalCity(document.originalScenePath).gameObject.scene;
                if (discardOnClose)
                    foreach (var item in closingDocuments.Where(d => d && (d.gameObject.scene.isDirty || EditorUtility.IsDirty(d)))) RestoreDiscardedTerrain(item);
                SceneManager.SetActiveScene(source);
                RestoreIsolation();
                DisposePreview();
                foreach (var scene in closingDocuments.Where(d => d).Select(d => d.gameObject.scene).Distinct().ToArray())
                    if (!EditorSceneManager.CloseScene(scene, true)) throw new InvalidOperationException("작업 씬 닫기 실패: " + scene.name);
                if (editView) editView.Close();
            }
            catch (Exception error) { Debug.LogError("[원경 편집 종료] " + error.Message); }
        }
    }
}
