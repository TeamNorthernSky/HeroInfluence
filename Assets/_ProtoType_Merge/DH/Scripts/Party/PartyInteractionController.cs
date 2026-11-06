using System;
using System.Collections;
using UnityEngine;

public class PartyInteractionController
{
    public static event Action<PartyGridMover, ResourceType> FieldResourceCollected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetResourceCollected() => FieldResourceCollected = null;

    private readonly GridManager gridManager;
    private readonly CombatEncounterManager combatEncounterManager;
    private readonly PartyGridMover ownerParty;
    private readonly MonoBehaviour coroutineOwner;
    private readonly Func<Vector2Int> currentGridProvider;

    private Coroutine pendingInteractionCoroutine;

    private bool inputLocked;
    private bool enemyInteractionInProgress;
    private int continuationReadyFrames;
    private PartyIdentity partyIdentity;

    public bool IsInputLocked
    {
        get => inputLocked || HasMobileContinuation;
        private set => inputLocked = value;
    }

    private bool HasMobileContinuation => partyIdentity != null &&
        MapProgressRepository.Instance != null &&
        MapProgressRepository.Instance.PendingMobileEncounter?.PartyId == partyIdentity.PartyId;

    public event Action<Vector2Int> AdjacentItemCellEntered;
    public event Action<HeroUnionUnit> AdjacentHeroUnionDetected;
    public event Action<MapEventObject> AdjacentMapEventDetected;
    public event Action<WorldEventObject> AdjacentWorldEventDetected;

    public PartyInteractionController(
        GridManager gridManager,
        CombatEncounterManager combatEncounterManager,
        PartyGridMover ownerParty,
        MonoBehaviour coroutineOwner,
        Func<Vector2Int> currentGridProvider)
    {
        this.gridManager = gridManager;
        this.combatEncounterManager = combatEncounterManager;
        this.ownerParty = ownerParty;
        this.coroutineOwner = coroutineOwner;
        this.currentGridProvider = currentGridProvider;
        partyIdentity = ownerParty != null ? ownerParty.GetComponent<PartyIdentity>() : null;
        SceneFadeController.TransitionFailed += HandleTransitionFailed;

        if (this.ownerParty != null)
        {
            this.ownerParty.PathUpdated += HandlePathUpdated;
        }
    }

    public void HandleGridEntered(Vector2Int enteredGrid)
    {
        if (DHGameEndState.IsEnding)
            return;

        if (HasMobileContinuation)
            return;

        if (HasPendingCombatResult())
            return;

        if (gridManager == null)
            return;

        if (ZoneEntryGuidanceController.IsActive)
        {
            ZoneEntryGuidanceController.Instance.TryCompleteFromPartyGrid(enteredGrid);
            return;
        }

        if (TryHandleMainEventAtGrid(enteredGrid))
            return;

        HandleAdjacentHeroUnionProximity(enteredGrid);
    }

    public void HandleMoveCompleted()
    {
        if (DHGameEndState.IsEnding)
            return;

        if (HasPendingCombatResult())
            return;

        if (IsInputLocked)
            return;

        if (gridManager == null || ownerParty == null)
            return;

        if (ZoneEntryGuidanceController.IsActive)
            return;

        Vector2Int currentGrid = ownerParty.GetCurrentGrid();

        if (gridManager.TryGetTutorialEnemyEncounterZoneOwner(currentGrid, out TutorialEnemyObject tutorialEnemy))
        {
            CancelPendingInteraction();
            IsInputLocked = tutorialEnemy.TryStartTutorialCombat();
            return;
        }

        if (gridManager.TryGetTutorialOutpostInteractionOwner(currentGrid, out TutorialOutpostObject tutorialOutpost))
        {
            CancelPendingInteraction();
            IsInputLocked = tutorialOutpost.TryInteract();
            return;
        }

        if (HandleAdjacentOutpostProximity(currentGrid))
            return;

        if (HandleVillainUnionProximity(currentGrid))
            return;

        if (combatEncounterManager == null)
            return;

        if (!gridManager.TryGetEnemyEncounterZoneOwner(currentGrid, out EnemyGridMover enemy))
            return;

        if (enemy.IsStatic)
            ReserveMobileContinuation(currentGrid);
        BeginEnemyInteraction(enemy);
    }

