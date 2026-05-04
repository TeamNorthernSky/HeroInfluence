using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private EnemyGridMover enemyPrefab;
    [SerializeField, Min(1)] private int spawnInterval = 3;
    [SerializeField, Min(1)] private int maxActiveEnemies = 3;
    [SerializeField] private bool spawnOneEnemyOnStart = true;

    private readonly List<ProductionBaseCandidate> productionBaseCandidates = new List<ProductionBaseCandidate>();
    private bool hasSpawnedInitialEnemy;

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

        if (spawnOneEnemyOnStart && !hasSpawnedInitialEnemy)
        {
            TrySpawnOneEnemy();
            hasSpawnedInitialEnemy = true;
        }
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.DayAdvanced -= HandleDayAdvanced;
    }

    [ContextMenu("Try Spawn One Enemy")]
    public void TrySpawnOneEnemy()
    {
        if (enemyPrefab == null || gridManager == null)
            return;

        if (GetActiveEnemyCount() >= maxActiveEnemies)
            return;

        if (!TryGetPlayerMainCastle(out CastleUnit playerMainCastle))
            return;

        Vector2Int playerCastleGrid = playerMainCastle.GetCurrentGrid();
        CollectProductionBaseCandidates();
        SortCandidatesByDistanceToPlayerCastle(playerCastleGrid);

        for (int i = 0; i < productionBaseCandidates.Count; i++)
        {
            ProductionBaseCandidate candidate = productionBaseCandidates[i];
            if (TrySpawnFromCandidate(candidate, playerCastleGrid))
                return;
        }
    }

    private void HandleDayAdvanced(int currentDay)
    {
        if (spawnInterval <= 0 || currentDay % spawnInterval != 0)
            return;

        TrySpawnOneEnemy();
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

    private bool TrySpawnFromCandidate(ProductionBaseCandidate candidate, Vector2Int playerCastleGrid)
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

        if (TrySpawnAtGrid(candidate.SpawnCells[primaryIndex]))
            return true;

        return secondaryIndex >= 0 && TrySpawnAtGrid(candidate.SpawnCells[secondaryIndex]);
    }

    private bool TrySpawnAtGrid(Vector2Int spawnGrid)
    {
        if (!gridManager.CanOccupyCell(spawnGrid, null, true))
            return false;

        EnemyGridMover spawnedEnemy = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity, enemyRoot);
        EnemyUnitBootstrap enemyBootstrap = spawnedEnemy.GetComponent<EnemyUnitBootstrap>();
        enemyBootstrap?.InitializeEnemyUnits();
        spawnedEnemy.SnapToGridPosition(spawnGrid);
        return true;
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

    private int GetActiveEnemyCount()
    {
        if (enemyRegistry == null)
            return 0;

        int activeEnemyCount = 0;
        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
                activeEnemyCount++;
        }

        return activeEnemyCount;
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
