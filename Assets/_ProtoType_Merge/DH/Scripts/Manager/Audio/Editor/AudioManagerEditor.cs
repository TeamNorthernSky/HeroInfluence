using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioManager))]
public sealed class AudioManagerEditor : Editor
{
    private SerializedProperty clipCatalogProperty;
    private SerializedProperty bgmSourceProperty;
    private SerializedProperty sfxSourceProperty;
    private SerializedProperty masterVolumeProperty;
    private SerializedProperty bgmVolumeProperty;
    private SerializedProperty sfxVolumeProperty;
    private SerializedProperty playBgmOnSceneLoadedProperty;
    private SerializedProperty stopBgmWhenSceneHasNoBindingProperty;
    private SerializedProperty sceneBgmBindingsProperty;
    private SerializedProperty dontDestroyOnLoadProperty;

    private void OnEnable()
    {
        clipCatalogProperty = serializedObject.FindProperty("clipCatalog");
        bgmSourceProperty = serializedObject.FindProperty("bgmSource");
        sfxSourceProperty = serializedObject.FindProperty("sfxSource");
        masterVolumeProperty = serializedObject.FindProperty("masterVolume");
        bgmVolumeProperty = serializedObject.FindProperty("bgmVolume");
        sfxVolumeProperty = serializedObject.FindProperty("sfxVolume");
        playBgmOnSceneLoadedProperty = serializedObject.FindProperty("playBgmOnSceneLoaded");
        stopBgmWhenSceneHasNoBindingProperty = serializedObject.FindProperty("stopBgmWhenSceneHasNoBinding");
        sceneBgmBindingsProperty = serializedObject.FindProperty("sceneBgmBindings");
        dontDestroyOnLoadProperty = serializedObject.FindProperty("dontDestroyOnLoad");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(clipCatalogProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sources", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(bgmSourceProperty);
        EditorGUILayout.PropertyField(sfxSourceProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Volumes", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(masterVolumeProperty);
        EditorGUILayout.PropertyField(bgmVolumeProperty);
        EditorGUILayout.PropertyField(sfxVolumeProperty);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scene BGM", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(playBgmOnSceneLoadedProperty);
        EditorGUILayout.PropertyField(stopBgmWhenSceneHasNoBindingProperty);
        DrawSceneBgmBindings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lifetime", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(dontDestroyOnLoadProperty);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSceneBgmBindings()
    {
        DHAudioClipCatalog catalog = clipCatalogProperty.objectReferenceValue as DHAudioClipCatalog;
        List<string> bgmKeys = GetBgmKeys(catalog);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Scene Bgm Bindings", EditorStyles.boldLabel);
        if (GUILayout.Button("+", GUILayout.Width(28f)))
            sceneBgmBindingsProperty.InsertArrayElementAtIndex(sceneBgmBindingsProperty.arraySize);
        EditorGUILayout.EndHorizontal();

        if (sceneBgmBindingsProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox("Add scene names and matching BGM keys from the audio clip catalog.", MessageType.Info);
        }

        for (int i = 0; i < sceneBgmBindingsProperty.arraySize; i++)
        {
            SerializedProperty element = sceneBgmBindingsProperty.GetArrayElementAtIndex(i);
            SerializedProperty sceneNameProperty = element.FindPropertyRelative("sceneName");
            SerializedProperty bgmKeyProperty = element.FindPropertyRelative("bgmKey");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Binding {i}", EditorStyles.boldLabel);
            if (GUILayout.Button("-", GUILayout.Width(28f)))
            {
                sceneBgmBindingsProperty.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(sceneNameProperty);
            DrawBgmKeyField(bgmKeyProperty, bgmKeys);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawBgmKeyField(SerializedProperty bgmKeyProperty, List<string> bgmKeys)
    {
        if (bgmKeys.Count == 0)
        {
            EditorGUILayout.PropertyField(bgmKeyProperty);
            return;
        }

        string currentKey = DHAudioClipCatalog.NormalizeKey(bgmKeyProperty.stringValue);
        List<string> options = new List<string> { string.Empty };
        options.AddRange(bgmKeys);

        int currentIndex = options.FindIndex(key =>
            string.Equals(key, currentKey, System.StringComparison.OrdinalIgnoreCase));

        if (currentIndex < 0 && !string.IsNullOrWhiteSpace(currentKey))
        {
            options.Add(currentKey);
            currentIndex = options.Count - 1;
        }

        if (currentIndex < 0)
            currentIndex = 0;

        string[] labels = new string[options.Count];
        labels[0] = "<None>";
        for (int i = 1; i < options.Count; i++)
            labels[i] = options[i];

        int nextIndex = EditorGUILayout.Popup("Bgm Key", currentIndex, labels);
        bgmKeyProperty.stringValue = options[nextIndex];
    }

    private static List<string> GetBgmKeys(DHAudioClipCatalog catalog)
    {
        List<string> keys = new List<string>();
        if (catalog == null || catalog.BgmClips == null)
            return keys;

        for (int i = 0; i < catalog.BgmClips.Count; i++)
        {
            string key = catalog.BgmClips[i].Key;
            if (!string.IsNullOrWhiteSpace(key) && !keys.Contains(key))
                keys.Add(key);
        }

        keys.Sort(System.StringComparer.OrdinalIgnoreCase);
        return keys;
    }
}