    private void BeginEnemyInteraction(EnemyGridMover enemy)
    {
        if (enemyInteractionInProgress)
            return;

        CancelPendingInteraction();
        enemyInteractionInProgress = true;
        IsInputLocked = true;
        if (EnemyEventEncounterService.TryOpenEncounterChat(ownerParty, enemy, HandleEnemyEncounterClosed))
        {
            return;
        }

        if (!enemy.IsStatic)
        {
            enemyInteractionInProgress = false;
            IsInputLocked = false;
            Debug.LogWarning("Mobile enemy encounter chat could not be opened. Skipping combat.", enemy);
            return;
        }

        bool combatStarted = combatEncounterManager.BeginCombat(ownerParty, enemy);
        enemyInteractionInProgress = combatStarted;
        IsInputLocked = combatStarted;
    }

    private bool TryPrioritizeEnemyInteraction()
    {
        CombatContext context = CombatContext.Instance;
        if (enemyInteractionInProgress || HasMobileContinuation ||
            (combatEncounterManager != null && combatEncounterManager.IsCombatActive) ||
            (context != null && (context.Result != CombatResult.None || context.CombatParty != null ||
                context.CombatEnemy != null || context.HasEventBattle)))
            return true;

        if (gridManager == null || ownerParty == null ||
            !gridManager.TryGetEnemyEncounterZoneOwner(ownerParty.GetCurrentGrid(), out _))
            return false;

        // A chat already owns the encounter; never open it again from an item/event callback.
        if (ChatModalController.IsOpen || (ChatManager.Instance != null && ChatManager.Instance.IsRunning))
            return true;

        IsInputLocked = false;
        ownerParty.SnapToGridPosition(ownerParty.GetCurrentGrid(), notifyMoveCompleted: false);
        HandleMoveCompleted();
        return true;
    }

    private void ReserveMobileContinuation(Vector2Int grid)
    {
        if (partyIdentity == null || gridManager == null || MapProgressRepository.Instance == null ||
            !gridManager.TryGetMobileEnemyAtInteractionCell(grid, out _))
            return;

        MapProgressRepository.Instance.ReserveMobileEncounter(partyIdentity.PartyId,
            ownerParty.gameObject.scene.name, grid, GameManager.Instance != null ? GameManager.Instance.CurrentDay : 0);
        continuationReadyFrames = 0;
    }

    public void TickMobileContinuation()
    {
        if (!HasMobileContinuation || ownerParty == null || gridManager == null)
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        var pending = repository.PendingMobileEncounter;
        if (DHGameEndState.IsEnding ||
            pending.SceneName != ownerParty.gameObject.scene.name ||
            (GameManager.Instance != null && pending.Day != GameManager.Instance.CurrentDay))
        {
            repository.ClearMobileEncounter();
            return;
        }

        CombatContext context = CombatContext.Instance;
        if (DHGameStateRestoreService.IsRestoring || inputLocked || ownerParty.IsMoving ||
            GateCameraFocusController.IsBusy ||
            (context != null && (context.Result != CombatResult.None || context.HasEventBattle ||
                context.CombatParty != null || context.CombatEnemy != null)) ||
            (combatEncounterManager != null && combatEncounterManager.IsCombatActive) ||
            (ChatManager.Instance != null && ChatManager.Instance.IsRunning) ||
            ChatModalController.IsOpen || ModalManager.HasAny ||
            (SceneFadeController.Instance != null && SceneFadeController.Instance.IsTransitioning))
        {
            continuationReadyFrames = 0;
            return;
        }

        if (ownerParty.GetCurrentGrid() != pending.Grid || !HasLivingPartyUnit())
        {
            repository.ClearMobileEncounter();
            return;
        }

        // Let close callbacks, result flags and deferred object removal settle first.
        if (++continuationReadyFrames < 2)
            return;

        repository.ClearMobileEncounter();
        continuationReadyFrames = 0;
        if (gridManager.TryGetMobileEnemyAtInteractionCell(pending.Grid, out EnemyGridMover enemy))
            BeginEnemyInteraction(enemy);
    }

