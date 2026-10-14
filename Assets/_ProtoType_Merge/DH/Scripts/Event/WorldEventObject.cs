using System;
using System.Collections;
using UnityEngine;

[ExecuteAlways]
public sealed class WorldEventObject : MonoBehaviour
{
    public static event Action<WorldEventObject, PartyGridMover> EventInteracted;
    private const string EditorPreviewVisualName = "__WorldEventNpcPreview";

    [Header("World Event")]
    [SerializeField] private string worldEventId;
    [SerializeField] private bool oneShot = true;

    [Header("Grid")]
    [SerializeField] private GridManager gridManager;

    [Header("NPC Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private LevelPrefabRegistry prefabRegistry;
    [SerializeField] private bool spawnNpcVisualOnEnable = true;

    private WorldEventRegistry worldEventRegistry;
    private GameObject spawnedNpcVisual;
    private Coroutine npcVisualRetryCoroutine;
    private bool isTriggering;
#if UNITY_EDITOR
    private bool editorPreviewRefreshQueued;
#endif

    public string WorldEventId => string.IsNullOrWhiteSpace(worldEventId) ? string.Empty : worldEventId.Trim();
    public bool OneShot => oneShot;
    public string ProgressKey => BuildProgressKey(WorldEventId);
    public int ResolvedNpcType => TryResolveTemplate(out DHWorldEventTemplate template) ? template.NpcType : 0;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (Application.isPlaying && oneShot && IsCompleted())
        {
            gameObject.SetActive(false);
            return;
        }

        worldEventRegistry?.Register(this);

        if (Application.isPlaying && spawnNpcVisualOnEnable)
            RequestNpcVisualRefresh();
        else if (!Application.isPlaying)
            QueueEditorNpcVisualRefresh();
    }

    private void OnDisable()
    {
        worldEventRegistry?.Unregister(this);
        StopNpcVisualRetry();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            QueueEditorNpcVisualRefresh();
    }

    public Vector2Int GetCurrentGrid(GridManager targetGridManager = null)
    {
        GridManager resolvedGridManager = targetGridManager != null ? targetGridManager : ResolveGridManager();
        return resolvedGridManager != null ? resolvedGridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    public bool OccupiesGrid(Vector2Int grid, GridManager targetGridManager = null)
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.OccupiesCell(grid);

        return GetCurrentGrid(targetGridManager) == grid;
    }

    public void ApplyInitialData(string nextWorldEventId)
    {
        worldEventId = string.IsNullOrWhiteSpace(nextWorldEventId) ? string.Empty : nextWorldEventId.Trim();

        if (Application.isPlaying && spawnNpcVisualOnEnable)
            RequestNpcVisualRefresh();
        else if (!Application.isPlaying)
            QueueEditorNpcVisualRefresh();
    }

    public bool TryTrigger(PartyGridMover party, Action<WorldEventObject> closedCallback = null)
    {
        if (!Application.isPlaying || isTriggering)
            return false;

        if (oneShot && IsCompleted())
            return false;

        if (string.IsNullOrWhiteSpace(WorldEventId))
        {
            Debug.LogWarning($"[WorldEventObject] {name}: WorldEventId is missing.", this);
            return false;
        }

        DHWorldEventRuntimeManager runtimeManager = DHWorldEventRuntimeManager.EnsureInstance();
        if (runtimeManager == null)
            return false;

        isTriggering = true;
        EventInteracted?.Invoke(this, party);

        bool started = runtimeManager.TryStartEvent(this, party, () =>
        {
            isTriggering = false;
            closedCallback?.Invoke(this);
        });

        if (!started)
            isTriggering = false;

        return started;
    }

    [ContextMenu("Refresh NPC Visual")]
    public void RefreshNpcVisual()
    {
        if (Application.isPlaying)
            TryRefreshNpcVisual();
        else
            RefreshEditorNpcVisual();
    }

    private void RequestNpcVisualRefresh()
    {
        if (TryRefreshNpcVisual())
            return;

        if (!isActiveAndEnabled || npcVisualRetryCoroutine != null)
            return;

        npcVisualRetryCoroutine = StartCoroutine(RetryRefreshNpcVisual());
    }

    private IEnumerator RetryRefreshNpcVisual()
    {
        const int retryFrames = 8;
        for (int i = 0; i < retryFrames; i++)
        {
            yield return null;

            if (!isActiveAndEnabled)
                break;

            if (TryRefreshNpcVisual())
                break;
        }

        npcVisualRetryCoroutine = null;
    }

    private bool TryRefreshNpcVisual()
    {
        if (!TryResolveTemplate(out DHWorldEventTemplate template) || template.NpcType < 0)
            return false;

        LevelPrefabRegistry registry = ResolvePrefabRegistry();
        if (registry == null || !registry.TryGetWorldEventNpcPrefab(template.NpcType, out GameObject prefab) || prefab == null)
            return false;

        Transform root = visualRoot != null ? visualRoot : transform;
        DestroyNpcVisual(spawnedNpcVisual);
        DestroyNamedChild(root, EditorPreviewVisualName);

        spawnedNpcVisual = CreateNpcVisual(prefab, root, string.Empty, HideFlags.None);
        return true;
    }

    private void QueueEditorNpcVisualRefresh()
    {
#if UNITY_EDITOR
        if (editorPreviewRefreshQueued || !CanUseEditorPreview())
            return;

        editorPreviewRefreshQueued = true;
        UnityEditor.EditorApplication.delayCall += RefreshQueuedEditorNpcVisual;
#endif
    }

#if UNITY_EDITOR
    private void RefreshQueuedEditorNpcVisual()
    {
        editorPreviewRefreshQueued = false;
        if (this == null || Application.isPlaying)
            return;

        RefreshEditorNpcVisual();
    }
#endif

    private void RefreshEditorNpcVisual()
    {
        if (!CanUseEditorPreview())
            return;

        ResolveReferences();

        Transform root = visualRoot != null ? visualRoot : transform;
        DestroyNamedChild(root, EditorPreviewVisualName);

        if (!TryResolveTemplate(out DHWorldEventTemplate template) || template.NpcType < 0)
            return;

        LevelPrefabRegistry registry = ResolvePrefabRegistry();
        if (registry == null || !registry.TryGetWorldEventNpcPrefab(template.NpcType, out GameObject prefab) || prefab == null)
            return;

        spawnedNpcVisual = CreateNpcVisual(
            prefab,
            root,
            EditorPreviewVisualName,
            HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild);
    }

    private bool CanUseEditorPreview()
    {
        return !Application.isPlaying
            && gameObject != null
            && gameObject.scene.IsValid()
            && gameObject.scene.isLoaded;
    }

    private static GameObject CreateNpcVisual(GameObject prefab, Transform root, string overrideName, HideFlags hideFlags)
    {
        GameObject instance = Instantiate(prefab, root);
        if (!string.IsNullOrWhiteSpace(overrideName))
            instance.name = overrideName;

        instance.hideFlags = hideFlags;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        instance.transform.localScale = Vector3.one;
        return instance;
    }

    private static void DestroyNpcVisual(GameObject visual)
    {
        if (visual == null)
            return;

        if (Application.isPlaying)
            Destroy(visual);
        else
            DestroyImmediate(visual);
    }

    private static void DestroyNamedChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null || child.name != childName)
                continue;

