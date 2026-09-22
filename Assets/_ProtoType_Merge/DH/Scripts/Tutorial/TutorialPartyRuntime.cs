using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyGridMover))]
[RequireComponent(typeof(TutorialPartyComposition))]
public class TutorialPartyRuntime : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private CombatPromptService combatPromptService;
    [SerializeField] private QuarterViewCameraFollower cameraFollower;

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

        // Tutorial progress is isolated from the main exploration MapProgressRepository.
        // The shared mover normally persists party position for DHScene_3, so disable that path here.
        gridMover?.SetPersistentStateEnabled(false);

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (combatPromptService == null)
            combatPromptService = FindFirstObjectByType<CombatPromptService>();

        if (cameraFollower == null)
            cameraFollower = ResolveCameraFollower();

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
        StartCoroutine(RestoreTutorialPartyStateAfterGridMoverStart());
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

    private IEnumerator RestoreTutorialPartyStateAfterGridMoverStart()
    {
        yield return null;

        TutorialProgressRepository repository = TutorialProgressRepository.Instance;
        TutorialPartyProgressState state = repository != null ? repository.PartyState : null;
        if (state == null || gridMover == null)
            yield break;

        if (state.HasCurrentGrid)
            gridMover.SnapToGridPosition(state.CurrentGrid, notifyMoveCompleted: false);

        if (state.HasRemainingMovePoints)
            gridMover.SetRemainingMovePoints(state.RemainingMovePoints);

        SnapCameraToParty();
    }

    private void SnapCameraToParty()
    {
        if (cameraFollower == null)
            cameraFollower = ResolveCameraFollower();

        if (cameraFollower == null || gridMover == null)
            return;

        cameraFollower.SetFollowTarget(gridMover.transform);
        cameraFollower.SetFollowEnabled(true);
        cameraFollower.SnapToFollowTarget();
    }

    private static QuarterViewCameraFollower ResolveCameraFollower()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.TryGetComponent(out QuarterViewCameraFollower follower))
            return follower;

        return FindFirstObjectByType<QuarterViewCameraFollower>();
    }
}