    private bool HasLivingPartyUnit()
    {
        PartyComposition composition = ownerParty.GetComponent<PartyComposition>();
        PersistentUnitRepository repository = PersistentUnitRepository.Instance;
        if (composition == null || repository == null)
            return false;
        foreach (int unitIndex in composition.UnitIndices)
        {
            if (repository.TryGetUnit(unitIndex, out UnitPersistentData unit) && unit != null &&
                !unit.IsIncapacitated && unit.CurrentHp > 0f)
                return true;
        }
        return false;
    }

    private void HandleTransitionFailed(UnityEngine.SceneManagement.Scene sourceScene)
    {
        if (ownerParty == null || ownerParty.gameObject.scene != sourceScene) return;
        enemyInteractionInProgress = false;
        IsInputLocked = false;
        RequestMoveRangeRefresh();
    }

    public void Dispose()
    {
        SceneFadeController.TransitionFailed -= HandleTransitionFailed;
        CancelPendingInteraction();
        IsInputLocked = false;
        
        if (ownerParty != null)
        {
            ownerParty.PathUpdated -= HandlePathUpdated;
        }
    }

    private void HandlePathUpdated(System.Collections.Generic.List<Vector2Int> remainingPath)
    {
        if (DHGameEndState.IsEnding)
            return;

        if (HasPendingCombatResult())
            return;

        if (remainingPath == null || remainingPath.Count == 0) return;
        if (gridManager == null || ownerParty == null) return;

        if (ZoneEntryGuidanceController.IsActive)
        {
            TryHandleTargetInteraction(allowEvents: false);
            return;
        }

        TryHandleTargetInteraction(allowEvents: true);
    }

    private bool TryHandleTargetInteraction(bool allowEvents)
    {
        if (!ownerParty.TargetInteractionGrid.HasValue)
            return false;

        Vector2Int targetInteractionGrid = ownerParty.TargetInteractionGrid.Value;

        bool isItem = gridManager.TryGetGridItemObjectAtGrid(targetInteractionGrid, out _);
        bool isEvent = !isItem && allowEvents && gridManager.TryGetEventObjectAtGrid(targetInteractionGrid, out _);
        WorldEventObject worldEvent = null;
        bool isWorldEvent = !isItem && !isEvent && allowEvents &&
            gridManager.TryGetWorldEventObjectAtGrid(targetInteractionGrid, out worldEvent);
        bool isSubEvent = !isItem && !isEvent && !isWorldEvent && allowEvents &&
            gridManager.TryGetSubEventObjectAtGrid(targetInteractionGrid, out _);

        if (!isItem && !isEvent && !isWorldEvent && !isSubEvent)
            return false;

        Vector2Int currentGrid = ownerParty.GetCurrentGrid();

        bool inInteractionRange = isWorldEvent
            ? worldEvent.IsInteractionCell(currentGrid)
            : IsAdjacentOrSame(currentGrid, targetInteractionGrid) && currentGrid != targetInteractionGrid;
        if (inInteractionRange)
        {
            if (TryPrioritizeEnemyInteraction())
                return true;

            ownerParty.SnapToGridPosition(currentGrid);

            if (isItem)
            {
                OnAdjacentItemCellEntered(targetInteractionGrid);
            }
            else if (isEvent)
            {
                OnAdjacentEventCellEntered(targetInteractionGrid);
            }
            else if (isWorldEvent)
            {
                OnAdjacentWorldEventCellEntered(targetInteractionGrid);
            }
            else if (isSubEvent)
            {
                OnAdjacentSubEventCellEntered(targetInteractionGrid);
            }

            return true;
        }

        return false;
    }

    private bool TryHandleMainEventAtGrid(Vector2Int enteredGrid)
    {
        if (gridManager == null || !gridManager.TryGetMainEventAtInteractionCell(enteredGrid, out MainEventObject mainEvent))
            return false;

        CancelPendingInteraction();
        bool started = mainEvent.TryTrigger(ownerParty, HandleMainEventClosed);
        if (started)
            ReserveMobileContinuation(enteredGrid);
        IsInputLocked = started;
        return started;
    }

