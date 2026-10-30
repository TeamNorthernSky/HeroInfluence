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

/// <summary>Build-only derived data. Existing catalogs, registry keys and original prefabs stay authoritative.</summary>
public sealed class BattleEffectWarmupBuilder : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public const string GeneratedRoot = "Assets/_ProtoType_Merge/JC/GeneratedVfxWarmup";
    private const string Output = GeneratedRoot + "/Resources";
    public int callbackOrder => 100;

    public void OnPreprocessBuild(BuildReport report) => Generate();
    public void OnPostprocessBuild(BuildReport report) => Cleanup();

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

            var common = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Contains("/Resources/BattleEffects/"))
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
            AssetDatabase.CreateAsset(data, Output + "/BattleEffectWarmupData.asset");
            AssetDatabase.SaveAssets();
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
