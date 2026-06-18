using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class InteractionCellOverlayController : MonoBehaviour
{
    private enum OverlayCellType
    {
        Neutral,
        Player,
        Enemy,
        OverlappedEnemy
    }

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private CastleRegistry castleRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private Transform overlayRoot;
    [SerializeField] private GameObject overlayPrefab;

    [Header("Display")]
    [SerializeField] private bool showOverlappedZones;
    [SerializeField] private bool hideFoggedCells = true;
    [SerializeField] private Color singleZoneColor = new Color(1f, 0f, 0f, 0.28f);
    [SerializeField] private Color overlappedZoneColor = new Color(1f, 0f, 0f, 0.12f);
    [SerializeField] private Color playerInteractionColor = new Color(0f, 0.35f, 1f, 0.28f);
    [SerializeField] private Color neutralInteractionColor = new Color(1f, 0.85f, 0f, 0.28f);
    [SerializeField, Range(0.1f, 1.2f)] private float cellScale = 0.92f;
    [SerializeField] private float yOffset = 0.035f;
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;

    private readonly Dictionary<Vector2Int, OverlayInstance> activeOverlays = new Dictionary<Vector2Int, OverlayInstance>();
    private readonly Stack<OverlayInstance> overlayPool = new Stack<OverlayInstance>();
    private readonly HashSet<EnemyGridMover> subscribedEnemies = new HashSet<EnemyGridMover>();
    private readonly Dictionary<Vector2Int, OverlayCellType> desiredCells = new Dictionary<Vector2Int, OverlayCellType>();
    private readonly List<Vector2Int> removalBuffer = new List<Vector2Int>();

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
        RequestRefresh();
    }

    private void OnDisable()
    {
        UnsubscribeRegistry();
        UnsubscribeEnemies();
        Outpost.OutpostClaimed -= HandleOutpostChanged;
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
    }

    private void BuildDesiredZones()
    {
        desiredCells.Clear();
        AddEnemyEncounterCells();
        AddOutpostInteractionCells();
        AddCastleInteractionCells();
        AddVillainUnionInteractionCells();
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
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    Vector2Int grid = enemyGrid + new Vector2Int(x, y);
                    EnemyEncounterZoneState state = gridManager.GetEnemyEncounterZoneState(grid, out _);
                    if (state != EnemyEncounterZoneState.SingleEnemyZone
                        && (!showOverlappedZones || state != EnemyEncounterZoneState.OverlappedEnemyZone))
                    {
                        continue;
                    }

                    if (hideFoggedCells && fogGridManager != null && !fogGridManager.IsVisible(grid))
                        continue;

                    OverlayCellType type = state == EnemyEncounterZoneState.OverlappedEnemyZone
                        ? OverlayCellType.OverlappedEnemy
                        : OverlayCellType.Enemy;
                    TrySetDesiredCell(grid, type);
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
                if (hideFoggedCells && fogGridManager != null && !fogGridManager.IsVisible(grid))
                    continue;

                TrySetDesiredCell(grid, type);
            }
        }
    }

    private void AddCastleInteractionCells()
    {
        if (castleRegistry == null)
            return;

        IReadOnlyList<CastleUnit> castles = castleRegistry.Castles;
        for (int i = 0; i < castles.Count; i++)
        {
            CastleUnit castle = castles[i];
            if (castle == null || !castle.gameObject.activeInHierarchy)
                continue;

            IReadOnlyList<Vector2Int> interactionCells = castle.GetInteractionCells();
            for (int cellIndex = 0; cellIndex < interactionCells.Count; cellIndex++)
            {
                Vector2Int grid = interactionCells[cellIndex];
                if (hideFoggedCells && fogGridManager != null && !fogGridManager.IsVisible(grid))
                    continue;

                TrySetDesiredCell(grid, OverlayCellType.Player);
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
                if (hideFoggedCells && fogGridManager != null && !fogGridManager.IsVisible(grid))
                    continue;

                TrySetDesiredCell(grid, OverlayCellType.Enemy);
            }
        }
    }

    private void TrySetDesiredCell(Vector2Int grid, OverlayCellType type)
    {
        if (desiredCells.TryGetValue(grid, out OverlayCellType existing)
            && GetPriority(existing) >= GetPriority(type))
        {
            return;
        }

        desiredCells[grid] = type;
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
        position.y = gridManager.GetLandSurfaceY() + yOffset;
        instance.GameObject.transform.position = position;

        float size = Mathf.Max(0.01f, gridManager.CellSize * cellScale);
        instance.GameObject.transform.localScale = new Vector3(size, 1f, size);

        instance.ApplyColor(GetColor(type));
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
            OverlayCellType.OverlappedEnemy => overlappedZoneColor,
            _ => singleZoneColor
        };
    }

    private static int GetPriority(OverlayCellType type)
    {
        return type switch
        {
            OverlayCellType.OverlappedEnemy => 4,
            OverlayCellType.Enemy => 3,
            OverlayCellType.Player => 2,
            _ => 1
        };
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (castleRegistry == null)
            castleRegistry = FindFirstObjectByType<CastleRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (fogGridManager == null)
            fogGridManager = FindFirstObjectByType<FogGridManager>();
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
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateOverlayMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = "Interaction Cell Overlay Material",
            renderQueue = (int)RenderQueue.Transparent
        };

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

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private void OnValidate()
    {
        cellScale = Mathf.Clamp(cellScale, 0.1f, 1.2f);
        refreshInterval = Mathf.Max(0.02f, refreshInterval);
    }

    private sealed class OverlayInstance
    {
        private readonly Renderer[] renderers;
        private readonly MaterialPropertyBlock propertyBlock;

        public OverlayInstance(GameObject gameObject)
        {
            GameObject = gameObject;
            renderers = gameObject != null
                ? gameObject.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
        }

        public GameObject GameObject { get; }

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
}