    private bool HandleAdjacentOutpostProximity(Vector2Int enteredGrid)
    {
        if (!gridManager.TryGetOutpostAtInteractionCell(enteredGrid, out Outpost outpost))
            return false;

        if (!outpost.IsClaimableByPlayer)
            return false;

        ReserveMobileContinuation(enteredGrid);
        BeginAdjacentOutpostClaim(outpost, enteredGrid);
        return true;
    }

    private bool HandleVillainUnionProximity(Vector2Int enteredGrid)
    {
        if (!gridManager.TryGetVillainUnionAtInteractionCell(enteredGrid, out VillainUnionBase villainUnionBase))
            return false;

        ReserveMobileContinuation(enteredGrid);
        CancelPendingInteraction();
        IsInputLocked = true;
        if (!TryShowDefenderCombatChat(
                villainUnionBase.ZoneId,
                villainUnionBase.DefenderCombatChatId,
                () => IsInputLocked = false) || !ChatModalController.IsOpen)
        {
            IsInputLocked = false;
            Debug.LogWarning(
                $"[VillainUnion] Combat chat could not be opened: zone='{villainUnionBase.ZoneId}', chatId={villainUnionBase.DefenderCombatChatId}.",
                villainUnionBase);
        }

        return true;
    }

    private void OnAdjacentEventCellEntered(Vector2Int eventGrid)
    {
        if (TryPrioritizeEnemyInteraction())
            return;
        CancelPendingInteraction();

        IsInputLocked = true;
        pendingInteractionCoroutine = coroutineOwner.StartCoroutine(InvokeDelayedEventInteraction(eventGrid));
    }

    private void OnAdjacentSubEventCellEntered(Vector2Int subEventGrid)
    {
        if (TryPrioritizeEnemyInteraction())
            return;
        CancelPendingInteraction();

        IsInputLocked = true;
        pendingInteractionCoroutine = coroutineOwner.StartCoroutine(InvokeDelayedSubEventInteraction(subEventGrid));
    }

    private void OnAdjacentWorldEventCellEntered(Vector2Int worldEventGrid)
    {
        if (TryPrioritizeEnemyInteraction())
            return;
        CancelPendingInteraction();

        IsInputLocked = true;
        pendingInteractionCoroutine = coroutineOwner.StartCoroutine(InvokeDelayedWorldEventInteraction(worldEventGrid));
    }

    private IEnumerator InvokeDelayedEventInteraction(Vector2Int eventGrid)
    {
        pendingInteractionCoroutine = null;

        if (TryPrioritizeEnemyInteraction())
            yield break;

        if (DHGameEndState.IsEnding)
        {
            IsInputLocked = false;
            yield break;
        }

        if (gridManager == null)
        {
            IsInputLocked = false;
            yield break;
        }

        Vector2Int currentGrid = currentGridProvider != null ? currentGridProvider() : eventGrid;
        if (!IsAdjacentOrSame(currentGrid, eventGrid))
        {
            IsInputLocked = false;
            yield break;
        }

        if (!gridManager.TryGetEventObjectAtGrid(eventGrid, out MapEventObject mapEvent))
        {
            IsInputLocked = false;
            yield break;
        }

        mapEvent.Interact(ownerParty);
        AdjacentMapEventDetected?.Invoke(mapEvent);
        IsInputLocked = false;
        RequestMoveRangeRefresh();
    }

    private IEnumerator InvokeDelayedWorldEventInteraction(Vector2Int worldEventGrid)
    {
        pendingInteractionCoroutine = null;

        if (TryPrioritizeEnemyInteraction())
            yield break;

        if (DHGameEndState.IsEnding)
        {
            IsInputLocked = false;
            yield break;
        }

        if (gridManager == null)
        {
            IsInputLocked = false;
            yield break;
        }

        Vector2Int currentGrid = currentGridProvider != null ? currentGridProvider() : worldEventGrid;
        if (!gridManager.TryGetWorldEventObjectAtGrid(worldEventGrid, out WorldEventObject worldEvent)
            || !worldEvent.IsInteractionCell(currentGrid))
        {
            IsInputLocked = false;
            yield break;
        }

        bool started = worldEvent.TryTrigger(ownerParty, HandleWorldEventClosed);
        AdjacentWorldEventDetected?.Invoke(worldEvent);
        IsInputLocked = started;
    }

