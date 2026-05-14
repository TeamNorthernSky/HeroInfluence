using System;
using UnityEngine;

public class MapEventObject : MonoBehaviour
{
    public static event Action<MapEventObject> EventInteracted;

    [SerializeField] private string eventKey = "event_001";
    [SerializeField] private ResourceType requireResource = ResourceType.Money;
    [SerializeField] private int requireAmount = 100;

    private MapEventRegistry eventRegistry;

    public string EventKey => eventKey;
    public ResourceType RequireResource => requireResource;
    public int RequireAmount => requireAmount;

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

    // [JC 260514 머지후처리] ResourceManager 직접 의존 폐기 → Game.Economy 사용
    public bool TryExecuteEvent()
    {
        if (Game.Economy == null) return false;

        if (Game.Economy.Spend(requireResource, requireAmount))
        {
            ExecuteEvent();
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    private void ExecuteEvent()
    {
        // TODO: 실제 이벤트 로직 구현
        Debug.Log($"[MapEvent] Executing event '{eventKey}'... Consumed {requireAmount} {requireResource}");
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
