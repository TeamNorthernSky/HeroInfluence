using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelTilemapGenerator : MonoBehaviour
{
    [Header("Tile Sources")]
    [SerializeField] private LevelTileRegistry tileRegistry;
    [SerializeField] private string obstacleTileKey = "obstacle";

    [Header("Tilemap Targets")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap obstacleTilemap;

    public void Generate(LevelData levelData)
    {
        if (levelData == null || tileRegistry == null)
            return;

        ClearTilemaps();
        GenerateGroundTiles(levelData);
        GenerateObstacleTiles(levelData);
    }

    public void ClearTilemaps()
    {
        if (groundTilemap != null)
            groundTilemap.ClearAllTiles();

        if (obstacleTilemap != null)
            obstacleTilemap.ClearAllTiles();
    }

    private void GenerateGroundTiles(LevelData levelData)
    {
        if (groundTilemap == null)
            return;

        var placements = levelData.GroundTilePlacements;
        for (int i = 0; i < placements.Count; i++)
        {
            TilePlacementData placement = placements[i];
            if (!tileRegistry.TryGetTile(placement.TileKey, out TileBase tile))
                continue;

            groundTilemap.SetTile(ToTileCell(placement.GridPosition), tile);
        }
    }

    private void GenerateObstacleTiles(LevelData levelData)
    {
        if (obstacleTilemap == null)
            return;

        if (!tileRegistry.TryGetTile(obstacleTileKey, out TileBase obstacleTile))
            return;

        var obstacleCells = levelData.ObstacleCells;
        for (int i = 0; i < obstacleCells.Count; i++)
            obstacleTilemap.SetTile(ToTileCell(obstacleCells[i]), obstacleTile);
    }

    private static Vector3Int ToTileCell(Vector2Int grid)
    {
        return new Vector3Int(grid.x, grid.y, 0);
    }
}
