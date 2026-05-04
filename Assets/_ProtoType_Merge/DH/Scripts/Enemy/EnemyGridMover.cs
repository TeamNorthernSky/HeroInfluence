using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyIdentity))]
[RequireComponent(typeof(EnemyComposition))]
public class EnemyGridMover : MonoBehaviour
{
    public event System.Action<EnemyGridMover, Vector2Int> GridChanged;
    public event System.Action<EnemyGridMover, Vector2Int> MoveStepStarted;

    [Header("References")]
    [SerializeField] private GridManager gridManager;

    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float arriveThreshold = 0.01f;
    [SerializeField] private int movePointsPerTurn = 5;

    private Vector2Int currentGrid;
    private float fixedY;
    private EnemyTargetType currentTargetType;
    private Component currentTarget;
    private EnemyRegistry enemyRegistry;
    private EnemyIdentity enemyIdentity;
    private EnemyComposition enemyComposition;

    public int EnemyId => enemyIdentity != null ? enemyIdentity.EnemyId : 0;
    public int MovePointsPerTurn => Mathf.Max(0, movePointsPerTurn);
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
        enemyRegistry?.Register(this);
    }

    private void OnDisable()
    {
        enemyRegistry?.Unregister(this);
    }

    public Vector2Int GetCurrentGrid()
    {
        return currentGrid;
    }

    public bool HasTarget()
    {
        return currentTarget != null && currentTargetType != EnemyTargetType.None;
    }

    public void InitializePersistentIdentity(int nextEnemyId)
    {
        enemyIdentity ??= GetComponent<EnemyIdentity>();
        enemyIdentity?.SetEnemyId(nextEnemyId);
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

        Vector3 worldPosition = gridManager.GridToWorldCenter(grid);
        worldPosition.y = fixedY;
        transform.position = worldPosition;

        if (changed)
            GridChanged?.Invoke(this, currentGrid);
    }

    public IEnumerator MoveAlongPath(List<Vector2Int> path)
    {
        if (path == null || path.Count <= 1 || gridManager == null)
            yield break;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2Int nextGrid = path[i];
            MoveStepStarted?.Invoke(this, nextGrid);

            Vector3 target = gridManager.GridToWorldCenter(nextGrid);
            target.y = fixedY;

            while ((transform.position - target).sqrMagnitude > arriveThreshold * arriveThreshold)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = target;
            currentGrid = nextGrid;
            GridChanged?.Invoke(this, currentGrid);
        }
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
}
