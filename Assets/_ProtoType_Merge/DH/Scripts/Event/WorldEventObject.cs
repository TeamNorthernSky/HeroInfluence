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

    private WorldEventRegistry worldEventRegistry;
    private bool isTriggering;

    public string WorldEventId => string.IsNullOrWhiteSpace(worldEventId) ? string.Empty : worldEventId.Trim();
    public bool OneShot => oneShot;
    public string ProgressKey => BuildProgressKey(WorldEventId);

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
    }

    private static string BuildProgressKey(string eventId)
    {
        string normalizedId = MapProgressKey.NormalizeSegment(eventId);
        return string.IsNullOrWhiteSpace(normalizedId)
            ? "world_event"
            : $"world_event_{normalizedId}";
    }
}

