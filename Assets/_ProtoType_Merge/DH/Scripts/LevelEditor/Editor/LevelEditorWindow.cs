using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class LevelEditorWindow : EditorWindow
{
    private const string MenuPath = "DH Work/Level Editor";

    private sealed class TileClipboard
    {
        public LevelData SourceLevelData;
        public Vector2Int Size;
        public readonly List<CopiedTile> Tiles = new List<CopiedTile>();
    }

    private readonly struct CopiedTile
    {
        public readonly Vector2Int Offset;
        public readonly string TileKey;

        public CopiedTile(Vector2Int offset, string tileKey)
        {
            Offset = offset;
            TileKey = tileKey;
        }
    }

    private readonly struct BrushGroup
    {
        public readonly string Title;
        public readonly LevelEditorBrushType[] Brushes;
        public readonly string[] Labels;

        public BrushGroup(string title, LevelEditorBrushType[] brushes, string[] labels)
        {
            Title = title;
            Brushes = brushes;
            Labels = labels;
        }
    }

    private static readonly BrushGroup[] BrushGroups =
    {
        new BrushGroup(
            "Terrain",
            new[]
            {
                LevelEditorBrushType.GroundTile,
                LevelEditorBrushType.Obstacle
            },
            new[] { "GroundTile", "Obstacle" }),
        new BrushGroup(
            "Building",
            new[]
            {
                LevelEditorBrushType.Outpost,
                LevelEditorBrushType.HeroUnion,
                LevelEditorBrushType.VillainUnion
            },
            new[] { "Outpost", "HeroUnion", "VillainUnion" }),
        new BrushGroup(
            "Event",
            new[]
            {
                LevelEditorBrushType.Event,
                LevelEditorBrushType.MainEvent,
                LevelEditorBrushType.SubEvent
            },
            new[] { "MapEvent", "MainEvent", "SubEvent" }),
        new BrushGroup(
            "Enemy",
            new[]
            {
                LevelEditorBrushType.EnemyGroup,
                LevelEditorBrushType.EnemySpawnPoint
            },
            new[] { "EnemyGroup", "EnemySpawn" }),
        new BrushGroup(
            "Visual",
            new[]
            {
                LevelEditorBrushType.DecorativeObject
            },
            new[] { "Decorative" }),
        new BrushGroup(
            "Etc",
            new[]
            {
                LevelEditorBrushType.Item,
                LevelEditorBrushType.GateBlocker
            },
            new[] { "Item", "Gate" }),
        new BrushGroup(
            "Tool",
            new[]
            {
                LevelEditorBrushType.GroundTileErase,
                LevelEditorBrushType.Erase
            },
            new[] { "TileErase", "Erase" })
    };

    private LevelEditorController controller;
    private Vector2 scrollPosition;
    private bool sceneEditingEnabled = true;
    private string sceneStatus;
    private Vector2Int? lastEditedGroundTileGrid;
    private Vector2Int? lastEditedObstacleGrid;
    private Vector2Int? tileSelectionStart;
    private Vector2Int? tileSelectionEnd;
    private bool isSelectingTiles;
    private TileClipboard tileClipboard;

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
        LevelEditorBrushType brushType = (LevelEditorBrushType)brushTypeProperty.intValue;

        if (brushType == LevelEditorBrushType.Item)
        {
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedItemResourceType"), new GUIContent("Resource Type"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedItemAmount"), new GUIContent("Amount"));
        }

        if (brushType == LevelEditorBrushType.Outpost)
        {
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedOutpostType"), new GUIContent("Outpost Type"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedOutpostResourcePerTurn"), new GUIContent("Resource/Turn"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedOutpostInitialState"), new GUIContent("Initial State"));
        }

        if (brushType == LevelEditorBrushType.Event)
        {
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedMapEventType"), new GUIContent("Event Type"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedMapEventRequireAmount"), new GUIContent("Require Amount"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedMapEventEffectAmount"), new GUIContent("Effect Amount"));
        }

        if (brushType == LevelEditorBrushType.MainEvent)
            DrawMainEventPrefabSelector(serializedController);

        if (brushType == LevelEditorBrushType.SubEvent)
            DrawSubEventPrefabSelector(serializedController);

        if (brushType == LevelEditorBrushType.EnemyGroup)
        {
            EditorGUILayout.PropertyField(serializedController.FindProperty("enemyGroupKey"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("enemyBehaviorType"));
        }

        if (brushType == LevelEditorBrushType.GroundTile)
            DrawGroundTileSelector(serializedController);

        if (brushType == LevelEditorBrushType.HeroUnion)
            DrawHeroUnionPrefabSelector(serializedController);

        if (brushType == LevelEditorBrushType.DecorativeObject)
            DrawDecorativeObjectSelector(serializedController);

        if (brushType == LevelEditorBrushType.GateBlocker)
        {
            DrawGatePrefabSelector(serializedController);
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedGateId"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedGateFirstZoneId"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedGateSecondZoneId"));
        }

        if (brushType == LevelEditorBrushType.EnemySpawnPoint)
        {
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedEnemySpawnZoneId"), new GUIContent("Zone"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedEnemySpawnEnemyGroupKey"), new GUIContent("CSV Group"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedEnemySpawnChatId"), new GUIContent("Spawn Chat"));
            EditorGUILayout.PropertyField(serializedController.FindProperty("selectedEnemySpawnEncounterChatId"), new GUIContent("Meet Chat"));
        }

        DrawTileClipboardControls(serializedController, brushType);

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

    private void DrawTileClipboardControls(SerializedObject serializedController, LevelEditorBrushType brushType)
    {
        SerializedProperty brushTypeProperty = serializedController.FindProperty("brushType");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Tile Clipboard", EditorStyles.boldLabel);

        bool selectionMode = brushType == LevelEditorBrushType.TileSelection;
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Toggle(selectionMode, "Selection Brush", "Button") != selectionMode)
                brushTypeProperty.intValue = selectionMode
                    ? (int)LevelEditorBrushType.GroundTile
                    : (int)LevelEditorBrushType.TileSelection;

            if (GUILayout.Button("Clear Selection"))
                ClearTileSelection();
        }

        string selectionText = HasTileSelection()
            ? $"Selection : {GetSelectionMin()} ~ {GetSelectionMax()}"
            : "Selection : None";
        EditorGUILayout.LabelField(selectionText, EditorStyles.miniLabel);

        string clipboardText = tileClipboard != null
            ? $"Clipboard : {tileClipboard.Size.x}x{tileClipboard.Size.y}, Tiles {tileClipboard.Tiles.Count}"
            : "Clipboard : Empty";
        EditorGUILayout.LabelField(clipboardText, EditorStyles.miniLabel);
        EditorGUILayout.HelpBox("Tile selection uses Ctrl+C to copy, Ctrl+V to paste at the hovered cell, Esc to clear selection.", MessageType.Info);
    }

    private void DrawGroundTileSelector(SerializedObject serializedController)
    {
        SerializedProperty registryProperty = serializedController.FindProperty("tileRegistry");
        SerializedProperty selectedKeyProperty = serializedController.FindProperty("selectedTileKey");

        EditorGUILayout.PropertyField(registryProperty);

        LevelTileRegistry registry = registryProperty.objectReferenceValue as LevelTileRegistry;
        if (registry == null)
        {
            EditorGUILayout.HelpBox("GroundTile brush needs a LevelTileRegistry.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        IReadOnlyList<LevelTileEntry> entries = registry.TileEntries;
        if (entries == null || entries.Count == 0)
        {
            EditorGUILayout.HelpBox("LevelTileRegistry has no tile entries.", MessageType.Info);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        List<string> keys = new List<string>();
        List<Sprite> sprites = new List<Sprite>();
        for (int i = 0; i < entries.Count; i++)
        {
            LevelTileEntry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.TileKey))
                continue;

            keys.Add(entry.TileKey);
            sprites.Add(entry.Sprite);
        }

        if (keys.Count == 0)
        {
            EditorGUILayout.HelpBox("LevelTileRegistry entries do not have tile keys.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        int selectedIndex = Mathf.Max(0, keys.IndexOf(selectedKeyProperty.stringValue));
        int nextIndex = EditorGUILayout.Popup("Tile Key", selectedIndex, keys.ToArray());
        selectedKeyProperty.stringValue = keys[Mathf.Clamp(nextIndex, 0, keys.Count - 1)];

        Sprite selectedSprite = sprites[Mathf.Clamp(nextIndex, 0, sprites.Count - 1)];
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Preview", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
            Rect rect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(64f), GUILayout.Height(64f));
            if (selectedSprite != null)
                DrawSpritePreview(rect, selectedSprite);
            else
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.15f));
        }
    }

    private void DrawHeroUnionPrefabSelector(SerializedObject serializedController)
    {
        SerializedProperty selectedKeyProperty = serializedController.FindProperty("selectedHeroUnionPrefabKey");

        LevelPrefabRegistry registry = controller.LevelLoader != null ? controller.LevelLoader.PrefabRegistry : null;
        if (registry == null)
        {
            EditorGUILayout.HelpBox("HeroUnion brush needs a LevelPrefabRegistry on the LevelLoader.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        IReadOnlyList<HeroUnionPrefabEntry> entries = registry.HeroUnionPrefabs;
        if (entries == null || entries.Count == 0)
        {
            EditorGUILayout.HelpBox("LevelPrefabRegistry has no extra HeroUnion entries. The default HeroUnion prefab will be used.", MessageType.Info);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        List<string> keys = new List<string> { string.Empty };
        List<string> labels = new List<string> { "Default" };
        for (int i = 0; i < entries.Count; i++)
        {
            HeroUnionPrefabEntry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.PrefabKey))
                continue;

            keys.Add(entry.PrefabKey);
            labels.Add(entry.PrefabKey);
        }

        int selectedIndex = Mathf.Max(0, keys.IndexOf(selectedKeyProperty.stringValue));
        int nextIndex = EditorGUILayout.Popup("Prefab Key", selectedIndex, labels.ToArray());
        selectedKeyProperty.stringValue = keys[Mathf.Clamp(nextIndex, 0, keys.Count - 1)];
    }

    private void DrawDecorativeObjectSelector(SerializedObject serializedController)
    {
        SerializedProperty selectedKeyProperty = serializedController.FindProperty("selectedDecorativeObjectKey");

        LevelPrefabRegistry registry = controller.LevelLoader != null ? controller.LevelLoader.PrefabRegistry : null;
        if (registry == null)
        {
            EditorGUILayout.HelpBox("Decorative brush needs a LevelPrefabRegistry on the LevelLoader.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        IReadOnlyList<DecorativeObjectPrefabEntry> entries = registry.DecorativeObjectPrefabs;
        if (entries == null || entries.Count == 0)
        {
            EditorGUILayout.HelpBox("LevelPrefabRegistry has no decorative object entries.", MessageType.Info);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        List<string> keys = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            DecorativeObjectPrefabEntry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.PrefabKey))
                continue;

            keys.Add(entry.PrefabKey);
        }

        if (keys.Count == 0)
        {
            EditorGUILayout.HelpBox("Decorative object entries do not have prefab keys.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        int selectedIndex = Mathf.Max(0, keys.IndexOf(selectedKeyProperty.stringValue));
        int nextIndex = EditorGUILayout.Popup("Prefab Key", selectedIndex, keys.ToArray());
        selectedKeyProperty.stringValue = keys[Mathf.Clamp(nextIndex, 0, keys.Count - 1)];
    }

    private void DrawMainEventPrefabSelector(SerializedObject serializedController)
    {
        SerializedProperty selectedKeyProperty = serializedController.FindProperty("selectedMainEventPrefabKey");

        LevelPrefabRegistry registry = controller.LevelLoader != null ? controller.LevelLoader.PrefabRegistry : null;
        if (registry == null)
        {
            EditorGUILayout.HelpBox("MainEvent brush needs a LevelPrefabRegistry on the LevelLoader.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        IReadOnlyList<MainEventPrefabEntry> entries = registry.MainEventPrefabs;
        if (entries == null || entries.Count == 0)
        {
            EditorGUILayout.HelpBox("LevelPrefabRegistry has no MainEvent entries.", MessageType.Info);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        List<string> keys = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            MainEventPrefabEntry entry = entries[i];
            if (!string.IsNullOrWhiteSpace(entry.PrefabKey))
                keys.Add(entry.PrefabKey);
        }

        if (keys.Count == 0)
        {
            EditorGUILayout.HelpBox("MainEvent entries do not have prefab keys.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        int selectedIndex = Mathf.Max(0, keys.IndexOf(selectedKeyProperty.stringValue));
        int nextIndex = EditorGUILayout.Popup("Prefab Key", selectedIndex, keys.ToArray());
        selectedKeyProperty.stringValue = keys[Mathf.Clamp(nextIndex, 0, keys.Count - 1)];

        if (registry.TryGetMainEventPrefab(selectedKeyProperty.stringValue, out MainEventObject prefab) && prefab != null)
        {
            EditorGUILayout.LabelField("Event Key", prefab.EventKey);
            EditorGUILayout.LabelField("Zone ID", prefab.ZoneId.ToString());
            EditorGUILayout.LabelField("Chat ID", prefab.ChatId.ToString());
        }
    }

    private void DrawSubEventPrefabSelector(SerializedObject serializedController)
    {
        SerializedProperty selectedKeyProperty = serializedController.FindProperty("selectedSubEventPrefabKey");

        LevelPrefabRegistry registry = controller.LevelLoader != null ? controller.LevelLoader.PrefabRegistry : null;
        if (registry == null)
        {
            EditorGUILayout.HelpBox("SubEvent brush needs a LevelPrefabRegistry on the LevelLoader.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        IReadOnlyList<SubEventPrefabEntry> entries = registry.SubEventPrefabs;
        if (entries == null || entries.Count == 0)
        {
            EditorGUILayout.HelpBox("LevelPrefabRegistry has no SubEvent entries.", MessageType.Info);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        List<string> keys = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            SubEventPrefabEntry entry = entries[i];
            if (!string.IsNullOrWhiteSpace(entry.PrefabKey))
                keys.Add(entry.PrefabKey);
        }

        if (keys.Count == 0)
        {
            EditorGUILayout.HelpBox("SubEvent entries do not have prefab keys.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        int selectedIndex = Mathf.Max(0, keys.IndexOf(selectedKeyProperty.stringValue));
        int nextIndex = EditorGUILayout.Popup("Prefab Key", selectedIndex, keys.ToArray());
        selectedKeyProperty.stringValue = keys[Mathf.Clamp(nextIndex, 0, keys.Count - 1)];

        if (registry.TryGetSubEventPrefab(selectedKeyProperty.stringValue, out SubEventObject prefab) && prefab != null)
        {
            EditorGUILayout.LabelField("Event Key", prefab.EventKey);
            EditorGUILayout.LabelField("Zone ID", prefab.ZoneId.ToString());
            EditorGUILayout.LabelField("Chat ID", prefab.ChatId.ToString());
        }
    }

    private void DrawGatePrefabSelector(SerializedObject serializedController)
    {
        SerializedProperty selectedKeyProperty = serializedController.FindProperty("selectedGatePrefabKey");

        LevelPrefabRegistry registry = controller.LevelLoader != null ? controller.LevelLoader.PrefabRegistry : null;
        if (registry == null)
        {
            EditorGUILayout.HelpBox("Gate brush needs a LevelPrefabRegistry on the LevelLoader.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        IReadOnlyList<GatePrefabEntry> entries = registry.GatePrefabs;
        if (entries == null || entries.Count == 0)
        {
            EditorGUILayout.HelpBox("LevelPrefabRegistry has no Gate entries.", MessageType.Info);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        List<string> keys = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            GatePrefabEntry entry = entries[i];
            if (!string.IsNullOrWhiteSpace(entry.PrefabKey) && !keys.Contains(entry.PrefabKey))
                keys.Add(entry.PrefabKey);
        }

        if (keys.Count == 0)
        {
            EditorGUILayout.HelpBox("Gate entries do not have prefab keys.", MessageType.Warning);
            EditorGUILayout.PropertyField(selectedKeyProperty);
            return;
        }

        int selectedIndex = Mathf.Max(0, keys.IndexOf(selectedKeyProperty.stringValue));
        int nextIndex = EditorGUILayout.Popup("Prefab Key", selectedIndex, keys.ToArray());
        selectedKeyProperty.stringValue = keys[Mathf.Clamp(nextIndex, 0, keys.Count - 1)];

        if (registry.TryGetGatePrefab(selectedKeyProperty.stringValue, out GateFootprint prefab) && prefab != null)
            EditorGUILayout.LabelField("Footprint", prefab.FootprintType.ToString());
        else
            EditorGUILayout.HelpBox($"Gate prefab key '{selectedKeyProperty.stringValue}' is not registered.", MessageType.Warning);
    }

    private void DrawBrushSelector(SerializedProperty brushTypeProperty)
    {
        LevelEditorBrushType brushType = (LevelEditorBrushType)brushTypeProperty.intValue;

        for (int i = 0; i < BrushGroups.Length; i++)
        {
            BrushGroup group = BrushGroups[i];
            EditorGUILayout.LabelField(group.Title, EditorStyles.miniBoldLabel);

            int selectedIndex = GetBrushIndex(group.Brushes, brushType);
            int nextIndex = GUILayout.SelectionGrid(selectedIndex, group.Labels, 3);

            if (nextIndex >= 0 && nextIndex < group.Brushes.Length)
            {
                LevelEditorBrushType nextBrush = group.Brushes[nextIndex];
                if (nextBrush != brushType)
                {
                    brushTypeProperty.intValue = (int)nextBrush;
                    sceneStatus = $"Brush : {nextBrush}";
                    brushType = nextBrush;
                }
            }

            EditorGUILayout.Space(2f);
        }
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

        if (context.BrushType == LevelEditorBrushType.GroundTile)
        {
            if (context.TileRegistry == null)
                EditorGUILayout.HelpBox("GroundTile brush needs a LevelTileRegistry.", MessageType.Warning);
            else if (string.IsNullOrWhiteSpace(context.SelectedTileKey))
                EditorGUILayout.HelpBox("GroundTile brush needs a selected Tile Key.", MessageType.Warning);
            else if (!context.TileRegistry.TryGetSprite(context.SelectedTileKey, out _))
                EditorGUILayout.HelpBox($"Tile key '{context.SelectedTileKey}' was not found in the LevelTileRegistry.", MessageType.Warning);
        }

        if (context.BrushType == LevelEditorBrushType.DecorativeObject)
        {
            if (context.PrefabRegistry == null)
                EditorGUILayout.HelpBox("Decorative brush needs a LevelPrefabRegistry.", MessageType.Warning);
            else if (string.IsNullOrWhiteSpace(context.SelectedDecorativeObjectKey))
                EditorGUILayout.HelpBox("Decorative brush needs a selected Prefab Key.", MessageType.Warning);
            else if (!context.PrefabRegistry.TryGetDecorativeObjectPrefab(context.SelectedDecorativeObjectKey, out GameObject decorativePrefab))
                EditorGUILayout.HelpBox($"Decorative prefab key '{context.SelectedDecorativeObjectKey}' was not found in the LevelPrefabRegistry.", MessageType.Warning);
            else if (decorativePrefab.GetComponent<DecorativeObjectPlacement>() == null)
                EditorGUILayout.HelpBox($"Decorative prefab '{context.SelectedDecorativeObjectKey}' needs a DecorativeObjectPlacement component.", MessageType.Warning);
        }

        if (context.BrushType == LevelEditorBrushType.MainEvent)
        {
            if (context.PrefabRegistry == null)
                EditorGUILayout.HelpBox("MainEvent brush needs a LevelPrefabRegistry.", MessageType.Warning);
            else if (string.IsNullOrWhiteSpace(context.SelectedMainEventPrefabKey))
                EditorGUILayout.HelpBox("MainEvent brush needs a selected Prefab Key.", MessageType.Warning);
            else if (!context.PrefabRegistry.TryGetMainEventPrefab(context.SelectedMainEventPrefabKey, out MainEventObject mainEventPrefab))
                EditorGUILayout.HelpBox($"MainEvent prefab key '{context.SelectedMainEventPrefabKey}' was not found in the LevelPrefabRegistry.", MessageType.Warning);
            else if (mainEventPrefab.ChatId <= 0)
                EditorGUILayout.HelpBox($"MainEvent prefab '{context.SelectedMainEventPrefabKey}' needs a Chat ID.", MessageType.Warning);
        }

        if (context.BrushType == LevelEditorBrushType.SubEvent)
        {
            if (context.PrefabRegistry == null)
                EditorGUILayout.HelpBox("SubEvent brush needs a LevelPrefabRegistry.", MessageType.Warning);
            else if (string.IsNullOrWhiteSpace(context.SelectedSubEventPrefabKey))
                EditorGUILayout.HelpBox("SubEvent brush needs a selected Prefab Key.", MessageType.Warning);
            else if (!context.PrefabRegistry.TryGetSubEventPrefab(context.SelectedSubEventPrefabKey, out SubEventObject subEventPrefab))
                EditorGUILayout.HelpBox($"SubEvent prefab key '{context.SelectedSubEventPrefabKey}' was not found in the LevelPrefabRegistry.", MessageType.Warning);
            else if (subEventPrefab.ChatId <= 0)
                EditorGUILayout.HelpBox($"SubEvent prefab '{context.SelectedSubEventPrefabKey}' needs a Chat ID.", MessageType.Warning);
        }

        if (context.BrushType == LevelEditorBrushType.GateBlocker)
        {
            if (context.PrefabRegistry == null)
                EditorGUILayout.HelpBox("Gate brush needs a LevelPrefabRegistry.", MessageType.Warning);
            else if (string.IsNullOrWhiteSpace(context.SelectedGatePrefabKey))
                EditorGUILayout.HelpBox("Gate brush needs a selected Prefab Key.", MessageType.Warning);
            else if (!context.PrefabRegistry.TryGetGatePrefab(context.SelectedGatePrefabKey, out GateFootprint gatePrefab))
                EditorGUILayout.HelpBox($"Gate prefab key '{context.SelectedGatePrefabKey}' was not found in the LevelPrefabRegistry.", MessageType.Warning);
            else if (gatePrefab == null)
                EditorGUILayout.HelpBox($"Gate prefab '{context.SelectedGatePrefabKey}' is missing.", MessageType.Warning);
        }

        if (!IsPrefablessBrush(context.BrushType) && context.PrefabRegistry == null)
            EditorGUILayout.HelpBox("LevelLoader needs a LevelPrefabRegistry.", MessageType.Warning);
        else if (!IsPrefablessBrush(context.BrushType) && !HasBrushPrefab(context, out string prefabWarning))
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

        bool hasHoveredGrid = TryGetMouseGrid(context, currentEvent.mousePosition, out Vector2Int hoveredGrid);
        if (HandleTileClipboardShortcuts(context, currentEvent, hasHoveredGrid, hoveredGrid))
        {
            sceneView.Repaint();
            return;
        }

        bool isTileSelectionBrush = context.BrushType == LevelEditorBrushType.TileSelection;
        if (isTileSelectionBrush)
            DrawTileSelection(context);
        if (isTileSelectionBrush && hasHoveredGrid)
            DrawClipboardPreview(context, hoveredGrid);

        if (currentEvent.type == EventType.MouseUp)
        {
            lastEditedGroundTileGrid = null;
            lastEditedObstacleGrid = null;
            if (isSelectingTiles && context.BrushType == LevelEditorBrushType.TileSelection)
            {
                if (hasHoveredGrid)
                    tileSelectionEnd = hoveredGrid;

                isSelectingTiles = false;
                currentEvent.Use();
            }

            return;
        }

        if (!hasHoveredGrid)
            return;

        DrawHoverPreview(context, hoveredGrid);

        if (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDrag)
            sceneView.Repaint();

        bool isTileBrush = context.BrushType == LevelEditorBrushType.GroundTile ||
            context.BrushType == LevelEditorBrushType.GroundTileErase;
        bool isObstacleBrush = context.BrushType == LevelEditorBrushType.Obstacle;
        bool leftPaintEvent = currentEvent.button == 0 &&
            (currentEvent.type == EventType.MouseDown ||
             (currentEvent.type == EventType.MouseDrag && (isTileBrush || isTileSelectionBrush || isObstacleBrush)));
        bool rightEraseEvent = currentEvent.button == 1 && currentEvent.type == EventType.MouseDown;

        if (leftPaintEvent)
        {
            if (currentEvent.type == EventType.MouseDown)
                lastEditedGroundTileGrid = null;

            if (context.BrushType == LevelEditorBrushType.TileSelection)
                BeginOrUpdateTileSelection(hoveredGrid, currentEvent.type == EventType.MouseDown);
            else if (context.BrushType == LevelEditorBrushType.Erase)
                EraseAtGrid(context, hoveredGrid);
            else
                PlaceAtGrid(context, hoveredGrid);

            if (isTileBrush)
                lastEditedGroundTileGrid = hoveredGrid;
        if (isObstacleBrush)
            lastEditedObstacleGrid = hoveredGrid;

            currentEvent.Use();
        }
        else if (rightEraseEvent)
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

        for (int i = 0; i < levelData.GroundTilePlacements.Count; i++)
        {
            TilePlacementData placement = levelData.GroundTilePlacements[i];
            DrawFootprint(context, BuildFootprint(null, placement.GridPosition), new Color(0.25f, 0.55f, 1f, 0.05f), new Color(0.25f, 0.55f, 1f, 0.24f));
        }

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
            DrawFootprint(context, BuildFootprint(GetEventPrefab(context, placement.EventType), placement.GridPosition), new Color(0.75f, 0.35f, 1f, 0.10f), new Color(0.75f, 0.35f, 1f, 0.65f));
        }

        for (int i = 0; i < levelData.MainEventPlacements.Count; i++)
        {
            MainEventPlacementData placement = levelData.MainEventPlacements[i];
            DrawMainEventInteractionZone(context, placement.GridPosition, new Color(0.2f, 0.9f, 1f, 0.06f), new Color(0.2f, 0.9f, 1f, 0.28f));
            DrawFootprint(context, BuildFootprint(GetMainEventPrefab(context, placement.PrefabKey), placement.GridPosition), new Color(0.2f, 0.9f, 1f, 0.10f), new Color(0.2f, 0.9f, 1f, 0.65f));
        }

        for (int i = 0; i < levelData.SubEventPlacements.Count; i++)
        {
            SubEventPlacementData placement = levelData.SubEventPlacements[i];
            DrawFootprint(context, BuildFootprint(GetSubEventPrefab(context, placement.PrefabKey), placement.GridPosition), new Color(0.35f, 0.95f, 0.95f, 0.10f), new Color(0.35f, 0.95f, 0.95f, 0.65f));
        }

        for (int i = 0; i < levelData.EnemyPlacements.Count; i++)
        {
            EnemyPlacementData placement = levelData.EnemyPlacements[i];
            DrawEnemyEncounterZone(context, placement.GridPosition, new Color(1f, 0.3f, 0.05f, 0.06f), new Color(1f, 0.3f, 0.05f, 0.28f));
            DrawFootprint(context, BuildFootprint(GetEnemyGroupPrefab(context), placement.GridPosition), new Color(1f, 0.3f, 0.05f, 0.14f), new Color(1f, 0.3f, 0.05f, 0.75f));
        }

        for (int i = 0; i < levelData.DecorativeObjectPlacements.Count; i++)
        {
            DecorativeObjectPlacementData placement = levelData.DecorativeObjectPlacements[i];
            DrawFootprint(context, BuildFootprint(null, placement.GridPosition), new Color(1f, 0.65f, 0.2f, 0.10f), new Color(1f, 0.65f, 0.2f, 0.65f));
        }

        for (int i = 0; i < levelData.GatePlacements.Count; i++)
        {
            GatePlacementData placement = levelData.GatePlacements[i];
            IReadOnlyList<Vector2Int> blockerCells = placement.BlockerCells;
            for (int cellIndex = 0; cellIndex < blockerCells.Count; cellIndex++)
                DrawFootprint(context, BuildFootprint(null, blockerCells[cellIndex]), new Color(0.45f, 0.15f, 1f, 0.12f), new Color(0.45f, 0.15f, 1f, 0.75f));
        }

        for (int i = 0; i < levelData.EnemySpawnPointPlacements.Count; i++)
        {
            EnemySpawnPointPlacementData placement = levelData.EnemySpawnPointPlacements[i];
            DrawFootprint(context, BuildFootprint(null, placement.GridPosition), new Color(1f, 0.55f, 0.05f, 0.12f), new Color(1f, 0.55f, 0.05f, 0.75f));
        }

        if (levelData.HeroUnionPlacement.HasPlacement)
            DrawFootprint(context, BuildFootprint(GetHeroUnionPrefab(context, levelData.HeroUnionPlacement.PrefabKey), levelData.HeroUnionPlacement.GridPosition), new Color(1f, 0.85f, 0.1f, 0.12f), new Color(1f, 0.85f, 0.1f, 0.75f));

        if (levelData.VillainUnionPlacement.HasPlacement)
            DrawFootprint(context, BuildFootprint(GetVillainUnionPrefab(context), levelData.VillainUnionPlacement.GridPosition), new Color(1f, 0.2f, 0.55f, 0.12f), new Color(1f, 0.2f, 0.55f, 0.75f));
    }

    private void DrawHoverPreview(LevelEditorContext context, Vector2Int anchor)
    {
        if (context.BrushType == LevelEditorBrushType.GroundTile)
        {
            bool canPaint = CanPaintGroundTile(context, out string tileReason);
            DrawFootprint(
                context,
                BuildFootprint(null, anchor),
                canPaint ? new Color(0.25f, 0.65f, 1f, 0.20f) : new Color(1f, 0f, 0f, 0.20f),
                canPaint ? new Color(0.25f, 0.65f, 1f, 1f) : new Color(1f, 0f, 0f, 1f));

            DrawSceneLabel(context, anchor, canPaint ? context.SelectedTileKey : tileReason);
            return;
        }

        if (context.BrushType == LevelEditorBrushType.GroundTileErase)
        {
            bool hasTile = context.LevelData.HasGroundTileAt(anchor);
            DrawFootprint(
                context,
                BuildFootprint(null, anchor),
                hasTile ? new Color(1f, 0.6f, 0.1f, 0.20f) : new Color(1f, 1f, 1f, 0.08f),
                hasTile ? new Color(1f, 0.6f, 0.1f, 1f) : new Color(1f, 1f, 1f, 0.65f));

            DrawSceneLabel(context, anchor, hasTile ? "Erase GroundTile" : "No GroundTile");
            return;
        }

        if (context.BrushType == LevelEditorBrushType.TileSelection)
        {
            DrawFootprint(
                context,
                BuildFootprint(null, anchor),
                new Color(0.15f, 0.45f, 1f, 0.12f),
                new Color(0.15f, 0.45f, 1f, 0.8f));
            DrawSceneLabel(context, anchor, "Drag Tile Selection");
            return;
        }

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

        if (context.BrushType == LevelEditorBrushType.GateBlocker)
        {
            bool canPlaceGateBlocker =
                TryBuildGateFootprint(context, anchor, out List<Vector2Int> gateFootprint, out string gateReason) &&
                CanPlaceFootprint(context, gateFootprint, out gateReason) &&
                !string.IsNullOrWhiteSpace(context.SelectedGateId);
            DrawFootprint(
                context,
                gateFootprint ?? BuildFootprint(null, anchor),
                canPlaceGateBlocker ? new Color(0.45f, 0.15f, 1f, 0.20f) : new Color(1f, 0f, 0f, 0.20f),
                canPlaceGateBlocker ? new Color(0.45f, 0.15f, 1f, 1f) : new Color(1f, 0f, 0f, 1f));
            DrawSceneLabel(
                context,
                anchor,
                canPlaceGateBlocker ? $"{context.SelectedGateId} / {context.SelectedGatePrefabKey}" :
                    string.IsNullOrWhiteSpace(context.SelectedGateId) ? "Gate Id is missing." : gateReason);
            return;
        }

        if (context.BrushType == LevelEditorBrushType.EnemySpawnPoint)
        {
            bool canPlaceEnemySpawnPoint = context.LevelData.IsInsideGrid(anchor) && !string.IsNullOrWhiteSpace(context.SelectedEnemySpawnZoneId);
            DrawFootprint(
                context,
                BuildFootprint(null, anchor),
                canPlaceEnemySpawnPoint ? new Color(1f, 0.55f, 0.05f, 0.20f) : new Color(1f, 0f, 0f, 0.20f),
                canPlaceEnemySpawnPoint ? new Color(1f, 0.55f, 0.05f, 1f) : new Color(1f, 0f, 0f, 1f));
            DrawSceneLabel(context, anchor, canPlaceEnemySpawnPoint ? context.SelectedEnemySpawnZoneId : "Spawn Zone Id is missing.");
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

        if (context.BrushType == LevelEditorBrushType.EnemyGroup)
        {
            DrawEnemyEncounterZone(
                context,
                anchor,
                canPlace ? new Color(1f, 0.1f, 0.1f, 0.08f) : new Color(1f, 0f, 0f, 0.12f),
                canPlace ? new Color(1f, 0.1f, 0.1f, 0.45f) : new Color(1f, 0f, 0f, 0.75f));
        }

        if (context.BrushType == LevelEditorBrushType.MainEvent)
        {
            DrawMainEventInteractionZone(
                context,
                anchor,
                canPlace ? new Color(0.2f, 0.9f, 1f, 0.08f) : new Color(1f, 0f, 0f, 0.12f),
                canPlace ? new Color(0.2f, 0.9f, 1f, 0.45f) : new Color(1f, 0f, 0f, 0.75f));
        }

        DrawFootprint(context, footprint, fill, outline);

        if (!canPlace)
            DrawSceneLabel(context, anchor, reason);
    }

    private void PlaceAtGrid(LevelEditorContext context, Vector2Int anchor)
    {
        if (context.BrushType == LevelEditorBrushType.GroundTileErase)
        {
            if (lastEditedGroundTileGrid.HasValue && lastEditedGroundTileGrid.Value == anchor)
                return;

            Undo.RecordObject(context.LevelData, "Erase GroundTile");
            context.LevelData.EraseGroundTileAt(anchor);
            sceneStatus = $"Erased GroundTile at {anchor}.";
            CommitLevelDataChange(context);
            return;
        }

        if (context.BrushType == LevelEditorBrushType.GroundTile)
        {
            if (lastEditedGroundTileGrid.HasValue && lastEditedGroundTileGrid.Value == anchor)
                return;

            if (!CanPaintGroundTile(context, out string tileReason))
            {
                sceneStatus = tileReason;
                Repaint();
                return;
            }

            Undo.RecordObject(context.LevelData, "Paint GroundTile");
            context.LevelData.SetGroundTile(anchor, context.SelectedTileKey);
            sceneStatus = $"Painted {context.SelectedTileKey} at {anchor}.";
            CommitLevelDataChange(context);
            return;
        }

        if (context.BrushType == LevelEditorBrushType.Obstacle &&
            lastEditedObstacleGrid.HasValue &&
            lastEditedObstacleGrid.Value == anchor)
        {
            return;
        }

        if (context.BrushType == LevelEditorBrushType.GateBlocker)
        {
            if (string.IsNullOrWhiteSpace(context.SelectedGateId))
            {
                sceneStatus = "Gate Id is missing.";
                Repaint();
                return;
            }

            if (!TryBuildGateFootprint(context, anchor, out List<Vector2Int> gateFootprint, out string gateReason)
                || !CanPlaceFootprint(context, gateFootprint, out gateReason))
            {
                sceneStatus = gateReason;
                Repaint();
                return;
            }

            Undo.RecordObject(context.LevelData, "Place Gate");
            context.LevelData.SetGatePlacement(
                context.SelectedGateId,
                context.SelectedGateFirstZoneId,
                context.SelectedGateSecondZoneId,
                anchor,
                context.SelectedGatePrefabKey,
                gateFootprint);
            sceneStatus = $"Placed Gate '{context.SelectedGateId}' ({context.SelectedGatePrefabKey}) at {anchor}.";
            CommitLevelDataChange(context);
            return;
        }

        if (context.BrushType == LevelEditorBrushType.EnemySpawnPoint)
        {
            if (string.IsNullOrWhiteSpace(context.SelectedEnemySpawnZoneId))
            {
                sceneStatus = "Enemy spawn Zone Id is missing.";
                Repaint();
                return;
            }

            Undo.RecordObject(context.LevelData, "Place Enemy Spawn Point");
            context.LevelData.SetEnemySpawnPoint(
                anchor,
                context.SelectedEnemySpawnZoneId,
                context.SelectedEnemySpawnEnemyGroupKey,
                0,
                context.SelectedEnemySpawnChatId,
                0,
                context.SelectedEnemySpawnEncounterChatId,
                string.Empty);
            sceneStatus = string.IsNullOrWhiteSpace(context.SelectedEnemySpawnEnemyGroupKey)
                ? $"Placed EnemySpawnPoint '{context.SelectedEnemySpawnZoneId}' at {anchor}."
                : $"Placed EnemySpawnPoint '{context.SelectedEnemySpawnZoneId}' / '{context.SelectedEnemySpawnEnemyGroupKey}' at {anchor}.";
            CommitLevelDataChange(context);
            return;
        }

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
                context.LevelData.SetItem(anchor, context.SelectedItemResourceType, context.SelectedItemAmount);
                break;
            case LevelEditorBrushType.Outpost:
                context.LevelData.SetOutpost(
                    anchor,
                    context.SelectedOutpostType,
                    context.SelectedOutpostResourcePerTurn,
                    context.SelectedOutpostInitialState);
                break;
            case LevelEditorBrushType.Event:
                context.LevelData.SetEvent(
                    anchor,
                    context.SelectedMapEventType,
                    context.SelectedMapEventRequireAmount,
                    context.SelectedMapEventEffectAmount);
                break;
            case LevelEditorBrushType.MainEvent:
                context.LevelData.SetMainEvent(anchor, context.SelectedMainEventPrefabKey);
                break;
            case LevelEditorBrushType.SubEvent:
                context.LevelData.SetSubEvent(anchor, context.SelectedSubEventPrefabKey);
                break;
            case LevelEditorBrushType.EnemyGroup:
                context.LevelData.SetEnemyPlacement(anchor, context.EnemyGroupKey, context.EnemyBehaviorType);
                break;
            case LevelEditorBrushType.DecorativeObject:
                context.LevelData.SetDecorativeObject(
                    anchor,
                    context.SelectedDecorativeObjectKey);
                break;
            case LevelEditorBrushType.HeroUnion:
                context.LevelData.SetHeroUnion(anchor, context.SelectedHeroUnionPrefabKey);
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
        if (label == "DecorativeObject")
            context.LevelData.RemoveDecorativeObjectAt(anchor);
        else if (label == "MainEvent")
            context.LevelData.RemoveMainEventAt(anchor);
        else if (label == "SubEvent")
            context.LevelData.RemoveSubEventAt(anchor);
        else
            context.LevelData.EraseNonGroundTileAt(anchor);
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
        context.SelectedItemResourceType = controller.SelectedItemResourceType;
        context.SelectedItemAmount = controller.SelectedItemAmount;
        context.SelectedOutpostType = controller.SelectedOutpostType;
        context.SelectedOutpostResourcePerTurn = controller.SelectedOutpostResourcePerTurn;
        context.SelectedOutpostInitialState = controller.SelectedOutpostInitialState;
        context.SelectedMapEventType = controller.SelectedMapEventType;
        context.SelectedMapEventKey = controller.SelectedMapEventKey;
        context.SelectedMapEventRequireAmount = controller.SelectedMapEventRequireAmount;
        context.SelectedMapEventEffectAmount = controller.SelectedMapEventEffectAmount;
        context.EnemyGroupKey = controller.EnemyGroupKey;
        context.EnemyBehaviorType = controller.EnemyBehaviorType;
        context.TileRegistry = controller.TileRegistry;
        context.SelectedTileKey = controller.SelectedTileKey;
        context.SelectedHeroUnionPrefabKey = controller.SelectedHeroUnionPrefabKey;
        context.SelectedDecorativeObjectKey = controller.SelectedDecorativeObjectKey;
        context.SelectedMainEventPrefabKey = controller.SelectedMainEventPrefabKey;
        context.SelectedSubEventPrefabKey = controller.SelectedSubEventPrefabKey;
        context.SelectedGatePrefabKey = controller.SelectedGatePrefabKey;
        context.SelectedGateId = controller.SelectedGateId;
        context.SelectedGateFirstZoneId = controller.SelectedGateFirstZoneId;
        context.SelectedGateSecondZoneId = controller.SelectedGateSecondZoneId;
        context.SelectedEnemySpawnZoneId = controller.SelectedEnemySpawnZoneId;
        context.SelectedEnemySpawnEnemyGroupKey = controller.SelectedEnemySpawnEnemyGroupKey;
        context.SelectedEnemySpawnChatZoneId = controller.SelectedEnemySpawnChatZoneId;
        context.SelectedEnemySpawnChatId = controller.SelectedEnemySpawnChatId;
        context.SelectedEnemySpawnEncounterChatZoneId = controller.SelectedEnemySpawnEncounterChatZoneId;
        context.SelectedEnemySpawnEncounterChatId = controller.SelectedEnemySpawnEncounterChatId;
        context.SelectedEnemySpawnEventBattleKey = controller.SelectedEnemySpawnEventBattleKey;
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

    private bool HandleTileClipboardShortcuts(
        LevelEditorContext context,
        Event currentEvent,
        bool hasHoveredGrid,
        Vector2Int hoveredGrid)
    {
        if (currentEvent.type != EventType.KeyDown)
            return false;

        bool modifierPressed = currentEvent.control || currentEvent.command;
        if (modifierPressed && currentEvent.keyCode == KeyCode.C)
        {
            CopySelectedTiles(context);
            currentEvent.Use();
            return true;
        }

        if (modifierPressed && currentEvent.keyCode == KeyCode.V)
        {
            if (hasHoveredGrid)
                PasteTilesAt(context, hoveredGrid);
            else
                sceneStatus = "Move the mouse over the grid to paste tiles.";

            currentEvent.Use();
            return true;
        }

        if (currentEvent.keyCode == KeyCode.Escape)
        {
            ClearTileSelection();
            sceneStatus = "Tile selection cleared.";
            currentEvent.Use();
            return true;
        }

        return false;
    }

    private void BeginOrUpdateTileSelection(Vector2Int grid, bool begin)
    {
        if (begin || !tileSelectionStart.HasValue)
            tileSelectionStart = grid;

        tileSelectionEnd = grid;
        isSelectingTiles = true;
        sceneStatus = $"Tile selection : {GetSelectionMin()} ~ {GetSelectionMax()}";
        Repaint();
        SceneView.RepaintAll();
    }

    private void CopySelectedTiles(LevelEditorContext context)
    {
        if (!HasTileSelection())
        {
            sceneStatus = "Select a tile area before copying.";
            Repaint();
            return;
        }

        Vector2Int min = GetSelectionMin();
        Vector2Int max = GetSelectionMax();
        TileClipboard clipboard = new TileClipboard
        {
            SourceLevelData = context.LevelData,
            Size = new Vector2Int(max.x - min.x + 1, max.y - min.y + 1)
        };

        IReadOnlyList<TilePlacementData> placements = context.LevelData.GroundTilePlacements;
        for (int i = 0; i < placements.Count; i++)
        {
            TilePlacementData placement = placements[i];
            Vector2Int grid = placement.GridPosition;
            if (grid.x < min.x || grid.x > max.x || grid.y < min.y || grid.y > max.y)
                continue;

            if (string.IsNullOrWhiteSpace(placement.TileKey))
                continue;

            clipboard.Tiles.Add(new CopiedTile(grid - min, placement.TileKey));
        }

        tileClipboard = clipboard;
        sceneStatus = $"Copied tile area {clipboard.Size.x}x{clipboard.Size.y}. Tiles : {clipboard.Tiles.Count}.";
        Repaint();
    }

    private void PasteTilesAt(LevelEditorContext context, Vector2Int anchor)
    {
        if (tileClipboard == null)
        {
            sceneStatus = "Tile clipboard is empty.";
            Repaint();
            return;
        }

        if (tileClipboard.SourceLevelData != context.LevelData)
        {
            sceneStatus = "Tile clipboard can only paste into the source LevelData.";
            Repaint();
            return;
        }

        int pastedCount = 0;
        Undo.RecordObject(context.LevelData, "Paste GroundTiles");

        for (int i = 0; i < tileClipboard.Tiles.Count; i++)
        {
            CopiedTile copiedTile = tileClipboard.Tiles[i];
            Vector2Int targetGrid = anchor + copiedTile.Offset;
            if (!context.LevelData.IsInsideGrid(targetGrid))
                continue;

            context.LevelData.SetGroundTile(targetGrid, copiedTile.TileKey);
            pastedCount++;
        }

        if (pastedCount == 0)
        {
            sceneStatus = "No copied tiles were inside the grid.";
            Repaint();
            return;
        }

        sceneStatus = $"Pasted {pastedCount} tile(s) at {anchor}.";
        CommitLevelDataChange(context);
    }

    private void DrawTileSelection(LevelEditorContext context)
    {
        if (!HasTileSelection())
            return;

        DrawBounds(
            context,
            GetSelectionMin(),
            GetSelectionMax(),
            new Color(0.15f, 0.45f, 1f, 0.12f),
            new Color(0.15f, 0.45f, 1f, 1f));
    }

    private void DrawClipboardPreview(LevelEditorContext context, Vector2Int anchor)
    {
        if (tileClipboard == null || tileClipboard.SourceLevelData != context.LevelData)
            return;

        bool hasInsideCell = false;
        bool hasOutsideCell = false;

        for (int y = 0; y < tileClipboard.Size.y; y++)
        {
            for (int x = 0; x < tileClipboard.Size.x; x++)
            {
                Vector2Int grid = anchor + new Vector2Int(x, y);
                if (context.LevelData.IsInsideGrid(grid))
                    hasInsideCell = true;
                else
                    hasOutsideCell = true;
            }
        }

        if (!hasInsideCell)
        {
            DrawCell(context, anchor, new Color(1f, 0f, 0f, 0.16f), new Color(1f, 0f, 0f, 1f));
            DrawSceneLabel(context, anchor, "Paste outside grid");
            return;
        }

        Color fill = hasOutsideCell
            ? new Color(1f, 0.85f, 0.15f, 0.12f)
            : new Color(0.2f, 1f, 0.3f, 0.12f);
        Color outline = hasOutsideCell
            ? new Color(1f, 0.85f, 0.15f, 1f)
            : new Color(0.2f, 1f, 0.3f, 1f);

        for (int y = 0; y < tileClipboard.Size.y; y++)
        {
            for (int x = 0; x < tileClipboard.Size.x; x++)
            {
                Vector2Int grid = anchor + new Vector2Int(x, y);
                if (!context.LevelData.IsInsideGrid(grid))
                    continue;

                DrawCell(context, grid, fill, outline);
            }
        }

        DrawSceneLabel(context, anchor, hasOutsideCell ? "Paste Tiles Partial" : "Paste Tiles");
    }

    private void ClearTileSelection()
    {
        tileSelectionStart = null;
        tileSelectionEnd = null;
        isSelectingTiles = false;
        Repaint();
        SceneView.RepaintAll();
    }

    private bool HasTileSelection()
    {
        return tileSelectionStart.HasValue && tileSelectionEnd.HasValue;
    }

    private Vector2Int GetSelectionMin()
    {
        Vector2Int start = tileSelectionStart ?? Vector2Int.zero;
        Vector2Int end = tileSelectionEnd ?? start;
        return new Vector2Int(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y));
    }

    private Vector2Int GetSelectionMax()
    {
        Vector2Int start = tileSelectionStart ?? Vector2Int.zero;
        Vector2Int end = tileSelectionEnd ?? start;
        return new Vector2Int(Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
    }

    private static bool HasBrushPrefab(LevelEditorContext context, out string reason)
    {
        if (context.BrushType == LevelEditorBrushType.Erase ||
            IsPrefablessBrush(context.BrushType))
        {
            reason = null;
            return true;
        }

        return TryGetBrushPrefab(context, out _, out reason);
    }

    private static bool IsGroundTileBrush(LevelEditorBrushType brushType)
    {
        return brushType == LevelEditorBrushType.GroundTile ||
            brushType == LevelEditorBrushType.GroundTileErase;
    }

    private static bool IsPrefablessBrush(LevelEditorBrushType brushType)
    {
        return IsGroundTileBrush(brushType) ||
            brushType == LevelEditorBrushType.TileSelection ||
            brushType == LevelEditorBrushType.EnemySpawnPoint;
    }

    private static bool CanPaintGroundTile(LevelEditorContext context, out string reason)
    {
        if (context.TileRegistry == null)
        {
            reason = "LevelTileRegistry is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(context.SelectedTileKey))
        {
            reason = "Tile Key is missing.";
            return false;
        }

        if (!context.TileRegistry.TryGetSprite(context.SelectedTileKey, out Sprite sprite) || sprite == null)
        {
            reason = $"Tile key '{context.SelectedTileKey}' was not found.";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool TryBuildBrushFootprint(
        LevelEditorContext context,
        Vector2Int anchor,
        out List<Vector2Int> footprint,
        out string reason)
    {
        footprint = null;

        if (context.BrushType == LevelEditorBrushType.DecorativeObject)
        {
            if (!TryGetBrushPrefab(context, out _, out reason))
                return false;

            footprint = BuildFootprint(null, anchor);
            return true;
        }

        if (context.BrushType == LevelEditorBrushType.GateBlocker)
            return TryBuildGateFootprint(context, anchor, out footprint, out reason);

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
                prefab = GetItemPrefab(context, context.SelectedItemResourceType);
                reason = prefab == null ? $"Item prefab is missing for {context.SelectedItemResourceType}." : null;
                return prefab != null;
            case LevelEditorBrushType.Outpost:
                prefab = GetOutpostPrefab(context, context.SelectedOutpostType);
                reason = prefab == null ? $"Outpost prefab is missing for {context.SelectedOutpostType}." : null;
                return prefab != null;
            case LevelEditorBrushType.Event:
                prefab = GetEventPrefab(context, context.SelectedMapEventType);
                reason = prefab == null ? $"Event prefab is missing for {context.SelectedMapEventKey}." : null;
                return prefab != null;
            case LevelEditorBrushType.MainEvent:
                if (string.IsNullOrWhiteSpace(context.SelectedMainEventPrefabKey))
                {
                    reason = "MainEvent prefab key is missing.";
                    return false;
                }

                prefab = GetMainEventPrefab(context, context.SelectedMainEventPrefabKey);
                reason = prefab == null ? $"MainEvent prefab is missing for {context.SelectedMainEventPrefabKey}." : null;
                return prefab != null;
            case LevelEditorBrushType.SubEvent:
                if (string.IsNullOrWhiteSpace(context.SelectedSubEventPrefabKey))
                {
                    reason = "SubEvent prefab key is missing.";
                    return false;
                }

                prefab = GetSubEventPrefab(context, context.SelectedSubEventPrefabKey);
                reason = prefab == null ? $"SubEvent prefab is missing for {context.SelectedSubEventPrefabKey}." : null;
                return prefab != null;
            case LevelEditorBrushType.GateBlocker:
                if (string.IsNullOrWhiteSpace(context.SelectedGatePrefabKey))
                {
                    reason = "Gate prefab key is missing.";
                    return false;
                }

                prefab = GetGatePrefab(context, context.SelectedGatePrefabKey);
                reason = prefab == null ? $"Gate prefab is missing for {context.SelectedGatePrefabKey}." : null;
                return prefab != null;
            case LevelEditorBrushType.HeroUnion:
                prefab = GetHeroUnionPrefab(context, context.SelectedHeroUnionPrefabKey);
                reason = prefab == null ? "HeroUnion prefab is missing." : null;
                return prefab != null;
            case LevelEditorBrushType.VillainUnion:
                prefab = GetVillainUnionPrefab(context);
                reason = prefab == null ? "VillainUnion prefab is missing." : null;
                return prefab != null;
            case LevelEditorBrushType.EnemyGroup:
                prefab = GetEnemyGroupPrefab(context);
                reason = prefab == null ? "EnemyGroup prefab is missing." : null;
                return prefab != null;
            case LevelEditorBrushType.DecorativeObject:
                if (string.IsNullOrWhiteSpace(context.SelectedDecorativeObjectKey))
                {
                    reason = "Decorative prefab key is missing.";
                    return false;
                }

                prefab = GetDecorativeObjectPrefab(context, context.SelectedDecorativeObjectKey);
                if (prefab == null)
                {
                    reason = $"Decorative prefab is missing for {context.SelectedDecorativeObjectKey}.";
                    return false;
                }

                if (prefab.GetComponent<DecorativeObjectPlacement>() == null)
                {
                    reason = $"Decorative prefab '{context.SelectedDecorativeObjectKey}' needs a DecorativeObjectPlacement component.";
                    return false;
                }

                reason = null;
                return true;
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

        if (context.BrushType == LevelEditorBrushType.HeroUnion && context.LevelData.HeroUnionPlacement.HasPlacement)
        {
            reason = "HeroUnion is already placed. Erase it first.";
            return false;
        }

        if (context.BrushType == LevelEditorBrushType.VillainUnion && context.LevelData.VillainUnionPlacement.HasPlacement)
        {
            reason = "VillainUnion is already placed. Erase it first.";
            return false;
        }

        if (context.BrushType == LevelEditorBrushType.DecorativeObject)
        {
            reason = null;
            return true;
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
            if (FootprintsOverlap(footprint, BuildFootprint(GetEventPrefab(context, placement.EventType), placement.GridPosition)))
            {
                reason = "Event overlaps this footprint.";
                return true;
            }
        }

        for (int i = 0; i < levelData.MainEventPlacements.Count; i++)
        {
            MainEventPlacementData placement = levelData.MainEventPlacements[i];
            if (FootprintsOverlap(footprint, BuildFootprint(GetMainEventPrefab(context, placement.PrefabKey), placement.GridPosition)))
            {
                reason = "MainEvent overlaps this footprint.";
                return true;
            }
        }

        for (int i = 0; i < levelData.SubEventPlacements.Count; i++)
        {
            SubEventPlacementData placement = levelData.SubEventPlacements[i];
            if (FootprintsOverlap(footprint, BuildFootprint(GetSubEventPrefab(context, placement.PrefabKey), placement.GridPosition)))
            {
                reason = "SubEvent overlaps this footprint.";
                return true;
            }
        }

        for (int i = 0; i < levelData.EnemyPlacements.Count; i++)
        {
            EnemyPlacementData placement = levelData.EnemyPlacements[i];
            if (FootprintsOverlap(footprint, BuildFootprint(GetEnemyGroupPrefab(context), placement.GridPosition)))
            {
                reason = "EnemyGroup overlaps this footprint.";
                return true;
            }
        }

        if (context.BrushType != LevelEditorBrushType.Obstacle)
        {
            for (int i = 0; i < levelData.DecorativeObjectPlacements.Count; i++)
            {
                DecorativeObjectPlacementData placement = levelData.DecorativeObjectPlacements[i];
                if (FootprintsOverlap(footprint, BuildFootprint(null, placement.GridPosition)))
                {
                    reason = "DecorativeObject overlaps this footprint.";
                    return true;
                }
            }
        }

        for (int i = 0; i < levelData.GatePlacements.Count; i++)
        {
            GatePlacementData placement = levelData.GatePlacements[i];
            IReadOnlyList<Vector2Int> blockerCells = placement.BlockerCells;
            for (int cellIndex = 0; cellIndex < blockerCells.Count; cellIndex++)
            {
                if (!FootprintsOverlap(footprint, BuildFootprint(null, blockerCells[cellIndex])))
                    continue;

                reason = "GateBlocker overlaps this footprint.";
                return true;
            }
        }

        for (int i = 0; i < levelData.EnemySpawnPointPlacements.Count; i++)
        {
            EnemySpawnPointPlacementData placement = levelData.EnemySpawnPointPlacements[i];
            if (FootprintsOverlap(footprint, BuildFootprint(null, placement.GridPosition)))
            {
                reason = "EnemySpawnPoint overlaps this footprint.";
                return true;
            }
        }

        if (levelData.HeroUnionPlacement.HasPlacement
            && FootprintsOverlap(footprint, BuildFootprint(GetHeroUnionPrefab(context, levelData.HeroUnionPlacement.PrefabKey), levelData.HeroUnionPlacement.GridPosition)))
        {
            reason = "HeroUnion overlaps this footprint.";
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

        if (levelData.HeroUnionPlacement.HasPlacement)
        {
            anchor = levelData.HeroUnionPlacement.GridPosition;
            footprint = BuildFootprint(GetHeroUnionPrefab(context, levelData.HeroUnionPlacement.PrefabKey), anchor);
            if (footprint.Contains(grid))
            {
                label = "HeroUnion";
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
            footprint = BuildFootprint(GetEventPrefab(context, placement.EventType), anchor);
            if (footprint.Contains(grid))
            {
                label = "Event";
                return true;
            }
        }

        for (int i = 0; i < levelData.MainEventPlacements.Count; i++)
        {
            MainEventPlacementData placement = levelData.MainEventPlacements[i];
            anchor = placement.GridPosition;
            footprint = BuildFootprint(GetMainEventPrefab(context, placement.PrefabKey), anchor);
            if (footprint.Contains(grid))
            {
                label = "MainEvent";
                return true;
            }
        }

        for (int i = 0; i < levelData.SubEventPlacements.Count; i++)
        {
            SubEventPlacementData placement = levelData.SubEventPlacements[i];
            anchor = placement.GridPosition;
            footprint = BuildFootprint(GetSubEventPrefab(context, placement.PrefabKey), anchor);
            if (footprint.Contains(grid))
            {
                label = "SubEvent";
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

        for (int i = 0; i < levelData.EnemyPlacements.Count; i++)
        {
            EnemyPlacementData placement = levelData.EnemyPlacements[i];
            anchor = placement.GridPosition;
            footprint = BuildFootprint(GetEnemyGroupPrefab(context), anchor);
            if (footprint.Contains(grid))
            {
                label = $"EnemyGroup {placement.EnemyGroupKey}";
                return true;
            }
        }

        for (int i = 0; i < levelData.DecorativeObjectPlacements.Count; i++)
        {
            DecorativeObjectPlacementData placement = levelData.DecorativeObjectPlacements[i];
            anchor = placement.GridPosition;
            footprint = BuildFootprint(null, anchor);
            if (footprint.Contains(grid))
            {
                label = "DecorativeObject";
                return true;
            }
        }

        for (int i = 0; i < levelData.GatePlacements.Count; i++)
        {
            GatePlacementData placement = levelData.GatePlacements[i];
            IReadOnlyList<Vector2Int> blockerCells = placement.BlockerCells;
            for (int cellIndex = 0; cellIndex < blockerCells.Count; cellIndex++)
            {
                anchor = blockerCells[cellIndex];
                footprint = BuildFootprint(null, anchor);
                if (footprint.Contains(grid))
                {
                    label = $"GateBlocker {placement.GateId}";
                    return true;
                }
            }
        }

        for (int i = 0; i < levelData.EnemySpawnPointPlacements.Count; i++)
        {
            EnemySpawnPointPlacementData placement = levelData.EnemySpawnPointPlacements[i];
            anchor = placement.GridPosition;
            footprint = BuildFootprint(null, anchor);
            if (footprint.Contains(grid))
            {
                label = $"EnemySpawnPoint {placement.ZoneId}";
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

    private static bool TryBuildGateFootprint(
        LevelEditorContext context,
        Vector2Int anchor,
        out List<Vector2Int> footprint,
        out string reason)
    {
        footprint = null;

        GateFootprint prefab = GetGateFootprintPrefab(context, context.SelectedGatePrefabKey);
        if (prefab == null)
        {
            reason = string.IsNullOrWhiteSpace(context.SelectedGatePrefabKey)
                ? "Gate prefab key is missing."
                : $"Gate prefab is missing for {context.SelectedGatePrefabKey}.";
            return false;
        }

        footprint = new List<Vector2Int>();
        prefab.CollectOccupiedCells(anchor, footprint);
        reason = null;
        return footprint.Count > 0;
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

    private static GameObject GetEventPrefab(LevelEditorContext context, MapEventType eventType)
    {
        if (context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetEventPrefab(eventType, out MapEventObject prefab)
            && prefab != null)
        {
            return prefab.gameObject;
        }

        return null;
    }

    private static GameObject GetMainEventPrefab(LevelEditorContext context, string prefabKey)
    {
        return context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetMainEventPrefab(prefabKey, out MainEventObject prefab)
            && prefab != null
                ? prefab.gameObject
                : null;
    }

    private static GameObject GetSubEventPrefab(LevelEditorContext context, string prefabKey)
    {
        return context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetSubEventPrefab(prefabKey, out SubEventObject prefab)
            && prefab != null
            ? prefab.gameObject
            : null;
    }

    private static GameObject GetGatePrefab(LevelEditorContext context, string prefabKey)
    {
        GateFootprint prefab = GetGateFootprintPrefab(context, prefabKey);
        return prefab != null ? prefab.gameObject : null;
    }

    private static GateFootprint GetGateFootprintPrefab(LevelEditorContext context, string prefabKey)
    {
        return context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetGatePrefab(prefabKey, out GateFootprint prefab)
            && prefab != null
            ? prefab
            : null;
    }

    private static GameObject GetHeroUnionPrefab(LevelEditorContext context, string prefabKey)
    {
        return context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetHeroUnionPrefab(prefabKey, out HeroUnionUnit prefab)
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

    private static GameObject GetEnemyGroupPrefab(LevelEditorContext context)
    {
        return context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetEnemyGroupPrefab(out EnemyGridMover prefab)
            && prefab != null
                ? prefab.gameObject
                : null;
    }

    private static GameObject GetDecorativeObjectPrefab(LevelEditorContext context, string prefabKey)
    {
        return context.PrefabRegistry != null
            && context.PrefabRegistry.TryGetDecorativeObjectPrefab(prefabKey, out GameObject prefab)
                ? prefab
                : null;
    }

    private static void DrawEnemyEncounterZone(LevelEditorContext context, Vector2Int anchor, Color fill, Color outline)
    {
        DrawFootprint(context, BuildEncounterZone(anchor), fill, outline);
    }

    private static void DrawMainEventInteractionZone(LevelEditorContext context, Vector2Int anchor, Color fill, Color outline)
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

    private static void DrawBounds(LevelEditorContext context, Vector2Int min, Vector2Int max, Color fill, Color outline)
    {
        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                Vector2Int grid = new Vector2Int(x, y);
                if (!context.LevelData.IsInsideGrid(grid))
                    continue;

                bool edge = x == min.x || x == max.x || y == min.y || y == max.y;
                DrawCell(context, grid, fill, edge ? outline : new Color(outline.r, outline.g, outline.b, outline.a * 0.35f));
            }
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

    private static void DrawSpritePreview(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
            return;

        Rect textureRect = sprite.textureRect;
        Rect uv = new Rect(
            textureRect.x / sprite.texture.width,
            textureRect.y / sprite.texture.height,
            textureRect.width / sprite.texture.width,
            textureRect.height / sprite.texture.height);

        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
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

    private static int GetBrushIndex(LevelEditorBrushType[] brushes, LevelEditorBrushType brushType)
    {
        if (brushes == null)
            return -1;

        for (int i = 0; i < brushes.Length; i++)
        {
            if (brushes[i] == brushType)
                return i;
        }

        return -1;
    }

    private struct LevelEditorContext
    {
        public LevelData LevelData;
        public LevelLoader LevelLoader;
        public GridManager GridManager;
        public LevelPrefabRegistry PrefabRegistry;
        public Camera InputCamera;
        public LevelEditorBrushType BrushType;
        public ResourceType SelectedItemResourceType;
        public int SelectedItemAmount;
        public OutpostType SelectedOutpostType;
        public int SelectedOutpostResourcePerTurn;
        public OutpostState SelectedOutpostInitialState;
        public MapEventType SelectedMapEventType;
        public string SelectedMapEventKey;
        public int SelectedMapEventRequireAmount;
        public int SelectedMapEventEffectAmount;
        public string EnemyGroupKey;
        public EnemyBehaviorType EnemyBehaviorType;
        public LevelTileRegistry TileRegistry;
        public string SelectedTileKey;
        public string SelectedHeroUnionPrefabKey;
        public string SelectedDecorativeObjectKey;
        public string SelectedMainEventPrefabKey;
        public string SelectedSubEventPrefabKey;
        public string SelectedGatePrefabKey;
        public string SelectedGateId;
        public string SelectedGateFirstZoneId;
        public string SelectedGateSecondZoneId;
        public string SelectedEnemySpawnZoneId;
        public string SelectedEnemySpawnEnemyGroupKey;
        public int SelectedEnemySpawnChatZoneId;
        public int SelectedEnemySpawnChatId;
        public int SelectedEnemySpawnEncounterChatZoneId;
        public int SelectedEnemySpawnEncounterChatId;
        public string SelectedEnemySpawnEventBattleKey;
        public bool ApplyLevelAfterEdit;
        public LayerMask GroundMask;
    }
}
