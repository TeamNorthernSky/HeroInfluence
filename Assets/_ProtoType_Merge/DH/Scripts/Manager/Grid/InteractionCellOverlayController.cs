using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Optional presentation only. The controller retains cell selection and encounter picking.
public interface IInteractionCellOverlayVisual
{
    void RenderCells(IReadOnlyList<InteractionOverlayCell> cells, GridManager grid);
    void ClearCells();
}

public readonly struct InteractionOverlayCell
{
    public readonly object Owner;
    public readonly Vector2Int Grid;
    public readonly Color Color;
    // JC | 2026-11-05 | DH의 기존 셀 우선순위를 표시 계층에 전달하여 겹친 윤곽선도 같은 우선순위를 사용합니다.
    public readonly int Priority;
    public InteractionOverlayCell(object owner, Vector2Int grid, Color color, int priority = 0)
    { Owner = owner; Grid = grid; Color = color; Priority = priority; }
}

public class InteractionCellOverlayController : MonoBehaviour
{
    private enum OverlayCellType
    {
        Neutral,
        Player,
        Enemy,
        OverlappedEnemy,
        MainEvent,
        External
    }

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;
    [SerializeField] private MainEventRegistry mainEventRegistry;
    [SerializeField] private Transform overlayRoot;
    [SerializeField] private GameObject overlayPrefab;

    [Header("Display")]
    [SerializeField] private Color singleZoneColor = new Color(1f, 0f, 0f, 0.28f);
    [SerializeField] private Color overlappedZoneColor = new Color(0.55f, 0f, 0f, 0.55f);
    [SerializeField] private Color playerInteractionColor = new Color(0f, 0.35f, 1f, 0.28f);
    [SerializeField] private Color neutralInteractionColor = new Color(1f, 0.85f, 0f, 0.28f);
    [SerializeField] private Color mainEventInteractionColor = new Color(0.2f, 0.9f, 1f, 0.32f);
    [SerializeField, Range(1f, 1.2f)] private float cellScale = 1f;
    [SerializeField] private float yOffset = 0.035f;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;

    private readonly Dictionary<Vector2Int, OverlayInstance> activeOverlays = new Dictionary<Vector2Int, OverlayInstance>();
    private readonly Stack<OverlayInstance> overlayPool = new Stack<OverlayInstance>();
    private readonly HashSet<EnemyGridMover> subscribedEnemies = new HashSet<EnemyGridMover>();
    private readonly Dictionary<Vector2Int, OverlayCellType> desiredCells = new Dictionary<Vector2Int, OverlayCellType>();
    private readonly Dictionary<Vector2Int, Color> externalDesiredCellColors = new Dictionary<Vector2Int, Color>();
    private readonly Dictionary<object, ExternalOverlaySet> externalOverlaySets = new Dictionary<object, ExternalOverlaySet>();
    private readonly List<Vector2Int> removalBuffer = new List<Vector2Int>();

    // Group the tiles emitted by each combat source before testing the inner 80% of its whole footprint.
    private struct EncounterCell
    {
        public Behaviour owner;
        public Vector2Int grid;
        public OverlayCellType type;
    }
    private readonly List<EncounterCell> encounterCells = new List<EncounterCell>();
    private readonly Dictionary<Behaviour, Bounds> encounterBounds = new Dictionary<Behaviour, Bounds>();

    private void AddEncounterCell(Behaviour owner, Vector2Int cell, OverlayCellType type)
    {
        encounterCells.Add(new EncounterCell { owner = owner, grid = cell, type = type });
        TrySetDesiredCell(cell, type, owner);
    }

    private void CacheVisibleEncounterCells()
    {
        encounterBounds.Clear();
        int kept = 0;
        float width = Mathf.Max(.01f, gridManager.CellSize * Mathf.Max(1f, cellScale));
        for (int i = 0; i < encounterCells.Count; i++)
        {
            var cell = encounterCells[i];
            if (!desiredCells.TryGetValue(cell.grid, out var visible) || visible != cell.type ||
                !activeOverlays.TryGetValue(cell.grid, out var tile) || !tile.GameObject.activeInHierarchy) continue;
            var bounds = new Bounds(tile.GameObject.transform.position, new Vector3(width, 0, width));
            if (encounterBounds.TryGetValue(cell.owner, out var previous)) bounds.Encapsulate(previous);
            encounterBounds[cell.owner] = bounds;
            encounterCells[kept++] = cell;
        }
        encounterCells.RemoveRange(kept, encounterCells.Count - kept);
    }

