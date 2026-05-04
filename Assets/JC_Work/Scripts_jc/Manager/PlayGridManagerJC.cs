using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 데이터 관리 매니저. GameManager 하위에 배치.
/// 순수 데이터 레이어 — 시각적 표현은 담당하지 않음.
///
/// 그리드 사이즈는 width/height 분리(직사각형 허용),
/// 인프라는 최대 8192² 까지 가정. 기본값 64×64.
/// </summary>
public class PlayGridManagerJC : MonoBehaviour
{
    public const float CellSize = 1f;
    public const int MaxSupportedSize = 8192;

    [Header("Grid Size")]
    [SerializeField, Min(1)] private int width = 64;
    [SerializeField, Min(1)] private int height = 64;

    public int Width => width;
    public int Height => height;
    public int CellCount => width * height;

    private GridCell[] cells;
    private Dictionary<int, MapObject> mapObjects = new Dictionary<int, MapObject>();
    private int nextObjectId = 0;

    private static readonly int GridWorldSizeId = Shader.PropertyToID("_GridWorldSize");

    public void Initialize()
    {
        // 인프라 한계 검증
        if (width > MaxSupportedSize || height > MaxSupportedSize)
        {
            Debug.LogError($"[PlayGridManagerJC] 그리드 크기 ({width}x{height})가 최대 지원 크기 {MaxSupportedSize}를 초과합니다");
            width = Mathf.Min(width, MaxSupportedSize);
            height = Mathf.Min(height, MaxSupportedSize);
        }

        cells = new GridCell[width * height];
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = GridCell.CreateDefault();
        }

        // 셰이더용 글로벌 벡터: (worldWidth, worldHeight, 1/worldWidth, 1/worldHeight)
        float worldW = width * CellSize;
        float worldH = height * CellSize;
        Shader.SetGlobalVector(GridWorldSizeId, new Vector4(worldW, worldH, 1f / worldW, 1f / worldH));

        Debug.Log($"[PlayGridManagerJC] {width}x{height} 그리드 초기화 완료 (셀 {cells.Length}개, 월드 {worldW}x{worldH})");
    }

    // --- 셀 접근 ---

    /// <summary>
    /// 셀을 값 복사로 반환. 범위 밖이면 default 반환.
    /// 셀을 변경하려면 PlayGridManagerJC의 변경 메서드(PlaceObject, RemoveObject, RevealArea 등)를 사용할 것.
    /// </summary>
    public GridCell GetCell(int x, int y)
    {
        if (!IsInBounds(x, y)) return default;
        return cells[y * width + x];
    }

    public GridCell GetCell(Vector2Int pos) => GetCell(pos.x, pos.y);

    /// <summary>
    /// 범위 검사를 명시적으로 분리한 형태. 범위 밖이면 false.
    /// </summary>
    public bool TryGetCell(int x, int y, out GridCell cell)
    {
        if (!IsInBounds(x, y))
        {
            cell = default;
            return false;
        }
        cell = cells[y * width + x];
        return true;
    }

    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public bool IsInBounds(Vector2Int pos) => IsInBounds(pos.x, pos.y);

    public bool IsWalkable(int x, int y)
    {
        if (!IsInBounds(x, y)) return false;
        var cell = cells[y * width + x];
        return cell.isWalkable && !cell.IsOccupied;
    }

    public bool IsWalkable(Vector2Int pos) => IsWalkable(pos.x, pos.y);

    // --- 좌표 변환 ---

    /// <summary>
    /// 그리드 좌표 → 월드 좌표 (셀 중심).
    /// 그리드 (0,0)은 월드 원점, X축 = 그리드 X, Z축 = 그리드 Y.
    /// </summary>
    public static Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(
            x * CellSize + CellSize * 0.5f,
            0f,
            y * CellSize + CellSize * 0.5f
        );
    }

    public static Vector3 GridToWorld(Vector2Int pos) => GridToWorld(pos.x, pos.y);

    /// <summary>
    /// 월드 좌표 → 그리드 좌표.
    /// </summary>
    public static Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / CellSize),
            Mathf.FloorToInt(worldPos.z / CellSize)
        );
    }

    // --- 오브젝트 배치 ---

    /// <summary>
    /// MapObject를 그리드에 배치. 점유 셀들을 갱신.
    /// </summary>
    public bool PlaceObject(MapObject obj)
    {
        var occupiedCells = obj.GetOccupiedCells();

        // 배치 가능 여부 확인
        foreach (var pos in occupiedCells)
        {
            if (!IsInBounds(pos.x, pos.y))
            {
                Debug.LogWarning($"[PlayGridManagerJC] 배치 실패: ({pos.x},{pos.y})가 범위 밖");
                return false;
            }
            if (cells[pos.y * width + pos.x].IsOccupied)
            {
                Debug.LogWarning($"[PlayGridManagerJC] 배치 실패: ({pos.x},{pos.y})가 이미 점유됨 (ID:{cells[pos.y * width + pos.x].occupantId})");
                return false;
            }
        }

        // ID 부여 및 등록
        obj.id = nextObjectId++;
        mapObjects[obj.id] = obj;

        // 셀 점유 갱신 (struct이므로 인덱스 직접 변경)
        foreach (var pos in occupiedCells)
        {
            int idx = pos.y * width + pos.x;
            cells[idx].occupantId = obj.id;
            if (!obj.isWalkable)
                cells[idx].isWalkable = false;
        }

        return true;
    }

    /// <summary>
    /// MapObject를 그리드에서 제거. 점유 셀들을 해제.
    /// </summary>
    public void RemoveObject(int objectId)
    {
        if (!mapObjects.TryGetValue(objectId, out var obj)) return;

        foreach (var pos in obj.GetOccupiedCells())
        {
            if (!IsInBounds(pos.x, pos.y)) continue;
            int idx = pos.y * width + pos.x;
            if (cells[idx].occupantId == objectId)
            {
                cells[idx].occupantId = -1;
                cells[idx].isWalkable = true;
            }
        }

        mapObjects.Remove(objectId);
    }

    public MapObject GetMapObject(int objectId)
    {
        return mapObjects.TryGetValue(objectId, out var obj) ? obj : null;
    }

    // --- 시야 ---

    /// <summary>
    /// 지정 위치 주변 시야 범위 내 셀을 Visible로 설정.
    /// </summary>
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
                {
                    cells[y * width + x].visibility = GridCell.VisibilityState.Visible;
                }
            }
        }
    }

    /// <summary>
    /// 현재 Visible인 셀들을 Fogged로 전환 (턴 종료 시 등).
    /// </summary>
    public void FogAllVisible()
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i].visibility == GridCell.VisibilityState.Visible)
                cells[i].visibility = GridCell.VisibilityState.Fogged;
        }
    }

    // --- 전체 셀 배열 접근 (시각화, 세이브 등) ---

    public GridCell[] GetAllCells() => cells;
}
