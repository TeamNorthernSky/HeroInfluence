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
        Generate(levelData, Vector2Int.zero, false);
    }

    public void Generate(LevelData levelData, Vector2Int offset, bool clearBeforeGenerate)
    {
        if (levelData == null || tileRegistry == null)
            return;

        if (clearBeforeGenerate)
            ClearTilemaps();

        GenerateGroundTiles(levelData, offset);
        GenerateObstacleTiles(levelData, offset);
    }

    public void ClearTilemaps()
    {
        if (groundTilemap != null)
            groundTilemap.ClearAllTiles();

        if (obstacleTilemap != null)
            obstacleTilemap.ClearAllTiles();
    }

    private void GenerateGroundTiles(LevelData levelData, Vector2Int offset)
    {
        if (groundTilemap == null)
            return;

        var placements = levelData.GroundTilePlacements;
        for (int i = 0; i < placements.Count; i++)
        {
            TilePlacementData placement = placements[i];
            if (!tileRegistry.TryGetTile(placement.TileKey, out TileBase tile))
                continue;

            groundTilemap.SetTile(ToTileCell(placement.GridPosition + offset), tile);
        }
    }

    private void GenerateObstacleTiles(LevelData levelData, Vector2Int offset)
    {
        if (obstacleTilemap == null)
            return;

        if (!tileRegistry.TryGetTile(obstacleTileKey, out TileBase obstacleTile))
            return;

        var obstacleCells = levelData.ObstacleCells;
        for (int i = 0; i < obstacleCells.Count; i++)
            obstacleTilemap.SetTile(ToTileCell(obstacleCells[i] + offset), obstacleTile);
    }

    private static Vector3Int ToTileCell(Vector2Int grid)
    {
        return new Vector3Int(grid.x, grid.y, 0);
    }
}
