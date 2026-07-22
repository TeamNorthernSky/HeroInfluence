using System;
using UnityEngine;

public class SubEventObject : MonoBehaviour
{
    [Header("Chat")]
    [SerializeField] private string eventKey = "sub_event_001";
    [SerializeField] private int zoneId = 1;
    [SerializeField] private int chatId;

    [Header("Grid")]
    [SerializeField] private GridManager gridManager;

    private SubEventRegistry subEventRegistry;
    private bool isTriggering;
    private Action<SubEventObject> pendingClosedCallback;

    public string EventKey => string.IsNullOrWhiteSpace(eventKey) ? name : eventKey.Trim();
    public int ZoneId => Mathf.Max(1, zoneId);
    public int ChatId => Mathf.Max(0, chatId);

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (Application.isPlaying && IsCompleted())
        {
            gameObject.SetActive(false);
            return;
        }

        subEventRegistry?.Register(this);
    }

    private void OnDisable()
    {
        subEventRegistry?.Unregister(this);
    }

    private void OnValidate()
    {
        zoneId = Mathf.Max(1, zoneId);
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

    public bool TryTrigger(PartyGridMover party, Action<SubEventObject> closedCallback = null)
    {
        if (!Application.isPlaying || isTriggering || IsCompleted())
            return false;

        if (ChatId <= 0)
        {
            Debug.LogWarning($"[SubEventObject] {EventKey}: Chat ID is missing.", this);
            return false;
        }

        ChatManager chatManager = ChatManager.Instance;
        if (chatManager == null)
        {
            Debug.LogWarning($"[SubEventObject] {EventKey}: ChatManager was not found.", this);
            return false;
        }

        if (chatManager.IsRunning)
            return false;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null || !catalog.TryGetChat(ZoneId, ChatId, out ChatDBEventData chat) || chat == null)
        {
            Debug.LogWarning($"[SubEventObject] {EventKey}: Chat data was not found. Zone: {ZoneId}, Chat_ID: {ChatId}", this);
            return false;
        }

        isTriggering = true;
        pendingClosedCallback = closedCallback;
        ChatModalController.Show(ZoneId, ChatId, HandleChatClosed);

        if (!chatManager.IsRunning)
        {
            isTriggering = false;
            pendingClosedCallback = null;
            return false;
        }

        return true;
    }

    private void HandleChatClosed()
    {
        MarkCompleted();
        isTriggering = false;
        Action<SubEventObject> closedCallback = pendingClosedCallback;
        pendingClosedCallback = null;
        gameObject.SetActive(false);
        closedCallback?.Invoke(this);
    }

    private bool IsCompleted()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.IsSubEventCompleted(EventKey);
    }

    private void MarkCompleted()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        repository?.MarkSubEventCompleted(EventKey);
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

        if (subEventRegistry == null)
            subEventRegistry = FindFirstObjectByType<SubEventRegistry>();
    }
}
