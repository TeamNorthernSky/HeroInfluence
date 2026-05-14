using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class LevelEditorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelData levelData;
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Camera inputCamera;

    [Header("Brush")]
    [SerializeField] private LevelEditorBrushType brushType = LevelEditorBrushType.Obstacle;
    [SerializeField] private ItemPlacementPreset itemPreset;
    [FormerlySerializedAs("minePreset")]
    [SerializeField] private OutpostPlacementPreset outpostPreset;
    [SerializeField] private EventPlacementPreset eventPreset;

    [Header("Behaviour")]
    [SerializeField] private bool allowRuntimeEditing;
    [SerializeField] private bool applyLevelAfterEdit = true;
    [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;

    [Header("Debug View")]
    [SerializeField] private bool drawEditorGizmos = true;
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 0f, 0.75f);
    [SerializeField] private Color obstacleColor = new Color(1f, 0.3f, 0.3f, 0.75f);
    [SerializeField] private Color itemColor = new Color(0.2f, 0.9f, 0.3f, 0.75f);
    [FormerlySerializedAs("mineColor")]
    [SerializeField] private Color outpostColor = new Color(0.2f, 0.8f, 1f, 0.75f);
    [SerializeField] private Color eventColor = new Color(0.75f, 0.45f, 1f, 0.75f);
    [SerializeField] private Color stayEnemyColor = new Color(1f, 0.15f, 0.15f, 0.75f);
    [SerializeField] private Color stayEnemyEncounterZoneColor = new Color(1f, 0.15f, 0.15f, 0.25f);
    [SerializeField] private Color castleColor = new Color(0.95f, 0.85f, 0.25f, 0.75f);
    [SerializeField] private Color villainUnionColor = new Color(0.95f, 0.25f, 0.55f, 0.75f);

    private Vector2Int? hoveredGrid;

    public LevelData LevelData => levelData;
    public LevelLoader LevelLoader => levelLoader;
    public GridManager GridManager => gridManager;
    public Camera InputCamera => inputCamera;
    public LevelEditorBrushType BrushType => brushType;
    public ItemPlacementPreset ItemPreset => itemPreset;
    public OutpostPlacementPreset OutpostPreset => outpostPreset;
    public EventPlacementPreset EventPreset => eventPreset;
    public bool ApplyLevelAfterEdit => applyLevelAfterEdit;
    public LayerMask GroundMask => groundMask;

    private void Update()
    {
        if (!Application.isPlaying || !allowRuntimeEditing)
            return;

        if (levelData == null || gridManager == null)
            return;

        UpdateHoveredGrid();

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
            ApplyBrushAtHoveredGrid();
        else if (Input.GetMouseButtonDown(1))
            EraseAtHoveredGrid();
    }

    [ContextMenu("Apply Current Level")]
    public void ApplyCurrentLevel()
    {
        levelLoader?.LoadLevel();
    }

    private void UpdateHoveredGrid()
    {
        hoveredGrid = TryGetMouseGrid(out Vector2Int grid) ? grid : null;
    }

    private bool TryGetMouseGrid(out Vector2Int grid)
    {
        grid = Vector2Int.zero;

        Camera targetCamera = inputCamera != null ? inputCamera : Camera.main;
        if (targetCamera == null)
            return false;

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            return false;

        grid = gridManager.WorldToGrid(hit.point);
        return levelData.IsInsideGrid(grid);
    }

    private void ApplyBrushAtHoveredGrid()
    {
        if (!hoveredGrid.HasValue)
            return;

        Vector2Int grid = hoveredGrid.Value;

        switch (brushType)
        {
            case LevelEditorBrushType.Obstacle:
                levelData.SetObstacle(grid);
                break;
            case LevelEditorBrushType.Item:
                if (itemPreset == null)
                    return;

                levelData.SetItem(grid, itemPreset.ResourceType, Mathf.Max(1, itemPreset.Amount));
                break;
            case LevelEditorBrushType.Outpost:
                if (outpostPreset == null)
                    return;

                levelData.SetOutpost(
                    grid,
                    outpostPreset.OutpostType,
                    Mathf.Max(1, outpostPreset.ResourcePerTurn),
                    outpostPreset.InitialState);
                break;
            case LevelEditorBrushType.Event:
                if (eventPreset == null)
                    return;

                levelData.SetEvent(grid, eventPreset.EventKey);
                break;
            case LevelEditorBrushType.StayEnemy:
                levelData.SetStayEnemy(grid);
                break;
            case LevelEditorBrushType.Castle:
                levelData.SetCastle(grid);
                break;
            case LevelEditorBrushType.VillainUnion:
                levelData.SetVillainUnion(grid);
                break;
            case LevelEditorBrushType.Erase:
                levelData.EraseAt(grid);
                break;
        }

        MarkLevelDataDirty();

        if (applyLevelAfterEdit)
            levelLoader?.LoadLevel();
    }

    private void EraseAtHoveredGrid()
    {
        if (!hoveredGrid.HasValue)
            return;

        levelData.EraseAt(hoveredGrid.Value);
        MarkLevelDataDirty();

        if (applyLevelAfterEdit)
            levelLoader?.LoadLevel();
    }

    private void OnDrawGizmos()
    {
        if (!drawEditorGizmos || levelData == null || gridManager == null)
            return;

        DrawPlacedCells();
        DrawHoveredCell();
    }

    private void DrawPlacedCells()
    {
        float y = gridManager.GetLandSurfaceY() + 0.05f;
        float size = Mathf.Max(0.05f, gridManager.CellSize * 0.9f);

        for (int i = 0; i < levelData.ObstacleCells.Count; i++)
            DrawCell(levelData.ObstacleCells[i], obstacleColor, y, size);

        for (int i = 0; i < levelData.ItemPlacements.Count; i++)
            DrawCell(levelData.ItemPlacements[i].GridPosition, itemColor, y, size);

        for (int i = 0; i < levelData.OutpostPlacements.Count; i++)
            DrawCell(levelData.OutpostPlacements[i].GridPosition, outpostColor, y, size);

        for (int i = 0; i < levelData.EventPlacements.Count; i++)
            DrawCell(levelData.EventPlacements[i].GridPosition, eventColor, y, size);

        for (int i = 0; i < levelData.StayEnemyCells.Count; i++)
            DrawStayEnemyCells(levelData.StayEnemyCells[i], y, size);

        if (levelData.CastlePlacement.HasPlacement)
            DrawCell(levelData.CastlePlacement.GridPosition, castleColor, y, size);

        if (levelData.VillainUnionPlacement.HasPlacement)
            DrawCell(levelData.VillainUnionPlacement.GridPosition, villainUnionColor, y, size);
    }

    private void DrawHoveredCell()
    {
        if (!hoveredGrid.HasValue)
            return;

        float y = gridManager.GetLandSurfaceY() + 0.1f;
        float size = Mathf.Max(0.05f, gridManager.CellSize);
        DrawCell(hoveredGrid.Value, hoverColor, y, size);
    }

    private void DrawCell(Vector2Int grid, Color color, float y, float size)
    {
        Vector3 center = gridManager.GridToWorldCenter(grid);
        center.y = y;

        Gizmos.color = color;
        Gizmos.DrawWireCube(center, new Vector3(size, 0.02f, size));
    }

    private void DrawStayEnemyCells(Vector2Int grid, float y, float size)
    {
        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                Vector2Int zoneGrid = new Vector2Int(grid.x + offsetX, grid.y + offsetY);
                if (!levelData.IsInsideGrid(zoneGrid))
                    continue;

                DrawCell(zoneGrid, stayEnemyEncounterZoneColor, y, size);
            }
        }

        DrawCell(grid, stayEnemyColor, y, size);
    }

    private void MarkLevelDataDirty()
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(levelData);
#endif
    }
}
