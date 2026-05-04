using System.Collections.Generic;
using UnityEngine;

public class KJ_PlayGridManager : MonoBehaviour
{
    public const float CellSize = 1f;
    public const int MaxSupportedSize = 8192;

    [Header("Grid Size")]
    [SerializeField, Min(1)] private int width = 64;
    [SerializeField, Min(1)] private int height = 64;

    public int Width => width;
    public int Height => height;
    public int CellCount => width * height;

    private KJ_GridCell[] cells;
    private readonly Dictionary<int, KJ_MapObject> mapObjects = new Dictionary<int, KJ_MapObject>();
    private int nextObjectId;

    private static readonly int GridWorldSizeId = Shader.PropertyToID("_GridWorldSize");

    public void Initialize()
    {
        if (width > MaxSupportedSize || height > MaxSupportedSize)
        {
            Debug.LogError($"[KJ_PlayGridManager] Grid size ({width}x{height}) exceeds {MaxSupportedSize}.");
            width = Mathf.Min(width, MaxSupportedSize);
            height = Mathf.Min(height, MaxSupportedSize);
        }

        cells = new KJ_GridCell[width * height];
        for (int i = 0; i < cells.Length; i++)
            cells[i] = KJ_GridCell.CreateDefault();

        float worldW = width * CellSize;
        float worldH = height * CellSize;
        Shader.SetGlobalVector(GridWorldSizeId, new Vector4(worldW, worldH, 1f / worldW, 1f / worldH));
    }

    public KJ_GridCell GetCell(int x, int y)
    {
        if (!IsInBounds(x, y))
            return default;
        return cells[y * width + x];
    }

    public KJ_GridCell GetCell(Vector2Int pos) => GetCell(pos.x, pos.y);

    public bool TryGetCell(int x, int y, out KJ_GridCell cell)
    {
        if (!IsInBounds(x, y))
        {
            cell = default;
            return false;
        }

        cell = cells[y * width + x];
        return true;
    }

    public bool IsInBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
    public bool IsInBounds(Vector2Int pos) => IsInBounds(pos.x, pos.y);

    public bool IsWalkable(int x, int y)
    {
        if (!IsInBounds(x, y))
            return false;

        var cell = cells[y * width + x];
        return cell.isWalkable && !cell.IsOccupied;
    }

    public bool IsWalkable(Vector2Int pos) => IsWalkable(pos.x, pos.y);

    public static Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(
            x * CellSize + CellSize * 0.5f,
            0f,
            y * CellSize + CellSize * 0.5f
        );
    }

    public static Vector3 GridToWorld(Vector2Int pos) => GridToWorld(pos.x, pos.y);

    public static Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / CellSize),
            Mathf.FloorToInt(worldPos.z / CellSize)
        );
    }

    public bool PlaceObject(KJ_MapObject obj)
    {
        var occupiedCells = obj.GetOccupiedCells();

        foreach (var pos in occupiedCells)
        {
            if (!IsInBounds(pos.x, pos.y))
                return false;

            if (cells[pos.y * width + pos.x].IsOccupied)
                return false;
        }

        obj.id = nextObjectId++;
        mapObjects[obj.id] = obj;

        foreach (var pos in occupiedCells)
        {
            int idx = pos.y * width + pos.x;
            cells[idx].occupantId = obj.id;
            if (!obj.isWalkable)
                cells[idx].isWalkable = false;
        }

        return true;
    }

    public void RemoveObject(int objectId)
    {
        if (!mapObjects.TryGetValue(objectId, out var obj))
            return;

        foreach (var pos in obj.GetOccupiedCells())
        {
            if (!IsInBounds(pos.x, pos.y))
                continue;

            int idx = pos.y * width + pos.x;
            if (cells[idx].occupantId == objectId)
            {
                cells[idx].occupantId = -1;
                cells[idx].isWalkable = true;
            }
        }

        mapObjects.Remove(objectId);
    }

    public KJ_MapObject GetMapObject(int objectId)
    {
        return mapObjects.TryGetValue(objectId, out var obj) ? obj : null;
    }

    public void RevealArea(Vector2Int center, int radius)
    {
        int r2 = radius * radius;
        int minX = Mathf.Max(0, center.x - radius);
        int maxX = Mathf.Min(width - 1, center.x + radius);
        int minY = Mathf.Max(0, center.y - radius);
        int maxY = Mathf.Min(height - 1, center.y + radius);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - center.y;
            int dy2 = dy * dy;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - center.x;
                if (dx * dx + dy2 <= r2)
                    cells[y * width + x].visibility = KJ_GridCell.VisibilityState.Visible;
            }
        }
    }

    public void FogAllVisible()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].visibility == KJ_GridCell.VisibilityState.Visible)
                cells[i].visibility = KJ_GridCell.VisibilityState.Fogged;
        }
    }

    public KJ_GridCell[] GetAllCells() => cells;
}
