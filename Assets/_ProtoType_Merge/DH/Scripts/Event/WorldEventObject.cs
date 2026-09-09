using System;
using UnityEngine;

public sealed class WorldEventObject : MonoBehaviour
{
    public static event Action<WorldEventObject, PartyGridMover> EventInteracted;

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
    private bool isTriggering;

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
            RefreshNpcVisual();
    }

    private void OnDisable()
    {
        worldEventRegistry?.Unregister(this);
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
        if (!TryResolveTemplate(out DHWorldEventTemplate template) || template.NpcType <= 0)
            return;

        LevelPrefabRegistry registry = ResolvePrefabRegistry();
        if (registry == null || !registry.TryGetWorldEventNpcPrefab(template.NpcType, out GameObject prefab) || prefab == null)
            return;

        Transform root = visualRoot != null ? visualRoot : transform;
        if (spawnedNpcVisual != null)
        {
            if (Application.isPlaying)
                Destroy(spawnedNpcVisual);
            else
                DestroyImmediate(spawnedNpcVisual);
        }

        spawnedNpcVisual = Instantiate(prefab, root);
        spawnedNpcVisual.transform.localPosition = Vector3.zero;
        spawnedNpcVisual.transform.localRotation = Quaternion.identity;
        spawnedNpcVisual.transform.localScale = Vector3.one;
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

        DHWorldEventCatalog catalog = DHWorldEventCatalog.Instance;
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

