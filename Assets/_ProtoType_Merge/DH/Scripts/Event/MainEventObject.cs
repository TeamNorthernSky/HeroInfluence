using System;
using System.Collections.Generic;
using UnityEngine;

public class MainEventObject : MonoBehaviour
{
    private const string BattleResultKey = "Flag_BattleResult";

    [Header("Chat")]
    [SerializeField] private string eventKey = "main_event_001";
    [SerializeField] private int zoneId = 1;
    [SerializeField] private int chatId;

    [Header("Interaction")]
    [SerializeField] private GridManager gridManager;
    [SerializeField, Min(1)] private int interactionRadius = 1;

    private MainEventRegistry mainEventRegistry;
    private bool isTriggering;
    private bool eventBattleRequestedDuringTrigger;
    private Action<MainEventObject> pendingClosedCallback;

    public string EventKey => string.IsNullOrWhiteSpace(eventKey) ? name : eventKey.Trim();
    public int ZoneId => Mathf.Max(1, zoneId);
    public int ChatId => Mathf.Max(0, chatId);
    public int InteractionRadius => Mathf.Max(1, interactionRadius);

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

        mainEventRegistry?.Register(this);
    }

    private void OnDisable()
    {
        mainEventRegistry?.Unregister(this);
    }

    private void OnValidate()
    {
        zoneId = Mathf.Max(1, zoneId);
        interactionRadius = Mathf.Max(1, interactionRadius);
    }

    public Vector2Int GetCurrentGrid(GridManager targetGridManager = null)
    {
        GridManager resolvedGridManager = targetGridManager != null ? targetGridManager : ResolveGridManager();
        return resolvedGridManager != null ? resolvedGridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    public IReadOnlyList<Vector2Int> GetInteractionCells(GridManager targetGridManager = null)
    {
        Vector2Int origin = GetCurrentGrid(targetGridManager);
        int radius = InteractionRadius;
        List<Vector2Int> cells = new List<Vector2Int>((radius * 2 + 1) * (radius * 2 + 1) - 1);

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x == 0 && y == 0)
                    continue;

                cells.Add(origin + new Vector2Int(x, y));
            }
        }

        return cells;
    }

    public bool IsInteractionCell(Vector2Int grid, GridManager targetGridManager = null)
    {
        Vector2Int origin = GetCurrentGrid(targetGridManager);
        int dx = Mathf.Abs(grid.x - origin.x);
        int dy = Mathf.Abs(grid.y - origin.y);
        return dx <= InteractionRadius && dy <= InteractionRadius && (dx != 0 || dy != 0);
    }

    public bool OccupiesGrid(Vector2Int grid, GridManager targetGridManager = null)
    {
        MultiGridOccupant occupant = GetComponent<MultiGridOccupant>();
        if (occupant != null)
            return occupant.OccupiesCell(grid);

        return GetCurrentGrid(targetGridManager) == grid;
    }

    public bool TryTrigger(PartyGridMover party, Action<MainEventObject> closedCallback = null)
    {
        if (!Application.isPlaying || isTriggering || IsCompleted())
            return false;

        if (ChatId <= 0)
        {
            Debug.LogWarning($"[MainEventObject] {EventKey}: Chat ID is missing.", this);
            return false;
        }

        ChatManager chatManager = ChatManager.Instance;
        if (chatManager == null)
        {
            Debug.LogWarning($"[MainEventObject] {EventKey}: ChatManager was not found.", this);
            return false;
        }

        if (chatManager.IsRunning)
            return false;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null || !catalog.TryGetChat(ZoneId, ChatId, out ChatDBEventData chat) || chat == null)
        {
            Debug.LogWarning($"[MainEventObject] {EventKey}: Chat data was not found. Zone: {ZoneId}, Chat_ID: {ChatId}", this);
            return false;
        }

        isTriggering = true;
        eventBattleRequestedDuringTrigger = false;
        pendingClosedCallback = closedCallback;
        SubscribeEventBattleRequested();
        ChatModalController.Show(ZoneId, ChatId, HandleChatClosed);

        if (!chatManager.IsRunning)
        {
            isTriggering = false;
            eventBattleRequestedDuringTrigger = false;
            pendingClosedCallback = null;
            UnsubscribeEventBattleRequested();
            return false;
        }

        return true;
    }

    private void HandleChatClosed()
    {
        isTriggering = false;
        UnsubscribeEventBattleRequested();
        Action<MainEventObject> closedCallback = pendingClosedCallback;
        pendingClosedCallback = null;

        if (ShouldRemainAfterEventBattleDefeat())
        {
            eventBattleRequestedDuringTrigger = false;
            closedCallback?.Invoke(this);
            return;
        }

        eventBattleRequestedDuringTrigger = false;
        MarkCompleted();
        gameObject.SetActive(false);
        closedCallback?.Invoke(this);
    }

    private void SubscribeEventBattleRequested()
    {
        DHEventEffectRuntimeManager effectManager = DHEventEffectRuntimeManager.EnsureInstance();
        effectManager.EventBattleRequested -= HandleEventBattleRequested;
        effectManager.EventBattleRequested += HandleEventBattleRequested;
    }

    private void UnsubscribeEventBattleRequested()
    {
        DHEventEffectRuntimeManager effectManager = DHEventEffectRuntimeManager.Instance;
        if (effectManager != null)
            effectManager.EventBattleRequested -= HandleEventBattleRequested;
    }

    private void HandleEventBattleRequested(DHEventBattleEffectRequest request)
    {
        eventBattleRequestedDuringTrigger = true;
    }

    private bool ShouldRemainAfterEventBattleDefeat()
    {
        if (!eventBattleRequestedDuringTrigger)
            return false;

        DHEventStateRepository eventStateRepository = DHEventStateRepository.Instance;
        return eventStateRepository != null &&
            eventStateRepository.TryGetNumericValue(BattleResultKey, out float battleResult) &&
            Mathf.Approximately(battleResult, 0f);
    }

    private bool IsCompleted()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        return repository != null && repository.IsMainEventCompleted(EventKey);
    }

    private void MarkCompleted()
    {
        MapProgressRepository repository = MapProgressRepository.Instance;
        repository?.MarkMainEventCompleted(EventKey);
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

        if (mainEventRegistry == null)
            mainEventRegistry = FindFirstObjectByType<MainEventRegistry>();
    }
}
