using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public enum EnemyBehaviorType
{
    Mobile = 0,
    Static = 1
}

public interface IGridEnemyObject
{
    Vector2Int GetCurrentGrid();
    bool IsInteractionCell(Vector2Int grid);
    void GetInteractionCells(List<Vector2Int> results);
}

[RequireComponent(typeof(EnemyIdentity))]
[RequireComponent(typeof(EnemyComposition))]
public class EnemyGridMover : MonoBehaviour, IGridEnemyObject
{
    public event System.Action<EnemyGridMover, Vector2Int> GridChanged;
    public event System.Action<EnemyGridMover, Vector2Int> MoveStepStarted;
    public event System.Action<EnemyGridMover, bool> MovementStateChanged;

    [Header("References")]
    [SerializeField] private GridManager gridManager;

    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float arriveThreshold = 0.01f;
    [SerializeField] private int movePointsPerTurn = 5;
    [SerializeField] private EnemyBehaviorType behaviorType = EnemyBehaviorType.Mobile;

    private Vector2Int currentGrid;
    private float fixedY;
    private EnemyTargetType currentTargetType;
    private Component currentTarget;
    private EnemyRegistry enemyRegistry;
    private EnemyIdentity enemyIdentity;
    private EnemyComposition enemyComposition;
    private bool isMoving;

    public string EnemyId => enemyIdentity != null ? enemyIdentity.EnemyId : string.Empty;
    public int MovePointsPerTurn => Mathf.Max(0, movePointsPerTurn);
    public EnemyBehaviorType BehaviorType => behaviorType;
    public bool IsStatic => behaviorType == EnemyBehaviorType.Static;
    public bool IsMoving => isMoving;
    public EnemyTargetType CurrentTargetType => currentTargetType;
    public Component CurrentTarget => currentTarget;

    private void Awake()
    {
        enemyIdentity = GetComponent<EnemyIdentity>();
        enemyComposition = GetComponent<EnemyComposition>();

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        fixedY = transform.position.y;
        currentGrid = gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
    }

    private void OnEnable()
    {
        ResolveRegistry();
        GridChanged -= HandleGridChanged;
        GridChanged += HandleGridChanged;
        enemyRegistry?.Register(this);
    }

    private void OnDisable()
    {
        GridChanged -= HandleGridChanged;
        enemyRegistry?.Unregister(this);
    }

    public Vector2Int GetCurrentGrid()
    {
        return currentGrid;
    }

    public bool IsInteractionCell(Vector2Int grid)
    {
        return GridManager.GridDistance(grid, currentGrid) == 1;
    }

    public void GetInteractionCells(List<Vector2Int> results)
    {
        if (results == null)
            return;

        results.Clear();
        for (int i = 0; i < GridManager.Directions8.Length; i++)
            results.Add(currentGrid + GridManager.Directions8[i]);
    }

    public bool HasTarget()
    {
        return currentTarget != null && currentTargetType != EnemyTargetType.None;
    }

    public void InitializePersistentIdentity(string nextEnemyId)
    {
        enemyIdentity ??= GetComponent<EnemyIdentity>();
        enemyIdentity?.SetEnemyId(nextEnemyId);
    }

    public void InitializePlacementIdentity(string nextPlacementKey)
    {
        enemyIdentity ??= GetComponent<EnemyIdentity>();
        enemyIdentity?.SetPlacementKey(nextPlacementKey);
    }

    public void SetBehaviorType(EnemyBehaviorType nextBehaviorType)
    {
        behaviorType = nextBehaviorType;
    }

    public void SetTarget(EnemyTargetType targetType, Component target)
    {
        currentTargetType = target != null ? targetType : EnemyTargetType.None;
        currentTarget = target;
    }

    public void ClearTarget()
    {
        currentTargetType = EnemyTargetType.None;
        currentTarget = null;
    }

    public void SnapToGridPosition(Vector2Int grid)
    {
        bool changed = currentGrid != grid;
        currentGrid = grid;

        if (gridManager == null)
        {
            if (changed)
                GridChanged?.Invoke(this, currentGrid);
            return;
        }

        transform.position = GetWorldPositionForGrid(grid);

        if (changed)
            GridChanged?.Invoke(this, currentGrid);
    }

    public IEnumerator MoveAlongPath(
        List<Vector2Int> path,
        Func<EnemyGridMover, Vector2Int, int, bool> onStepArrived = null)
    {
        if (path == null || path.Count <= 1 || gridManager == null)
            yield break;

        SetMoving(true);
        try
        {
            for (int i = 1; i < path.Count; i++)
            {
                Vector2Int nextGrid = path[i];
                MoveStepStarted?.Invoke(this, nextGrid);

                Vector3 target = GetWorldPositionForGrid(nextGrid);

                while ((transform.position - target).sqrMagnitude > arriveThreshold * arriveThreshold)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
                    yield return null;
                }

                transform.position = target;
                currentGrid = nextGrid;
                GridChanged?.Invoke(this, currentGrid);

                bool shouldContinue = onStepArrived == null || onStepArrived(this, currentGrid, i);
                if (!shouldContinue)
                    yield break;
            }
        }
        finally
        {
            SetMoving(false);
        }
    }

    private void SetMoving(bool moving)
    {
        if (isMoving == moving)
            return;

        isMoving = moving;
        MovementStateChanged?.Invoke(this, isMoving);
    }

    private void ResolveRegistry()
    {
        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (enemyIdentity == null)
            enemyIdentity = GetComponent<EnemyIdentity>();

        if (enemyComposition == null)
            enemyComposition = GetComponent<EnemyComposition>();
    }

    private void HandleGridChanged(EnemyGridMover enemy, Vector2Int grid)
    {
        if (!Application.isPlaying || enemy != this)
            return;

        enemyIdentity ??= GetComponent<EnemyIdentity>();
        if (enemyIdentity == null || string.IsNullOrWhiteSpace(enemyIdentity.PlacementKey))
            return;

        MapProgressRepository repository = MapProgressRepository.Instance;
        repository?.SetEnemyGrid(enemyIdentity.PlacementKey, grid);
    }

    private Vector3 GetWorldPositionForGrid(Vector2Int grid)
    {
        Vector3 worldPosition = gridManager.GridToWorldCenter(grid);
        float heightOffset = fixedY - gridManager.GetLandSurfaceY();
        worldPosition.y = gridManager.GetCellSurfaceY(grid) + heightOffset;
        return worldPosition;
    }
}