    private IEnumerator InvokeDelayedSubEventInteraction(Vector2Int subEventGrid)
    {
        pendingInteractionCoroutine = null;

        if (TryPrioritizeEnemyInteraction())
            yield break;

        if (DHGameEndState.IsEnding)
        {
            IsInputLocked = false;
            yield break;
        }

        if (gridManager == null)
        {
            IsInputLocked = false;
            yield break;
        }

        Vector2Int currentGrid = currentGridProvider != null ? currentGridProvider() : subEventGrid;
        if (!IsAdjacentOrSame(currentGrid, subEventGrid))
        {
            IsInputLocked = false;
            yield break;
        }

        if (!gridManager.TryGetSubEventObjectAtGrid(subEventGrid, out SubEventObject subEvent))
        {
            IsInputLocked = false;
            yield break;
        }

        bool started = subEvent.TryTrigger(ownerParty, HandleSubEventClosed);
        IsInputLocked = started;
    }

    private void HandleAdjacentHeroUnionProximity(Vector2Int enteredGrid)
    {
        if (!gridManager.TryGetAdjacentHeroUnionObject(enteredGrid, out HeroUnionUnit heroUnion))
            return;

        if (!heroUnion.IsClaimedByHero)
        {
            heroUnion.ClaimByHero();
            return;
        }

        AdjacentHeroUnionDetected?.Invoke(heroUnion);
    }

    private void OnAdjacentItemCellEntered(Vector2Int itemGrid)
    {
        if (TryPrioritizeEnemyInteraction())
            return;
        CancelPendingInteraction();

        IsInputLocked = true;
        pendingInteractionCoroutine = coroutineOwner.StartCoroutine(InvokeDelayedItemPickup(itemGrid));
        AdjacentItemCellEntered?.Invoke(itemGrid);
    }

    private void BeginAdjacentOutpostClaim(Outpost outpost, Vector2Int interactionGrid)
    {
        CancelPendingInteraction();

        IsInputLocked = true;
        pendingInteractionCoroutine = coroutineOwner.StartCoroutine(InvokeDelayedOutpostClaim(outpost, interactionGrid));
    }

    private IEnumerator InvokeDelayedItemPickup(Vector2Int itemGrid)
    {
        pendingInteractionCoroutine = null;
        if (TryPrioritizeEnemyInteraction())
            yield break;

        if (DHGameEndState.IsEnding)
        {
            pendingInteractionCoroutine = null;
            IsInputLocked = false;
            yield break;
        }

        if (gridManager == null)
        {
            pendingInteractionCoroutine = null;
            IsInputLocked = false;
            yield break;
        }

        Vector2Int currentGrid = currentGridProvider != null ? currentGridProvider() : itemGrid;
        if (!IsAdjacentOrSame(currentGrid, itemGrid))
        {
            pendingInteractionCoroutine = null;
            IsInputLocked = false;
            yield break;
        }

        if (!gridManager.TryGetGridItemObjectAtGrid(itemGrid, out IGridItemObject itemObject))
        {
            pendingInteractionCoroutine = null;
            IsInputLocked = false;
            yield break;
        }

        float inputUnlockDelay = CollectItem(itemGrid, itemObject, ownerParty != null ? ownerParty.transform : null);
        if (itemObject is ItemObject resource)
            FieldResourceCollected?.Invoke(ownerParty, resource.resourceType);
        else if (itemObject is TutorialItemObject tutorialResource)
            FieldResourceCollected?.Invoke(ownerParty, tutorialResource.ResourceType);
        if (inputUnlockDelay > 0f)
            yield return new WaitForSeconds(inputUnlockDelay);

        pendingInteractionCoroutine = null;
        IsInputLocked = false;
        RequestMoveRangeRefresh();
    }