            DestroyNpcVisual(child.gameObject);
        }
    }

    private void StopNpcVisualRetry()
    {
        if (npcVisualRetryCoroutine == null)
            return;

        StopCoroutine(npcVisualRetryCoroutine);
        npcVisualRetryCoroutine = null;
    }

    public void Complete()
    {
        if (oneShot)
            MarkCompleted();

        gameObject.SetActive(false);
    }

    private bool IsCompleted()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.IsEventCompleted(ProgressKey);
    }

    private void MarkCompleted()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        repository?.MarkEventCompleted(ProgressKey);
    }

    private GridManager ResolveGridManager()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        return gridManager;
    }

    private void ResolveReferences()
    {
        ResolveGridManager();

        if (worldEventRegistry == null)
            worldEventRegistry = FindFirstObjectByType<WorldEventRegistry>();

        if (visualRoot == null)
            visualRoot = transform;

        ResolvePrefabRegistry();
    }

    private LevelPrefabRegistry ResolvePrefabRegistry()
    {
        if (prefabRegistry == null)
            prefabRegistry = FindFirstObjectByType<LevelPrefabRegistry>();

        return prefabRegistry;
    }

    private bool TryResolveTemplate(out DHWorldEventTemplate template)
    {
        template = null;
        if (string.IsNullOrWhiteSpace(WorldEventId))
            return false;

        DHWorldEventCatalog catalog = DHWorldEventCatalog.Instance != null
            ? DHWorldEventCatalog.Instance
            : FindFirstObjectByType<DHWorldEventCatalog>();
        return catalog != null && catalog.TryGetEvent(WorldEventId, out template) && template != null;
    }

    private static string BuildProgressKey(string eventId)
    {
        string normalizedId = MapProgressKey.NormalizeSegment(eventId);
        return string.IsNullOrWhiteSpace(normalizedId)
            ? "world_event"
            : $"world_event_{normalizedId}";
    }
}