    public Component PickEncounter(Ray ray, float maxDistance, out float nearest)
    {
        nearest = maxDistance;
        if (gridManager == null || !isActiveAndEnabled || Mathf.Abs(ray.direction.y) < .0001f) return null;
        Behaviour best = null;
        float bestCenterDistance = float.PositiveInfinity;
        foreach (var cell in encounterCells)
        {
            if (cell.owner == null || !cell.owner.isActiveAndEnabled) continue;
            var tile = activeOverlays[cell.grid].GameObject.transform;
            float t = (tile.position.y - ray.origin.y) / ray.direction.y;
            if (t < 0 || t > nearest) continue;
            var point = ray.GetPoint(t);
            var local = tile.InverseTransformPoint(point);
            if (Mathf.Abs(local.x) > .5f || Mathf.Abs(local.z) > .5f) continue;
            var bounds = encounterBounds[cell.owner];
            var delta = point - bounds.center;
            if (Mathf.Abs(delta.x) > bounds.extents.x * .8f || Mathf.Abs(delta.z) > bounds.extents.z * .8f) continue;
            float centerDistance = delta.x * delta.x + delta.z * delta.z;
            if (t == nearest && centerDistance >= bestCenterDistance) continue;
            best = cell.owner;
            nearest = t;
            bestCenterDistance = centerDistance;
        }
        return best;
    }

    private readonly Dictionary<Vector2Int, object> desiredOwners = new Dictionary<Vector2Int, object>();
    private readonly List<InteractionOverlayCell> visibleCells = new List<InteractionOverlayCell>();
    // JC | 2026-11-09: 윤곽선 전용 소유권입니다. 기존 desiredCells/encounterCells를 바꾸지 않아
    // DH의 이동·호버·클릭·전투 판정은 보존하고, 고정 전투 > 이동 적 > 협회 순으로 표시만 나눕니다.
    private readonly Dictionary<Vector2Int, InteractionOverlayCell> visualCellsByGrid = new Dictionary<Vector2Int, InteractionOverlayCell>();
    private IInteractionCellOverlayVisual visualOverride;
    // JC | 2026-11-04 | 범위 판정과 클릭 처리는 기존 DH 셀이 담당하고, 외형만 연결된 영역 렌더러에 위임합니다.
    public IInteractionCellOverlayVisual VisualOverride
    {
        get => visualOverride;
        set
        {
            if (ReferenceEquals(visualOverride, value)) return;
            visualOverride?.ClearCells();
            visualOverride = value;
            foreach (var tile in activeOverlays.Values) tile.SetPresentationHidden(value != null);
            foreach (var tile in overlayPool) tile.SetPresentationHidden(value != null);
            RequestRefresh();
        }
    }

    private float nextRefreshTime;
    private Mesh generatedMesh;
    private Material generatedMaterial;
    private bool refreshRequested;

    private void Awake()
    {
        ResolveReferences();
        EnsureOverlayRoot();
        EnsureGeneratedAssets();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeRegistry();
        SubscribeSceneEnemies();
        Outpost.OutpostClaimed -= HandleOutpostChanged;
        Outpost.OutpostClaimed += HandleOutpostChanged;
        HeroUnionUnit.HeroUnionStateChanged -= HandleHeroUnionChanged;
        HeroUnionUnit.HeroUnionStateChanged += HandleHeroUnionChanged;
        SubscribeMainEventRegistry();
        RequestRefresh();
    }

    private void OnDisable()
    {
        UnsubscribeRegistry();
        UnsubscribeEnemies();
        Outpost.OutpostClaimed -= HandleOutpostChanged;
        HeroUnionUnit.HeroUnionStateChanged -= HandleHeroUnionChanged;
        UnsubscribeMainEventRegistry();
        ClearActiveOverlays();
    }

    private void OnDestroy()
    {
        if (generatedMaterial != null)
            Destroy(generatedMaterial);

        if (generatedMesh != null)
            Destroy(generatedMesh);
    }

    private void Update()
    {
        if (!refreshRequested && Time.time < nextRefreshTime)
            return;

        refreshRequested = false;
        nextRefreshTime = Time.time + refreshInterval;
        RefreshOverlays();
    }

