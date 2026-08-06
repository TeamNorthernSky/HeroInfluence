using System;
using UnityEngine;

public class SubEventObject : MonoBehaviour
{
    private const string BattleResultKey = "Flag_BattleResult";

    [Header("Chat")]
    [SerializeField] private string eventKey = "sub_event_001";
    [SerializeField] private int zoneId = 1;
    [SerializeField] private int chatId;

    [Header("Grid")]
    [SerializeField] private GridManager gridManager;

    private SubEventRegistry subEventRegistry;
    private bool isTriggering;
    private bool eventBattleRequestedDuringTrigger;
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
        if (catalog == null || !catalog.TryGetChatTemplate(ZoneId, ChatId, out DHEventChatTemplate chat) || chat == null)
        {
            Debug.LogWarning($"[SubEventObject] {EventKey}: Chat data was not found. Zone: {ZoneId}, Chat_ID: {ChatId}", this);
            return false;
        }

        isTriggering = true;
        eventBattleRequestedDuringTrigger = false;
        pendingClosedCallback = closedCallback;
        SubscribeEventBattleRequested();
        // Sub events are item/map-event style triggers, but may still launch event battles from chat effects.
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
        Action<SubEventObject> closedCallback = pendingClosedCallback;
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

        // Defeated event battles leave the sub event on the map so the player can retry the route.
        DHEventStateRepository eventStateRepository = DHEventStateRepository.Instance;
        return eventStateRepository != null &&
            eventStateRepository.TryGetNumericValue(BattleResultKey, out float battleResult) &&
            Mathf.Approximately(battleResult, 0f);
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
