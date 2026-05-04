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
    [SerializeField] private string partyId = "party_001";

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
    [SerializeField] private Color spawnColor = new Color(1f, 0.6f, 0.2f, 0.75f);

    private Vector2Int? hoveredGrid;

    private void Update()
    {
        if (!allowRuntimeEditing && Application.isPlaying)
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
                    outpostPreset.ResourceType,
                    Mathf.Max(1, outpostPreset.ResourcePerTurn),
                    outpostPreset.InitialState);
                break;
            case LevelEditorBrushType.PartySpawn:
                levelData.SetPartySpawn(grid, partyId);
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

        for (int i = 0; i < levelData.PartySpawns.Count; i++)
            DrawCell(levelData.PartySpawns[i].GridPosition, spawnColor, y, size);
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

    private void MarkLevelDataDirty()
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(levelData);
#endif
    }
}
