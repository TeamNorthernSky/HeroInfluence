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

    // [JC 추가 260511] 정지 시 진단 로그
    [SerializeField] private bool logStopMovement = false;

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

    // [JC 추가 260511] 위치 영속화: PartyPersistentData.LastGrid가 있으면 그 위치로 복원
    // Start로 둔 이유: PartyPersistentRepository 및 PartyUnitBootstrap이 먼저 동작하도록 보장
    // [JC 수정 260512] 머지 사이클: PartyPersistentRepository로 책임 이관됨
    // [JC 수정 260512] LastGrid 없을 때 currentGrid 재계산 + GridEntered 발화 추가.
    //   원인: Awake 시점에 transform.position 또는 GridManager 내부 상태가 부정확해 currentGrid가 (0,0)으로 박힘.
    //   결과: 본부 방문 인디케이터가 새 게임 첫 진입 시 활성화 안 되는 버그 발생. 이 보정으로 첫 진입부터 정확.
    private void Start()
    {
        var identity = GetComponent<PartyIdentity>();
        if (identity == null) return;

        var repo = PartyPersistentRepository.Instance;
        if (repo == null) return;

        if (!repo.TryGetParty(identity.PartyId, out var partyData) || partyData == null) return;

        if (partyData.HasLastGrid)
        {
            SnapToGridPosition(partyData.LastGrid);
        }
        else if (gridManager != null)
        {
            currentGrid = gridManager.WorldToGrid(transform.position);
            GridEntered?.Invoke(currentGrid);
        }
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
            // [JC 추가 260511] 한 칸 이동마다 위치 영속화 (이동 도중 정지 케이스 포함 안전 저장)
            PersistLastGrid();
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

    // [JC 추가 260511] 이동 중 클릭 정지. 마커는 유지하되 path는 재계산되도록 외부에서 처리.
    // 셀 사이에서 정지 시 가장 가까운 셀로 스냅. 1셀 진행으로 판정되면 이동력 1 차감 (무료 이동 방지).
    public void StopMovement()
    {
        if (!isMoving && pathQueue.Count == 0) return;
        pathQueue.Clear();
        isMoving = false;

        if (gridManager == null) return;

        Vector2Int previousGrid = currentGrid;
        int previousMP = movePointController != null ? movePointController.RemainingMovePoints : -1;

        Vector2Int nearestGrid = gridManager.WorldToGrid(transform.position);
        if (nearestGrid != previousGrid)
        {
            // 다음 셀로 80% 이상 진행한 상태에서 정지 → 한 셀 진행 처리
            currentGrid = nearestGrid;
            movePointController?.SpendStep();
        }

        Vector3 worldPos = gridManager.GridToWorldCenter(currentGrid);
        worldPos.y = fixedY;
        transform.position = worldPos;
        PersistLastGrid();

        if (logStopMovement)
        {
            int afterMP = movePointController != null ? movePointController.RemainingMovePoints : -1;
            Debug.Log($"[PartyGridMover.StopMovement] prev={previousGrid} nearest={nearestGrid} snapped={currentGrid} MP {previousMP}→{afterMP}", this);
        }
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
        // [JC 추가 260511] Snap도 영속화 (외부 위치 강제 변경 케이스 안전 처리)
        PersistLastGrid();
        NotifyPathUpdated();
    }

    // [JC 추가 260511] 현재 위치를 PartyPersistentData.LastGrid로 저장
    // [JC 수정 260512] 머지 사이클: PartyPersistentRepository로 책임 이관됨
    private void PersistLastGrid()
    {
        var identity = GetComponent<PartyIdentity>();
        if (identity == null) return;

        var repo = PartyPersistentRepository.Instance;
        if (repo == null) return;

        if (!repo.TryGetParty(identity.PartyId, out var partyData) || partyData == null) return;
        partyData.SetLastGrid(currentGrid);
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

