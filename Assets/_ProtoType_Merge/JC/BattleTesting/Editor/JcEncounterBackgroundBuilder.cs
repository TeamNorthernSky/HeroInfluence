using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JC.BattleTesting.Editor
{
    // 탐사 로더를 실행하지 않고 저장된 씬의 정적 외형을 읽습니다.
    // 원본 메시/재질은 참조하며, 임시 생성 메시·색상 텍스처만 영구 에셋으로 굽습니다.
    public static partial class JcEncounterBackgroundBuilder
    {
        public const string Folder = "Assets/_ProtoType_Merge/JC/BattleTesting/Resources/JC_Encounters";
        private const string SourceScene = "Assets/_ProtoType_Merge/Scenes/DHScene_3.unity";
        private static readonly string[] Keys = { "Main_1-2", "Main_1-1", "Main_2-2" };
        private static readonly string[] BattleKeys = { "BE006", "BE005", "BE122" };
        private static bool building;
        private static readonly HashSet<string> EnvironmentRoots = new HashSet<string> {
            "Land", "DecorativeSystemAndRoot", "JC_WorldDressing", "GateSystemAndRoot",
            "HeroUnionSystem", "OutpostSystemAndRoot", "VillainUnionSystem", "ObstacleRoot"
        };

        [MenuItem("JC/전투 테스트/탐사 인카운터 3종 생성·재추출")]
        public static void BuildAll()
        {
            // AssetDatabase 갱신 중 EditorApplication.update가 재진입할 수 있습니다.
            if (building) return;
            building = true;
            try { BuildAllCore(); }
            finally { building = false; }
        }

        private static void BuildAllCore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("배경 추출은 편집 모드에서만 가능합니다.");
            EnsureFolder(Folder);
            Scene source = SceneManager.GetSceneByPath(SourceScene);
            bool preview = !source.IsValid() || !source.isLoaded;
            if (!preview && source.isDirty) throw new InvalidOperationException("탐사씬에 저장하지 않은 변경이 있습니다. 저장 후 배경을 추출해 주세요.");
            try
            {
                if (preview) source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
                var roots = source.GetRootGameObjects();
                if (preview)
                    foreach (var color in roots.SelectMany(x => x.GetComponentsInChildren<BuildingColors.BgColorModifier>(true)))
                    { color.Rebind(); color.ApplyNow(); }
                var catalog = roots.SelectMany(x => x.GetComponentsInChildren<EventScriptCatalog>(true)).Single();
                catalog.Reload();
                ExportEventCatalog(catalog, source);
                for (int i = 0; i < Keys.Length; i++)
                {
                    var background = LoadOrCreate<JcEncounterBackground>(Folder + "/" + Keys[i] + "_Background.asset");
                    bool firstBuild = background.visualPrefab == null;
                    background.encounterKey = Keys[i];
                    background.sourceScene = SourceScene;
                    if (firstBuild) { background.scale = 3; background.yaw = i == 0 ? 90 : 0; }
                    ExportBackground(source, background);
                    if (background.fitBattleRoad) FitBattleRoad(background);
                    string presetPath = Folder + "/" + Keys[i] + ".asset";
                    bool newPreset = AssetDatabase.LoadAssetAtPath<JcEnemyPreset>(presetPath) == null;
                    var preset = LoadOrCreate<JcEnemyPreset>(presetPath);
                    preset.encounterKey = Keys[i]; preset.eventBattleKey = BattleKeys[i]; preset.eventZoneId = 1; preset.background = background;
                    if (!catalog.TryGetBattleEnemyGroupTemplate(1, BattleKeys[i], out var group) || group == null || group.Members.Count == 0)
                        throw new InvalidOperationException("원본 이벤트 전투 연결 오류: " + BattleKeys[i]);
                    if (newPreset) preset.enemyLevel = Mathf.Max(1, group.MinLevel);
                    EditorUtility.SetDirty(preset); AssetDatabase.SaveAssetIfDirty(preset);
                    EditorUtility.SetDirty(background); AssetDatabase.SaveAssetIfDirty(background);
                }
                Debug.Log("[JC 인카운터] 3종 추출 완료. 기본 첫 대상: Main_1-2. 탐사씬·카메라 프로필 미변경.");
            }
            finally { if (preview && source.IsValid()) EditorSceneManager.CloseScene(source, true); }
        }

        private static void ExportEventCatalog(EventScriptCatalog source, Scene scene)
        {
            var root = new GameObject("JC_TestEventCatalog");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var copy = root.AddComponent<EventScriptCatalog>();
                EditorUtility.CopySerialized(source, copy);
                var serialized = new SerializedObject(copy);
                serialized.FindProperty("dontDestroyOnLoad").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/EventCatalog.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void ExportBackground(Scene scene, JcEncounterBackground profile)
        {
            string output = Folder + "/" + profile.encounterKey + "_Generated";
            EnsureFolder(output);
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            var anchors = all.Where(x => x.name == profile.encounterKey || x.name == profile.encounterKey + "(Clone)").ToArray();
            if (anchors.Length != 1) throw new InvalidOperationException(profile.encounterKey + ": 인카운터 기준점은 정확히 하나여야 합니다.");
            var anchor = anchors[0];
            var capture = new Bounds(anchor.position, new Vector3(profile.captureHalfSize.x * 2, 1000, profile.captureHalfSize.y * 2));
            var root = new GameObject(profile.encounterKey + "_Visuals");
            SceneManager.MoveGameObjectToScene(root, scene);
            var context = new ExportContext(output);
            var report = new StringBuilder("기준점 " + anchor.position + " / 원본 반경 " + profile.captureHalfSize + "\n");
            int scenery = 0, props = 0;
            try
            {
                var envRoot = new GameObject("Scenery").transform; envRoot.SetParent(root.transform, false);
                var propRoot = new GameObject("EncounterProps").transform; propRoot.SetParent(root.transform, false);
                foreach (var renderer in all.Select(x => x.GetComponent<MeshRenderer>()).Where(x => x != null && x.enabled && x.gameObject.activeInHierarchy))
                {
                    var mesh = renderer.GetComponent<MeshFilter>();
                    if (mesh == null || mesh.sharedMesh == null || !capture.Intersects(renderer.bounds)) continue;
                    bool eventProp = renderer.transform.IsChildOf(anchor) && !IsAttachedToCharacter(renderer.transform, anchor);
                    bool ground = HasAncestor(renderer.transform, "Generated Tile Meshes");
                    bool environment = EnvironmentRoots.Contains(renderer.transform.root.name) && !IsAttachedToCharacter(renderer.transform, renderer.transform.root);
                    if (!ground && !environment && !eventProp) continue;
                    string path = GetPath(renderer.transform);
                    string id = Hash128.Compute(GetStablePath(renderer.transform)).ToString().Substring(0, 12);
                    var target = new GameObject(renderer.name.Replace('/', '_') + "__" + id);
                    target.transform.SetParent(eventProp ? propRoot : envRoot, false);
                    target.transform.localPosition = renderer.transform.position - anchor.position;
                    target.transform.localRotation = renderer.transform.rotation;
                    target.transform.localScale = renderer.transform.lossyScale;
                    target.AddComponent<MeshFilter>().sharedMesh = context.Mesh(mesh.sharedMesh, id);
                    var copy = target.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = context.Materials(renderer, id);
                    copy.shadowCastingMode = renderer.shadowCastingMode; copy.receiveShadows = renderer.receiveShadows;
                    copy.lightProbeUsage = LightProbeUsage.Off; copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    if (eventProp) { props++; report.AppendLine("소품: EncounterProps/" + target.name + " ← " + path); }
                    else scenery++;
                    // 배경만 회전·확대했을 때 전투 중앙 부근의 물체를 알립니다. 자동 삭제하지 않습니다.
                    var localCenter = Quaternion.Euler(0, profile.yaw, 0) * ((renderer.bounds.center - anchor.position) * profile.scale) + profile.battleOffset;
                    var ext = renderer.bounds.extents * profile.scale;
                    if (!ground && Mathf.Abs(localCenter.x) < 3 + ext.magnitude && Mathf.Abs(localCenter.z) < 8 + ext.magnitude)
                        report.AppendLine("진형 주변 확인: " + (eventProp ? "EncounterProps/" : "Scenery/") + target.name);
                }
                if (scenery == 0) throw new InvalidOperationException(profile.encounterKey + ": 배경 메시를 찾지 못했습니다.");
                profile.visualPrefab = PrefabUtility.SaveAsPrefabAsset(root, output + "/Background.prefab");
                profile.extractionReport = "풍경 " + scenery + " / 인카운터 정적 소품 " + props + "\n" + report;
                File.WriteAllText(output + "/ExtractionReport.txt", profile.extractionReport);
                AssetDatabase.ImportAsset(output + "/ExtractionReport.txt");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // 탐사 셀·통행 판정과 무관하게 외형의 도로 단면만 전투 폭에 맞춥니다.
        public static void FitBattleRoad(JcEncounterBackground profile)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서만 도로를 재구성할 수 있습니다.");
            if (profile.visualPrefab == null || profile.sourceRoadEdges.y <= profile.sourceRoadEdges.x || profile.battleRoadWidth <= 0)
                throw new InvalidOperationException("원본 배경 또는 도로 경계를 확인해 주세요.");
            var root = Object.Instantiate(profile.visualPrefab);
            root.name = profile.encounterKey + "_BattleVisuals";
            string output = Folder + "/" + profile.encounterKey + "_Generated";
            var rotation = Quaternion.Euler(0, profile.yaw, 0);
            try
            {
                root.transform.SetPositionAndRotation(profile.battleOffset, rotation);
                root.transform.localScale = Vector3.one * profile.scale;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (filter.name.StartsWith("TileChunk_", StringComparison.Ordinal))
                    {
                        var mesh = Object.Instantiate(filter.sharedMesh);
                        var vertices = mesh.vertices;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            var world = filter.transform.TransformPoint(vertices[i]);
                            world.x = FitRoadX(world.x, profile);
                            vertices[i] = world;
                        }
                        filter.transform.SetParent(root.transform.Find("Scenery"), true);
                        filter.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        filter.transform.localScale = Vector3.one / profile.scale;
                        mesh.vertices = vertices; mesh.RecalculateBounds(); mesh.RecalculateNormals(); mesh.RecalculateTangents();
                        string path = output + "/FittedMesh_" + filter.name.Substring(filter.name.LastIndexOf("__", StringComparison.Ordinal) + 2) + ".asset";
                        mesh.name = Path.GetFileNameWithoutExtension(path);
                        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); filter.sharedMesh = mesh; }
                        else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); filter.sharedMesh = existing; }
                    }
                    else
                    {
                        // 건물·산·소품은 변형하지 않고 도로 쪽 경계면을 기준으로 이동합니다.
                        var bounds = renderer.bounds;
                        float center = (profile.sourceRoadEdges.x + profile.sourceRoadEdges.y) * .5f;
                        bool right = bounds.center.x >= center;
                        float edge = right ? bounds.min.x : bounds.max.x;
                        float destination = FitRoadX(edge, profile);
                        float clearance = profile.battleRoadWidth * .5f + profile.roadsideClearance;
                        destination = right ? Mathf.Max(destination, clearance) : Mathf.Min(destination, -clearance);
                        filter.transform.position += Vector3.right * (destination - edge);
                    }
                }
                // 바닥은 월드 좌표로 구웠으므로 자식의 자세를 유지하며 루트를 정규화합니다.
                var children = root.GetComponentsInChildren<MeshFilter>(true).Select(x => x.transform).ToArray();
                var poses = children.Select(x => (position: x.position, rotation: x.rotation, scale: x.lossyScale)).ToArray();
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
                for (int i = 0; i < children.Length; i++) { children[i].SetPositionAndRotation(poses[i].position, poses[i].rotation); children[i].localScale = poses[i].scale; }
                profile.fittedVisualPrefab = PrefabUtility.SaveAsPrefabAsset(root, output + "/BattleBackground.prefab");
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static float FitRoadX(float x, JcEncounterBackground profile)
        {
            float left = profile.sourceRoadEdges.x, right = profile.sourceRoadEdges.y, half = profile.battleRoadWidth * .5f;
            // 도로 안은 폭 보정, 외부는 인도 구간을 넓혀 지형의 경계를 이어 줍니다.
            if (x < left) return x - left - half - profile.roadsideClearance * Mathf.Clamp01((left - x) / 3);
            if (x > right) return x - right + half + profile.roadsideClearance * Mathf.Clamp01((x - right) / 3);
            return Mathf.Lerp(-half, half, (x - left) / (right - left));
        }

        private static bool IsAttachedToCharacter(Transform t, Transform stop)
        {
            for (var p = t; p != null && p != stop; p = p.parent)
                if (p.name.StartsWith("Armature", StringComparison.OrdinalIgnoreCase) || p.GetComponent<BattleCharactor>() != null) return true;
            return false;
        }
        private static bool HasAncestor(Transform t, string name) { for (; t != null; t = t.parent) if (t.name == name) return true; return false; }
        private static string GetPath(Transform t) => t.parent == null ? t.name : GetPath(t.parent) + "/" + t.name;
        private static string GetStablePath(Transform t) => (t.parent == null ? "" : GetStablePath(t.parent) + "/") + t.name + "[" + t.GetSiblingIndex() + "]";
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private sealed class ExportContext
        {
            private readonly string folder;
            private readonly Dictionary<Mesh, Mesh> meshes = new Dictionary<Mesh, Mesh>();
            private readonly Dictionary<Texture, Texture> textures = new Dictionary<Texture, Texture>();
            public ExportContext(string path) { folder = path; }
            public Mesh Mesh(Mesh source, string id)
            {
                if (EditorUtility.IsPersistent(source)) return source;
                if (meshes.TryGetValue(source, out var cached)) return cached;
                cached = Save(Object.Instantiate(source), folder + "/Mesh_" + id + ".asset");
                meshes.Add(source, cached); return cached;
            }
            public Material[] Materials(MeshRenderer source, string id)
            {
                var originals = source.sharedMaterials; var result = new Material[originals.Length];
                var global = new MaterialPropertyBlock(); source.GetPropertyBlock(global);
                for (int i = 0; i < originals.Length; i++)
                {
                    var original = originals[i];
                    if (original == null) throw new InvalidOperationException("재질 누락: " + GetPath(source.transform));
                    var block = new MaterialPropertyBlock(); source.GetPropertyBlock(block, i);
                    if (block.isEmpty) block = global;
                    if (EditorUtility.IsPersistent(original) && block.isEmpty) { result[i] = original; continue; }
                    var material = new Material(original);
                    var shader = material.shader;
                    for (int p = 0; p < shader.GetPropertyCount(); p++)
                    {
                        string name = shader.GetPropertyName(p); int prop = Shader.PropertyToID(name);
                        var type = shader.GetPropertyType(p);
                        if (block.HasProperty(prop))
                        {
                            switch (type)
                            {
                                case ShaderPropertyType.Color: material.SetColor(prop, block.GetColor(prop)); break;
                                case ShaderPropertyType.Vector: material.SetVector(prop, block.GetVector(prop)); break;
                                case ShaderPropertyType.Float: case ShaderPropertyType.Range: material.SetFloat(prop, block.GetFloat(prop)); break;
                                case ShaderPropertyType.Int: material.SetInt(prop, block.GetInt(prop)); break;
                                case ShaderPropertyType.Texture: material.SetTexture(prop, block.GetTexture(prop)); break;
                            }
                        }
                        if (type == ShaderPropertyType.Texture)
                        {
                            var texture = material.GetTexture(prop);
                            if (texture != null && !EditorUtility.IsPersistent(texture)) material.SetTexture(prop, Texture(texture, id + "_" + i + "_" + p));
                        }
                    }
                    result[i] = Save(material, folder + "/Material_" + id + "_" + i + ".mat");
                }
                return result;
            }
            private Texture Texture(Texture source, string id)
            {
                if (textures.TryGetValue(source, out var cached)) return cached;
                var temp = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var active = RenderTexture.active;
                try
                {
                    Graphics.Blit(source, temp); RenderTexture.active = temp;
                    var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                    texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); texture.Apply();
                    texture.wrapMode = source.wrapMode; texture.filterMode = source.filterMode;
                    cached = Save(texture, folder + "/Texture_" + id + ".asset"); textures.Add(source, cached); return cached;
                }
                finally { RenderTexture.active = active; RenderTexture.ReleaseTemporary(temp); }
            }
            private static T Save<T>(T value, string path) where T : Object
            {
                value.hideFlags = HideFlags.None;
                value.name = Path.GetFileNameWithoutExtension(path);
                var existing = AssetDatabase.LoadAssetAtPath<T>(path);
                if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
                EditorUtility.CopySerialized(value, existing); Object.DestroyImmediate(value);
                EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); return existing;
            }
        }
    }
}