    [ContextMenu("Refresh Interaction Cell Overlay")]
    public void RequestRefresh()
    {
        refreshRequested = true;
    }

    public void SetExternalCells(object owner, IEnumerable<Vector2Int> cells, Color color)
    {
        if (owner == null)
            return;

        ExternalOverlaySet set = GetOrCreateExternalOverlaySet(owner);
        set.Cells.Clear();
        if (cells != null)
        {
            foreach (Vector2Int cell in cells)
                set.Cells.Add(cell);
        }

        set.Color = color;
        RequestRefresh();
    }

    public void ClearExternalCells(object owner)
    {
        if (owner == null)
            return;

        if (externalOverlaySets.Remove(owner))
            RequestRefresh();
    }

    private void RefreshOverlays()
    {
        ResolveReferences();
        if (gridManager == null)
        {
            ClearActiveOverlays();
            return;
        }

        SubscribeSceneEnemies();
        BuildDesiredZones();
        HideRemovedZones();
        ShowDesiredZones();
        CacheVisibleEncounterCells();
        if (visualOverride != null)
        {
            visibleCells.Clear();
            // JC | 2026-11-09: 클릭 후보를 만든 결과와 분리한 표시 셀만 기존 윤곽 생성기에 전달합니다.
            // 중첩 전용 그룹 없이 고정 영역을 온전히 남기고 이동 적은 남은 셀의 경계로 그립니다.
            foreach (var cell in visualCellsByGrid.Values)
                visibleCells.Add(cell);
            visualOverride.RenderCells(visibleCells, gridManager);
        }
    }

    private void BuildDesiredZones()
    {
        desiredCells.Clear();
        desiredOwners.Clear();
        encounterCells.Clear();
        externalDesiredCellColors.Clear();
        // JC | 2026-11-09: 이동·소멸·점령으로 바뀐 원본 영역을 매번 다시 합성해 이전 잘림이 남지 않게 합니다.
        visualCellsByGrid.Clear();
        AddMainEventInteractionCells();
        AddEnemyEncounterCells();
        AddOutpostInteractionCells();
        AddHeroUnionInteractionCells();
        AddVillainUnionInteractionCells();
        AddExternalOverlayCells();
    }

    private void AddExternalOverlayCells()
    {
        foreach (KeyValuePair<object, ExternalOverlaySet> pair in externalOverlaySets)
        {
            ExternalOverlaySet set = pair.Value;
            if (set == null)
                continue;

            for (int i = 0; i < set.Cells.Count; i++)
            {
                Vector2Int grid = set.Cells[i];
                if (TrySetDesiredCell(grid, OverlayCellType.External, pair.Key))
                {
                    externalDesiredCellColors[grid] = set.Color;
                    if (pair.Key is TutorialEnemyObject tutorialEnemy)
                        encounterCells.Add(new EncounterCell { owner = tutorialEnemy, grid = grid, type = OverlayCellType.External });
                }
            }
        }
    }

    private void AddMainEventInteractionCells()
    {
        IReadOnlyList<MainEventObject> mainEvents = mainEventRegistry != null
            ? mainEventRegistry.MainEvents
            : FindObjectsByType<MainEventObject>(FindObjectsSortMode.None);

        for (int i = 0; i < mainEvents.Count; i++)
        {
            MainEventObject mainEvent = mainEvents[i];
            if (mainEvent == null || !mainEvent.gameObject.activeInHierarchy)
                continue;

            IReadOnlyList<Vector2Int> interactionCells = mainEvent.GetInteractionCells(gridManager);
            for (int cellIndex = 0; cellIndex < interactionCells.Count; cellIndex++)
            {
                Vector2Int grid = interactionCells[cellIndex];
                AddEncounterCell(mainEvent, grid, OverlayCellType.MainEvent);
            }

            MultiGridOccupant occupant = mainEvent.GetComponent<MultiGridOccupant>();
            if (occupant != null)
            {
                IReadOnlyList<Vector2Int> occupiedCells = occupant.GetOccupiedCells();
                for (int cellIndex = 0; cellIndex < occupiedCells.Count; cellIndex++)
                    AddEncounterCell(mainEvent, occupiedCells[cellIndex], OverlayCellType.MainEvent);
            }
            else
            {
                AddEncounterCell(mainEvent, mainEvent.GetCurrentGrid(gridManager), OverlayCellType.MainEvent);
            }
        }
    }

