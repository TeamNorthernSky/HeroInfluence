using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.VFX;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>JC | 2026-11-11 | 원본 카탈로그를 기준으로 Play·빌드에 동일한 시각 준비 데이터를 생성합니다.</summary>
[InitializeOnLoad]
public sealed class BattleEffectWarmupBuilder : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public const string GeneratedRoot = "Assets/_ProtoType_Merge/JC/GeneratedVfxWarmup";
    private const string Output = GeneratedRoot + "/Resources";
    public int callbackOrder => 100;

    // JC | 2026-11-11 | 명시적 검증 Play에서만 준비 데이터를 검사합니다. 일반 테스트는 자산 검색조차 실행하지 않습니다.
    static BattleEffectWarmupBuilder()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode && JcPlayModePolicy.UsePlayerPreparation)
        {
            try { EnsureCurrent(); }
            catch (Exception exception)
            {
                // JC | 2026-11-11 | 준비 실패를 생략하고 실행하지 않도록 Play 진입만 취소하고 원인 로그를 남깁니다.
                EditorApplication.isPlaying = false;
                Debug.LogException(exception);
            }
        }
    }

    // JC | 2026-11-11 | 무거운 배포 준비 검증은 사용자가 명시적으로 선택하며 실행 도중 정책이 바뀌지 않게 합니다.
    private const string ValidationMenu = "Tools/JC/플레이 검증/빌드와 같은 이펙트 준비";

    [MenuItem(ValidationMenu)]
    private static void ToggleValidation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        JcPlayModePolicy.EditorValidationEnabled = !JcPlayModePolicy.EditorValidationEnabled;
        Menu.SetChecked(ValidationMenu, JcPlayModePolicy.EditorValidationEnabled);
    }

    [MenuItem(ValidationMenu, true)]
    private static bool ValidateMenu()
    {
        Menu.SetChecked(ValidationMenu, JcPlayModePolicy.EditorValidationEnabled);
        return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
    }

    // JC | 2026-11-11 | 에디터 테스트 설정과 관계없이 실제 빌드는 항상 기존 데이터 생성·정리를 수행합니다.
    public void OnPreprocessBuild(BuildReport report) => Generate();
    public void OnPostprocessBuild(BuildReport report) => Cleanup();

    // JC | 2026-11-11 | 매 Play마다 수백 프리팹을 재생성하지 않도록 원본 의존성·대상 플랫폼·생성 코드로 유효성을 확인합니다.
    // 저장하지 않은 원본이 있으면 캐시를 재사용하지 않으며, 원본을 임의 저장하지도 않습니다.
    public static void EnsureCurrent()
    {
        string fingerprint = SourceFingerprint();
        var data = AssetDatabase.LoadAssetAtPath<BattleEffectWarmupData>(Output + "/BattleEffectWarmupData.asset");
        if (!string.IsNullOrEmpty(fingerprint) && data != null && data.SourceFingerprint == fingerprint &&
            data.Catalogs != null && data.Catalogs.Length > 0 && data.Catalogs.All(c => c != null) &&
            data.Registries != null && data.Registries.Length > 0 && data.Registries.All(r => r != null) &&
            data.CommonEffects != null && data.Recipes != null && data.Recipes.Length > 0 &&
            data.Recipes.All(IsRecipeAvailable)) return;
        Generate();
    }

    // JC | 2026-11-11 | 파일을 삭제해도 로드된 Unity 참조가 남을 수 있어 실제 생성 파일의 존재까지 확인합니다.
    private static bool IsRecipeAvailable(BattleEffectWarmupData.Recipe recipe)
    {
        if (recipe == null || recipe.Source == null || recipe.Visual == null || recipe.Dependencies == null) return false;
        string path = AssetDatabase.GetAssetPath(recipe.Visual);
        return path.StartsWith(Output + "/", StringComparison.Ordinal) && System.IO.File.Exists(path);
    }

    private static string[] CommonEffectPaths() => AssetDatabase.FindAssets("t:Prefab")
        .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.Contains("/Resources/BattleEffects/")).ToArray();

    private static string SourceFingerprint()
    {
        var roots = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
            .Concat(CommonEffectPaths())
            .Append("Assets/_ProtoType_Merge/JC/Scripts/Editor/BattleEffectWarmupBuilder.cs")
            .Distinct().OrderBy(p => p, StringComparer.Ordinal).ToArray();
        foreach (string path in AssetDatabase.GetDependencies(roots, true))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            // JC | 2026-11-11 | 셰이더/스크립트의 내부 컴파일 dirty는 원본 편집이 아니므로 디스크 의존성 해시로 판정합니다.
            // 템플릿에 복사하는 프리팹·재질·설정의 미저장 편집만 캐시 재사용을 막습니다.
            if ((asset is GameObject || asset is Material || asset is ScriptableObject) && EditorUtility.IsDirty(asset)) return null;
            if (asset is GameObject prefab && prefab.GetComponentsInChildren<Component>(true)
                .Any(component => component != null && EditorUtility.IsDirty(component))) return null;
        }
        return Hash128.Compute(EditorUserBuildSettings.activeBuildTarget + "\n" + string.Join("\n",
            roots.Select(p => p + ":" + AssetDatabase.GetAssetDependencyHash(p)))).ToString();
    }

    public static void Cleanup()
    {
        if (AssetDatabase.IsValidFolder(GeneratedRoot)) AssetDatabase.DeleteAsset(GeneratedRoot);
    }

    public static void Generate()
    {
        Cleanup();
        System.IO.Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        var previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string[] dependencies = AssetDatabase.GetDependencies(scenes, true);
            var catalogs = LoadAssets<SkillPresentationCatalog>(dependencies);
            var registries = LoadAssets<EffectRegistry>(dependencies);
            if (catalogs.Length == 0 || registries.Length == 0)
                throw new BuildFailedException("[VFX Warmup] No presentation catalog/registry in enabled build scenes.");

            var sources = new HashSet<GameObject>();
            foreach (var registry in registries)
            {
                var serialized = new SerializedObject(registry);
                var entries = serialized.FindProperty("_entries");
                for (int i = 0; i < entries.arraySize; i++)
                    AddPrefab(entries.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue, sources);
            }
            foreach (var catalog in catalogs)
                foreach (string path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(catalog), true))
                    AddPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), sources);

            var common = CommonEffectPaths()
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToArray();
            foreach (var prefab in common) sources.Add(prefab);
            // Includes runtime-spawned children such as Nekoming's separate impact explosion.
            foreach (var source in sources.ToArray())
                foreach (string path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(source), true))
                    AddPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), sources);

            var data = ScriptableObject.CreateInstance<BattleEffectWarmupData>();
            data.Catalogs = catalogs;
            data.Registries = registries;
            data.CommonEffects = common;
            var recipes = new List<BattleEffectWarmupData.Recipe>();
            foreach (var source in sources.OrderBy(AssetDatabase.GetAssetPath))
            {
                string path = AssetDatabase.GetAssetPath(source);
                string guid = AssetDatabase.AssetPathToGUID(path);
                var visual = new GameObject(source.name + " (rendering only)");
                SceneManager.MoveGameObjectToScene(visual, previewScene);
                try
                {
                    // Copy native rendering components into fresh objects, never instantiate source scripts.
                    CopyVisualTree(source.transform, visual.transform);
                    var materials = new HashSet<Material>(visual.GetComponentsInChildren<Renderer>(true)
                        .SelectMany(r => r.sharedMaterials).Where(m => m != null));
                    // Script/preset material references also need warming (procedurally generated meshes).
                    foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                    {
                        var material = AssetDatabase.LoadAssetAtPath<Material>(dependency);
                        if (material == null || !materials.Add(material)) continue;
                        var proxy = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        UnityEngine.Object.DestroyImmediate(proxy.GetComponent<Collider>());
                        proxy.transform.SetParent(visual.transform, false);
                        proxy.GetComponent<Renderer>().sharedMaterial = material;
                    }
                    string outputPath = Output + "/" + guid + ".prefab";
                    visual.SetActive(false);
                    var template = PrefabUtility.SaveAsPrefabAsset(visual, outputPath);
                    if (template == null) throw new BuildFailedException("Could not save warmup visual: " + path);
                    recipes.Add(new BattleEffectWarmupData.Recipe
                    {
                        Source = source,
                        Visual = template,
                        Dependencies = AssetDatabase.GetDependencies(path, true)
                            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                            .Where(p => p != null && p != source && sources.Contains(p)).Distinct().ToArray()
                    });
                }
                finally { UnityEngine.Object.DestroyImmediate(visual); }
            }
            data.Recipes = recipes.ToArray();
            // JC | 2026-11-11 | 모든 템플릿 생성에 성공한 데이터만 다음 Play의 재사용 후보로 남깁니다.
            data.SourceFingerprint = SourceFingerprint();
            AssetDatabase.CreateAsset(data, Output + "/BattleEffectWarmupData.asset");
            // JC | 2026-11-11 | 자동 준비가 사용자의 다른 미저장 에셋까지 저장하지 않도록 생성 데이터만 저장합니다.
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log($"[VFX Warmup] Generated {recipes.Count} rendering recipes from build-scene catalogs.");
        }
        catch
        {
            Cleanup();
            throw;
        }
        finally { EditorSceneManager.ClosePreviewScene(previewScene); }
    }

    private static T[] LoadAssets<T>(IEnumerable<string> paths) where T : UnityEngine.Object =>
        paths.Select(AssetDatabase.LoadAssetAtPath<T>).Where(a => a != null).Distinct().ToArray();

    private static void AddPrefab(UnityEngine.Object obj, HashSet<GameObject> sources)
    {
        if (obj is GameObject prefab && AssetDatabase.GetAssetPath(prefab).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            sources.Add(prefab);
    }

    private static void CopyVisualTree(Transform source, Transform target)
    {
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        foreach (var component in source.GetComponents<Component>())
        {
            if (component == null) continue;
            Type type = component.GetType();
            if (type != typeof(ParticleSystem) && type != typeof(ParticleSystemRenderer) &&
                type != typeof(MeshFilter) && type != typeof(MeshRenderer) &&
                type != typeof(LineRenderer) && type != typeof(TrailRenderer) && type != typeof(VisualEffect)) continue;
            var copy = target.GetComponent(type);
            if (copy == null) copy = target.gameObject.AddComponent(type);
            try
            {
                if (component is VisualEffect sourceVfx && copy is VisualEffect targetVfx)
                {
                    // Copy the graph and exposed properties without copying native owner/renderer references.
                    targetVfx.visualEffectAsset = sourceVfx.visualEffectAsset;
                    var src = new SerializedObject(sourceVfx);
                    var dst = new SerializedObject(targetVfx);
                    var properties = src.FindProperty("m_PropertySheet");
                    if (properties != null) dst.CopyFromSerializedProperty(properties);
                    dst.ApplyModifiedPropertiesWithoutUndo();
                }
                else EditorUtility.CopySerialized(component, copy);
            }
            catch (Exception ex) { throw new BuildFailedException($"Visual copy failed: {source.name}, {type.FullName}: {ex}"); }
            if (copy is ParticleSystem ps)
            {
                var main = ps.main; main.playOnAwake = false; main.stopAction = ParticleSystemStopAction.None;
                var sub = ps.subEmitters; sub.enabled = false;
                // Never keep references from collision/trigger/sub-emitter modules into gameplay objects.
                var collision = ps.collision; collision.enabled = false;
                var trigger = ps.trigger; trigger.enabled = false;
                while (sub.subEmittersCount > 0) sub.RemoveSubEmitter(0);
            }
        }
        foreach (Transform child in source)
        {
            var node = new GameObject(child.name);
            node.transform.SetParent(target, false);
            CopyVisualTree(child, node.transform);
        }
    }
}
