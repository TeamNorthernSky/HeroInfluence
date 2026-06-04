using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyGridMover))]
public class PartyMovementVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PartyGridMover partyGridMover;
    [SerializeField] private List<UnitVisualMotionController> unitTurnControllers = new List<UnitVisualMotionController>();
    [SerializeField] private bool logMovementVisualChanges;

    private Vector2Int lastDirection;
    private bool hasLastDirection;
    private bool lastMoving;
    private bool hasMovingState;

    private bool ShouldLog => logMovementVisualChanges;

    private void Awake()
    {
        if (partyGridMover == null)
            partyGridMover = GetComponent<PartyGridMover>();

        CollectUnitTurnControllersIfEmpty();
    }

    private void OnEnable()
    {
        if (partyGridMover == null)
            partyGridMover = GetComponent<PartyGridMover>();

        if (partyGridMover != null)
        {
            partyGridMover.PathUpdated += HandlePathUpdated;
            partyGridMover.MovementStateChanged += HandleMovementStateChanged;
        }

        if (ShouldLog)
            Debug.Log($"[PartyMovementVisualController] {name} enabled. mover={(partyGridMover != null ? partyGridMover.name : "null")}", this);
    }

    private void Start()
    {
        if (partyGridMover != null)
        {
            HandlePathUpdated(partyGridMover.GetRemainingPath());
            ApplyMovingState(partyGridMover.IsMoving);
        }
    }

    private void Update()
    {
        if (partyGridMover == null)
            return;

        ApplyMovingState(partyGridMover.IsMoving);
    }

    private void OnDisable()
    {
        if (partyGridMover != null)
        {
            partyGridMover.PathUpdated -= HandlePathUpdated;
            partyGridMover.MovementStateChanged -= HandleMovementStateChanged;
        }
    }

    [ContextMenu("Collect Unit Turn Controllers")]
    public void CollectUnitTurnControllers()
    {
        unitTurnControllers.Clear();
        unitTurnControllers.AddRange(GetComponentsInChildren<UnitVisualMotionController>(true));
    }

    private void CollectUnitTurnControllersIfEmpty()
    {
        for (int i = 0; i < unitTurnControllers.Count; i++)
        {
            if (unitTurnControllers[i] != null)
                return;
        }

        CollectUnitTurnControllers();
    }

    private void HandlePathUpdated(List<Vector2Int> remainingPath)
    {
        if (remainingPath == null || remainingPath.Count < 2)
        {
            if (ShouldLog)
                Debug.Log($"[PartyMovementVisualController] {name} path ignored. count={(remainingPath != null ? remainingPath.Count : 0)}", this);
            return;
        }

        Vector2Int direction = remainingPath[1] - remainingPath[0];
        if (direction == Vector2Int.zero)
        {
            if (ShouldLog)
                Debug.Log($"[PartyMovementVisualController] {name} zero direction ignored. from={remainingPath[0]} to={remainingPath[1]}", this);
            return;
        }

        if (hasLastDirection && direction == lastDirection)
        {
            if (ShouldLog)
                Debug.Log($"[PartyMovementVisualController] {name} direction skipped. direction={direction}", this);
            return;
        }

        lastDirection = direction;
        hasLastDirection = true;
        if (ShouldLog)
            Debug.Log($"[PartyMovementVisualController] {name} direction={direction} units={unitTurnControllers.Count}", this);

        ApplyLookDirection(direction);
    }

    private void ApplyLookDirection(Vector2Int direction)
    {
        CollectUnitTurnControllersIfEmpty();

        for (int i = 0; i < unitTurnControllers.Count; i++)
        {
            UnitVisualMotionController turnController = unitTurnControllers[i];
            if (turnController == null)
                continue;

            turnController.SetLookDirection(direction);
        }
    }

    private void HandleMovementStateChanged(bool moving)
    {
        if (ShouldLog)
            Debug.Log($"[PartyMovementVisualController] {name} movement event. moving={moving}", this);

        ApplyMovingState(moving);
    }

    private void ApplyMovingState(bool moving)
    {
        if (hasMovingState && moving == lastMoving)
            return;

        lastMoving = moving;
        hasMovingState = true;
        CollectUnitTurnControllersIfEmpty();

        if (ShouldLog)
            Debug.Log($"[PartyMovementVisualController] {name} moving={moving} units={unitTurnControllers.Count}", this);

        for (int i = 0; i < unitTurnControllers.Count; i++)
        {
            UnitVisualMotionController turnController = unitTurnControllers[i];
            if (turnController == null)
                continue;

            turnController.SetMoving(moving);
        }
    }

}
