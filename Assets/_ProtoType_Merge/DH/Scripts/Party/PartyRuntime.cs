using System;
using UnityEngine;

[RequireComponent(typeof(PartyGridMover))]
[RequireComponent(typeof(PartyIdentity))]
[RequireComponent(typeof(PartyComposition))]
public class PartyRuntime : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private CombatEncounterManager combatEncounterManager;
    [SerializeField] private CombatPromptService combatPromptService;

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
        if (combatPromptService == null)
            combatPromptService = FindFirstObjectByType<CombatPromptService>();

        interactionController = new PartyInteractionController(
            gridManager,
            combatEncounterManager,
            combatPromptService,
            partyGridMover,
            itemPickupDelay,
            this,
            partyGridMover.GetCurrentGrid);

        interactionController.AdjacentItemCellEntered += HandleAdjacentItemCellEntered;
        interactionController.AdjacentCastleDetected += HandleAdjacentCastleDetected;
        partyGridMover.GridEntered += HandleGridEntered;
        partyGridMover.MoveCompleted += HandleMoveCompleted;
    }

    private void OnDestroy()
    {
        if (partyGridMover != null)
        {
            partyGridMover.GridEntered -= HandleGridEntered;
            partyGridMover.MoveCompleted -= HandleMoveCompleted;
        }

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

    private void HandleMoveCompleted()
    {
        interactionController?.HandleMoveCompleted();
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
