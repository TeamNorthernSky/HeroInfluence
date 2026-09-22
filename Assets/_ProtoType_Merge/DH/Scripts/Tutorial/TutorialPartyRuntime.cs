using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyGridMover))]
[RequireComponent(typeof(TutorialPartyComposition))]
public class TutorialPartyRuntime : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private CombatPromptService combatPromptService;

    [SerializeField] private TutorialPartyComposition composition;
    [SerializeField] private PartyGridMover gridMover;

    private PartyInteractionController interactionController;

    public PartyGridMover GridMover => gridMover;
    public TutorialPartyComposition Composition => composition;
    public bool HasAnyJoinedUnit => composition != null && composition.HasAnyJoinedUnit;
    public bool IsInputLocked => interactionController != null && interactionController.IsInputLocked;

    private void Awake()
    {
        if (composition == null)
            composition = GetComponent<TutorialPartyComposition>();

        if (gridMover == null)
            gridMover = GetComponent<PartyGridMover>();

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (combatPromptService == null)
            combatPromptService = FindFirstObjectByType<CombatPromptService>();

        interactionController = new PartyInteractionController(
            gridManager,
            null,
            combatPromptService,
            gridMover,
            this,
            gridMover.GetCurrentGrid);

        if (gridMover != null)
        {
            gridMover.GridEntered += HandleGridEntered;
            gridMover.MoveCompleted += HandleMoveCompleted;
        }

        composition?.InitializePartyVisuals();
    }

    private void OnDestroy()
    {
        if (gridMover != null)
        {
            gridMover.GridEntered -= HandleGridEntered;
            gridMover.MoveCompleted -= HandleMoveCompleted;
        }

        interactionController?.Dispose();
    }

    [System.Obsolete("Tutorial parties now start with every configured unit. This method is kept only for old scene hooks.")]
    public bool JoinUnit(string unitTemplateKey)
    {
        return composition != null && composition.JoinUnit(unitTemplateKey);
    }

    public IReadOnlyList<string> GetJoinedUnitTemplateKeys()
    {
        return composition != null
            ? composition.GetJoinedUnitTemplateKeys()
            : System.Array.Empty<string>();
    }

    private void HandleGridEntered(Vector2Int enteredGrid)
    {
        interactionController?.HandleGridEntered(enteredGrid);
    }

    private void HandleMoveCompleted()
    {
        interactionController?.HandleMoveCompleted();
    }
}
