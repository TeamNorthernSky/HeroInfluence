using System;
using System.Collections;
using UnityEngine;

public class PartyInteractionController
{
    private readonly GridManager gridManager;
    private readonly CombatEncounterManager combatEncounterManager;
    private readonly CombatPromptService combatPromptService;
    private readonly PartyGridMover ownerParty;
    private readonly float itemPickupDelay;
    private readonly MonoBehaviour coroutineOwner;
    private readonly Func<Vector2Int> currentGridProvider;

    private Coroutine pendingInteractionCoroutine;

    public bool IsInputLocked { get; private set; }

    public event Action<Vector2Int> AdjacentItemCellEntered;
    public event Action<HeroUnionUnit> AdjacentHeroUnionDetected;
    public event Action<MapEventObject> AdjacentMapEventDetected;

    public PartyInteractionController(
        GridManager gridManager,
        CombatEncounterManager combatEncounterManager,
        CombatPromptService combatPromptService,
        PartyGridMover ownerParty,
        float itemPickupDelay,
        MonoBehaviour coroutineOwner,
        Func<Vector2Int> currentGridProvider)
    {
        this.gridManager = gridManager;
        this.combatEncounterManager = combatEncounterManager;
        this.combatPromptService = combatPromptService;
        this.ownerParty = ownerParty;
        this.itemPickupDelay = itemPickupDelay;
        this.coroutineOwner = coroutineOwner;
        this.currentGridProvider = currentGridProvider;

        if (this.ownerParty != null)
        {
            this.ownerParty.PathUpdated += HandlePathUpdated;
        }
    }

    public void HandleGridEntered(Vector2Int enteredGrid)
    {
        if (DHGameEndState.IsEnding)
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
        HandleAdjacentOutpostProximity(enteredGrid);
        HandleVillainUnionProximity(enteredGrid);
    }

    public void HandleMoveCompleted()
    {
        if (DHGameEndState.IsEnding)
            return;

        if (HasPendingCombatResult())
            return;

        if (IsInputLocked)
            return;

        if (gridManager == null || combatEncounterManager == null || ownerParty == null)
            return;

        if (ZoneEntryGuidanceController.IsActive)
            return;

        if (!gridManager.TryGetEnemyEncounterZoneOwner(ownerParty.GetCurrentGrid(), out EnemyGridMover enemy))
            return;

        CancelPendingInteraction();
        if (EnemyEventEncounterService.TryOpenEncounterChat(ownerParty, enemy, HandleCombatPromptClosed))
        {
            IsInputLocked = true;
            return;
        }

        if (combatPromptService != null &&
            combatPromptService.TryOpenEnemyCombatPrompt(ownerParty, enemy, combatEncounterManager, HandleCombatPromptClosed))
        {
            IsInputLocked = true;
            return;
        }

        bool combatStarted = combatEncounterManager.BeginCombat(ownerParty, enemy);
        IsInputLocked = combatStarted;
    }

