using UnityEditor;
using UnityEngine;

public class LevelEditorWindow : EditorWindow
{
    private const string MenuPath = "DH Work/Level Editor";

    private LevelEditorController controller;
    private Vector2 scrollPosition;

    [MenuItem("Window/DH Work/Level Editor")]
    public static void Open()
    {
        LevelEditorWindow window = GetWindow<LevelEditorWindow>("Level Editor");
        window.minSize = new Vector2(340f, 420f);
        window.Show();
    }

    private void OnEnable()
    {
        TryAutoAssignController();
    }

    private void OnHierarchyChange()
    {
        if (controller == null)
            TryAutoAssignController();

        Repaint();
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

        if (controller == null)
        {
            EditorGUILayout.HelpBox(
                "Scene 안의 LevelEditorController를 연결하면 브러시와 설정을 한 곳에서 관리할 수 있습니다.",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
            return;
        }

        DrawControllerInspector();
        DrawQuickActions();
        DrawWarnings();

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
        EditorGUILayout.PropertyField(serializedController.FindProperty("brushType"));
        SerializedProperty brushTypeProperty = serializedController.FindProperty("brushType");
        LevelEditorBrushType brushType = (LevelEditorBrushType)brushTypeProperty.enumValueIndex;

        if (brushType == LevelEditorBrushType.Item)
            EditorGUILayout.PropertyField(serializedController.FindProperty("itemPreset"));

        if (brushType == LevelEditorBrushType.Outpost)
            EditorGUILayout.PropertyField(serializedController.FindProperty("outpostPreset"));

        EditorGUILayout.PropertyField(serializedController.FindProperty("partyId"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Behaviour", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedController.FindProperty("allowRuntimeEditing"));
        EditorGUILayout.PropertyField(serializedController.FindProperty("applyLevelAfterEdit"));
        EditorGUILayout.PropertyField(serializedController.FindProperty("groundMask"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Debug View", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedController.FindProperty("drawEditorGizmos"));

        if (serializedController.ApplyModifiedProperties())
            EditorUtility.SetDirty(controller);
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

        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty levelLoaderProperty = serializedController.FindProperty("levelLoader");
        LevelLoader levelLoader = levelLoaderProperty != null
            ? levelLoaderProperty.objectReferenceValue as LevelLoader
            : null;

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

        SerializedObject serializedController = new SerializedObject(controller);
        LevelData levelData = GetObjectReference<LevelData>(serializedController, "levelData");
        LevelLoader levelLoader = GetObjectReference<LevelLoader>(serializedController, "levelLoader");
        GridManager gridManager = GetObjectReference<GridManager>(serializedController, "gridManager");
        Camera inputCamera = GetObjectReference<Camera>(serializedController, "inputCamera");
        ItemPlacementPreset itemPreset = GetObjectReference<ItemPlacementPreset>(serializedController, "itemPreset");
        OutpostPlacementPreset outpostPreset = GetObjectReference<OutpostPlacementPreset>(serializedController, "outpostPreset");
        SerializedProperty brushTypeProperty = serializedController.FindProperty("brushType");
        LevelEditorBrushType brushType = (LevelEditorBrushType)brushTypeProperty.enumValueIndex;

        if (levelData == null)
            EditorGUILayout.HelpBox("LevelData가 연결되지 않았습니다.", MessageType.Warning);

        if (levelLoader == null)
            EditorGUILayout.HelpBox("LevelLoader가 연결되지 않았습니다.", MessageType.Warning);

        if (gridManager == null)
            EditorGUILayout.HelpBox("GridManager가 연결되지 않았습니다.", MessageType.Warning);

        if (inputCamera == null)
            EditorGUILayout.HelpBox("Input Camera가 비어 있습니다. Main Camera fallback에 의존하게 됩니다.", MessageType.Info);

        if (brushType == LevelEditorBrushType.Item && itemPreset == null)
            EditorGUILayout.HelpBox("Item brush needs an ItemPlacementPreset.", MessageType.Warning);

        if (brushType == LevelEditorBrushType.Outpost && outpostPreset == null)
            EditorGUILayout.HelpBox("Outpost brush needs an OutpostPlacementPreset.", MessageType.Warning);

        if (levelData != null)
        {
            EditorGUILayout.HelpBox(
                $"Grid Range : X {levelData.GridMin.x} ~ {levelData.GridMax.x}, Y {levelData.GridMin.y} ~ {levelData.GridMax.y}",
                MessageType.None);
        }

        if (levelLoader == null)
            return;

        SerializedObject serializedLoader = new SerializedObject(levelLoader);
        PartyRegistry partyRegistry = GetObjectReference<PartyRegistry>(serializedLoader, "partyRegistry");
        LevelPrefabRegistry prefabRegistry = GetObjectReference<LevelPrefabRegistry>(serializedLoader, "prefabRegistry");

        if (partyRegistry == null)
            EditorGUILayout.HelpBox("LevelLoader의 PartyRegistry가 연결되지 않았습니다.", MessageType.Warning);

        if (prefabRegistry == null)
            EditorGUILayout.HelpBox("LevelLoader의 LevelPrefabRegistry가 연결되지 않았습니다.", MessageType.Warning);
    }

    private void TryAutoAssignController()
    {
        controller = FindFirstObjectByType<LevelEditorController>();
    }

    private static T GetObjectReference<T>(SerializedObject serializedObject, string propertyName) where T : Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }
}
