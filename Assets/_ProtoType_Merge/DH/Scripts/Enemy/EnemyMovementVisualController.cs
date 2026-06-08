using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyGridMover))]
public class EnemyMovementVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyGridMover enemyGridMover;
    [SerializeField] private List<UnitVisualMotionController> unitMotionControllers = new List<UnitVisualMotionController>();
    [SerializeField] private bool logMovementVisualChanges;

    private Vector2Int lastDirection;
    private bool hasLastDirection;
    private bool lastMoving;
    private bool hasMovingState;

    private bool ShouldLog => logMovementVisualChanges;

    private void Awake()
    {
        if (enemyGridMover == null)
            enemyGridMover = GetComponent<EnemyGridMover>();

        CollectUnitMotionControllersIfEmpty();
    }

    private void OnEnable()
    {
        if (enemyGridMover == null)
            enemyGridMover = GetComponent<EnemyGridMover>();

        if (enemyGridMover != null)
        {
            enemyGridMover.MoveStepStarted += HandleMoveStepStarted;
            enemyGridMover.MovementStateChanged += HandleMovementStateChanged;
        }
    }

    private void Start()
    {
        if (enemyGridMover != null)
            ApplyMovingState(enemyGridMover.IsMoving);
    }

    private void Update()
    {
        if (enemyGridMover == null)
            return;

        ApplyMovingState(enemyGridMover.IsMoving);
    }

    private void OnDisable()
    {
        if (enemyGridMover != null)
        {
            enemyGridMover.MoveStepStarted -= HandleMoveStepStarted;
            enemyGridMover.MovementStateChanged -= HandleMovementStateChanged;
        }
    }

    [ContextMenu("Collect Unit Motion Controllers")]
    public void CollectUnitMotionControllers()
    {
        unitMotionControllers.Clear();
        unitMotionControllers.AddRange(GetComponentsInChildren<UnitVisualMotionController>(true));
    }

    private void CollectUnitMotionControllersIfEmpty()
    {
        for (int i = 0; i < unitMotionControllers.Count; i++)
        {
            if (unitMotionControllers[i] != null)
                return;
        }

        CollectUnitMotionControllers();
    }

    private void HandleMoveStepStarted(EnemyGridMover enemy, Vector2Int nextGrid)
    {
        if (enemy != enemyGridMover)
            return;

        Vector2Int direction = nextGrid - enemyGridMover.GetCurrentGrid();
        if (direction == Vector2Int.zero)
        {
            if (ShouldLog)
                Debug.Log($"[EnemyMovementVisualController] {name} zero direction ignored. nextGrid={nextGrid}", this);
            return;
        }

        if (hasLastDirection && direction == lastDirection)
        {
            if (ShouldLog)
                Debug.Log($"[EnemyMovementVisualController] {name} direction skipped. direction={direction}", this);
            return;
        }

        lastDirection = direction;
        hasLastDirection = true;

        if (ShouldLog)
            Debug.Log($"[EnemyMovementVisualController] {name} direction={direction} units={unitMotionControllers.Count}", this);

        ApplyLookDirection(direction);
    }

    private void HandleMovementStateChanged(EnemyGridMover enemy, bool moving)
    {
        if (enemy != enemyGridMover)
            return;

        if (ShouldLog)
            Debug.Log($"[EnemyMovementVisualController] {name} movement event. moving={moving}", this);

        ApplyMovingState(moving);
    }

    private void ApplyLookDirection(Vector2Int direction)
    {
        CollectUnitMotionControllersIfEmpty();

        for (int i = 0; i < unitMotionControllers.Count; i++)
        {
            UnitVisualMotionController motionController = unitMotionControllers[i];
            if (motionController == null)
                continue;

            motionController.SetLookDirection(direction);
        }
    }

    private void ApplyMovingState(bool moving)
    {
        if (hasMovingState && moving == lastMoving)
            return;

        lastMoving = moving;
        hasMovingState = true;
        CollectUnitMotionControllersIfEmpty();

        if (ShouldLog)
            Debug.Log($"[EnemyMovementVisualController] {name} moving={moving} units={unitMotionControllers.Count}", this);

        for (int i = 0; i < unitMotionControllers.Count; i++)
        {
            UnitVisualMotionController motionController = unitMotionControllers[i];
            if (motionController == null)
                continue;

            motionController.SetMoving(moving);
        }
    }
}
