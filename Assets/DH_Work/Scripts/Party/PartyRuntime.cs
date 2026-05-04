using System;
using UnityEngine;

[RequireComponent(typeof(PartyGridMover))]
[RequireComponent(typeof(PartyIdentity))]
[RequireComponent(typeof(PartyComposition))]
public class PartyRuntime : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private ResourceManager resourceManager;
    [SerializeField] private CombatEncounterManager combatEncounterManager;

    [Header("Interaction Settings")]
    [SerializeField] private float itemPickupDelay = 0.5f;

    private PartyGridMover partyGridMover;
    private PartyInteractionController interactionController;

    public bool IsInputLocked => interactionController != null && interactionController.IsInputLocked;

    public event Action<Vector2Int> AdjacentItemCellEntered;
    public event Action<CastleUnit> AdjacentCastleDetected;

    private void Awake()
    {
        partyGridMover = GetComponent<PartyGridMover>();
        interactionController = new PartyInteractionController(
            gridManager,
            resourceManager,
            combatEncounterManager,
            partyGridMover,
            itemPickupDelay,
            this,
            partyGridMover.GetCurrentGrid);

        interactionController.AdjacentItemCellEntered += HandleAdjacentItemCellEntered;
        interactionController.AdjacentCastleDetected += HandleAdjacentCastleDetected;
        partyGridMover.GridEntered += HandleGridEntered;
    }

    private void OnDestroy()
    {
        if (partyGridMover != null)
            partyGridMover.GridEntered -= HandleGridEntered;

        if (interactionController == null)
            return;

        interactionController.AdjacentItemCellEntered -= HandleAdjacentItemCellEntered;
        interactionController.AdjacentCastleDetected -= HandleAdjacentCastleDetected;
        interactionController.Dispose();
    }

    private void HandleGridEntered(Vector2Int enteredGrid)
    {
        interactionController?.HandleGridEntered(enteredGrid);
    }

    private void HandleAdjacentItemCellEntered(Vector2Int itemGrid)
    {
        AdjacentItemCellEntered?.Invoke(itemGrid);
    }

    private void HandleAdjacentCastleDetected(CastleUnit castle)
    {
        AdjacentCastleDetected?.Invoke(castle);
    }
}
