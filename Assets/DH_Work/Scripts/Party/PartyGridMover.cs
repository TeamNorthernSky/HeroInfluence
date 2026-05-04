using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PartyIdentity))]
[RequireComponent(typeof(PartyComposition))]
public class PartyGridMover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;

    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float arriveThreshold = 0.01f;
    [SerializeField] private int maxMovePoints = 10;

    private readonly Queue<Vector2Int> pathQueue = new Queue<Vector2Int>();
    private bool isMoving;
    private Vector2Int currentGrid;
    private float fixedY;
    private PartyMovePointController movePointController;

    public event Action<List<Vector2Int>> PathUpdated;
    public event Action<Vector2Int> GridEntered;
    public event Action MoveCompleted;

    private void Awake()
    {
        fixedY = transform.position.y;
        currentGrid = gridManager != null ? gridManager.WorldToGrid(transform.position) : Vector2Int.zero;
        movePointController = new PartyMovePointController(maxMovePoints);
    }

    private void Update()
    {
        if (!isMoving || pathQueue.Count == 0 || gridManager == null)
            return;

        Vector2Int nextGrid = pathQueue.Peek();
        Vector3 target = gridManager.GridToWorldCenter(nextGrid);
        target.y = fixedY;

        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude <= arriveThreshold * arriveThreshold)
        {
            transform.position = target;
            pathQueue.Dequeue();
            currentGrid = nextGrid;
            movePointController?.SpendStep();
            bool reachedPathEnd = pathQueue.Count == 0;

            GridEntered?.Invoke(currentGrid);
            NotifyPathUpdated();

            if (reachedPathEnd && pathQueue.Count == 0)
            {
                isMoving = false;
                MoveCompleted?.Invoke();
            }
        }
    }

    public Vector2Int GetCurrentGrid()
    {
        return currentGrid;
    }

    public bool IsMoving => isMoving;
    public int RemainingMovePoints => movePointController != null ? movePointController.RemainingMovePoints : 0;
    public int MaxMovePoints => maxMovePoints;

    public bool CanSpendMovePoints(int amount)
    {
        return movePointController != null && movePointController.CanSpend(amount);
    }

    public void ResetMovePointsToMax()
    {
        movePointController?.ResetToMax();
    }

    public void SnapToGridPosition(Vector2Int grid)
    {
        pathQueue.Clear();
        isMoving = false;
        currentGrid = grid;

        if (gridManager == null)
            return;

        Vector3 worldPosition = gridManager.GridToWorldCenter(grid);
        worldPosition.y = fixedY;
        transform.position = worldPosition;
        GridEntered?.Invoke(currentGrid);
        NotifyPathUpdated();
    }

    public List<Vector2Int> GetRemainingPath()
    {
        var remainingPath = new List<Vector2Int>();
        remainingPath.AddRange(pathQueue);
        return remainingPath;
    }

    public void MoveByGridPath(List<Vector2Int> fullPath)
    {
        pathQueue.Clear();
        isMoving = false;

        if (fullPath != null && fullPath.Count > 0)
            currentGrid = fullPath[0];

        if (fullPath == null || fullPath.Count <= 1)
        {
            NotifyPathUpdated();
            MoveCompleted?.Invoke();
            return;
        }

        // path[0]는 현재 위치이므로 제외하고 enqueue
        int moveCost = GetPathMoveCost(fullPath);
        if (!CanSpendMovePoints(moveCost))
        {
            NotifyPathUpdated();
            return;
        }

        for (int i = 1; i < fullPath.Count; i++)
            pathQueue.Enqueue(fullPath[i]);

        isMoving = pathQueue.Count > 0;
        NotifyPathUpdated();
    }

    private void NotifyPathUpdated()
    {
        var remainingPath = new List<Vector2Int> { GetCurrentGrid() };
        remainingPath.AddRange(pathQueue);
        PathUpdated?.Invoke(remainingPath);
    }

    private static int GetPathMoveCost(List<Vector2Int> path)
    {
        return path == null ? 0 : Mathf.Max(0, path.Count - 1);
    }
}

