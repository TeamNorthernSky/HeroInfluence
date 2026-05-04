using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(
    fileName = "LevelTileRegistry",
    menuName = "DH Work/Level Editor/Level Tile Registry")]
public class LevelTileRegistry : ScriptableObject
{
    [SerializeField] private List<LevelTileEntry> tileEntries = new List<LevelTileEntry>();

    public bool TryGetTile(string tileKey, out TileBase tile)
    {
        tile = null;

        if (string.IsNullOrWhiteSpace(tileKey))
            return false;

        for (int i = 0; i < tileEntries.Count; i++)
        {
            LevelTileEntry entry = tileEntries[i];
            if (!string.Equals(entry.TileKey, tileKey, StringComparison.Ordinal))
                continue;

            tile = entry.Tile;
            return tile != null;
        }

        return false;
    }
}

[Serializable]
public struct LevelTileEntry
{
    [SerializeField] private string tileKey;
    [SerializeField] private TileBase tile;

    public string TileKey => tileKey;
    public TileBase Tile => tile;
}
