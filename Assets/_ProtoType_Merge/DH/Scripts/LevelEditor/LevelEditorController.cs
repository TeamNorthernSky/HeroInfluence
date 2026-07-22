using System.Collections.Generic;
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
    [FormerlySerializedAs("enemyGroupIndex")]
    [SerializeField] private string enemyGroupKey = "FEP001";
    [SerializeField] private EnemyBehaviorType enemyBehaviorType = EnemyBehaviorType.Mobile;
    [SerializeField] private LevelTileRegistry tileRegistry;
    [SerializeField] private string selectedTileKey;
    [SerializeField] private string selectedHeroUnionPrefabKey;
    [SerializeField] private string selectedDecorativeBuildingKey;
    [SerializeField] private string selectedMainEventPrefabKey;
    [SerializeField] private string selectedSubEventPrefabKey;
    [SerializeField] private string selectedGatePrefabKey = "horizon";
    [SerializeField] private string selectedGateId = "gate_001";
    [SerializeField] private string selectedGateFirstZoneId = "zone_001";
    [SerializeField] private string selectedGateSecondZoneId = "zone_002";
    [SerializeField, Min(1)] private int selectedGateOpenDurationTurns = 3;
    [SerializeField] private string selectedEnemySpawnZoneId = "zone_002";
    [SerializeField] private string selectedEnemySpawnEnemyGroupKey;

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
    [FormerlySerializedAs("stayEnemyColor")]
    [SerializeField] private Color enemyGroupColor = new Color(1f, 0.15f, 0.15f, 0.75f);
    [FormerlySerializedAs("stayEnemyEncounterZoneColor")]
    [SerializeField] private Color enemyGroupEncounterZoneColor = new Color(1f, 0.15f, 0.15f, 0.25f);
    [SerializeField] private Color heroUnionColor = new Color(0.95f, 0.85f, 0.25f, 0.75f);
    [SerializeField] private Color villainUnionColor = new Color(0.95f, 0.25f, 0.55f, 0.75f);
    [SerializeField] private Color decorativeBuildingColor = new Color(0.95f, 0.65f, 0.25f, 0.75f);
    [SerializeField] private Color mainEventColor = new Color(0.2f, 0.9f, 1f, 0.75f);
    [SerializeField] private Color mainEventInteractionColor = new Color(0.2f, 0.9f, 1f, 0.25f);
    [SerializeField] private Color subEventColor = new Color(0.35f, 0.95f, 0.95f, 0.75f);
    [SerializeField] private Color gateBlockerColor = new Color(0.45f, 0.15f, 1f, 0.75f);
    [SerializeField] private Color enemySpawnPointColor = new Color(1f, 0.55f, 0.05f, 0.75f);

    private Vector2Int? hoveredGrid;

    public LevelData LevelData => levelData;
    public LevelLoader LevelLoader => levelLoader;
    public GridManager GridManager => gridManager;
    public Camera InputCamera => inputCamera;
    public LevelEditorBrushType BrushType => brushType;
    public ItemPlacementPreset ItemPreset => itemPreset;
    public OutpostPlacementPreset OutpostPreset => outpostPreset;
    public EventPlacementPreset EventPreset => eventPreset;
    public string EnemyGroupKey => string.IsNullOrWhiteSpace(enemyGroupKey) ? string.Empty : enemyGroupKey.Trim();
    public EnemyBehaviorType EnemyBehaviorType => enemyBehaviorType;
    public LevelTileRegistry TileRegistry => tileRegistry;
    public string SelectedTileKey => selectedTileKey;
    public string SelectedHeroUnionPrefabKey => string.IsNullOrWhiteSpace(selectedHeroUnionPrefabKey) ? string.Empty : selectedHeroUnionPrefabKey.Trim();
    public string SelectedDecorativeBuildingKey => selectedDecorativeBuildingKey;
    public string SelectedMainEventPrefabKey => string.IsNullOrWhiteSpace(selectedMainEventPrefabKey) ? string.Empty : selectedMainEventPrefabKey.Trim();
    public string SelectedSubEventPrefabKey => string.IsNullOrWhiteSpace(selectedSubEventPrefabKey) ? string.Empty : selectedSubEventPrefabKey.Trim();
    public string SelectedGatePrefabKey => string.IsNullOrWhiteSpace(selectedGatePrefabKey) ? string.Empty : selectedGatePrefabKey.Trim();
    public string SelectedGateId => selectedGateId;
    public string SelectedGateFirstZoneId => selectedGateFirstZoneId;
    public string SelectedGateSecondZoneId => selectedGateSecondZoneId;
    public int SelectedGateOpenDurationTurns => Mathf.Max(1, selectedGateOpenDurationTurns);
    public string SelectedEnemySpawnZoneId => selectedEnemySpawnZoneId;
    public string SelectedEnemySpawnEnemyGroupKey => string.IsNullOrWhiteSpace(selectedEnemySpawnEnemyGroupKey) ? string.Empty : selectedEnemySpawnEnemyGroupKey.Trim();
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

                levelData.SetEvent(
                    grid,
                    eventPreset.EventType,
                    eventPreset.RequireAmount,
                    eventPreset.EffectAmount);
                break;
            case LevelEditorBrushType.EnemyGroup:
                levelData.SetEnemyPlacement(grid, EnemyGroupKey, enemyBehaviorType);
                break;
            case LevelEditorBrushType.GroundTile:
                if (string.IsNullOrWhiteSpace(selectedTileKey))
                    return;

                levelData.SetGroundTile(grid, selectedTileKey);
                break;
            case LevelEditorBrushType.GroundTileErase:
                levelData.EraseGroundTileAt(grid);
                break;
            case LevelEditorBrushType.DecorativeBuilding:
                if (string.IsNullOrWhiteSpace(selectedDecorativeBuildingKey))
                    return;

                levelData.SetDecorativeBuilding(grid, selectedDecorativeBuildingKey);
                break;
            case LevelEditorBrushType.MainEvent:
                if (string.IsNullOrWhiteSpace(selectedMainEventPrefabKey))
                    return;

                levelData.SetMainEvent(grid, selectedMainEventPrefabKey);
                break;
            case LevelEditorBrushType.SubEvent:
                if (string.IsNullOrWhiteSpace(selectedSubEventPrefabKey))
                    return;

                levelData.SetSubEvent(grid, selectedSubEventPrefabKey);
                break;
            case LevelEditorBrushType.GateBlocker:
                ApplyGateBrush(grid);
                break;
            case LevelEditorBrushType.EnemySpawnPoint:
                levelData.SetEnemySpawnPoint(grid, selectedEnemySpawnZoneId, SelectedEnemySpawnEnemyGroupKey);
                break;
            case LevelEditorBrushType.HeroUnion:
                levelData.SetHeroUnion(grid, SelectedHeroUnionPrefabKey);
                break;
            case LevelEditorBrushType.VillainUnion:
                levelData.SetVillainUnion(grid);
                break;
            case LevelEditorBrushType.Erase:
                levelData.EraseNonGroundTileAt(grid);
                break;
            case LevelEditorBrushType.TileSelection:
                return;
        }

        MarkLevelDataDirty();

        if (applyLevelAfterEdit)
            levelLoader?.LoadLevel();
    }

    private void EraseAtHoveredGrid()
    {
        if (!hoveredGrid.HasValue)
            return;

        levelData.EraseNonGroundTileAt(hoveredGrid.Value);
        MarkLevelDataDirty();

        if (applyLevelAfterEdit)
            levelLoader?.LoadLevel();
    }

    private void ApplyGateBrush(Vector2Int grid)
    {
        LevelPrefabRegistry prefabRegistry = levelLoader != null ? levelLoader.PrefabRegistry : null;
        if (prefabRegistry == null ||
            string.IsNullOrWhiteSpace(SelectedGatePrefabKey) ||
            !prefabRegistry.TryGetGatePrefab(SelectedGatePrefabKey, out GateFootprint gatePrefab) ||
            gatePrefab == null)
        {
            levelData.AddGateBlockerCell(
                selectedGateId,
                selectedGateFirstZoneId,
                selectedGateSecondZoneId,
                grid,
                SelectedGateOpenDurationTurns);
            return;
        }

        List<Vector2Int> blockerCells = new List<Vector2Int>();
        gatePrefab.CollectOccupiedCells(grid, blockerCells);
        levelData.SetGatePlacement(
            selectedGateId,
            selectedGateFirstZoneId,
            selectedGateSecondZoneId,
            grid,
            SelectedGatePrefabKey,
            blockerCells,
            SelectedGateOpenDurationTurns);
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

        for (int i = 0; i < levelData.EnemyPlacements.Count; i++)
            DrawEnemyGroupCells(levelData.EnemyPlacements[i].GridPosition, y, size);

        for (int i = 0; i < levelData.DecorativeBuildingPlacements.Count; i++)
            DrawCell(levelData.DecorativeBuildingPlacements[i].GridPosition, decorativeBuildingColor, y, size);

        for (int i = 0; i < levelData.MainEventPlacements.Count; i++)
            DrawMainEventCells(levelData.MainEventPlacements[i].GridPosition, y, size);

        for (int i = 0; i < levelData.SubEventPlacements.Count; i++)
            DrawCell(levelData.SubEventPlacements[i].GridPosition, subEventColor, y, size);

        for (int i = 0; i < levelData.GatePlacements.Count; i++)
        {
            IReadOnlyList<Vector2Int> blockerCells = levelData.GatePlacements[i].BlockerCells;
            for (int cellIndex = 0; cellIndex < blockerCells.Count; cellIndex++)
                DrawCell(blockerCells[cellIndex], gateBlockerColor, y, size);
        }

        for (int i = 0; i < levelData.EnemySpawnPointPlacements.Count; i++)
            DrawCell(levelData.EnemySpawnPointPlacements[i].GridPosition, enemySpawnPointColor, y, size);

        if (levelData.HeroUnionPlacement.HasPlacement)
            DrawCell(levelData.HeroUnionPlacement.GridPosition, heroUnionColor, y, size);

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

    private void DrawEnemyGroupCells(Vector2Int grid, float y, float size)
    {
        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                Vector2Int zoneGrid = new Vector2Int(grid.x + offsetX, grid.y + offsetY);
                if (!levelData.IsInsideGrid(zoneGrid))
                    continue;

                DrawCell(zoneGrid, enemyGroupEncounterZoneColor, y, size);
            }
        }

        DrawCell(grid, enemyGroupColor, y, size);
    }

    private void DrawMainEventCells(Vector2Int grid, float y, float size)
    {
        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                Vector2Int interactionGrid = new Vector2Int(grid.x + offsetX, grid.y + offsetY);
                if (!levelData.IsInsideGrid(interactionGrid))
                    continue;

                DrawCell(interactionGrid, mainEventInteractionColor, y, size);
            }
        }

        DrawCell(grid, mainEventColor, y, size);
    }

    private void MarkLevelDataDirty()
    {
#if UNITY_EDITOR
        EditorUtility.SetDirty(levelData);
#endif
    }
}
