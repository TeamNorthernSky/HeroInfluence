using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Boot remains the authored source. This derived prefab is only a direct-start fallback.
// Preview scenes keep their own AudioManager; no source scene is modified or saved.
[InitializeOnLoad]
public sealed class BootAudioPrefabBuilder : AssetPostprocessor, IPreprocessBuildWithReport
{
    public const string SourceScene = "Assets/_ProtoType_Merge/Scenes/BootScene.unity";
    public const string OutputPath = "Assets/Resources/BootAudioManager.prefab";
    private const string Version = "BootAudio:v2:";
    private static bool generating;
    public int callbackOrder => 90;

    static BootAudioPrefabBuilder()
    {
        EditorApplication.delayCall += PrepareInEditor;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) EnsureCurrent();
    }

    private static void PrepareInEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        try { EnsureCurrent(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (generating) return;
        if (imported.Contains(SourceScene) || deleted.Contains(OutputPath) || movedFrom.Contains(OutputPath))
        {
            EditorApplication.delayCall -= PrepareInEditor;
            EditorApplication.delayCall += PrepareInEditor;
        }
    }

    public void OnPreprocessBuild(BuildReport report) => EnsureCurrent();

    public static void EnsureCurrent()
    {
        if (generating || EditorApplication.isPlaying) return;
        // The prefab copies scene-authored fields/references, not imported clip data.
        // Library dependency hashes differ between checkouts even for identical files.
        string sceneSourceText = System.IO.File.ReadAllText(SourceScene).TrimStart('\uFEFF').Replace("\r\n", "\n");
        string fingerprint;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            fingerprint = Version + BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sceneSourceText))).Replace("-", "").ToLowerInvariant();
        var importer = AssetImporter.GetAtPath(OutputPath);
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath);
        if (existing != null && existing.GetComponent<AudioManager>() != null &&
            importer != null && importer.userData == fingerprint) return;

        generating = true;
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(SourceScene);
        bool openedForRead = false;
        try
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
                openedForRead = true;
            }
            if (scene.isDirty)
                throw new BuildFailedException("[Boot Audio] Boot씬에 저장하지 않은 변경이 있습니다. 직접 저장한 뒤 다시 준비하세요.");
            var managers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<AudioManager>(true)).ToArray();
            if (managers.Length != 1 || managers[0].ClipCatalog == null ||
                !managers[0].gameObject.activeInHierarchy || !managers[0].enabled)
                throw new BuildFailedException("[Boot Audio] Boot씬의 활성 AudioManager 및 카탈로그를 확인하세요.");
            var audio = new SerializedObject(managers[0]);
            foreach (string source in new[] { "bgmSource", "sfxSource" })
            {
                var component = audio.FindProperty(source).objectReferenceValue as AudioSource;
                if (component == null || !component.transform.IsChildOf(managers[0].transform))
                    throw new BuildFailedException("[Boot Audio] 사운드 소스는 AudioManager 계층 내부에 있어야 합니다: " + source);
            }
            if (!audio.FindProperty("dontDestroyOnLoad").boolValue)
                throw new BuildFailedException("[Boot Audio] Boot AudioManager의 씬 간 유지 설정이 필요합니다.");

            PrefabUtility.SaveAsPrefabAsset(managers[0].gameObject, OutputPath, out bool success);
            if (!success) throw new BuildFailedException("[Boot Audio] 직접 시작용 사운드 프리팹 생성 실패");
            importer = AssetImporter.GetAtPath(OutputPath);
            importer.userData = fingerprint;
            importer.SaveAndReimport();
        }
        finally
        {
            if (openedForRead && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            generating = false;
        }
    }
}