    private void AddEnemyEncounterCells()
    {
        if (enemyRegistry == null)
            return;

        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyGridMover enemy = enemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
                continue;

            Vector2Int enemyGrid = enemy.GetCurrentGrid();
            AddEncounterCell(enemy, enemyGrid, OverlayCellType.Enemy);

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    Vector2Int grid = enemyGrid + new Vector2Int(x, y);
                    // JC | 2026-11-09: 다른 적의 본체 셀도 원래 표시 영역 후보에는 포함합니다.
                    // 아래 DH 논리 상태 필터는 그대로 두어 고정 영역에 시각적인 구멍만 생기는 것을 막습니다.
                    TrySetVisualCell(grid, OverlayCellType.Enemy, enemy);
                    EnemyEncounterZoneState state = gridManager.GetEnemyEncounterZoneState(grid, out _);
                    if (state != EnemyEncounterZoneState.SingleEnemyZone
                        && state != EnemyEncounterZoneState.OverlappedEnemyZone)
                    {
                        continue;
                    }

                    OverlayCellType type = state == EnemyEncounterZoneState.OverlappedEnemyZone
                        ? OverlayCellType.OverlappedEnemy
                        : OverlayCellType.Enemy;
                    AddEncounterCell(enemy, grid, type);
                }
            }
        }
    }

    private void AddOutpostInteractionCells()
    {
        if (outpostRegistry == null)
            return;

        IReadOnlyList<Outpost> outposts = outpostRegistry.Outposts;
        for (int i = 0; i < outposts.Count; i++)
        {
            Outpost outpost = outposts[i];
            if (outpost == null || !outpost.gameObject.activeInHierarchy)
                continue;

            OverlayCellType type = GetOutpostOverlayType(outpost.outpostState);
            IReadOnlyList<Vector2Int> interactionCells = outpost.GetAdjacentInteractionCells(gridManager);
            for (int cellIndex = 0; cellIndex < interactionCells.Count; cellIndex++)
            {
                Vector2Int grid = interactionCells[cellIndex];
                TrySetDesiredCell(grid, type, outpost);
            }
        }
    }

    private void AddHeroUnionInteractionCells()
    {
        if (heroUnionRegistry == null)
            return;

        IReadOnlyList<HeroUnionUnit> heroUnions = heroUnionRegistry.HeroUnions;
        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (heroUnion == null || !heroUnion.gameObject.activeInHierarchy)
                continue;

            IReadOnlyList<Vector2Int> interactionCells = heroUnion.GetInteractionCells();
            for (int cellIndex = 0; cellIndex < interactionCells.Count; cellIndex++)
            {
                Vector2Int grid = interactionCells[cellIndex];
                TrySetDesiredCell(grid, heroUnion.IsClaimedByHero ? OverlayCellType.Player : OverlayCellType.Neutral, heroUnion);
            }
        }
    }

    private void AddVillainUnionInteractionCells()
    {
        if (villainUnionBaseRegistry == null)
            return;

        IReadOnlyList<VillainUnionBase> bases = villainUnionBaseRegistry.VillainUnionBases;
        for (int i = 0; i < bases.Count; i++)
        {
            VillainUnionBase villainUnionBase = bases[i];
            if (villainUnionBase == null || !villainUnionBase.gameObject.activeInHierarchy)
                continue;

            IReadOnlyList<Vector2Int> interactionCells = villainUnionBase.GetInteractionCells();
            for (int cellIndex = 0; cellIndex < interactionCells.Count; cellIndex++)
            {
                Vector2Int grid = interactionCells[cellIndex];
                TrySetDesiredCell(grid, OverlayCellType.Enemy, villainUnionBase);
            }
        }
    }

    private bool TrySetDesiredCell(Vector2Int grid, OverlayCellType type, object owner)
    {
        if (gridManager != null && gridManager.HasObstacle(grid))
            return false;

        // JC | 2026-11-09: 기존 판정의 선점 여부와 무관하게 원본 후보를 표시용 합성에 전달합니다.
        TrySetVisualCell(grid, type, owner);
        if (desiredCells.TryGetValue(grid, out OverlayCellType existing)
            && GetPriority(existing) >= GetPriority(type))
        {
            return false;
        }

        desiredCells[grid] = type;
        desiredOwners[grid] = owner;
        return true;
    }

    // JC | 2026-11-09: 렌더에서만 중첩 색상을 일반 적 색상으로 통일하고 소유권 우선순위를 적용합니다.
    // 고정 적·메인이벤트·적 전초기지·적기지는 동일한 고정 전투 표시 우선순위입니다.
    // 외부 표시와 비전투 색상은 기존 규칙을 유지하며, 이 결과는 논리/클릭 데이터에 역으로 쓰지 않습니다.
    private void TrySetVisualCell(Vector2Int grid, OverlayCellType type, object owner)
    {
        if (gridManager != null && gridManager.HasObstacle(grid))
            return;
        if (type == OverlayCellType.OverlappedEnemy)
            type = OverlayCellType.Enemy;
        int priority = type == OverlayCellType.Enemy
            ? (owner is EnemyGridMover enemy && !enemy.IsStatic ? 3 : 5)
            : GetPriority(type);
        if (visualCellsByGrid.TryGetValue(grid, out var existing) && existing.Priority >= priority)
            return;
        Color color = type == OverlayCellType.External ? externalOverlaySets[owner].Color : GetColor(type);
        visualCellsByGrid[grid] = new InteractionOverlayCell(owner, grid, color, priority);
    }

    private void HideRemovedZones()
    {
        removalBuffer.Clear();
        foreach (KeyValuePair<Vector2Int, OverlayInstance> pair in activeOverlays)
        {
            if (!desiredCells.ContainsKey(pair.Key))
                removalBuffer.Add(pair.Key);
        }

        for (int i = 0; i < removalBuffer.Count; i++)
        {
            Vector2Int grid = removalBuffer[i];
            if (!activeOverlays.TryGetValue(grid, out OverlayInstance instance))
                continue;

            activeOverlays.Remove(grid);
            ReleaseOverlay(instance);
        }
    }

    private void ShowDesiredZones()
    {
        foreach (KeyValuePair<Vector2Int, OverlayCellType> pair in desiredCells)
        {
            if (!activeOverlays.TryGetValue(pair.Key, out OverlayInstance instance))
            {
                instance = GetOverlay();
                activeOverlays.Add(pair.Key, instance);
            }

            ApplyOverlay(instance, pair.Key, pair.Value);
        }
    }

    private OverlayInstance GetOverlay()
    {
        OverlayInstance instance = overlayPool.Count > 0 ? overlayPool.Pop() : CreateOverlay();
        if (instance.GameObject != null)
            instance.GameObject.SetActive(true);

        return instance;
    }

    private OverlayInstance CreateOverlay()
    {
        GameObject overlayObject;
        if (overlayPrefab != null)
        {
            overlayObject = Instantiate(overlayPrefab, overlayRoot);
        }
        else
        {
            overlayObject = new GameObject("InteractionCellOverlay");
            overlayObject.transform.SetParent(overlayRoot, false);

            MeshFilter meshFilter = overlayObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = generatedMesh;

            MeshRenderer meshRenderer = overlayObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = generatedMaterial;
        }

        Collider[] colliders = overlayObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Destroy(colliders[i]);

        return new OverlayInstance(overlayObject);
    }

    private void ApplyOverlay(OverlayInstance instance, Vector2Int grid, OverlayCellType type)
    {
        if (instance == null || instance.GameObject == null)
            return;

        Vector3 position = gridManager.GridToWorldCenter(grid);
        position.y = gridManager.GetCellSurfaceY(grid) + yOffset;
        instance.GameObject.transform.position = position;

        float size = Mathf.Max(0.01f, gridManager.CellSize * Mathf.Max(1f, cellScale));
        instance.GameObject.transform.localScale = new Vector3(size, 1f, size);

        Color color = type == OverlayCellType.External && externalDesiredCellColors.TryGetValue(grid, out Color externalColor)
            ? externalColor
            : GetColor(type);
        instance.ApplyLighting(generatedMaterial);
        instance.ApplyColor(color);
        instance.SetPresentationHidden(visualOverride != null);
    }

    private void ReleaseOverlay(OverlayInstance instance)
    {
        if (instance == null || instance.GameObject == null)
            return;

        instance.GameObject.SetActive(false);
        overlayPool.Push(instance);
    }

    private void ClearActiveOverlays()
    {
        visualOverride?.ClearCells();
        // JC | 2026-11-09: 연결 해제 시 표시 전용 셀과 소유자 참조도 함께 비웁니다.
        visualCellsByGrid.Clear();
        visibleCells.Clear();
        encounterCells.Clear();
        encounterBounds.Clear();
        foreach (KeyValuePair<Vector2Int, OverlayInstance> pair in activeOverlays)
            ReleaseOverlay(pair.Value);

        activeOverlays.Clear();
    }

    private void SubscribeRegistry()
    {
        if (enemyRegistry == null)
            return;

        enemyRegistry.EnemyRegistered -= HandleEnemyRegistered;
        enemyRegistry.EnemyUnregistered -= HandleEnemyUnregistered;
        enemyRegistry.EnemyRegistered += HandleEnemyRegistered;
        enemyRegistry.EnemyUnregistered += HandleEnemyUnregistered;
    }

    private void UnsubscribeRegistry()
    {
        if (enemyRegistry == null)
            return;

        enemyRegistry.EnemyRegistered -= HandleEnemyRegistered;
        enemyRegistry.EnemyUnregistered -= HandleEnemyUnregistered;
    }

    private void SubscribeSceneEnemies()
    {
        if (enemyRegistry == null)
            return;

        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
            SubscribeEnemy(enemies[i]);
    }

    private void SubscribeEnemy(EnemyGridMover enemy)
    {
        if (enemy == null || subscribedEnemies.Contains(enemy))
            return;

        subscribedEnemies.Add(enemy);
        enemy.GridChanged += HandleEnemyGridChanged;
    }

    private void UnsubscribeEnemy(EnemyGridMover enemy)
    {
        if (enemy == null || !subscribedEnemies.Remove(enemy))
            return;

        enemy.GridChanged -= HandleEnemyGridChanged;
    }

    private void UnsubscribeEnemies()
    {
        foreach (EnemyGridMover enemy in subscribedEnemies)
        {
            if (enemy != null)
                enemy.GridChanged -= HandleEnemyGridChanged;
        }

        subscribedEnemies.Clear();
    }

    private void SubscribeMainEventRegistry()
    {
        if (mainEventRegistry == null)
            return;

        mainEventRegistry.MainEventRegistered -= HandleMainEventChanged;
        mainEventRegistry.MainEventUnregistered -= HandleMainEventChanged;
        mainEventRegistry.MainEventRegistered += HandleMainEventChanged;
        mainEventRegistry.MainEventUnregistered += HandleMainEventChanged;
    }

    private void UnsubscribeMainEventRegistry()
    {
        if (mainEventRegistry == null)
            return;

        mainEventRegistry.MainEventRegistered -= HandleMainEventChanged;
        mainEventRegistry.MainEventUnregistered -= HandleMainEventChanged;
    }

    private void HandleEnemyRegistered(EnemyGridMover enemy)
    {
        SubscribeEnemy(enemy);
        RequestRefresh();
    }

    private void HandleEnemyUnregistered(EnemyGridMover enemy)
    {
        UnsubscribeEnemy(enemy);
        RequestRefresh();
    }

    private void HandleEnemyGridChanged(EnemyGridMover enemy, Vector2Int grid)
    {
        RequestRefresh();
    }

    private void HandleOutpostChanged(Outpost outpost)
    {
        RequestRefresh();
    }

    private void HandleHeroUnionChanged(HeroUnionUnit heroUnion)
    {
        RequestRefresh();
    }

    private void HandleMainEventChanged(MainEventObject mainEvent)
    {
        RequestRefresh();
    }

    private OverlayCellType GetOutpostOverlayType(OutpostState state)
    {
        return state switch
        {
            OutpostState.Claimed => OverlayCellType.Player,
            OutpostState.EnemyClaimed => OverlayCellType.Enemy,
            _ => OverlayCellType.Neutral
        };
    }

    private Color GetColor(OverlayCellType type)
    {
        return type switch
        {
            OverlayCellType.Neutral => neutralInteractionColor,
            OverlayCellType.Player => playerInteractionColor,
            OverlayCellType.MainEvent => mainEventInteractionColor,
            OverlayCellType.OverlappedEnemy => overlappedZoneColor,
            _ => singleZoneColor
        };
    }

    private static int GetPriority(OverlayCellType type)
    {
        return type switch
        {
            OverlayCellType.External => 6,
            OverlayCellType.MainEvent => 5,
            OverlayCellType.OverlappedEnemy => 4,
            OverlayCellType.Enemy => 3,
            OverlayCellType.Player => 2,
            _ => 1
        };
    }

    private ExternalOverlaySet GetOrCreateExternalOverlaySet(object owner)
    {
        if (!externalOverlaySets.TryGetValue(owner, out ExternalOverlaySet set) || set == null)
        {
            set = new ExternalOverlaySet();
            externalOverlaySets[owner] = set;
        }

        return set;
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (mainEventRegistry == null)
            mainEventRegistry = FindFirstObjectByType<MainEventRegistry>();

    }

    private void EnsureOverlayRoot()
    {
        if (overlayRoot != null)
            return;

        GameObject root = new GameObject("InteractionCellOverlays");
        root.transform.SetParent(transform, false);
        overlayRoot = root.transform;
    }

    private void EnsureGeneratedAssets()
    {
        if (generatedMesh == null)
            generatedMesh = CreateOverlayMesh();

        if (generatedMaterial == null)
            generatedMaterial = CreateOverlayMaterial(singleZoneColor);
    }

    private static Mesh CreateOverlayMesh()
    {
        Mesh mesh = new Mesh
        {
            name = "Interaction Cell Overlay Mesh"
        };

        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, -0.5f)
        };
        mesh.triangles = new[]
        {
            0, 1, 2,
            0, 2, 3,
            2, 1, 0,
            3, 2, 0
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        // 기존 양면 표시와 투명도는 유지하고, 상쇄되는 법선만 지면 위 방향으로 지정합니다.
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateOverlayMaterial(Color color)
    {
        Shader shader = Resources.Load<Shader>("JcNightLit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = "Interaction Cell Overlay Lit Material",
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_JcCombatOverlay")) material.SetFloat("_JcCombatOverlay", 1);
        material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        // 모든 월드 셀 안내 표시가 조명과 그림자를 받도록 통일합니다.
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0f);
        material.SetFloat("_SpecularHighlights", 0f);
        material.SetFloat("_EnvironmentReflections", 0f);
        material.SetFloat("_ReceiveShadows", 1f);
        material.SetColor("_EmissionColor", Color.black);
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.DisableKeyword("_EMISSION");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private void OnValidate()
    {
        cellScale = Mathf.Clamp(cellScale, 1f, 1.2f);
        refreshInterval = Mathf.Max(0.02f, refreshInterval);
        if (overlappedZoneColor.a < 0.35f)
            overlappedZoneColor.a = 0.55f;
    }

    private sealed class OverlayInstance
    {
        private readonly Renderer[] renderers;
        private readonly MaterialPropertyBlock propertyBlock;
        private readonly Material[][] originalMaterials;
        private Material lightingMaterial;

        public OverlayInstance(GameObject gameObject)
        {
            GameObject = gameObject;
            renderers = gameObject != null
                ? gameObject.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            originalMaterials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i].sharedMaterials;
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                renderers[i].receiveShadows = true;
            }
        }

        public GameObject GameObject { get; }
        private bool presentationHidden;
        private bool[] previousForceRenderingOff;
        public void SetPresentationHidden(bool hidden)
        {
            if (presentationHidden == hidden) return;
            if (hidden) previousForceRenderingOff = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;
                if (hidden) previousForceRenderingOff[i] = renderers[i].forceRenderingOff;
                renderers[i].forceRenderingOff = hidden || previousForceRenderingOff[i];
            }
            presentationHidden = hidden;
        }

        public void ApplyLighting(Material material)
        {
            if (lightingMaterial == material) return;
            lightingMaterial = material;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (material == null) renderers[i].sharedMaterials = originalMaterials[i];
                else
                {
                    var materials = new Material[originalMaterials[i].Length];
                    for (int slot = 0; slot < materials.Length; slot++) materials[slot] = material;
                    renderers[i].sharedMaterials = materials;
                }
            }
        }

        public void ApplyColor(Color color)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }

    private sealed class ExternalOverlaySet
    {
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();
        public Color Color;
    }
}