    public void Dispose()
    {
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

        bool isItem = gridManager.TryGetItemObjectAtGrid(targetInteractionGrid, out ItemObject item);
        bool isEvent = allowEvents && gridManager.TryGetEventObjectAtGrid(targetInteractionGrid, out MapEventObject mapEvent);
        bool isSubEvent = allowEvents && gridManager.TryGetSubEventObjectAtGrid(targetInteractionGrid, out SubEventObject subEvent);

        if (!isItem && !isEvent && !isSubEvent)
            return false;

        Vector2Int currentGrid = ownerParty.GetCurrentGrid();

        if (IsAdjacentOrSame(currentGrid, targetInteractionGrid) && currentGrid != targetInteractionGrid)
        {
            ownerParty.SnapToGridPosition(currentGrid);

            if (isItem)
            {
                OnAdjacentItemCellEntered(targetInteractionGrid);
            }
            else if (isEvent)
            {
                OnAdjacentEventCellEntered(targetInteractionGrid);
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
        IsInputLocked = started;
        return started;
    }

    private void HandleAdjacentOutpostProximity(Vector2Int enteredGrid)
    {
        if (!TryGetOutpostAtInteractionCell(enteredGrid, out Outpost outpost))
            return;

        if (!outpost.IsClaimableByPlayer)
            return;

        BeginAdjacentOutpostClaim(outpost, enteredGrid);
    }

    private void HandleVillainUnionProximity(Vector2Int enteredGrid)
    {
        if (!TryGetVillainUnionAtInteractionCell(enteredGrid, out VillainUnionBase villainUnionBase))
            return;

        CancelPendingInteraction();
        if (TryShowDefenderCombatChat(
                villainUnionBase.ZoneId,
                villainUnionBase.DefenderCombatChatId,
                () => BeginVillainUnionDefenderCombat(villainUnionBase)))
        {
            IsInputLocked = true;
            return;
        }

        BeginVillainUnionDefenderCombat(villainUnionBase);
    }

    private void OnAdjacentEventCellEntered(Vector2Int eventGrid)
    {
        CancelPendingInteraction();

        IsInputLocked = true;
        pendingInteractionCoroutine = coroutineOwner.StartCoroutine(InvokeDelayedEventInteraction(eventGrid));
    }

    private void OnAdjacentSubEventCellEntered(Vector2Int subEventGrid)
    {
        CancelPendingInteraction();

        IsInputLocked = true;
        pendingInteractionCoroutine = coroutineOwner.StartCoroutine(InvokeDelayedSubEventInteraction(subEventGrid));
    }

    private IEnumerator InvokeDelayedEventInteraction(Vector2Int eventGrid)
    {
        yield return new WaitForSeconds(itemPickupDelay);

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
    }

    private IEnumerator InvokeDelayedSubEventInteraction(Vector2Int subEventGrid)
    {
        yield return new WaitForSeconds(itemPickupDelay);

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
        yield return new WaitForSeconds(itemPickupDelay);

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

        if (!gridManager.TryGetItemObjectAtGrid(itemGrid, out ItemObject itemObject))
        {
            pendingInteractionCoroutine = null;
            IsInputLocked = false;
            yield break;
        }

        float inputUnlockDelay = CollectItem(itemGrid, itemObject);
        if (inputUnlockDelay > 0f)
            yield return new WaitForSeconds(inputUnlockDelay);

        pendingInteractionCoroutine = null;
        IsInputLocked = false;
    }

    private static float CollectItem(Vector2Int itemGrid, ItemObject itemObject)
    {
        if (itemObject == null)
            return 0f;

        // [JC 260514 머지후처리] ItemObject가 GameManager 통합 패턴(Game.Economy)을 내부 사용하므로 인자 없이 호출.
        MapProgressRepository repository = MapProgressRepository.Instance;
        if (repository != null)
            repository.MarkItemCollected(MapProgressKey.ForItem(itemGrid));

        return itemObject.GetItem();
    }

    private IEnumerator InvokeDelayedOutpostClaim(Outpost outpost, Vector2Int interactionGrid)
    {
        yield return new WaitForSeconds(itemPickupDelay);

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
        if (!IsOutpostInteractionCell(outpost, currentGrid))
        {
            IsInputLocked = false;
            yield break;
        }

        if (outpost.RequiresDefenderCombat)
        {
            if (TryShowDefenderCombatChat(
                    outpost.ZoneId,
                    outpost.DefenderCombatChatId,
                    () => BeginOutpostDefenderCombat(outpost)))
            {
                IsInputLocked = true;
                yield break;
            }

            BeginOutpostDefenderCombat(outpost);
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

    private void HandleCombatPromptClosed(bool startedCombat)
    {
        IsInputLocked = startedCombat;
    }

    private bool TryShowDefenderCombatChat(string zoneId, int chatId, Action onClosed)
    {
        if (!TryResolveChatZoneId(zoneId, out int chatZoneId) || chatId <= 0)
            return false;

        EventScriptCatalog catalog = EventScriptCatalog.Instance;
        if (catalog == null || !catalog.TryGetChat(chatZoneId, chatId, out ChatDBEventData chat) || chat == null)
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

    private void BeginOutpostDefenderCombat(Outpost outpost)
    {
        bool combatStarted = combatEncounterManager != null &&
            combatEncounterManager.BeginOutpostDefenderCombat(ownerParty, outpost);
        IsInputLocked = combatStarted;
    }

    private void BeginVillainUnionDefenderCombat(VillainUnionBase villainUnionBase)
    {
        bool combatStarted = combatEncounterManager != null &&
            combatEncounterManager.BeginVillainUnionDefenderCombat(ownerParty, villainUnionBase);
        IsInputLocked = combatStarted;
    }

    private void HandleMainEventClosed(MainEventObject mainEvent)
    {
        IsInputLocked = false;
    }

    private void HandleSubEventClosed(SubEventObject subEvent)
    {
        IsInputLocked = false;
    }

    private static bool IsAdjacentOrSame(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1;
    }

    private bool TryGetOutpostAtInteractionCell(Vector2Int grid, out Outpost outpost)
    {
        outpost = null;

        Outpost[] outposts = UnityEngine.Object.FindObjectsByType<Outpost>(FindObjectsSortMode.None);
        for (int i = 0; i < outposts.Length; i++)
        {
            Outpost candidate = outposts[i];
            if (candidate == null)
                continue;

            System.Collections.Generic.IReadOnlyList<Vector2Int> interactionCells =
                candidate.GetAdjacentInteractionCells(gridManager);
            if (interactionCells == null)
                continue;

            for (int j = 0; j < interactionCells.Count; j++)
            {
                if (interactionCells[j] != grid)
                    continue;

                outpost = candidate;
                return true;
            }
        }

        return false;
    }

    private bool IsOutpostInteractionCell(Outpost outpost, Vector2Int grid)
    {
        if (outpost == null)
            return false;

        System.Collections.Generic.IReadOnlyList<Vector2Int> interactionCells =
            outpost.GetAdjacentInteractionCells(gridManager);
        if (interactionCells == null)
            return false;

        for (int i = 0; i < interactionCells.Count; i++)
        {
            if (interactionCells[i] == grid)
                return true;
        }

        return false;
    }

    private static bool TryGetVillainUnionAtInteractionCell(Vector2Int grid, out VillainUnionBase villainUnionBase)
    {
        villainUnionBase = null;

        VillainUnionBase[] bases = UnityEngine.Object.FindObjectsByType<VillainUnionBase>(FindObjectsSortMode.None);
        for (int i = 0; i < bases.Length; i++)
        {
            VillainUnionBase candidate = bases[i];
            if (candidate == null)
                continue;

            System.Collections.Generic.IReadOnlyList<Vector2Int> interactionCells = candidate.GetInteractionCells();
            if (interactionCells == null)
                continue;

            for (int j = 0; j < interactionCells.Count; j++)
            {
                if (interactionCells[j] != grid)
                    continue;

                villainUnionBase = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool HasPendingCombatResult()
    {
        CombatContext context = CombatContext.Instance;
        return context != null && context.Result != CombatResult.None;
    }
}
