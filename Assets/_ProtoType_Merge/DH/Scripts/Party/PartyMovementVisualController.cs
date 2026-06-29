using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PartyGridMover))]
public class PartyMovementVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PartyGridMover partyGridMover;
    [SerializeField] private List<UnitVisualMotionController> unitTurnControllers = new List<UnitVisualMotionController>();

    private Vector2Int lastDirection;
    private bool hasLastDirection;
    private bool lastMoving;
    private bool hasMovingState;

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
            return;

        Vector2Int direction = remainingPath[1] - remainingPath[0];
        if (direction == Vector2Int.zero)
            return;

        if (hasLastDirection && direction == lastDirection)
            return;

        lastDirection = direction;
        hasLastDirection = true;
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
        ApplyMovingState(moving);
    }

    private void ApplyMovingState(bool moving)
    {
        if (hasMovingState && moving == lastMoving)
            return;

        lastMoving = moving;
        hasMovingState = true;
        CollectUnitTurnControllersIfEmpty();

        for (int i = 0; i < unitTurnControllers.Count; i++)
        {
            UnitVisualMotionController turnController = unitTurnControllers[i];
            if (turnController == null)
                continue;

            turnController.SetMoving(moving);
        }
    }

}
