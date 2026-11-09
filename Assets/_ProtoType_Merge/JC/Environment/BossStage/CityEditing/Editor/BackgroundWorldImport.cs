using System;
using System.Collections.Generic;
using System.Linq;
using JC.BattleTesting;
using JC.Environment;
using JC.EnvironmentDetail;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace JC.BackgroundEditing
{
    public static partial class BackgroundOps
    {
        // JC | 2026-11-05 | 월드의 환경만 가져옵니다. 예전 확장 도시 생성/중앙 건물 삭제는 호출하지 않습니다.
        static readonly HashSet<string> EnvironmentRoots = new() { "Land", "LevelEditor", "DecorativeSystemAndRoot", "JC_WorldDressing", "GateSystemAndRoot", "HeroUnionSystem", "OutpostSystemAndRoot", "VillainUnionSystem", "ObstacleRoot" };
        // JC | 2026-11-05 | 차선은 환경 제어기 아래에서 별도 생성됩니다. 월드 설정을 적용한 실제 표시 메시만 가져옵니다.
        public static MeshFilter[] WorldRoadLines(Scene world) => world.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<JcExploreRoadMarkings>(true))
            .SelectMany(c => c.GetComponentsInChildren<MeshFilter>())
            .Where(f => f.sharedMesh && f.GetComponent<MeshRenderer>() && f.GetComponent<MeshRenderer>().enabled && !f.GetComponent<MeshRenderer>().forceRenderingOff).ToArray();
        public static bool IsProp(MeshFilter f)
        {
            for (var t = f.transform; t; t = t.parent) if (t.name == "건물 배치") return true;
            return f.name.StartsWith("BG", StringComparison.Ordinal);
        }
        static bool Mountain(Transform node)
        {
            for (var t = node; t; t = t.parent)
                if (t.name.StartsWith("Mount", StringComparison.OrdinalIgnoreCase) || t.name.Contains("경계 산")) return true;
            return false;
        }
        public static void CopyLights(Transform source, Transform destination)
        {
            // JC | 2026-11-05 | 가로등의 위치/광량/점등 시각과 실제 Light 연결을 복제합니다. DH 제어 코드는 그대로 재사용합니다.
            foreach (var light in source.GetComponentsInChildren<Light>(true)) CopyLight(light,source,destination);
        }
        static void CopyLight(Light light,Transform sourceFrame,Transform destination)
        {
                var go = Child(destination, light.name);
                // JC | 2026-11-05 | 광원도 외형과 같은 상대 좌표로 복사하여 원경 배율에 따라 위치가 벌어지지 않게 합니다.
                go.transform.localPosition=sourceFrame.InverseTransformPoint(light.transform.position);
                go.transform.localRotation=Quaternion.Inverse(sourceFrame.rotation)*light.transform.rotation;
                var copy = go.AddComponent<Light>(); EditorUtility.CopySerialized(light, copy);
                var data = light.GetComponent<UniversalAdditionalLightData>();
                if (data) EditorUtility.CopySerialized(data, go.AddComponent<UniversalAdditionalLightData>());
                var timer = light.GetComponentInParent<DayNightLightController>();
                if (timer)
                {
                    var controller = go.AddComponent<DayNightLightController>(); EditorUtility.CopySerialized(timer, controller);
                    var serialized = new SerializedObject(controller); serialized.FindProperty("targetLight").objectReferenceValue = copy; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
        }
        // JC | 2026-11-05 | 프리뷰와 최종 결과는 동일한 복사 경로로 메시와 독립 광원을 함께 보존합니다.
        public static void CopyCityVisuals(BackgroundDocument document, Transform destination)
        {
            foreach(var source in new[]{document.Terrain,document.Buildings})
            {
                foreach(var f in source.GetComponentsInChildren<MeshFilter>()) if(f.gameObject.activeInHierarchy) Visual(f,destination);
                foreach(var light in source.GetComponentsInChildren<Light>(true))
                    if(!light.GetComponentInParent<MeshFilter>()) {
                        var holder=Child(destination,light.name+" 배치");holder.transform.localPosition=light.transform.position;holder.transform.localRotation=light.transform.rotation;
                        CopyLights(light.transform,holder.transform);
                    }
            }
        }
        public static string ImportWorld()
        {
            Guard();
            const string dir = Local + "/World";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(dir + "/Workspace.prefab")) throw new InvalidOperationException("새 원경 작업본이 이미 있습니다. 기존 작업본을 덮어쓰지 않습니다.");
            var originalActive = SceneManager.GetActiveScene();
            var world = SceneManager.GetSceneByPath(JcDayNightController.ScenePath);
            bool opened = !world.IsValid() || !world.isLoaded;
            if (!opened) throw new InvalidOperationException("원본 월드맵을 닫은 뒤 가져오세요. 현재 열린 씬의 미저장 상태를 변경하지 않습니다.");
            var scratch = EditorSceneManager.NewPreviewScene();
            var transient = new List<UnityEngine.Object>();
            try
            {
                world = EditorSceneManager.OpenScene(JcDayNightController.ScenePath, OpenSceneMode.Additive);
                var overlay = JcExploreDetailOverlay.Ensure(world); if (overlay) overlay.Refresh();
                foreach (var road in world.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<JcExploreRoadMarkings>(true))) { road.ApplyProfile(); road.RefreshNow(); }
                var input = new GameObject("최신 월드 원경 · 산 제외"); SceneManager.MoveGameObjectToScene(input, scratch);
                var props = Child(input.transform, "건물 배치").transform; var terrain = Child(input.transform, "지형 결과").transform;
                var roots = world.GetRootGameObjects().Where(g => EnvironmentRoots.Contains(g.name)).ToArray();
                var filters = roots.SelectMany(g => g.GetComponentsInChildren<MeshFilter>()).Concat(WorldRoadLines(world)).Distinct().Where(f => f.sharedMesh && !Mountain(f.transform) && f.GetComponent<MeshRenderer>() && f.GetComponent<MeshRenderer>().enabled && !f.GetComponent<MeshRenderer>().forceRenderingOff).ToArray();
                var attachedLights = new HashSet<Light>();
                var visualMap = new Dictionary<MeshFilter,GameObject>();
                int count = 0;
                foreach (var f in filters)
                {
                    bool ground = f.transform.root.name == "LevelEditor" || f.transform.root.name == "Land" || f.transform.root.name == "JC_WorldDressing" || f.GetComponentInParent<JcExploreRoadMarkings>();
                    var go = Visual(f, ground ? terrain : props);
                    visualMap.Add(f,go);
                    foreach (var l in f.GetComponentsInChildren<Light>(true)) attachedLights.Add(l);
                    // JC | 2026-11-05 | 현재 색상 오버라이드를 재질 스냅샷에 담되 원본 재질은 수정하지 않습니다.
                    var r = f.GetComponent<Renderer>(); var materials = r.sharedMaterials;
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        var block = new MaterialPropertyBlock(); r.GetPropertyBlock(block, slot);
                        if (block.isEmpty) r.GetPropertyBlock(block);
                        if (block.isEmpty || !materials[slot]) continue;
                        var m = new Material(materials[slot]); transient.Add(m);
                        foreach (var prop in new[] { "_BaseColor", "_Color", "_EdgeColor", "_EmissionColor" }) if (m.HasProperty(prop) && block.HasColor(prop)) m.SetColor(prop, block.GetColor(prop));
                        materials[slot] = m;
                    }
                    go.GetComponent<Renderer>().sharedMaterials = materials; count++;
                }
                // 메시와 광원이 형제인 배치도 누락하지 않습니다. 가까운 동일 배치의 메시 아래로 연결합니다.
                foreach (var light in roots.SelectMany(g => g.GetComponentsInChildren<Light>(true)).Where(l => l.type != LightType.Directional && !attachedLights.Contains(l)))
                {
                    var placement=light.GetComponentInParent<DecorativeObjectPlacement>();
                    var pair=placement?visualMap.FirstOrDefault(p=>p.Key.transform.IsChildOf(placement.transform)):default;
                    if(pair.Key) CopyLight(light,pair.Key.transform,pair.Value.transform);
                    else {var target=Child(props,"가로등 광원 · "+light.name);target.transform.localPosition=light.transform.position;target.transform.localRotation=light.transform.rotation;CopyLights(light.transform,target.transform);}
                    attachedLights.Add(light);
                }
                Folder(dir); new AssetCopier(dir + "/Assets", false).RemapHierarchy(input);
                var baseline = PrefabUtility.SaveAsPrefabAsset(input, dir + "/Baseline.prefab");
                var settings = JcBossBattleBackground.Load();
                if (!settings) throw new InvalidOperationException("보스 배경 프로필 없음");
                var sun = world.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>()).First(l => l.type == LightType.Directional);
                var sunObject = new GameObject("원경 태양 원본"); SceneManager.MoveGameObjectToScene(sunObject, scratch);
                EditorUtility.CopySerialized(sun, sunObject.AddComponent<Light>());
                sunObject.transform.rotation = sun.transform.rotation;
                var sunData = sun.GetComponent<UniversalAdditionalLightData>(); if (sunData) EditorUtility.CopySerialized(sunData, sunObject.AddComponent<UniversalAdditionalLightData>());
                settings.citySun = PrefabUtility.SaveAsPrefabAsset(sunObject, dir + "/WorldSun.prefab").GetComponent<Light>();
                var time = world.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<JcDayNightController>(true)).FirstOrDefault();
                settings.cityTimeProfile = time && time.profile ? time.profile : Resources.Load<JcDayNightProfile>("JC_DayNight_Default");
                EditorUtility.SetDirty(settings);
                var doc = input.AddComponent<BackgroundDocument>(); doc.baseline = baseline; doc.stage = settings.visualPrefab; doc.assetFolder = dir;
                doc.preserveWorld = true; doc.limitArea = false; doc.blueHighWindows = false;
                var bounds = new Bounds(); bool first = true;
                foreach (var r in input.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
                doc.editMin = new Vector2Int(Mathf.FloorToInt(bounds.min.x), Mathf.FloorToInt(bounds.min.z));
                doc.editMax = new Vector2Int(Mathf.CeilToInt(bounds.max.x), Mathf.CeilToInt(bounds.max.z));
                var file = dir + "/Workspace.prefab"; PrefabUtility.SaveAsPrefabAsset(input, file); AssetDatabase.SaveAssets();
                return file + " / 메시=" + count + " / 광원=" + input.GetComponentsInChildren<Light>(true).Length;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scratch);
                if (world.IsValid() && world.isLoaded) EditorSceneManager.CloseScene(world, true);
                SceneManager.SetActiveScene(originalActive);
                foreach (var value in transient) if (value) UnityEngine.Object.DestroyImmediate(value);
            }
        }
        public static BackgroundDocument OpenLatest()
        {
            // JC | 2026-11-05 | 현재 미저장 편집본을 우선 보존하고, 이후에는 고정 작업 씬 하나만 재사용합니다.
            Guard(); var current = OpenDocuments();
            if (current.Length > 1) throw new InvalidOperationException("원경 작업 씬이 여러 개 열려 있습니다. 계속 편집할 작업 씬 하나만 남겨 주세요.");
            if (current.Length == 1) return current[0];
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkspacePath))
            {
                var saved = EditorSceneManager.OpenScene(WorkspacePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(saved);
                return saved.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BackgroundDocument>(true)).Single();
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Local + "/World/Workspace.prefab");
            if (!prefab) throw new InvalidOperationException("최신 월드 작업본이 없습니다.");
            // 최초 준비본만 인스턴스화합니다. 저장 버튼은 이름을 묻지 않고 WorkspacePath에 기록합니다.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            SceneManager.SetActiveScene(scene); return go.GetComponent<BackgroundDocument>();
        }
    }
}
