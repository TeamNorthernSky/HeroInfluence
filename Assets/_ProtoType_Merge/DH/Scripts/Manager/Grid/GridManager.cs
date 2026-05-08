using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    private static readonly Vector2Int[] directions8 =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1)
    };

    [Header("Grid 기준 오브젝트")]
    [SerializeField] private Transform landTransform;

    [Header("Grid Settings")]
    [FormerlySerializedAs("hexRadius")]
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private bool useTilemapGrid;
    [SerializeField] private GridLayout tilemapGridLayout;
    [SerializeField] private Tilemap referenceTilemap;
    [SerializeField] private bool tilemapUsesXZPlane = true;

    [Header("Obstacle Settings")]
    [Tooltip("이 레이어에 있는 콜라이더는 장애물로 간주합니다.")]
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private LayerMask itemLayerMask;
    [FormerlySerializedAs("mineLayerMask")]
    [SerializeField] private LayerMask outpostLayerMask;
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private LayerMask enemyLayerMask;
    [SerializeField] private LayerMask castleLayerMask;
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private CastleRegistry castleRegistry;
    [FormerlySerializedAs("mineRegistry")]
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;
    [SerializeField] private MultiGridOccupantRegistry multiGridOccupantRegistry;
    [SerializeField] private bool restrictMovementToVisibleCells = true;
    [Tooltip("셀 워커블 검사 시, 셀 크기 대비 체크 박스 비율(너무 크면 오탐, 너무 작으면 통과).")]
    [SerializeField, Range(0.1f, 1f)] private float obstacleCheckFill = 0.9f;

    [Header("Debug Gizmos")]
    [SerializeField] private int debugQMin = -15;
    [SerializeField] private int debugQMax = 15;
    [SerializeField] private int debugRMin = -15;
    [SerializeField] private int debugRMax = 15;
    [SerializeField, Range(0.01f, 0.5f)] private float debugCenterSphereRadius = 0.08f;

    // 전제 조건: Land 월드 중심 = (0, 0, 0)
    // 필요 시 인스펙터에서 원점 오프셋 확장 가능
    private Vector3 gridOrigin = Vector3.zero;

    public float CellSize => cellSize;
    public Transform LandTransform => landTransform;
    public Transform GroundRaycastTransform
        => landTransform != null
            ? landTransform
            : referenceTilemap != null
                ? referenceTilemap.transform
                : tilemapGridLayout != null
                    ? tilemapGridLayout.transform
                    : null;
    public bool UsesTilemapGrid => useTilemapGrid && tilemapGridLayout != null;
    public static Vector2Int[] Directions8 => directions8;

    private void Awake()
    {
        ResolveTilemapReferences();

        if (cellSize <= 0f)
            cellSize = 1f;

        SyncCellSizeFromTilemap();

        if (fogGridManager == null)
            fogGridManager = FindFirstObjectByType<FogGridManager>();

        if (castleRegistry == null)
            castleRegistry = FindFirstObjectByType<CastleRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (multiGridOccupantRegistry == null)
            multiGridOccupantRegistry = FindFirstObjectByType<MultiGridOccupantRegistry>();
    }

    private void OnValidate()
    {
        ResolveTilemapReferences();
        SyncCellSizeFromTilemap();

        if (castleRegistry == null)
            castleRegistry = FindFirstObjectByType<CastleRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (multiGridOccupantRegistry == null)
            multiGridOccupantRegistry = FindFirstObjectByType<MultiGridOccupantRegistry>();
    }

    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        if (UsesTilemapGrid)
        {
            Vector3Int cell = tilemapGridLayout.WorldToCell(worldPosition);
            return tilemapUsesXZPlane
                ? new Vector2Int(cell.x, cell.z)
                : new Vector2Int(cell.x, cell.y);
        }

        float localX = worldPosition.x - gridOrigin.x;
        float localZ = worldPosition.z - gridOrigin.z;

        int x = Mathf.RoundToInt(localX / cellSize);
        int y = Mathf.RoundToInt(localZ / cellSize);
        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorldCenter(Vector2Int grid)
    {
        if (UsesTilemapGrid)
        {
            Vector3Int cell = tilemapUsesXZPlane
                ? new Vector3Int(grid.x, 0, grid.y)
                : new Vector3Int(grid.x, grid.y, 0);

            if (referenceTilemap != null)
                return referenceTilemap.GetCellCenterWorld(cell);
        }

        float x = gridOrigin.x + cellSize * grid.x;
        float z = gridOrigin.z + cellSize * grid.y;

        return new Vector3(x, 0f, z);
    }

    public static int GridDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    public bool HasObstacle(Vector2Int grid)
    {
        return HasBlockingCollider(grid, obstacleLayerMask);
    }

    public bool HasOtherPlayer(Vector2Int grid, Transform selfTransform)
    {
        return HasBlockingCollider(grid, playerLayerMask, selfTransform);
    }

    public bool HasItem(Vector2Int grid)
    {
        return HasBlockingCollider(grid, itemLayerMask);
    }

    public bool TryGetItemObjectAtGrid(Vector2Int grid, out ItemObject itemObject)
    {
        itemObject = null;

        Vector3 center = GridToWorldCenter(grid);
        center.y = GetLandSurfaceY() + 0.5f;

        Vector3 halfExtents = new Vector3(cellSize * 0.5f * obstacleCheckFill, 0.5f, cellSize * 0.5f * obstacleCheckFill);
        Collider[] cols = Physics.OverlapBox(center, halfExtents, Quaternion.identity, itemLayerMask);

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null)
                continue;

            itemObject = c.GetComponentInParent<ItemObject>();
            if (itemObject != null)
                return true;
        }

        return false;
    }

    public bool HasOutpost(Vector2Int grid)
    {
        return TryGetOutpostObjectAtGrid(grid, out _);
    }

    public bool HasEnemy(Vector2Int grid, Transform selfTransform = null)
    {
        return HasBlockingCollider(grid, enemyLayerMask, selfTransform);
    }

    public bool HasMultiGridOccupant(Vector2Int grid, Transform selfTransform = null)
    {
        return TryGetMultiGridOccupantAtGrid(grid, out _, selfTransform);
    }

    public bool HasCastle(Vector2Int grid, Transform selfTransform = null)
    {
        return HasBlockingCollider(grid, castleLayerMask, selfTransform)
            || TryGetCastleByMultiGrid(grid, out _, selfTransform);
    }

    public bool TryGetOutpostObjectAtGrid(Vector2Int grid, out Outpost outpost)
    {
        outpost = null;

        IReadOnlyList<Outpost> outposts = outpostRegistry != null
            ? outpostRegistry.Outposts
            : FindObjectsByType<Outpost>(FindObjectsSortMode.None);

        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost candidate = outposts[i];
            if (candidate == null || !candidate.OccupiesGrid(grid, this))
                continue;

            outpost = candidate;
            return true;
        }

        Vector3 center = GridToWorldCenter(grid);
        center.y = GetLandSurfaceY() + 0.5f;

        Vector3 halfExtents = new Vector3(cellSize * 0.5f * obstacleCheckFill, 0.5f, cellSize * 0.5f * obstacleCheckFill);
        Collider[] cols = Physics.OverlapBox(center, halfExtents, Quaternion.identity, outpostLayerMask);

        for (int i = 0; i < cols.Length; i++)
        {
            Collider col = cols[i];
            if (col == null)
                continue;

            outpost = col.GetComponentInParent<Outpost>();
            if (outpost != null)
                return true;
        }

        return false;
    }

    public bool TryGetEnemyObjectAtGrid(Vector2Int grid, out EnemyGridMover enemy)
    {
        enemy = null;

        Vector3 center = GridToWorldCenter(grid);
        center.y = GetLandSurfaceY() + 0.5f;

        Vector3 halfExtents = new Vector3(cellSize * 0.5f * obstacleCheckFill, 0.5f, cellSize * 0.5f * obstacleCheckFill);
        Collider[] cols = Physics.OverlapBox(center, halfExtents, Quaternion.identity, enemyLayerMask);

        for (int i = 0; i < cols.Length; i++)
        {
            Collider col = cols[i];
            if (col == null)
                continue;

            enemy = col.GetComponentInParent<EnemyGridMover>();
            if (enemy != null)
                return true;
        }

        return false;
    }

    public bool TryGetCastleObjectAtGrid(Vector2Int grid, out CastleUnit castle)
    {
        castle = null;

        Vector3 center = GridToWorldCenter(grid);
        center.y = GetLandSurfaceY() + 0.5f;

        Vector3 halfExtents = new Vector3(cellSize * 0.5f * obstacleCheckFill, 0.5f, cellSize * 0.5f * obstacleCheckFill);
        Collider[] cols = Physics.OverlapBox(center, halfExtents, Quaternion.identity, castleLayerMask);

        for (int i = 0; i < cols.Length; i++)
        {
            Collider col = cols[i];
            if (col == null)
                continue;

            castle = col.GetComponentInParent<CastleUnit>();
            if (castle != null)
                return true;
        }

        return TryGetCastleByMultiGrid(grid, out castle);
    }

    public bool HasItemOrOutpost(Vector2Int grid)
    {
        return HasItem(grid) || HasOutpost(grid);
    }

    public bool HasVillainUnionBase(Vector2Int grid)
    {
        return TryGetVillainUnionBaseAtGrid(grid, out _);
    }

    public bool HasInteractionTarget(Vector2Int grid)
    {
        return HasItem(grid) || HasOutpost(grid) || HasEnemy(grid) || HasCastle(grid) || HasVillainUnionBase(grid);
    }

    public bool IsVisibleCell(Vector2Int grid)
    {
        if (!restrictMovementToVisibleCells || fogGridManager == null)
            return true;

        return fogGridManager.IsVisible(grid);
    }

    public bool TryGetAdjacentItemGrid(Vector2Int grid, out Vector2Int itemGrid)
    {
        for (int i = 0; i < directions8.Length; i++)
        {
            Vector2Int candidate = grid + directions8[i];
            if (HasItem(candidate))
            {
                itemGrid = candidate;
                return true;
            }
        }

        itemGrid = grid;
        return false;
    }

    public bool TryGetAdjacentOutpostGrid(Vector2Int grid, out Vector2Int outpostGrid)
    {
        for (int i = 0; i < directions8.Length; i++)
        {
            Vector2Int candidate = grid + directions8[i];
            if (TryGetOutpostObjectAtGrid(candidate, out _))
            {
                outpostGrid = candidate;
                return true;
            }
        }

        outpostGrid = grid;
        return false;
    }

    public bool TryGetAdjacentEnemyGrid(Vector2Int grid, out Vector2Int enemyGrid)
    {
        for (int i = 0; i < directions8.Length; i++)
        {
            Vector2Int candidate = grid + directions8[i];
            if (HasEnemy(candidate))
            {
                enemyGrid = candidate;
                return true;
            }
        }

        enemyGrid = grid;
        return false;
    }

    public bool TryGetAdjacentCastleObject(Vector2Int grid, out CastleUnit castle)
    {
        castle = null;

        CastleUnit[] castles = FindObjectsByType<CastleUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < castles.Length; i++)
        {
            CastleUnit candidate = castles[i];
            if (candidate == null)
                continue;

            if (!IsAdjacentToCastle(grid, candidate))
                continue;

            castle = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetVillainUnionBaseAtGrid(Vector2Int grid, out VillainUnionBase villainUnionBase)
    {
        villainUnionBase = null;

        IReadOnlyList<VillainUnionBase> bases = villainUnionBaseRegistry != null
            ? villainUnionBaseRegistry.VillainUnionBases
            : FindObjectsByType<VillainUnionBase>(FindObjectsSortMode.None);

        for (int i = 0; i < bases.Count; i++)
        {
            VillainUnionBase candidate = bases[i];
            if (candidate == null)
                continue;

            MultiGridOccupant occupant = candidate.GetComponent<MultiGridOccupant>();
            if (occupant != null)
            {
                if (!occupant.OccupiesCell(grid))
                    continue;
            }
            else if (candidate.GetCurrentGrid() != grid)
            {
                continue;
            }

            villainUnionBase = candidate;
            return true;
        }

        return false;
    }

    public bool IsAdjacentToCastle(Vector2Int grid, CastleUnit castle)
    {
        if (castle == null)
            return false;

        MultiGridOccupant occupant = castle.GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.IsAdjacentOuterCell(grid);

        return IsAdjacentToSingleCell(grid, castle.GetCurrentGrid());
    }

    public bool CanEnterCell(Vector2Int grid, Vector2Int destination, Transform selfTransform = null, bool ignoreFogVisibility = false)
    {
        if (HasObstacle(grid))
            return false;

        if (HasOtherPlayer(grid, selfTransform))
            return false;

        if (!ignoreFogVisibility && !IsVisibleCell(grid))
            return false;

        if (grid == destination)
            return true;

        if (HasEnemy(grid, selfTransform))
            return false;

        if (HasMultiGridOccupant(grid, selfTransform))
            return false;

        if (HasCastle(grid, selfTransform))
            return false;

        return !HasItemOrOutpost(grid);
    }

    public bool CanOccupyCell(Vector2Int grid, Transform selfTransform = null, bool ignoreFogVisibility = false)
    {
        if (HasObstacle(grid))
            return false;

        if (HasOtherPlayer(grid, selfTransform))
            return false;

        if (!ignoreFogVisibility && !IsVisibleCell(grid))
            return false;

        if (HasEnemy(grid, selfTransform))
            return false;

        if (HasMultiGridOccupant(grid, selfTransform))
            return false;

        if (HasCastle(grid, selfTransform))
            return false;

        return !HasItemOrOutpost(grid);
    }

    private bool HasBlockingCollider(Vector2Int grid, LayerMask layerMask)
    {
        return HasBlockingCollider(grid, layerMask, null);
    }

    private bool HasBlockingCollider(Vector2Int grid, LayerMask layerMask, Transform ignoredTransform)
    {
        Vector3 center = GridToWorldCenter(grid);
        center.y = GetLandSurfaceY() + 0.5f;

        Vector3 halfExtents = new Vector3(cellSize * 0.5f * obstacleCheckFill, 0.5f, cellSize * 0.5f * obstacleCheckFill);
        Collider[] cols = Physics.OverlapBox(center, halfExtents, Quaternion.identity, layerMask);

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null)
                continue;

            if (landTransform != null && (c.transform == landTransform || c.transform.IsChildOf(landTransform)))
                continue;

            if (ignoredTransform != null && (c.transform == ignoredTransform || c.transform.IsChildOf(ignoredTransform)))
                continue;

            return true;
        }

        return false;
    }

    private bool TryGetMultiGridOccupantAtGrid(Vector2Int grid, out MultiGridOccupant occupant, Transform ignoredTransform = null)
    {
        occupant = null;

        IReadOnlyList<MultiGridOccupant> occupants = multiGridOccupantRegistry != null
            ? multiGridOccupantRegistry.Occupants
            : FindObjectsByType<MultiGridOccupant>(FindObjectsSortMode.None);

        for (int i = 0; i < occupants.Count; i++)
        {
            MultiGridOccupant candidate = occupants[i];
            if (candidate == null)
                continue;

            Transform candidateTransform = candidate.transform;
            if (ignoredTransform != null
                && (candidateTransform == ignoredTransform || candidateTransform.IsChildOf(ignoredTransform)))
            {
                continue;
            }

            if (!candidate.OccupiesCell(grid))
                continue;

            occupant = candidate;
            return true;
        }

        return false;
    }

    private bool TryGetCastleByMultiGrid(Vector2Int grid, out CastleUnit castle, Transform ignoredTransform = null)
    {
        castle = null;

        IReadOnlyList<CastleUnit> castles = castleRegistry != null
            ? castleRegistry.Castles
            : FindObjectsByType<CastleUnit>(FindObjectsSortMode.None);

        for (int i = 0; i < castles.Count; i++)
        {
            CastleUnit candidate = castles[i];
            if (candidate == null)
                continue;

            Transform candidateTransform = candidate.transform;
            if (ignoredTransform != null
                && (candidateTransform == ignoredTransform || candidateTransform.IsChildOf(ignoredTransform)))
            {
                continue;
            }

            MultiGridOccupant occupant = candidate.GetComponent<MultiGridOccupant>();
            if (occupant != null)
            {
                if (!occupant.OccupiesCell(grid))
                    continue;

                castle = candidate;
                return true;
            }

            if (candidate.GetCurrentGrid() != grid)
                continue;

            castle = candidate;
            return true;
        }

        return false;
    }

    private static bool IsAdjacentToSingleCell(Vector2Int fromGrid, Vector2Int targetGrid)
    {
        int dx = Mathf.Abs(fromGrid.x - targetGrid.x);
        int dy = Mathf.Abs(fromGrid.y - targetGrid.y);
        return dx <= 1 && dy <= 1 && (dx != 0 || dy != 0);
    }

    // Land의 y 높이에 맞춰 marker/player를 올려놓기 위한 헬퍼
    public float GetLandSurfaceY()
    {
        if (landTransform == null)
        {
            if (UsesTilemapGrid && tilemapGridLayout != null)
                return tilemapGridLayout.transform.position.y + 0.01f;

            return 0.01f;
        }

        // BoxCollider가 있으면 bounds 상단 사용
        if (landTransform.TryGetComponent<Collider>(out var col))
            return (col.bounds.max.y+0.01f);

        return landTransform.position.y + 0.01f;
    }

    private void OnDrawGizmos()
    {
        if (cellSize <= 0f)
            return;

        float y = GetLandSurfaceY();

        for (int q = debugQMin; q <= debugQMax; q++)
        {
            for (int r = debugRMin; r <= debugRMax; r++)
            {
                Vector3 center = GridToWorldCenter(new Vector2Int(q, r));
                center.y = y;

                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(center, debugCenterSphereRadius);

                DrawSquareOutlineGizmo(center);
            }
        }
    }

    private void DrawSquareOutlineGizmo(Vector3 center)
    {
        Gizmos.color = Color.cyan;
        float half = cellSize * 0.5f;
        Vector3 a = new Vector3(center.x - half, center.y, center.z - half);
        Vector3 b = new Vector3(center.x + half, center.y, center.z - half);
        Vector3 c = new Vector3(center.x + half, center.y, center.z + half);
        Vector3 d = new Vector3(center.x - half, center.y, center.z + half);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }

    private void ResolveTilemapReferences()
    {
        if (tilemapGridLayout == null && referenceTilemap != null)
            tilemapGridLayout = referenceTilemap.layoutGrid;

        if (referenceTilemap == null && tilemapGridLayout != null)
            referenceTilemap = tilemapGridLayout.GetComponentInChildren<Tilemap>();
    }

    private void SyncCellSizeFromTilemap()
    {
        if (!UsesTilemapGrid)
            return;

        Vector3 tilemapCellSize = tilemapGridLayout.cellSize;
        float xSize = Mathf.Abs(tilemapCellSize.x);
        float secondAxisSize = tilemapUsesXZPlane
            ? Mathf.Abs(tilemapCellSize.y > 0f ? tilemapCellSize.y : tilemapCellSize.z)
            : Mathf.Abs(tilemapCellSize.y);

        float resolvedCellSize = Mathf.Max(xSize, secondAxisSize);
        if (resolvedCellSize > 0f)
            cellSize = resolvedCellSize;
    }
}

