using System.Collections.Generic;
using UnityEngine;

// [JC 리팩토링 260513] EnemyPartyPool 기반.
// - 매 DayAdvanced 3턴 주기에 풀.PickRandomPrefab → 거점 후보지 위치에 Instantiate → 풀.TryRegisterInstance.
// - OnEnable 시 풀.LiveInstances 위치 영속 복원 (DHScene 재진입 시 자동 등장).
// - OnDisable 시 LiveInstances 위치를 풀에 동기화.
// - 권한 가드는 풀이 담당 (maxConcurrentInstances). 잘못된 요청은 풀이 차단.
// - enemyPrefab 단일 인스펙터 필드 폐기 (풀 카탈로그로 대체).
public class EnemySpawnController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private CastleRegistry castleRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private Transform enemyRoot;

    [Header("Spawn Rules")]
    [SerializeField, Min(1)] private int spawnInterval = 3;
    [SerializeField] private bool spawnOneEnemyOnStart = true;
    // [JC 추가 260513] 거점 SpawnCells 모두 점유 시 거점 주변 N칸 BFS로 빈 셀 탐색
    [SerializeField, Min(0)] private int fallbackSearchRadius = 4;

    private readonly List<ProductionBaseCandidate> productionBaseCandidates = new List<ProductionBaseCandidate>();
    private bool hasRestoredOnEnable;

    private readonly struct ProductionBaseCandidate
    {
        public ProductionBaseCandidate(Vector2Int baseGrid, IReadOnlyList<Vector2Int> spawnCells)
        {
            BaseGrid = baseGrid;
            SpawnCells = spawnCells;
        }

        public Vector2Int BaseGrid { get; }
        public IReadOnlyList<Vector2Int> SpawnCells { get; }
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (turnManager != null)
            turnManager.DayAdvanced += HandleDayAdvanced;

        if (!hasRestoredOnEnable)
        {
            RestoreLiveInstancesFromPool();
            hasRestoredOnEnable = true;
        }

        // [JC 260513] 매 DHScene 진입마다 spawn 가드: 풀의 lastSpawnTurn vs 현재 day.
        // 같은 day에 이미 spawn했으면 skip → "씬 재진입마다 새 적이 또 등장"하던 현상 방지.
        if (!spawnOneEnemyOnStart)
            return;

        EnemyPartyPool pool = EnemyPartyPool.Instance;
        if (pool == null || pool.LiveCount > 0)
            return;

        int currentDay = ResolveCurrentDay();
        if (pool.LastSpawnTurn == currentDay)
            return;

        if (TrySpawnOneEnemyFromPool())
            pool.MarkSpawnedAt(currentDay);
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.DayAdvanced -= HandleDayAdvanced;
    }

    [ContextMenu("Try Spawn One Enemy From Pool")]
    public bool TrySpawnOneEnemyFromPool()
    {
        EnemyPartyPool pool = EnemyPartyPool.Instance;
        if (pool == null || gridManager == null)
            return false;

        if (!pool.CanRegister())
            return false;

        if (!TryGetPlayerMainCastle(out CastleUnit playerMainCastle))
            return false;

        GameObject prefab = pool.PickRandomPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[EnemySpawnController] EnemyPartyPool catalog is empty.");
            return false;
        }

        Vector2Int playerCastleGrid = playerMainCastle.GetCurrentGrid();
        CollectProductionBaseCandidates();
        SortCandidatesByDistanceToPlayerCastle(playerCastleGrid);

        for (int i = 0; i < productionBaseCandidates.Count; i++)
        {
            ProductionBaseCandidate candidate = productionBaseCandidates[i];
            if (TrySpawnFromCandidate(prefab, candidate, playerCastleGrid))
                return true;
        }

        return false;
    }

    private void HandleDayAdvanced(int currentDay)
    {
        if (spawnInterval <= 0 || currentDay % spawnInterval != 0)
            return;

        // [JC 260513] 같은 day 중복 spawn 차단.
        EnemyPartyPool pool = EnemyPartyPool.Instance;
        if (pool != null && pool.LastSpawnTurn == currentDay)
            return;

        if (TrySpawnOneEnemyFromPool() && pool != null)
            pool.MarkSpawnedAt(currentDay);
    }

    private int ResolveCurrentDay()
    {
        if (GameManager.Instance != null)
            return GameManager.Instance.CurrentDay;
        if (turnManager != null)
        {
            // TurnManager의 현재 day 접근자 후보. CurrentDay 프로퍼티가 없으면 0 반환.
            var prop = turnManager.GetType().GetProperty("CurrentDay");
            if (prop != null && prop.PropertyType == typeof(int))
                return (int)prop.GetValue(turnManager);
        }
        return 0;
    }

    private void RestoreLiveInstancesFromPool()
    {
        EnemyPartyPool pool = EnemyPartyPool.Instance;
        if (pool == null || gridManager == null)
            return;

        IReadOnlyList<EnemyPartyInstance> instances = pool.LiveInstances;
        for (int i = 0; i < instances.Count; i++)
        {
            EnemyPartyInstance instance = instances[i];
            if (instance == null) continue;

            GameObject prefab = pool.FindPrefabByKey(instance.PrefabKey);
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemySpawnController] Pool prefab '{instance.PrefabKey}' not found for restore (instanceId='{instance.InstanceId}'). Skipped.");
                continue;
            }

            InstantiateAtGrid(prefab, instance.PrefabKey, instance.InstanceId, instance.LastKnownGrid, registerToPool: false);
        }
    }

    private void CollectProductionBaseCandidates()
    {
        productionBaseCandidates.Clear();

        if (villainUnionBaseRegistry != null)
        {
            IReadOnlyList<VillainUnionBase> villainUnionBases = villainUnionBaseRegistry.VillainUnionBases;
            for (int i = 0; i < villainUnionBases.Count; i++)
            {
                VillainUnionBase villainUnionBase = villainUnionBases[i];
                if (villainUnionBase == null)
                    continue;

                productionBaseCandidates.Add(new ProductionBaseCandidate(
                    villainUnionBase.GetCurrentGrid(),
                    GetBottomSpawnCells(villainUnionBase.transform, villainUnionBase.GetCurrentGrid())));
            }
        }

        if (outpostRegistry == null)
            return;

        IReadOnlyList<Outpost> outposts = outpostRegistry.Outposts;
        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost outpost = outposts[i];
            if (outpost == null || !outpost.IsEnemyClaimed)
                continue;

            Vector2Int outpostGrid = outpost.GetAnchorGrid(gridManager);
            productionBaseCandidates.Add(new ProductionBaseCandidate(
                outpostGrid,
                GetBottomSpawnCells(outpost.transform, outpostGrid)));
        }
    }

    private void SortCandidatesByDistanceToPlayerCastle(Vector2Int playerCastleGrid)
    {
        productionBaseCandidates.Sort((a, b) =>
            GridManager.GridDistance(a.BaseGrid, playerCastleGrid).CompareTo(
                GridManager.GridDistance(b.BaseGrid, playerCastleGrid)));
    }

    private bool TrySpawnFromCandidate(GameObject prefab, ProductionBaseCandidate candidate, Vector2Int playerCastleGrid)
    {
        if (candidate.SpawnCells == null || candidate.SpawnCells.Count == 0)
            return false;

        int primaryIndex = 0;
        int secondaryIndex = candidate.SpawnCells.Count > 1 ? 1 : -1;

        if (candidate.SpawnCells.Count > 1)
        {
            int firstDistance = GridManager.GridDistance(candidate.SpawnCells[0], playerCastleGrid);
            int secondDistance = GridManager.GridDistance(candidate.SpawnCells[1], playerCastleGrid);
            if (secondDistance < firstDistance)
            {
                primaryIndex = 1;
                secondaryIndex = 0;
            }
        }

        if (TrySpawnAtGrid(prefab, candidate.SpawnCells[primaryIndex]))
            return true;

        if (secondaryIndex >= 0 && TrySpawnAtGrid(prefab, candidate.SpawnCells[secondaryIndex]))
            return true;

        // 거점 1차 후보 셀 모두 점유 → 거점 주변 BFS로 fallback 탐색
        if (fallbackSearchRadius > 0 && TrySpawnNearBase(prefab, candidate.BaseGrid))
            return true;

        return false;
    }

    // [JC 추가 260513] 거점 주변 맨해튼 거리 N까지 BFS로 비어 있는 셀 첫 발견 시 spawn.
    private bool TrySpawnNearBase(GameObject prefab, Vector2Int baseGrid)
    {
        if (gridManager == null) return false;

        HashSet<Vector2Int> visited = new HashSet<Vector2Int> { baseGrid };
        Queue<(Vector2Int grid, int dist)> queue = new Queue<(Vector2Int, int)>();
        queue.Enqueue((baseGrid, 0));

        Vector2Int[] dirs = { new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(1, 0) };

        while (queue.Count > 0)
        {
            var (cur, dist) = queue.Dequeue();
            if (dist >= fallbackSearchRadius) continue;

            for (int d = 0; d < dirs.Length; d++)
            {
                Vector2Int next = cur + dirs[d];
                if (!visited.Add(next)) continue;
                queue.Enqueue((next, dist + 1));

                if (gridManager.CanOccupyCell(next, null, true))
                {
                    if (TrySpawnAtGrid(prefab, next))
                        return true;
                }
            }
        }

        return false;
    }

    private bool TrySpawnAtGrid(GameObject prefab, Vector2Int spawnGrid)
    {
        if (!gridManager.CanOccupyCell(spawnGrid, null, true))
            return false;

        EnemyPartyPool pool = EnemyPartyPool.Instance;
        if (pool == null)
            return false;

        string instanceId = pool.IssueInstanceId();
        string prefabKey = prefab != null ? prefab.name : string.Empty;

        InstantiateAtGrid(prefab, prefabKey, instanceId, spawnGrid, registerToPool: true);
        return true;
    }

    private void InstantiateAtGrid(GameObject prefab, string prefabKey, string instanceId, Vector2Int spawnGrid, bool registerToPool)
    {
        GameObject go = Instantiate(prefab, Vector3.zero, Quaternion.identity, enemyRoot);
        go.name = $"{prefabKey}_{instanceId}";

        EnemyGridMover mover = go.GetComponent<EnemyGridMover>();
        EnemyUnitBootstrap bootstrap = go.GetComponent<EnemyUnitBootstrap>();

        bootstrap?.InitializeEnemyUnits();
        mover?.SnapToGridPosition(spawnGrid);
        mover?.InitializeInstanceId(instanceId);

        // [JC 260513] 안 A — 위치 변경 즉시 풀에 반영. EnemyTurnController 이동도 자동 추적.
        if (mover != null)
            mover.GridChanged += HandleEnemyGridChanged;

        if (registerToPool)
        {
            EnemyPartyPool pool = EnemyPartyPool.Instance;
            pool?.TryRegisterInstance(instanceId, prefabKey, spawnGrid);
        }
    }

    private static void HandleEnemyGridChanged(EnemyGridMover mover, Vector2Int grid)
    {
        if (mover == null) return;
        string instanceId = mover.InstanceId;
        if (string.IsNullOrWhiteSpace(instanceId)) return;
        EnemyPartyPool.Instance?.UpdateInstanceGrid(instanceId, grid);
    }

    private List<Vector2Int> GetBottomSpawnCells(Transform originTransform, Vector2Int baseGrid)
    {
        MultiGridOccupant occupant = originTransform != null ? originTransform.GetComponent<MultiGridOccupant>() : null;
        if (occupant != null)
        {
            Vector2Int anchorGrid = occupant.AnchorGrid;
            Vector2Int size = occupant.Size;
            List<Vector2Int> bottomCells = new List<Vector2Int>(Mathf.Max(1, size.x));
            for (int x = 0; x < size.x; x++)
                bottomCells.Add(new Vector2Int(anchorGrid.x + x, anchorGrid.y - 1));

            return bottomCells;
        }

        return new List<Vector2Int>
        {
            new Vector2Int(baseGrid.x, baseGrid.y - 1),
            new Vector2Int(baseGrid.x + 1, baseGrid.y - 1)
        };
    }

    private int GetPoolLiveCount()
    {
        EnemyPartyPool pool = EnemyPartyPool.Instance;
        return pool != null ? pool.LiveCount : 0;
    }

    private bool TryGetPlayerMainCastle(out CastleUnit playerMainCastle)
    {
        playerMainCastle = null;

        if (castleRegistry == null)
            return false;

        IReadOnlyList<CastleUnit> castles = castleRegistry.Castles;
        for (int i = 0; i < castles.Count; i++)
        {
            CastleUnit castle = castles[i];
            if (castle == null)
                continue;

            playerMainCastle = castle;
            return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (turnManager == null)
            turnManager = FindFirstObjectByType<TurnManager>();

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (castleRegistry == null)
            castleRegistry = FindFirstObjectByType<CastleRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();
    }
}
