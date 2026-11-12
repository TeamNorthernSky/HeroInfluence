using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-12 | 실제 전투씬의 직렬화 연결을 재사용하고 테스트 진입/현재 외형만 추가합니다. 원본 씬과 사용자 프로필은 저장하지 않습니다.
    public static class JcStandaloneBattleSceneBuilder
    {
        public const string Folder = "Assets/_ProtoType_Merge/JC/JC_TestScenes";
        public const string Data = "Assets/_ProtoType_Merge/JC/BattleTesting/Standalone";
        public static readonly string[] Names = { "JC_BossTestScene", "BankTestScene", "JC_TutorialFirstBattleTestScene", "JC_Main12BattleTestScene" };
        [MenuItem("JC/전투 테스트/전용 4씬 생성·갱신")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 실행하세요.");
            foreach (string name in Names)
            {
                var existing = SceneManager.GetSceneByPath(Folder + "/" + name + ".unity");
                if (existing.IsValid() && existing.isLoaded) throw new InvalidOperationException("갱신 대상 테스트씬을 닫은 후 실행하세요: " + name);
            }
            Directory.CreateDirectory(Data); AssetDatabase.Refresh();
            var active = SceneManager.GetActiveScene();
            // 기존 은행 진입씬의 원본 DH 카탈로그 연결을 한 프리팹으로 공유합니다. 별도 카탈로그 코드를 만들지 않습니다.
            string catalogPath = Data + "/OriginalCatalogs.prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(catalogPath))
            {
                var bankScene = EditorSceneManager.OpenScene(Folder + "/BankTestScene.unity", OpenSceneMode.Additive);
                try
                {
                    var catalogs = bankScene.GetRootGameObjects().Single(r => r.GetComponent<DHCsvTemplateCatalog>());
                    PrefabUtility.SaveAsPrefabAsset(catalogs, catalogPath);
                }
                finally { EditorSceneManager.CloseScene(bankScene, true); }
            }
            try
            {
                var tutorialBackground = JcEncounterBackgroundBuilder.BuildStandaloneBackground(true, Data + "/TutorialBackground");
                var mainBackground = JcEncounterBackgroundBuilder.BuildStandaloneBackground(false, Data + "/Main12Background");
                for (int i = 0; i < Names.Length; i++)
                {
                    string source = "Assets/_ProtoType_Merge/Scenes/" + (i == 2 ? "TutorialBattleScene" : "TmpBattleScene") + ".unity";
                    string target = Folder + "/" + Names[i] + ".unity";
                    // 기존 보스/은행 .meta를 유지하므로 메뉴·에셋의 씬 참조가 끊기지 않습니다.
                    File.Copy(source, target, true); AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                    var scene = EditorSceneManager.OpenScene(target, OpenSceneMode.Additive);
                    try
                    {
                        SceneManager.SetActiveScene(scene);
                        foreach (var root in scene.GetRootGameObjects())
                        {
                            if (root.name == "Ground" || root.name == "HeroUnionZ1" || root.name == "DataManager" || root.name.StartsWith("BGOffice") ||
                                root.name.StartsWith("BGHouse") || root.name.StartsWith("BGStore") || root.name == "JC_TutorialConstructionBackdrop")
                                Object.DestroyImmediate(root);
                        }
                        var catalog = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(catalogPath), scene);
                        catalog.name = "JC_OriginalDataCatalog";
                        var owner = new GameObject("JC_BattleTest"); SceneManager.MoveGameObjectToScene(owner, scene);
                        var test = owner.AddComponent<JcStandaloneBattleTest>(); test.scenario = (JcStandaloneBattleTest.Scenario)i;
                        test.profile = EnsureConfig(i);
                        var background = i == 0 ? JcBossBattleBackground.Load().visualPrefab : i == 1 ? JcBankBattleBackground.Load().visualPrefab : i == 2 ? tutorialBackground : mainBackground;
                        test.stage = (GameObject)PrefabUtility.InstantiatePrefab(background, scene); test.stage.name = "JC_TestStage";
                        test.manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleSceneManager>(true)).Single();
                        var serialized = new SerializedObject(test.manager);
                        serialized.FindProperty("tutorialBattleKeyOverride").stringValue = "";
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        test.runtimeRoots = scene.GetRootGameObjects().Where(r => r != owner && r != catalog && r.activeSelf).ToArray();
                        // JC | 2026-11-12 | 보스 진형 이동/스폰 Awake 순서를 보장하도록 게임 로직 루트는 저장 시부터 비활성입니다. 무대·카메라·조명은 편집기에 표시합니다.
                        foreach (var root in test.runtimeRoots)
                            if (root.name == "PlayerPlace" || root.name == "EnemyPlace" || root.name == "BattleSceneManager" || root.name == "InputHandler" || root.name == "GridManager" || root.name == "EventSystem" || root.name == "UIManager" || root.name == "BattleSceneCanvas" || root.name == "JC_TutorialBattle") root.SetActive(false);
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene, target)) throw new InvalidOperationException("테스트씬 저장 실패: " + target);
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                }
            }
            finally { if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
        }
        static SimulationBattleConfig EnsureConfig(int index)
        {
            string path = Data + "/" + Names[index] + "_Start.asset";
            var config = AssetDatabase.LoadAssetAtPath<SimulationBattleConfig>(path);
            if (config) return config; // JC | 2026-11-12 | 씬 재생성 시 사용자가 저장한 시작 설정을 보존합니다.
            config = ScriptableObject.CreateInstance<SimulationBattleConfig>();
            config.enemyGroupKey = new[] { "BE490", "BE003", "TUFEP001", "BE006" }[index];
            config.eventZoneId = new[] { 2, 1, 0, 1 }[index]; config.enemyLevel = index == 0 ? 8 : 1;
            foreach (string key in new[] { "10001", "10002", "10003", "10004" })
                config.allies.Add(new SimulationAllyInput { Enabled = true, UnitTemplateKey = key, Level = index < 2 ? 8 : 1 });
            AssetDatabase.CreateAsset(config, path); return config;
        }
    }

    public static partial class JcEncounterBackgroundBuilder
    {
        // JC | 2026-11-12 | 현재 탐사씬과 실제 배경 생성기를 사용해 전용씬용 외형만 저장합니다. 원본 외형/카탈로그는 편집하지 않습니다.
        public static GameObject BuildStandaloneBackground(bool tutorial, string folder)
        {
            string path = "Assets/_ProtoType_Merge/Scenes/" + (tutorial ? "TutorialExploreScene" : "DHScene_3") + ".unity";
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("배경 입력 씬의 미저장 변경이 있습니다: " + path);
            JcDynamicBattleSource source = null; GameObject output = null, copy = null;
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var roots = scene.GetRootGameObjects();
                Vector3 anchor;
                var settings = new JcDynamicBattleSettings { addBattleSurfaceDetails = !tutorial };
                if (tutorial)
                {
                    anchor = roots.SelectMany(r => r.GetComponentsInChildren<TutorialEnemyObject>(true)).Single(e => e.TutorialBattleKey == "TUT_01").transform.position;
                    settings.axis = JcBattleRoadAxis.X; settings.encounterPosition = anchor; settings.enemyPosition = anchor + Vector3.right;
                }
                else
                {
                    var origin = roots.SelectMany(r => r.GetComponentsInChildren<MainEventObject>(true)).Single(e => e.ZoneId == 1 && e.EventKey == "MainQuest1-2");
                    anchor = origin.transform.position;
                    var entry = JcBattlePresentationProfile.Load()?.Find(1, origin.EventKey);
                    settings.encounterPosition = entry != null && entry.overrideAnchor ? entry.anchor : anchor;
                    settings.enemyPosition = settings.encounterPosition + Vector3.forward; settings.axis = entry?.axis ?? JcBattleRoadAxis.Auto;
                }
                source = JcDynamicBattleGeometry.Capture(scene, tutorial);
                var cameras = Resources.Load<JcBattleCameraProfile>("JC_BattleCamera_Default");
                cameras.viewA.Resolve(Vector3.zero, out var a, out _, out _); cameras.viewB.Resolve(Vector3.zero, out var b, out _, out _);
                output = JcDynamicBattleGeometry.Build(source, settings, null, new[] { a, b });
                // JC | 2026-11-12 | 생성기 내부의 비직렬화 광원 등록을 저장 가능한 참조로 전환합니다.
                foreach (var lamps in output.GetComponentsInChildren<JC.Environment.JcBattleLampGroup>(true)) lamps.CaptureTestStageReferences();
                copy = Object.Instantiate(output); copy.name = tutorial ? "TutorialFirstBackground" : "Main12Background";
                EnsureFolder(folder); var export = new ExportContext(folder);
                int index = 0;
                foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.hideFlags = HideFlags.None;
                    foreach (var component in t.GetComponents<Component>()) if (component) component.hideFlags = HideFlags.None;
                }
                // JC | 2026-11-12 | 생성 메시/재질 수백 개의 개별 재임포트를 묶어 에디터 대기를 줄입니다.
                AssetDatabase.StartAssetEditing();
                try
                {
                foreach (var renderer in copy.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string id = (index++).ToString("D4"); var mesh = renderer.GetComponent<MeshFilter>();
                    if (mesh && mesh.sharedMesh) mesh.sharedMesh = export.Mesh(mesh.sharedMesh, id);
                    renderer.sharedMaterials = export.Materials(renderer, id);
                }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                foreach (var transient in copy.GetComponentsInChildren<JcDynamicBattleOutput>(true)) Object.DestroyImmediate(transient);
                return PrefabUtility.SaveAsPrefabAsset(copy, folder + "/Background.prefab");
            }
            finally
            {
                if (copy) Object.DestroyImmediate(copy); if (output) Object.DestroyImmediate(output); if (source) Object.DestroyImmediate(source);
                if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
