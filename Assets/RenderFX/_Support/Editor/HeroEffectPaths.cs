using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class HeroEffectPaths
{
    [Serializable] private sealed class Entry { public string path; public string guid; }
    [Serializable] private sealed class Table { public Entry[] entries; }
    private static Dictionary<string, string> aliases;

    static HeroEffectPaths() => EditorApplication.projectChanged += () => aliases = null;

    public static string Resolve(string path)
    {
        if (string.IsNullOrEmpty(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))) return path;
        if (aliases == null)
        {
            aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/RenderFX/_Support/HeroEffectPaths.json");
            var table = text != null ? JsonUtility.FromJson<Table>(text.text) : null;
            if (table?.entries != null)
                foreach (var entry in table.entries) aliases[entry.path] = entry.guid;
        }
        return aliases.TryGetValue(path, out var guid) ? AssetDatabase.GUIDToAssetPath(guid) : path;
    }

    public static T Load<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(Resolve(path));
}