    private static float CollectItem(Vector2Int itemGrid, IGridItemObject itemObject, Transform flyTarget)
    {
        if (itemObject == null)
            return 0f;

        if (itemObject is ItemObject)
        {
            MapProgressRepository repository = MapProgressRepository.Instance;
            if (repository != null)
                repository.MarkItemCollected(MapProgressKey.ForItem(itemGrid));
        }

        return itemObject.CollectFromInteraction(flyTarget);
    }

    private IEnumerator InvokeDelayedOutpostClaim(Outpost outpost, Vector2Int interactionGrid)
    {
        pendingInteractionCoroutine = null;

        if (DHGameEndState.IsEnding)
        {
            IsInputLocked = false;
            yield break;
        }

        if (gridManager == null)
        {
            IsInputLocked = false;
            yield break;
        }

        if (outpost == null)
        {
            IsInputLocked = false;
            yield break;
        }

        Vector2Int currentGrid = currentGridProvider != null ? currentGridProvider() : interactionGrid;
        if (!outpost.IsInteractionCell(currentGrid, gridManager))
        {
            IsInputLocked = false;
            yield break;
        }

        if (outpost.IsEnemyClaimed)
        {
            IsInputLocked = true;
            if (!TryShowDefenderCombatChat(
                    outpost.ZoneId,
                    outpost.DefenderCombatChatId,
                    () => IsInputLocked = false) || !ChatModalController.IsOpen)
            {
                IsInputLocked = false;
                Debug.LogWarning(
                    $"[Outpost] Combat chat could not be opened: zone='{outpost.ZoneId}', chatId={outpost.DefenderCombatChatId}.",
                    outpost);
            }

            yield break;
        }

        if (outpost.CanClaimDirectlyByPlayer)
            outpost.Claim();

        IsInputLocked = false;
    }

    private void CancelPendingInteraction()
    {
        if (pendingInteractionCoroutine == null)
            return;

        coroutineOwner.StopCoroutine(pendingInteractionCoroutine);
        pendingInteractionCoroutine = null;
    }

    private void HandleEnemyEncounterClosed(bool startedCombat)
    {
        enemyInteractionInProgress = startedCombat;
        IsInputLocked = startedCombat;
    }

    private bool TryShowDefenderCombatChat(string zoneId, int chatId, Action onClosed)
    {
        if (!TryResolveChatZoneId(zoneId, out int chatZoneId) || chatId <= 0)
            return false;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null || !catalog.TryGetChatTemplate(chatZoneId, chatId, out DHEventChatTemplate chat) || chat == null)
            return false;

        ChatModalController.Show(chatZoneId, chatId, onClosed);
        return true;
    }

    private static bool TryResolveChatZoneId(string zoneId, out int chatZoneId)
    {
        chatZoneId = 0;
        if (string.IsNullOrWhiteSpace(zoneId))
            return false;

        string normalized = MapProgressKey.NormalizeSegment(zoneId);
        const string prefix = "zone_";
        string numberText = normalized.StartsWith(prefix, StringComparison.Ordinal)
            ? normalized.Substring(prefix.Length)
            : normalized;

        return int.TryParse(numberText, out chatZoneId) && chatZoneId > 0;
    }

    private void HandleMainEventClosed(MainEventObject mainEvent)
    {
        IsInputLocked = false;
        RequestMoveRangeRefresh();
    }

    private void HandleSubEventClosed(SubEventObject subEvent)
    {
        IsInputLocked = false;
        RequestMoveRangeRefresh();
    }

    private void HandleWorldEventClosed(WorldEventObject worldEvent)
    {
        IsInputLocked = false;
        RequestMoveRangeRefresh();
    }

    private void RequestMoveRangeRefresh()
    {
        if (coroutineOwner == null)
            return;

        coroutineOwner.StartCoroutine(RefreshMoveRangeNextFrame());
    }

    private static IEnumerator RefreshMoveRangeNextFrame()
    {
        yield return null;

        MoveRangeController moveRangeController = UnityEngine.Object.FindFirstObjectByType<MoveRangeController>();
        moveRangeController?.RefreshForCurrentParty();
    }

    private static bool IsAdjacentOrSame(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1;
    }

    private static bool HasPendingCombatResult()
    {
        CombatContext context = CombatContext.Instance;
        return context != null && context.Result != CombatResult.None;
    }
}
