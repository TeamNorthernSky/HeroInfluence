using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelTileRegistry",
    menuName = "DH Work/Level Editor/Level Tile Registry")]
public class LevelTileRegistry : ScriptableObject
{
    [Header("Tiles")]
    [SerializeField] private List<LevelTileEntry> tileEntries = new List<LevelTileEntry>();

    [Header("Materials")]
    [SerializeField] private List<LevelTileMaterialEntry> materialEntries = new List<LevelTileMaterialEntry>();

    public IReadOnlyList<LevelTileEntry> TileEntries => tileEntries;
    public IReadOnlyList<LevelTileMaterialEntry> MaterialEntries => materialEntries;

    private void OnValidate()
    {
        AutoFillMissingTileKeys();
    }

    [ContextMenu("Auto Fill Missing Tile Keys")]
    private void AutoFillMissingTileKeys()
    {
        if (tileEntries == null || tileEntries.Count == 0)
            return;

        bool changed = false;
        HashSet<string> usedKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < tileEntries.Count; i++)
        {
            LevelTileEntry entry = tileEntries[i];
            string existingKey = entry.TileKey;

            if (!string.IsNullOrWhiteSpace(existingKey))
            {
                usedKeys.Add(existingKey);
                continue;
            }

            Sprite sprite = entry.Sprite;
            if (sprite == null || string.IsNullOrWhiteSpace(sprite.name))
                continue;

            string generatedKey = CreateUniqueKey(sprite.name, usedKeys);
            entry.SetTileKey(generatedKey);
            tileEntries[i] = entry;
            usedKeys.Add(generatedKey);
            changed = true;
        }

#if UNITY_EDITOR
        if (changed)
            UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public bool TryGetSprite(string tileKey, out Sprite sprite)
    {
        sprite = null;

        if (string.IsNullOrWhiteSpace(tileKey))
            return false;

        for (int i = 0; i < tileEntries.Count; i++)
        {
            LevelTileEntry entry = tileEntries[i];
            if (!string.Equals(entry.TileKey, tileKey, StringComparison.Ordinal))
                continue;

            sprite = entry.Sprite;
            return sprite != null;
        }

        return false;
    }

    public bool TryGetMaterialTemplate(string materialKey, out Material materialTemplate)
    {
        materialTemplate = null;

        if (string.IsNullOrWhiteSpace(materialKey))
            return false;

        for (int i = 0; i < materialEntries.Count; i++)
        {
            LevelTileMaterialEntry entry = materialEntries[i];
            if (!string.Equals(entry.MaterialKey, materialKey, StringComparison.Ordinal))
                continue;

            materialTemplate = entry.MaterialTemplate;
            return materialTemplate != null;
        }

        return false;
    }

    private static string CreateUniqueKey(string baseKey, HashSet<string> usedKeys)
    {
        if (!usedKeys.Contains(baseKey))
            return baseKey;

        int suffix = 1;
        string candidate;
        do
        {
            candidate = $"{baseKey}_{suffix}";
            suffix++;
        }
        while (usedKeys.Contains(candidate));

        return candidate;
    }
}

[Serializable]
public struct LevelTileEntry
{
    [SerializeField] private string tileKey;
    [SerializeField] private Sprite sprite;

    public string TileKey => tileKey;
    public Sprite Sprite => sprite;

    public void SetTileKey(string value)
    {
        tileKey = value;
    }
}

[Serializable]
public struct LevelTileMaterialEntry
{
    [SerializeField] private string materialKey;
    [SerializeField] private Material materialTemplate;

    public string MaterialKey => materialKey;
    public Material MaterialTemplate => materialTemplate;
}
