using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class LevelEditorWindow : EditorWindow
{
    private const string MenuPath = "DH Work/Level Editor";

    private static readonly LevelEditorBrushType[] BrushOrder =
    {
        LevelEditorBrushType.Obstacle,
        LevelEditorBrushType.Item,
        LevelEditorBrushType.Outpost,
        LevelEditorBrushType.Event,
        LevelEditorBrushType.Castle,
        LevelEditorBrushType.VillainUnion,
        LevelEditorBrushType.StayEnemy,
        LevelEditorBrushType.Erase
    };

    private static readonly string[] BrushLabels =
    {
        "Obstacle",
        "Item",
        "Outpost",
        "Event",
        "Castle",
        "Villain",
        "StayEnemy",
        "Erase"
    };

    private LevelEditorController controller;
    private Vector2 scrollPosition;
    private bool sceneEditingEnabled = true;
    private string sceneStatus;

    [MenuItem("Window/DH Work/Level Editor")]
    public static void Open()
    {
        LevelEditorWindow window = GetWindow<LevelEditorWindow>("Level Editor");
        window.minSize = new Vector2(340f, 460f);
        window.Show();
    }

    private void OnEnable()
    {
        TryAutoAssignController();
        SceneView.duringSceneGui -= HandleSceneGUI;
        SceneView.duringSceneGui += HandleSceneGUI;
        Undo.undoRedoPerformed -= HandleUndoRedo;
        Undo.undoRedoPerformed += HandleUndoRedo;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= HandleSceneGUI;
        Undo.undoRedoPerformed -= HandleUndoRedo;
    }

    private void OnHierarchyChange()
    {
        if (controller == null)
            TryAutoAssignController();

        Repaint();
        SceneView.RepaintAll();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null)
        {
            LevelEditorController selectedController = Selection.activeGameObject.GetComponent<LevelEditorController>();
            if (selectedController != null)
                controller = selectedController;
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        DrawToolbar();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(4f);
        controller = (LevelEditorController)EditorGUILayout.ObjectField(
            "Controller",
            controller,
            typeof(LevelEditorController),
            true);

        sceneEditingEnabled = EditorGUILayout.Toggle("Scene Editing", sceneEditingEnabled);

        if (controller == null)
        {
            EditorGUILayout.HelpBox("Connect a LevelEditorController in the scene to edit level objects.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawControllerInspector();
        DrawQuickActions();
        DrawWarnings();

        if (!string.IsNullOrWhiteSpace(sceneStatus))
            EditorGUILayout.HelpBox(sceneStatus, MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label(MenuPath, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Find Scene Controller", EditorStyles.toolbarButton))
                TryAutoAssignController();

            if (GUILayout.Button("Ping", EditorStyles.toolbarButton) && controller != null)
                EditorGUIUtility.PingObject(controller.gameObject);
        }
    }

    private void DrawControllerInspector()
    {
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.Update();

        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedController.FindProperty("levelData"));
        EditorGUILayout.PropertyField(serializedController.FindProperty("levelLoader"));
        EditorGUILayout.PropertyField(serializedController.FindProperty("gridManager"));
        EditorGUILayout.PropertyField(serializedController.FindProperty("inputCamera"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
        DrawBrushSelector(serializedController.FindProperty("brushType"));

        SerializedProperty brushTypeProperty = serializedController.FindProperty("brushType");
        LevelEditorBrushType brushType = (LevelEditorBrushType)brushTypeProperty.enumValueIndex;

        if (brushType == LevelEditorBrushType.Item)
            EditorGUILayout.PropertyField(serializedController.FindProperty("itemPreset"));

        if (brushType == LevelEditorBrushType.Outpost)
            EditorGUILayout.PropertyField(serializedController.FindProperty("outpostPreset"));

        if (brushType == LevelEditorBrushType.Event)
            EditorGUILayout.PropertyField(serializedController.FindProperty("eventPreset"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Behaviour", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedController.FindProperty("allowRuntimeEditing"));
        EditorGUILayout.PropertyField(serializedController.FindProperty("applyLevelAfterEdit"));
        EditorGUILayout.PropertyField(serializedController.FindProperty("groundMask"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Debug View", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedController.FindProperty("drawEditorGizmos"));

        if (serializedController.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
        }
    }

    private void DrawBrushSelector(SerializedProperty brushTypeProperty)
    {
        LevelEditorBrushType brushType = (LevelEditorBrushType)brushTypeProperty.enumValueIndex;
        int selectedIndex = GetBrushOrderIndex(brushType);
        int nextIndex = GUILayout.SelectionGrid(selectedIndex, BrushLabels, 3);

        if (nextIndex < 0 || nextIndex >= BrushOrder.Length)
            return;

        LevelEditorBrushType nextBrush = BrushOrder[nextIndex];
        if (nextBrush == brushType)
            return;

        brushTypeProperty.enumValueIndex = (int)nextBrush;
        sceneStatus = $"Brush : {nextBrush}";
    }

    private void DrawQuickActions()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Level"))
            {
                controller.ApplyCurrentLevel();
                EditorUtility.SetDirty(controller);
            }

            if (GUILayout.Button("Select Controller"))
                Selection.activeObject = controller.gameObject;
        }

        LevelLoader levelLoader = controller.LevelLoader;
        if (levelLoader == null)
            return;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ping Loader"))
                EditorGUIUtility.PingObject(levelLoader.gameObject);

            if (GUILayout.Button("Select Loader"))
                Selection.activeObject = levelLoader.gameObject;
        }
    }

    private void DrawWarnings()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Checks", EditorStyles.boldLabel);

        if (!TryGetContext(out LevelEditorContext context, false))
        {
            if (controller.LevelData == null)
                EditorGUILayout.HelpBox("LevelData is not connected.", MessageType.Warning);

            if (controller.LevelLoader == null)
                EditorGUILayout.HelpBox("LevelLoader is not connected.", MessageType.Warning);

            if (controller.GridManager == null)
                EditorGUILayout.HelpBox("GridManager is not connected.", MessageType.Warning);

            return;
        }

        if (context.InputCamera == null)
            EditorGUILayout.HelpBox("Input Camera is empty. Scene View editing still uses the Scene camera.", MessageType.Info);

        if (context.LevelLoader == null)
            EditorGUILayout.HelpBox("LevelLoader is not connected.", MessageType.Warning);

        if (context.BrushType == LevelEditorBrushType.Item && context.ItemPreset == null)
            EditorGUILayout.HelpBox("Item brush needs an ItemPlacementPreset.", MessageType.Warning);

        if (context.BrushType == LevelEditorBrushType.Outpost && context.OutpostPreset == null)
            EditorGUILayout.HelpBox("Outpost brush needs an OutpostPlacementPreset.", MessageType.Warning);

        if (context.BrushType == LevelEditorBrushType.Event && context.EventPreset == null)
            EditorGUILayout.HelpBox("Event brush needs an EventPlacementPreset.", MessageType.Warning);

        if (context.PrefabRegistry == null)
            EditorGUILayout.HelpBox("LevelLoader needs a LevelPrefabRegistry.", MessageType.Warning);
        else if (!HasBrushPrefab(context, out string prefabWarning))
            EditorGUILayout.HelpBox(prefabWarning, MessageType.Warning);

        EditorGUILayout.HelpBox(
            $"Grid Range : X {context.LevelData.GridMin.x} ~ {context.LevelData.GridMax.x}, Y {context.LevelData.GridMin.y} ~ {context.LevelData.GridMax.y}",
            MessageType.None);
    }

    private void HandleSceneGUI(SceneView sceneView)
    {
        if (!sceneEditingEnabled)
            return;

        if (controller == null)
            TryAutoAssignController();

        if (!TryGetContext(out LevelEditorContext context, false))
            return;

        Event currentEvent = Event.current;
        DrawGrid(context);
        DrawExistingPlacements(context);

        if (currentEvent == null)
            return;

        if (!currentEvent.alt && currentEvent.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (currentEvent.alt)
            return;

        if (!TryGetMouseGrid(context, currentEvent.mousePosition, out Vector2Int hoveredGrid))
            return;

        DrawHoverPreview(context, hoveredGrid);

        if (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDrag)
            sceneView.Repaint();

        if (currentEvent.type != EventType.MouseDown)
            return;

        if (currentEvent.button == 0)
        {
            if (context.BrushType == LevelEditorBrushType.Erase)
                EraseAtGrid(context, hoveredGrid);
            else
                PlaceAtGrid(context, hoveredGrid);

            currentEvent.Use();
        }
        else if (currentEvent.button == 1)
        {
            EraseAtGrid(context, hoveredGrid);
            currentEvent.Use();
        }
    }

    private void DrawGrid(LevelEditorContext context)
    {
        GridManager gridManager = context.GridManager;
        LevelData levelData = context.LevelData;
        float cellSize = gridManager.CellSize;
        float y = gridManager.GetLandSurfaceY() + 0.18f;

        Vector3 minCenter = gridManager.GridToWorldCenter(levelData.GridMin);
        Vector3 maxCenter = gridManager.GridToWorldCenter(levelData.GridMax);
        float minX = minCenter.x - cellSize * 0.5f;
        float minZ = minCenter.z - cellSize * 0.5f;
        float maxX = maxCenter.x + cellSize * 0.5f;
        float maxZ = maxCenter.z + cellSize * 0.5f;

        CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;
        Handles.color = new Color(1f, 1f, 1f, 0.45f);

        for (int x = 0; x <= levelData.GridSize.x; x++)
        {
            float lineX = minX + x * cellSize;
            Handles.DrawAAPolyLine(
                2f,
                new Vector3(lineX, y, minZ),
                new Vector3(lineX, y, maxZ));
        }

        for (int z = 0; z <= levelData.GridSize.y; z++)
        {
            float lineZ = minZ + z * cellSize;
            Handles.DrawAAPolyLine(
                2f,
                new Vector3(minX, y, lineZ),
                new Vector3(maxX, y, lineZ));
        }

        Handles.zTest = previousZTest;
    }

    private void DrawExistingPlacements(LevelEditorContext context)
    {
        LevelData levelData = context.LevelData;

        for (int i = 0; i < levelData.ObstacleCells.Count; i++)
            DrawFootprint(context, BuildFootprint(null, levelData.ObstacleCells[i]), new Color(1f, 0.25f, 0.25f, 0.10f), new Color(1f, 0.25f, 0.25f, 0.65f));

        for (int i = 0; i < levelData.ItemPlacements.Count; i++)
        {
            ItemPlacementData placement = levelData.ItemPlacements[i];
            DrawFootprint(context, BuildFootprint(GetItemPrefab(context, placement.ResourceType), placement.GridPosition), new Color(0.1f, 0.9f, 0.25f, 0.10f), new Color(0.1f, 0.9f, 0.25f, 0.65f));
        }

        for (int i = 0; i < levelData.OutpostPlacements.Count; i++)
        {
            OutpostPlacementData placement = levelData.OutpostPlacements[i];
            DrawFootprint(context, BuildFootprint(GetOutpostPrefab(context, placement.OutpostType), placement.GridPosition), new Color(0.15f, 0.75f, 1f, 0.10f), new Color(0.15f, 0.75f, 1f, 0.65f));
        }

        for (int i = 0; i < levelData.EventPlacements.Count; i++)
        {
            EventPlacementData placement = levelData.EventPlacements[i];
            DrawFootprint(context, BuildFootprint(GetEventPrefab(context, placement.EventKey), placement.GridPosition), new Color(0.75f, 0.35f, 1f, 0.10f), new Color(0.75f, 0.35f, 1f, 0.65f));
        }

        for (int i = 0; i < levelData.StayEnemyCells.Count; i++)
        {
            Vector2Int stayEnemyGrid = levelData.StayEnemyCells[i];
            DrawStayEnemyEncounterZone(context, stayEnemyGrid, new Color(1f, 0.1f, 0.1f, 0.06f), new Color(1f, 0.1f, 0.1f, 0.28f));
            DrawFootprint(context, BuildFootprint(GetStayEnemyPrefab(context), stayEnemyGrid), new Color(1f, 0.1f, 0.1f, 0.14f), new Color(1f, 0.1f, 0.1f, 0.75f));
        }

        if (levelData.CastlePlacement.HasPlacement)
            DrawFootprint(context, BuildFootprint(GetCastlePrefab(context), levelData.CastlePlacement.GridPosition), new Color(1f, 0.85f, 0.1f, 0.12f), new Color(1f, 0.85f, 0.1f, 0.75f));

        if (levelData.VillainUnionPlacement.HasPlacement)
            DrawFootprint(context, BuildFootprint(GetVillainUnionPrefab(context), levelData.VillainUnionPlacement.GridPosition), new Color(1f, 0.2f, 0.55f, 0.12f), new Color(1f, 0.2f, 0.55f, 0.75f));
    }

    private void DrawHoverPreview(LevelEditorContext context, Vector2Int anchor)
    {
        if (context.BrushType == LevelEditorBrushType.Erase)
        {
            if (TryFindPlacementAtGrid(context, anchor, out _, out List<Vector2Int> deleteFootprint, out string deleteLabel))
            {
                DrawFootprint(context, deleteFootprint, new Color(1f, 0.45f, 0f, 0.20f), new Color(1f, 0.45f, 0f, 1f));
                DrawSceneLabel(context, anchor, $"Erase {deleteLabel}");
            }
            else
            {
                DrawFootprint(context, BuildFootprint(null, anchor), new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.65f));
            }

            return;
        }

        if (!TryBuildBrushFootprint(context, anchor, out List<Vector2Int> footprint, out string reason))
        {
            DrawFootprint(context, BuildFootprint(null, anchor), new Color(1f, 0f, 0f, 0.18f), new Color(1f, 0f, 0f, 1f));
            DrawSceneLabel(context, anchor, reason);
            return;
        }

        bool canPlace = CanPlaceFootprint(context, footprint, out reason);
        Color fill = canPlace ? new Color(0.2f, 1f, 0.3f, 0.18f) : new Color(1f, 0f, 0f, 0.20f);
        Color outline = canPlace ? new Color(0.2f, 1f, 0.3f, 1f) : new Color(1f, 0f, 0f, 1f);

        if (context.BrushType == LevelEditorBrushType.StayEnemy)
        {
            DrawStayEnemyEncounterZone(
                context,
                anchor,
                canPlace ? new Color(1f, 0.1f, 0.1f, 0.08f) : new Color(1f, 0f, 0f, 0.12f),
                canPlace ? new Color(1f, 0.1f, 0.1f, 0.45f) : new Color(1f, 0f, 0f, 0.75f));
        }

        DrawFootprint(context, footprint, fill, outline);

        if (!canPlace)
            DrawSceneLabel(context, anchor, reason);
    }

    private void PlaceAtGrid(LevelEditorContext context, Vector2Int anchor)
    {
        if (!TryBuildBrushFootprint(context, anchor, out List<Vector2Int> footprint, out string reason)
            || !CanPlaceFootprint(context, footprint, out reason))
        {
            sceneStatus = reason;
            Repaint();
            return;
        }

        Undo.RecordObject(context.LevelData, $"Place {context.BrushType}");

        switch (context.BrushType)
        {
            case LevelEditorBrushType.Obstacle:
                context.LevelData.SetObstacle(anchor);
                break;
            case LevelEditorBrushType.Item:
                context.LevelData.SetItem(anchor, context.ItemPreset.ResourceType, Mathf.Max(1, context.ItemPreset.Amount));
                break;
            case LevelEditorBrushType.Outpost:
                context.LevelData.SetOutpost(
                    anchor,
                    context.OutpostPreset.OutpostType,
                    Mathf.Max(1, context.OutpostPreset.ResourcePerTurn),
                    context.OutpostPreset.InitialState);
                break;
            case LevelEditorBrushType.Event:
                context.LevelData.SetEvent(anchor, context.EventPreset.EventKey);
                break;
            case LevelEditorBrushType.StayEnemy:
                context.LevelData.SetStayEnemy(anchor);
                break;
            case LevelEditorBrushType.Castle:
                context.LevelData.SetCastle(anchor);
                break;
            case LevelEditorBrushType.VillainUnion:
                context.LevelData.SetVillainUnion(anchor);
                break;
        }

        sceneStatus = $"Placed {context.BrushType} at {anchor}.";
        CommitLevelDataChange(context);
    }

    private void EraseAtGrid(LevelEditorContext context, Vector2Int grid)
    {
        if (!TryFindPlacementAtGrid(context, grid, out Vector2Int anchor, out _, out string label))
        {
            sceneStatus = $"Nothing to erase at {grid}.";
            Repaint();
            return;
        }

        Undo.RecordObject(context.LevelData, $"Erase {label}");
        context.LevelData.EraseAt(anchor);
        sceneStatus = $"Erased {label} at {anchor}.";
        CommitLevelDataChange(context);
    }

    private void CommitLevelDataChange(LevelEditorContext context)
    {
        EditorUtility.SetDirty(context.LevelData);

        if (context.ApplyLevelAfterEdit && context.LevelLoader != null)
            context.LevelLoader.LoadLevel();

        Repaint();
        SceneView.RepaintAll();
    }

    private void HandleUndoRedo()
    {
        if (TryGetContext(out LevelEditorContext context, false) && context.LevelLoader != null)
            context.LevelLoader.LoadLevel();

        Repaint();
        SceneView.RepaintAll();
    }

    private bool TryGetContext(out LevelEditorContext context, bool requireRegistry)
    {
        context = default;

        if (controller == null)
            return false;

        context.LevelData = controller.LevelData;
        context.LevelLoader = controller.LevelLoader;
        context.GridManager = controller.GridManager != null
            ? controller.GridManager
            : controller.LevelLoader != null ? controller.LevelLoader.GridManager : null;
        context.PrefabRegistry = controller.LevelLoader != null ? controller.LevelLoader.PrefabRegistry : null;
        context.InputCamera = controller.InputCamera;
        context.BrushType = controller.BrushType;
        context.ItemPreset = controller.ItemPreset;
        context.OutpostPreset = controller.OutpostPreset;
        context.EventPreset = controller.EventPreset;
        context.ApplyLevelAfterEdit = controller.ApplyLevelAfterEdit;
        context.GroundMask = controller.GroundMask;

        if (context.LevelData == null || context.GridManager == null)
            return false;

        return !requireRegistry || context.PrefabRegistry != null;
    }

    private bool TryGetMouseGrid(LevelEditorContext context, Vector2 mousePosition, out Vector2Int grid)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (context.GroundMask.value != 0
            && Physics.Raycast(ray, out RaycastHit hit, 1000f, context.GroundMask))
        {
            grid = context.GridManager.WorldToGrid(hit.point);
            return context.LevelData.IsInsideGrid(grid);
        }

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, context.GridManager.GetLandSurfaceY(), 0f));
        if (groundPlane.Raycast(ray, out float distance))
        {
            grid = context.GridManager.WorldToGrid(ray.GetPoint(distance));
            return context.LevelData.IsInsideGrid(grid);
        }

        grid = Vector2Int.zero;
        return false;
    }

    private static bool HasBrushPrefab(LevelEditorContext context, out string reason)
    {
        if (context.BrushType == LevelEditorBrushType.Erase)
        {
            reason = null;
            return true;
        }

        return TryGetBrushPrefab(context, out _, out reason);
    }

    private static bool TryBuildBrushFootprint(
        LevelEditorContext context,
        Vector2Int anchor,
        out List<Vector2Int> footprint,
        out string reason)
    {
        footprint = null;

        if (!TryGetBrushPrefab(context, out GameObject prefab, out reason))
            return false;

        footprint = BuildFootprint(prefab, anchor);
        return true;
    }

    private static bool TryGetBrushPrefab(LevelEditorContext context, out GameObject prefab, out string reason)
    {
        prefab = null;
        reason = null;

        if (context.PrefabRegistry == null)
        {
            reason = "LevelPrefabRegistry is missing.";
            return false;
        }

        switch (context.BrushType)
        {
            case LevelEditorBrushType.Obstacle:
                prefab = context.PrefabRegistry.ObstaclePrefab;
                reason = prefab == null ? "Obstacle prefab is missing." : null;
                return prefab != null;
            case LevelEditorBrushType.Item:
                if (context.ItemPreset == null)
                {
                    reason = "Item preset is missing.";
                    return false;
                }

                prefab = GetItemPrefab(context, context.ItemPreset.ResourceType);
                reason = prefab == null ? $"Item prefab is missing for {context.ItemPreset.ResourceType}." : null;
                return prefab != null;
            case LevelEditorBrushType.Outpost:
                if (context.OutpostPreset == null)
                {
                    reason = "Outpost preset is missing.";
                    return false;
                }

                prefab = GetOutpostPrefab(context, context.OutpostPreset.OutpostType);
                reason = prefab == null ? $"Outpost prefab is missing for {context.OutpostPreset.OutpostType}." : null;
                return prefab != null;
            case LevelEditorBrushType.Event:
                if (context.EventPreset == null)
                {
                    reason = "Event preset is missing.";
                    return false;
                }

                prefab = GetEventPrefab(context, context.EventPreset.EventKey);
                reason = prefab == null ? $"Event prefab is missing for {context.EventPreset.EventKey}." : null;
                return prefab != null;
            case LevelEditorBrushType.Castle:
                prefab = GetCastlePrefab(context);
                reason = prefab == null ? "Castle prefab is missing." : null;
                return prefab != null;
            case LevelEditorBrushType.VillainUnion:
                prefab = GetVillainUnionPrefab(context);
                reason = prefab == null ? "VillainUnion prefab is missing." : null;
                return prefab != null;
            case LevelEditorBrushType.StayEnemy:
                prefab = GetStayEnemyPrefab(context);
                reason = prefab == null ? "StayEnemy prefab is missing." : null;
                return prefab != null;
            default:
                reason = "This brush cannot place objects.";
                return false;
        }
    }

    private static bool CanPlaceFootprint(LevelEditorContext context, List<Vector2Int> footprint, out string reason)
    {
        for (int i = 0; i < footprint.Count; i++)
        {
            if (!context.LevelData.IsInsideGrid(footprint[i]))
            {
                reason = "Footprint is outside the grid.";
                return false;
            }
        }

        if (context.BrushType == LevelEditorBrushType.Castle && context.LevelData.CastlePlacement.HasPlacement)
        {
            reason = "Castle is already placed. Erase it first.";
            return false;
        }

        if (context.BrushType == LevelEditorBrushType.VillainUnion && context.LevelData.VillainUnionPlacement.HasPlacement)
        {
            reason = "VillainUnion is already placed. Erase it first.";
            return false;
        }

        if (TryFindBlockingPlacement(context, footprint, out reason))
            return false;

        reason = null;
        return true;
    }

    private static bool TryFindBlockingPlacement(LevelEditorContext context, List<Vector2Int> footprint, out string reason)
    {
        LevelData levelData = context.LevelData;

        for (int i = 0; i < levelData.ObstacleCells.Count; i++)
        {
            if (FootprintsOverlap(footprint, BuildFootprint(null, levelData.ObstacleCells[i])))
            {
                reason = "Obstacle overlaps this footprint.";
                return true;
            }
        }

        for (int i = 0; i < levelData.ItemPlacements.Count; i++)
        {
            ItemPlacementData placement = levelData.ItemPlacements[i];
            if (FootprintsOverlap(footprint, BuildFootprint(GetItemPrefab(context, placement.ResourceType), placement.GridPosition)))
            {
                reason = "Item overlaps this footprint.";
                return true;
            }
        }

        for (int i = 0; i < levelData.OutpostPlacements.Count; i++)
        {
            OutpostPlacementData placement = levelData.OutpostPlacements[i];
            if (FootprintsOverlap(footprint, BuildFootprint(GetOutpostPrefab(context, placement.OutpostType), placement.GridPosition)))
            {
                reason = "Outpost overlaps this footprint.";
                return true;
            }
        }

        for (int i = 0; i < levelData.EventPlacements.Count; i++)
        {
            EventPlacementData placement = levelData.EventPlacements[i];
            if (FootprintsOverlap(footprint, BuildFootprint(GetEventPrefab(context, placement.EventKey), placement.GridPosition)))
            {
                reason = "Event overlaps this footprint.";
                return true;
            }
        }

        for (int i = 0; i < levelData.StayEnemyCells.Count; i++)
        {
            if (FootprintsOverlap(footprint, BuildFootprint(GetStayEnemyPrefab(context), levelData.StayEnemyCells[i])))
            {
                reason = "StayEnemy overlaps this footprint.";
                return true;
            }
        }

        if (levelData.CastlePlacement.HasPlacement
            && FootprintsOverlap(footprint, BuildFootprint(GetCastlePrefab(context), levelData.CastlePlacement.GridPosition)))
        {
            reason = "Castle overlaps this footprint.";
            return true;
        }

        if (levelData.VillainUnionPlacement.HasPlacement
            && FootprintsOverlap(footprint, BuildFootprint(GetVillainUnionPrefab(context), levelData.VillainUnionPlacement.GridPosition)))
        {
            reason = "VillainUnion overlaps this footprint.";
            return true;
        }

        reason = null;
        return false;
    }

    private static bool TryFindPlacementAtGrid(
        LevelEditorContext context,
        Vector2Int grid,
        out Vector2Int anchor,
        out List<Vector2Int> footprint,
        out string label)
    {
        LevelData levelData = context.LevelData;

        if (levelData.CastlePlacement.HasPlacement)
        {
            anchor = levelData.CastlePlacement.GridPosition;
            footprint = BuildFootprint(GetCastlePrefab(context), anchor);
            if (footprint.Contains(grid))
            {
                label = "Castle";
                return true;
            }
        }

        if (levelData.VillainUnionPlacement.HasPlacement)
        {
            anchor = levelData.VillainUnionPlacement.GridPosition;
            footprint = BuildFootprint(GetVillainUnionPrefab(context), anchor);
            if (footprint.Contains(grid))
            {
                label = "VillainUnion";
                return true;
            }
        }

        for (int i = 0; i < levelData.OutpostPlacements.Count; i++)
        {
            OutpostPlacementData placement = levelData.OutpostPlacements[i];
            anchor = placement.GridPosition;
            footprint = BuildFootprint(GetOutpostPrefab(context, placement.OutpostType), anchor);
            if (footprint.Contains(grid))
            {
                label = "Outpost";
                return true;
            }
        }

        for (int i = 0; i < levelData.EventPlacements.Count; i++)
        {
            EventPlacementData placement = levelData.EventPlacements[i];
            anchor = placement.GridPosition;
            footprint = BuildFootprint(GetEventPrefab(context, placement.EventKey), anchor);
            if (footprint.Contains(grid))
            {
                label = "Event";
                return true;
            }
        }

        for (int i = 0; i < levelData.ItemPlacements.Count; i++)
        {
            ItemPlacementData placement = levelData.ItemPlacements[i];
            anchor = placement.GridPosition;
            footprint = BuildFootprint(GetItemPrefab(context, placement.ResourceType), anchor);
            if (footprint.Contains(grid))
            {
                label = "Item";
                return true;
            }
        }

        for (int i = 0; i < levelData.StayEnemyCells.Count; i++)
        {
            anchor = levelData.StayEnemyCells[i];
            footprint = BuildFootprint(GetStayEnemyPrefab(context), anchor);
            if (footprint.Contains(grid))
            {
                label = "StayEnemy";
                return true;
            }
        }

        for (int i = 0; i < levelData.ObstacleCells.Count; i++)
        {
            anchor = levelData.ObstacleCells[i];
            footprint = BuildFootprint(null, anchor);
            if (footprint.Contains(grid))
            {
                label = "Obstacle";
                return true;
            }
        }

        anchor = Vector2Int.zero;
        footprint = null;
        label = null;
        return false;
    }

    private static List<Vector2Int> BuildFootprint(GameObject prefab, Vector2Int anchor)
    {
        Vector2Int size = GetPrefabFootprintSize(prefab);
        List<Vector2Int> footprint = new List<Vector2Int>(size.x * size.y);

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
                footprint.Add(new Vector2Int(anchor.x + x, anchor.y + y));
        }

        return footprint;
    }

    private static Vector2Int GetPrefabFootprintSize(GameObject prefab)
    {
        if (prefab == null)
            return Vector2Int.one;

        MultiGridOccupant occupant = prefab.GetComponent<MultiGridOccupant>();
        return occupant != null ? occupant.Size : Vector2Int.one;
    }

    private static bool FootprintsOverlap(List<Vector2Int> first, List<Vector2Int> second)
    {
        for (int i = 0; i < first.Count; i++)
        {
            if (second.Contains(first[i]))
                return true;
        }

        return false;
    }

    private static GameObject GetItemPrefab(LevelEditorContext context, ResourceType resourceType)
    {
        if (context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetItemPrefab(resourceType, out ItemObject prefab)
            && prefab != null)
        {
            return prefab.gameObject;
        }

        return null;
    }

    private static GameObject GetOutpostPrefab(LevelEditorContext context, OutpostType outpostType)
    {
        if (context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetOutpostPrefab(outpostType, out Outpost prefab)
            && prefab != null)
        {
            return prefab.gameObject;
        }

        return null;
    }

    private static GameObject GetEventPrefab(LevelEditorContext context, string eventKey)
    {
        if (context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetEventPrefab(eventKey, out MapEventObject prefab)
            && prefab != null)
        {
            return prefab.gameObject;
        }

        return null;
    }

    private static GameObject GetCastlePrefab(LevelEditorContext context)
    {
        return context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetCastlePrefab(out CastleUnit prefab)
            && prefab != null
                ? prefab.gameObject
                : null;
    }

    private static GameObject GetVillainUnionPrefab(LevelEditorContext context)
    {
        return context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetVillainUnionBasePrefab(out VillainUnionBase prefab)
            && prefab != null
                ? prefab.gameObject
                : null;
    }

    private static GameObject GetStayEnemyPrefab(LevelEditorContext context)
    {
        return context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetStayEnemyPrefab(out EnemyGridMover prefab)
            && prefab != null
                ? prefab.gameObject
                : null;
    }

    private static void DrawStayEnemyEncounterZone(LevelEditorContext context, Vector2Int anchor, Color fill, Color outline)
    {
        DrawFootprint(context, BuildEncounterZone(anchor), fill, outline);
    }

    private static List<Vector2Int> BuildEncounterZone(Vector2Int anchor)
    {
        List<Vector2Int> footprint = new List<Vector2Int>(9);
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
                footprint.Add(new Vector2Int(anchor.x + x, anchor.y + y));
        }

        return footprint;
    }

    private static void DrawFootprint(LevelEditorContext context, List<Vector2Int> footprint, Color fill, Color outline)
    {
        if (footprint == null)
            return;

        for (int i = 0; i < footprint.Count; i++)
        {
            if (!context.LevelData.IsInsideGrid(footprint[i]))
                continue;

            DrawCell(context, footprint[i], fill, outline);
        }
    }

    private static void DrawCell(LevelEditorContext context, Vector2Int grid, Color fill, Color outline)
    {
        GridManager gridManager = context.GridManager;
        float halfSize = gridManager.CellSize * 0.48f;
        Vector3 center = gridManager.GridToWorldCenter(grid);
        center.y = gridManager.GetLandSurfaceY() + 0.22f;

        Vector3[] vertices =
        {
            new Vector3(center.x - halfSize, center.y, center.z - halfSize),
            new Vector3(center.x - halfSize, center.y, center.z + halfSize),
            new Vector3(center.x + halfSize, center.y, center.z + halfSize),
            new Vector3(center.x + halfSize, center.y, center.z - halfSize)
        };

        CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;
        Handles.DrawSolidRectangleWithOutline(vertices, fill, outline);
        Handles.zTest = previousZTest;
    }

    private static void DrawSceneLabel(LevelEditorContext context, Vector2Int grid, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Vector3 position = context.GridManager.GridToWorldCenter(grid);
        position.y = context.GridManager.GetLandSurfaceY() + 0.35f;
        Handles.Label(position, text, EditorStyles.helpBox);
    }

    private void TryAutoAssignController()
    {
        controller = FindFirstObjectByType<LevelEditorController>();
    }

    private static int GetBrushOrderIndex(LevelEditorBrushType brushType)
    {
        for (int i = 0; i < BrushOrder.Length; i++)
        {
            if (BrushOrder[i] == brushType)
                return i;
        }

        return 0;
    }

    private struct LevelEditorContext
    {
        public LevelData LevelData;
        public LevelLoader LevelLoader;
        public GridManager GridManager;
        public LevelPrefabRegistry PrefabRegistry;
        public Camera InputCamera;
        public LevelEditorBrushType BrushType;
        public ItemPlacementPreset ItemPreset;
        public OutpostPlacementPreset OutpostPreset;
        public EventPlacementPreset EventPreset;
        public bool ApplyLevelAfterEdit;
        public LayerMask GroundMask;
    }
}
