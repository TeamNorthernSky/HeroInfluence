using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace YT.Simulation
{
    [InitializeOnLoad]
    public static class YtOutpostSimulationBuilder
    {
        // 실제 기본 탐사씬이 교체되어도 GameManager의 현재 설정에서 찾아갑니다.
        private static string MainScene
        {
            get
            {
                var prefab = Resources.Load<GameObject>("GameManager");
                var manager = prefab != null ? prefab.GetComponentInChildren<GameSceneManager>(true) : null;
                if (manager == null) throw new InvalidOperationException("메인 GameSceneManager가 없습니다.");
                string sceneName = new SerializedObject(manager).FindProperty("explorationSceneDefault").stringValue;
                return AssetDatabase.FindAssets(sceneName + " t:Scene").Select(AssetDatabase.GUIDToAssetPath)
                    .Single(p => System.IO.Path.GetFileNameWithoutExtension(p) == sceneName);
            }
        }

        static YtOutpostSimulationBuilder() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

        [MenuItem("Tools/YT/Open Outpost Simulation")]
        public static void Open()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(YtOutpostSimulation.ScenePath) == null) Create();
            else EditorSceneManager.OpenScene(YtOutpostSimulation.ScenePath);
        }

        public static void Create()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("편집 모드에서 컴파일이 끝난 뒤 실행하세요.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(YtOutpostSimulation.ScenePath) != null)
                throw new InvalidOperationException("기존 모의전 씬은 덮어쓰지 않습니다.");
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("현재 씬을 먼저 저장하세요.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var source = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
            try
            {
                var data = source.GetRootGameObjects().Single(r => r.GetComponentInChildren<DHCsvTemplateCatalog>(true) != null);
                var copy = UnityEngine.Object.Instantiate(data);
                copy.name = "DataStorage";
                SceneManager.MoveGameObjectToScene(copy, scene);
            }
            finally { EditorSceneManager.CloseScene(source, true); }
            new GameObject("YT_OutpostSimulation").AddComponent<YtOutpostSimulation>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, YtOutpostSimulation.ScenePath);
            Debug.Log("[YT 모의전] 생성: " + YtOutpostSimulation.ScenePath);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || SceneManager.GetActiveScene().path != YtOutpostSimulation.ScenePath) return;
            try { SyncMainCatalogs(); }
            catch (Exception error)
            {
                Debug.LogError("[YT 모의전] 메인 밸런스 연결 실패: " + error.Message);
                EditorApplication.isPlaying = false;
            }
        }

        /// <summary>메인에서 테이블 자체가 교체되어도 매 Play마다 새 참조를 따릅니다. 원본 테이블은 수정하지 않습니다.</summary>
        public static void SyncMainCatalogs()
        {
            var target = SceneManager.GetActiveScene();
            var existing = SceneManager.GetSceneByPath(MainScene);
            bool opened = !existing.isLoaded;
            var source = opened ? EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive) : existing;
            try
            {
                foreach (var type in new[] { typeof(DHCsvTemplateCatalog), typeof(EventScriptCatalog), typeof(AssociationResourceCatalog), typeof(DHWorldEventCatalog) })
                {
                    var from = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(type, true)).Single();
                    var to = target.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(type, true)).Single();
                    EditorUtility.CopySerialized(from, to);
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(source, true); }
        }
    }
}
