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
    private bool eventBattleRequestedDuringTrigger;
    private bool waitingForPostBattleChat;
    private Action<SubEventObject> pendingClosedCallback;

    public bool WaitingForPostBattleChat => waitingForPostBattleChat;
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

        if (Application.isPlaying && IsCompleted() && !waitingForPostBattleChat)
        {
            gameObject.SetActive(false);
            return;
        }

        subEventRegistry?.Register(this);
    }

    private void OnDisable()
    {
        subEventRegistry?.Unregister(this);
        UnsubscribeEventBattleRequested();
        DHEventEffectRuntimeManager.Instance?.ClearActiveSubEventSource(EventKey);
    }

    private void OnDestroy()
    {
        UnsubscribeEventBattleRequested();
        JC.BattleTesting.JcArnoldBattlePresentation.ClearFixedOrigin(transform);
        DHEventEffectRuntimeManager.Instance?.ClearActiveSubEventSource(EventKey);
        pendingClosedCallback = null;
        isTriggering = false;
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
        if (!Application.isPlaying || isTriggering || waitingForPostBattleChat || IsCompleted())
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
        DHEventEffectRuntimeManager.EnsureInstance().SetActiveSubEventSource(EventKey);
        // Sub events are item/map-event style triggers, but may still launch event battles from chat effects.
        JC.BattleTesting.JcArnoldBattlePresentation.SetFixedOrigin(transform, ZoneId, EventKey);
        ChatModalController.Show(ZoneId, ChatId, HandleChatClosed);

        if (!chatManager.IsRunning)
        {
            JC.BattleTesting.JcArnoldBattlePresentation.ClearFixedOrigin(transform);
            isTriggering = false;
            eventBattleRequestedDuringTrigger = false;
            pendingClosedCallback = null;
            UnsubscribeEventBattleRequested();
            DHEventEffectRuntimeManager.Instance?.ClearActiveSubEventSource(EventKey);
            return false;
        }

        return true;
    }

    private void HandleChatClosed()
    {
        // The chat UI can finish closing after its source scene has been unloaded.
        if (this == null || !Application.isPlaying || !isTriggering)
            return;

        JC.BattleTesting.JcArnoldBattlePresentation.ClearFixedOrigin(transform);
        isTriggering = false;
        UnsubscribeEventBattleRequested();
        DHEventEffectRuntimeManager.Instance?.ClearActiveSubEventSource(EventKey);
        Action<SubEventObject> closedCallback = pendingClosedCallback;
        pendingClosedCallback = null;

        if (eventBattleRequestedDuringTrigger)
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
        if (request != null && string.Equals(
                DHEventStateRepository.NormalizeKey(request.SourceSubEventKey),
                DHEventStateRepository.NormalizeKey(EventKey), StringComparison.Ordinal))
            eventBattleRequestedDuringTrigger = true;
    }

    public void CompleteAfterEventBattleVictory(bool deferDeactivation = false)
    {
        if (!Application.isPlaying)
            return;

        MarkCompleted();
        waitingForPostBattleChat = deferDeactivation;
        if (!waitingForPostBattleChat)
            gameObject.SetActive(false);
    }

    public void FinishPostBattleChat()
    {
        if (!waitingForPostBattleChat)
            return;

        DeactivateByDisableEffect();
    }

    public void DeactivateByDisableEffect()
    {
        waitingForPostBattleChat = false;
        gameObject.SetActive(false);
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
