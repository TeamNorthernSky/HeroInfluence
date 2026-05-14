using System;
using System.Collections;
using UnityEngine;

public class PartyInteractionController
{
    // [JC 260514 머지후처리] GameManager 통합 (a 방식)으로 ResourceManager 직접 의존 폐기. Game.Economy 단축 접근자 사용.
    private readonly GridManager gridManager;
    private readonly CombatEncounterManager combatEncounterManager;
    private readonly PartyGridMover ownerParty;
    private readonly float itemPickupDelay;
    private readonly MonoBehaviour coroutineOwner;
    private readonly Func<Vector2Int> currentGridProvider;

    private Coroutine pendingInteractionCoroutine;

    public bool IsInputLocked { get; private set; }

    public event Action<Vector2Int> AdjacentItemCellEntered;
    public event Action<CastleUnit> AdjacentCastleDetected;
    public event Action<MapEventObject> AdjacentMapEventDetected;

    public PartyInteractionController(
        GridManager gridManager,
        CombatEncounterManager combatEncounterManager,
        PartyGridMover ownerParty,
        float itemPickupDelay,
        MonoBehaviour coroutineOwner,
        Func<Vector2Int> currentGridProvider)
    {
        this.gridManager = gridManager;
        this.combatEncounterManager = combatEncounterManager;
        this.ownerParty = ownerParty;
        this.itemPickupDelay = itemPickupDelay;
        this.coroutineOwner = coroutineOwner;
        this.currentGridProvider = currentGridProvider;
    }

    public void HandleGridEntered(Vector2Int enteredGrid)
    {
        if (gridManager == null)
            return;

        if (HandleAdjacentEnemyProximity(enteredGrid))
            return;

        HandleAdjacentCastleProximity(enteredGrid);
        HandleAdjacentItemProximity(enteredGrid);
        HandleAdjacentOutpostProximity(enteredGrid);
        HandleAdjacentEventProximity(enteredGrid);
    }

    public void Dispose()
    {
        CancelPendingInteraction();
        IsInputLocked = false;
    }

    private void HandleAdjacentItemProximity(Vector2Int enteredGrid)
    {
        if (!gridManager.TryGetAdjacentItemGrid(enteredGrid, out Vector2Int itemGrid))
            return;

        OnAdjacentItemCellEntered(itemGrid);
    }

    private void HandleAdjacentOutpostProximity(Vector2Int enteredGrid)
    {
        if (!gridManager.TryGetAdjacentOutpostGrid(enteredGrid, out Vector2Int outpostGrid))
            return;

        if (!gridManager.TryGetOutpostObjectAtGrid(outpostGrid, out Outpost outpost))
            return;

        if (!outpost.IsClaimableByPlayer)
            return;

        BeginAdjacentOutpostClaim(outpostGrid);
    }

    private void HandleAdjacentEventProximity(Vector2Int enteredGrid)
    {
        if (!gridManager.TryGetAdjacentEventGrid(enteredGrid, out Vector2Int eventGrid))
            return;

        if (!gridManager.TryGetEventObjectAtGrid(eventGrid, out MapEventObject mapEvent))
            return;

        mapEvent.Interact();
        AdjacentMapEventDetected?.Invoke(mapEvent);
    }

    private bool HandleAdjacentEnemyProximity(Vector2Int enteredGrid)
    {
        if (combatEncounterManager == null || ownerParty == null)
            return false;

        if (!gridManager.TryGetAdjacentEnemyGrid(enteredGrid, out Vector2Int enemyGrid))
            return false;

        if (!gridManager.TryGetEnemyObjectAtGrid(enemyGrid, out EnemyGridMover enemy))
            return false;

        CancelPendingInteraction();
        bool combatStarted = combatEncounterManager.BeginCombat(ownerParty, enemy);
        IsInputLocked = combatStarted;
        return combatStarted;
    }

    private void HandleAdjacentCastleProximity(Vector2Int enteredGrid)
    {
        if (!gridManager.TryGetAdjacentCastleObject(enteredGrid, out CastleUnit castle))
            return;

        AdjacentCastleDetected?.Invoke(castle);
    }

    private void OnAdjacentItemCellEntered(Vector2Int itemGrid)
    {
        CancelPendingInteraction();

        IsInputLocked = true;
        pendingInteractionCoroutine = coroutineOwner.StartCoroutine(InvokeDelayedItemPickup(itemGrid));
        AdjacentItemCellEntered?.Invoke(itemGrid);
    }

    private void BeginAdjacentOutpostClaim(Vector2Int outpostGrid)
    {
        CancelPendingInteraction();

        IsInputLocked = true;
        pendingInteractionCoroutine = coroutineOwner.StartCoroutine(InvokeDelayedOutpostClaim(outpostGrid));
    }

    private IEnumerator InvokeDelayedItemPickup(Vector2Int itemGrid)
    {
        yield return new WaitForSeconds(itemPickupDelay);

        pendingInteractionCoroutine = null;

        if (gridManager == null)
        {
            IsInputLocked = false;
            yield break;
        }

        Vector2Int currentGrid = currentGridProvider != null ? currentGridProvider() : itemGrid;
        if (!IsAdjacentOrSame(currentGrid, itemGrid))
        {
            IsInputLocked = false;
            yield break;
        }

        if (!gridManager.TryGetItemObjectAtGrid(itemGrid, out ItemObject itemObject))
        {
            IsInputLocked = false;
            yield break;
        }

        // [JC 260514 머지후처리] ItemObject가 GameManager 통합 패턴(Game.Economy)을 내부 사용하므로 인자 없이 호출.
        itemObject.GetItem();
        IsInputLocked = false;
    }

    private IEnumerator InvokeDelayedOutpostClaim(Vector2Int outpostGrid)
    {
        yield return new WaitForSeconds(itemPickupDelay);

        pendingInteractionCoroutine = null;

        if (gridManager == null)
        {
            IsInputLocked = false;
            yield break;
        }

        Vector2Int currentGrid = currentGridProvider != null ? currentGridProvider() : outpostGrid;
        if (!IsAdjacentOrSame(currentGrid, outpostGrid))
        {
            IsInputLocked = false;
            yield break;
        }

        if (!gridManager.TryGetOutpostObjectAtGrid(outpostGrid, out Outpost outpost))
        {
            IsInputLocked = false;
            yield break;
        }

        if (outpost.IsClaimableByPlayer)
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

    private static bool IsAdjacentOrSame(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return dx <= 1 && dy <= 1;
    }
}
