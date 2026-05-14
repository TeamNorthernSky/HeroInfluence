using System;
using UnityEngine;

public class MapEventObject : MonoBehaviour
{
    public static event Action<MapEventObject> EventInteracted;

    [SerializeField] private string eventKey = "event_001";

    private MapEventRegistry eventRegistry;

    public string EventKey => eventKey;

    private void OnEnable()
    {
        ResolveRegistry();
        eventRegistry?.Register(this);
    }

    private void OnDisable()
    {
        eventRegistry?.Unregister(this);
    }

    public void ApplyInitialData(string nextEventKey)
    {
        if (string.IsNullOrWhiteSpace(nextEventKey))
            return;

        eventKey = nextEventKey;
    }

    public void Interact()
    {
        EventInteracted?.Invoke(this);
    }

    public Vector2Int GetCurrentGrid(GridManager gridManager)
    {
        return gridManager != null
            ? gridManager.WorldToGrid(transform.position)
            : Vector2Int.zero;
    }

    public bool OccupiesGrid(Vector2Int grid, GridManager gridManager)
    {
        return GetCurrentGrid(gridManager) == grid;
    }

    private void ResolveRegistry()
    {
        if (eventRegistry == null)
            eventRegistry = FindFirstObjectByType<MapEventRegistry>();
    }
}
