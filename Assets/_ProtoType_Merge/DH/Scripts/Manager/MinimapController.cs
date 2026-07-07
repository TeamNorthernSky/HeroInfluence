using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RawImage targetImage;
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private Image iconPrefab;
    [SerializeField] private LevelData levelData;
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private LevelZoneLayoutData levelZoneLayoutData;
    [SerializeField] private LevelZoneLayoutLoader levelZoneLayoutLoader;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;

    [Header("Draw Options")]
    [SerializeField] private bool drawOnEnable = true;
    [SerializeField] private bool useFogVisibility = true;
    [SerializeField, Min(0f)] private float partyIconRefreshInterval = 0.1f;
    [SerializeField] private FilterMode filterMode = FilterMode.Point;

    [Header("Cell Colors")]
    [SerializeField] private Color unexploredColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color foggedColor = new Color(0.08f, 0.08f, 0.08f, 0.65f);
    [SerializeField] private Color visibleColor = new Color(0.22f, 0.28f, 0.24f, 1f);
    [SerializeField] private Color fallbackColor = new Color(0.22f, 0.28f, 0.24f, 1f);
    [SerializeField] private Color obstacleColor = new Color(0f, 0f, 0f, 1f);

    [Header("Strategic Objects")]
    [SerializeField] private bool showStrategicObjects = true;
    [SerializeField] private Color playerBuildingColor = new Color(0f, 0.35f, 1f, 1f);
    [SerializeField] private Color enemyBuildingColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color neutralOutpostColor = new Color(1f, 0.85f, 0f, 1f);

    [Header("Party Icons")]
    [SerializeField] private bool showPartyIcons = true;
    [SerializeField] private Color partyIconColor = new Color(0f, 1f, 0.1f, 1f);
    [SerializeField, Min(0.1f)] private float partyIconScale = 1f;

    private Texture2D minimapTexture;
    private Vector2Int textureSize;
    private Vector2Int currentGridSize;
    private float nextPartyIconRefreshTime;
    private readonly List<Image> partyIconPool = new List<Image>();
    private readonly HashSet<Vector2Int> obstacleCellCache = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> dirtyCells = new HashSet<Vector2Int>();
    private bool hasPendingTextureApply;

    private bool HasLoadedZoneData =>
        levelZoneLayoutLoader != null
        && levelZoneLayoutLoader.LoadedZones != null
        && levelZoneLayoutLoader.LoadedZones.Count > 0;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        SubscribeToLevelLoaders();
        SubscribeToFogGridManager();
        SubscribeToHeroUnionStateChanges();

        if (drawOnEnable)
            Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromLevelLoaders();
        UnsubscribeFromFogGridManager();
        UnsubscribeFromHeroUnionStateChanges();
    }

    private void OnDestroy()
    {
        ReleaseTexture();
    }

    private void Update()
    {
        if (partyIconRefreshInterval <= 0f || Time.unscaledTime < nextPartyIconRefreshTime)
            return;

        nextPartyIconRefreshTime = Time.unscaledTime + partyIconRefreshInterval;
        RefreshPartyIconsOnly();
    }

    private void LateUpdate()
    {
        ApplyDirtyCells();
    }

    [ContextMenu("Refresh Minimap")]
    public void Refresh()
    {
        ResolveReferences();

        if (targetImage == null || !TryGetGridSize(out Vector2Int gridSize))
            return;

        currentGridSize = gridSize;
        EnsureTexture(gridSize);
        RebuildObstacleCellCache();
        DrawCells(gridSize);
        DrawStrategicObjects(gridSize);
        minimapTexture.Apply(false);
        targetImage.texture = minimapTexture;
        dirtyCells.Clear();
        hasPendingTextureApply = false;

        DrawPartyIcons(gridSize);
    }

    public void RefreshPartyIconsOnly()
    {
        if (!showPartyIcons)
        {
            HidePartyIcons(0);
            return;
        }

        ResolveReferences();

        Vector2Int gridSize = currentGridSize;
        if (gridSize.x <= 0 || gridSize.y <= 0)
        {
            if (!TryGetGridSize(out gridSize))
            {
                HidePartyIcons(0);
                return;
            }

            currentGridSize = gridSize;
        }

        DrawPartyIcons(gridSize);
    }

    public void SetLevelData(LevelData nextLevelData)
    {
        levelData = nextLevelData;
        Refresh();
    }

    public void SetFogGridManager(FogGridManager nextFogGridManager)
    {
        if (fogGridManager == nextFogGridManager)
            return;

        if (isActiveAndEnabled && fogGridManager != null)
            UnsubscribeFromFogGridManager();

        fogGridManager = nextFogGridManager;

        if (isActiveAndEnabled && fogGridManager != null)
            SubscribeToFogGridManager();

        Refresh();
    }

    private void DrawCells(Vector2Int gridSize)
    {
        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Vector2Int grid = new Vector2Int(x, y);
                minimapTexture.SetPixel(x, y, GetCellColor(grid));
            }
        }
    }

    private void DrawStrategicObjects(Vector2Int gridSize)
    {
        if (!showStrategicObjects)
            return;

        DrawOutposts(gridSize);
        DrawHeroUnions(gridSize);
        DrawVillainUnionBases(gridSize);
    }

    private void DrawOutposts(Vector2Int gridSize)
    {
        Outpost[] fallbackOutposts = null;
        IReadOnlyList<Outpost> outposts = outpostRegistry != null ? outpostRegistry.Outposts : null;
        int count = outposts != null ? outposts.Count : 0;
        bool useFallback = count == 0;

        if (useFallback)
        {
            fallbackOutposts = FindObjectsByType<Outpost>(FindObjectsSortMode.None);
            count = fallbackOutposts.Length;
        }

        for (int i = 0; i < count; i++)
        {
            Outpost outpost = useFallback ? fallbackOutposts[i] : outposts[i];
            if (outpost == null || !outpost.isActiveAndEnabled)
                continue;

            DrawComponentFootprint(
                outpost,
                GetOutpostMinimapColor(outpost.outpostState),
                gridSize,
                outpost.GetAnchorGrid(gridManager));
        }
    }

    private void DrawHeroUnions(Vector2Int gridSize)
    {
        HeroUnionUnit[] fallbackHeroUnions = null;
        IReadOnlyList<HeroUnionUnit> heroUnions = heroUnionRegistry != null ? heroUnionRegistry.HeroUnions : null;
        int count = heroUnions != null ? heroUnions.Count : 0;
        bool useFallback = count == 0;

        if (useFallback)
        {
            fallbackHeroUnions = FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);
            count = fallbackHeroUnions.Length;
        }

        for (int i = 0; i < count; i++)
        {
            HeroUnionUnit heroUnion = useFallback ? fallbackHeroUnions[i] : heroUnions[i];
            if (heroUnion == null || !heroUnion.isActiveAndEnabled)
                continue;

            DrawComponentFootprint(heroUnion, GetHeroUnionMinimapColor(heroUnion), gridSize, heroUnion.GetCurrentGrid());
        }
    }

    private void DrawVillainUnionBases(Vector2Int gridSize)
    {
        VillainUnionBase[] fallbackBases = null;
        IReadOnlyList<VillainUnionBase> bases = villainUnionBaseRegistry != null ? villainUnionBaseRegistry.VillainUnionBases : null;
        int count = bases != null ? bases.Count : 0;
        bool useFallback = count == 0;

        if (useFallback)
        {
            fallbackBases = FindObjectsByType<VillainUnionBase>(FindObjectsSortMode.None);
            count = fallbackBases.Length;
        }

        for (int i = 0; i < count; i++)
        {
            VillainUnionBase villainUnionBase = useFallback ? fallbackBases[i] : bases[i];
            if (villainUnionBase == null || !villainUnionBase.isActiveAndEnabled)
                continue;

            DrawComponentFootprint(
                villainUnionBase,
                enemyBuildingColor,
                gridSize,
                villainUnionBase.GetAnchorGrid());
        }
    }

    private void DrawComponentFootprint(Component component, Color color, Vector2Int gridSize, Vector2Int fallbackGrid)
    {
        if (component == null)
            return;

        MultiGridOccupant occupant = component.GetComponent<MultiGridOccupant>();
        if (occupant == null)
        {
            DrawStrategicCell(fallbackGrid, color, gridSize);
            return;
        }

        IReadOnlyList<Vector2Int> occupiedCells = occupant.GetOccupiedCells();
        for (int i = 0; i < occupiedCells.Count; i++)
            DrawStrategicCell(occupiedCells[i], color, gridSize);
    }

    private void DrawStrategicCell(Vector2Int grid, Color color, Vector2Int gridSize)
    {
        if (!IsGridInMinimap(grid, gridSize) || IsUnexplored(grid))
            return;

        minimapTexture.SetPixel(grid.x, grid.y, color);
    }

    private bool TryGetStrategicCellColor(Vector2Int grid, out Color color)
    {
        if (showStrategicObjects)
        {
            if (TryGetOutpostColorAtGrid(grid, out color))
                return true;

            if (TryGetHeroUnionColorAtGrid(grid, out color))
                return true;

            if (TryGetVillainUnionColorAtGrid(grid, out color))
                return true;
        }

        color = default;
        return false;
    }

    private bool TryGetOutpostColorAtGrid(Vector2Int grid, out Color color)
    {
        Outpost[] fallbackOutposts = null;
        IReadOnlyList<Outpost> outposts = outpostRegistry != null ? outpostRegistry.Outposts : null;
        int count = outposts != null ? outposts.Count : 0;
        bool useFallback = count == 0;

        if (useFallback)
        {
            fallbackOutposts = FindObjectsByType<Outpost>(FindObjectsSortMode.None);
            count = fallbackOutposts.Length;
        }

        for (int i = 0; i < count; i++)
        {
            Outpost outpost = useFallback ? fallbackOutposts[i] : outposts[i];
            if (outpost == null || !outpost.isActiveAndEnabled)
                continue;

            if (!ComponentContainsGrid(outpost, grid, outpost.GetAnchorGrid(gridManager)))
                continue;

            color = GetOutpostMinimapColor(outpost.outpostState);
            return true;
        }

        color = default;
        return false;
    }

    private bool TryGetHeroUnionColorAtGrid(Vector2Int grid, out Color color)
    {
        HeroUnionUnit[] fallbackHeroUnions = null;
        IReadOnlyList<HeroUnionUnit> heroUnions = heroUnionRegistry != null ? heroUnionRegistry.HeroUnions : null;
        int count = heroUnions != null ? heroUnions.Count : 0;
        bool useFallback = count == 0;

        if (useFallback)
        {
            fallbackHeroUnions = FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);
            count = fallbackHeroUnions.Length;
        }

        for (int i = 0; i < count; i++)
        {
            HeroUnionUnit heroUnion = useFallback ? fallbackHeroUnions[i] : heroUnions[i];
            if (heroUnion == null || !heroUnion.isActiveAndEnabled)
                continue;

            if (!ComponentContainsGrid(heroUnion, grid, heroUnion.GetCurrentGrid()))
                continue;

            color = GetHeroUnionMinimapColor(heroUnion);
            return true;
        }

        color = default;
        return false;
    }

    private Color GetHeroUnionMinimapColor(HeroUnionUnit heroUnion)
    {
        return heroUnion != null && heroUnion.IsClaimedByHero ? playerBuildingColor : neutralOutpostColor;
    }

    private bool TryGetVillainUnionColorAtGrid(Vector2Int grid, out Color color)
    {
        VillainUnionBase[] fallbackBases = null;
        IReadOnlyList<VillainUnionBase> bases = villainUnionBaseRegistry != null
            ? villainUnionBaseRegistry.VillainUnionBases
            : null;
        int count = bases != null ? bases.Count : 0;
        bool useFallback = count == 0;

        if (useFallback)
        {
            fallbackBases = FindObjectsByType<VillainUnionBase>(FindObjectsSortMode.None);
            count = fallbackBases.Length;
        }

        for (int i = 0; i < count; i++)
        {
            VillainUnionBase villainUnionBase = useFallback ? fallbackBases[i] : bases[i];
            if (villainUnionBase == null || !villainUnionBase.isActiveAndEnabled)
                continue;

            if (!ComponentContainsGrid(villainUnionBase, grid, villainUnionBase.GetAnchorGrid()))
                continue;

            color = enemyBuildingColor;
            return true;
        }

        color = default;
        return false;
    }

    private static bool ComponentContainsGrid(Component component, Vector2Int grid, Vector2Int fallbackGrid)
    {
        if (component == null)
            return false;

        MultiGridOccupant occupant = component.GetComponent<MultiGridOccupant>();
        if (occupant == null)
            return grid == fallbackGrid;

        IReadOnlyList<Vector2Int> occupiedCells = occupant.GetOccupiedCells();
        for (int i = 0; i < occupiedCells.Count; i++)
        {
            if (occupiedCells[i] == grid)
                return true;
        }

        return false;
    }

    private Color GetOutpostMinimapColor(OutpostState state)
    {
        return state switch
        {
            OutpostState.Claimed => playerBuildingColor,
            OutpostState.EnemyClaimed => enemyBuildingColor,
            _ => neutralOutpostColor
        };
    }

    private void DrawPartyIcons(Vector2Int gridSize)
    {
        if (!showPartyIcons || targetImage == null)
        {
            HidePartyIcons(0);
            return;
        }

        ResolveIconRoot();

        int iconIndex = 0;
        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party != null && party.isActiveAndEnabled)
        {
            Vector2Int grid = party.GetCurrentGrid();
            if (IsGridInMinimap(grid, gridSize))
            {
                Image icon = GetPartyIcon(iconIndex);
                ApplyIcon(icon, grid, gridSize, partyIconColor, GetCellIconSize(gridSize, partyIconScale));
                iconIndex++;
            }
        }

        HidePartyIcons(iconIndex);
    }

    private void ApplyIcon(Image icon, Vector2Int grid, Vector2Int gridSize, Color color, float size)
    {
        if (icon == null || iconRoot == null)
            return;

        RectTransform iconTransform = icon.rectTransform;
        iconTransform.anchoredPosition = GridToMinimapPosition(grid, gridSize);
        iconTransform.sizeDelta = new Vector2(size, size);
        icon.color = color;

        if (!icon.gameObject.activeSelf)
            icon.gameObject.SetActive(true);
    }

    private Image GetPartyIcon(int index)
    {
        while (partyIconPool.Count <= index)
            partyIconPool.Add(CreatePartyIcon());

        return partyIconPool[index];
    }

    private Image CreatePartyIcon()
    {
        ResolveIconRoot();

        Image icon = iconPrefab != null
            ? Instantiate(iconPrefab, iconRoot)
            : new GameObject("Party Minimap Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();

        RectTransform rectTransform = icon.rectTransform;
        rectTransform.SetParent(iconRoot, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;

        icon.raycastTarget = false;
        icon.color = partyIconColor;
        icon.gameObject.SetActive(false);
        return icon;
    }

    private void HidePartyIcons(int firstHiddenIndex)
    {
        for (int i = firstHiddenIndex; i < partyIconPool.Count; i++)
        {
            if (partyIconPool[i] != null)
                partyIconPool[i].gameObject.SetActive(false);
        }
    }

    private Vector2 GridToMinimapPosition(Vector2Int grid, Vector2Int gridSize)
    {
        Rect rect = iconRoot != null ? iconRoot.rect : targetImage.rectTransform.rect;
        float normalizedX = (grid.x + 0.5f) / Mathf.Max(1, gridSize.x);
        float normalizedY = (grid.y + 0.5f) / Mathf.Max(1, gridSize.y);
        return new Vector2(normalizedX * rect.width, normalizedY * rect.height);
    }

    private float GetCellIconSize(Vector2Int gridSize, float scale)
    {
        Rect rect = iconRoot != null ? iconRoot.rect : targetImage.rectTransform.rect;
        float width = rect.width / Mathf.Max(1, gridSize.x);
        float height = rect.height / Mathf.Max(1, gridSize.y);
        return Mathf.Min(width, height) * scale;
    }

    private static bool IsGridInMinimap(Vector2Int grid, Vector2Int gridSize)
    {
        return grid.x >= 0 && grid.y >= 0 && grid.x < gridSize.x && grid.y < gridSize.y;
    }

    private Color GetCellColor(Vector2Int grid)
    {
        if (!useFogVisibility || fogGridManager == null)
            return IsObstacleCell(grid) ? obstacleColor : fallbackColor;

        FogVisibilityState visibility = fogGridManager.GetVisibility(grid);
        if (visibility == FogVisibilityState.Unexplored)
            return MakeOpaque(unexploredColor);

        if (IsObstacleCell(grid))
            return obstacleColor;

        return visibility switch
        {
            FogVisibilityState.Visible => visibleColor,
            FogVisibilityState.Fogged => foggedColor,
            _ => MakeOpaque(unexploredColor)
        };
    }

    private bool IsObstacleCell(Vector2Int grid)
    {
        return obstacleCellCache.Contains(grid);
    }

    private void RebuildObstacleCellCache()
    {
        obstacleCellCache.Clear();

        if (HasLoadedZoneData)
        {
            IReadOnlyList<LoadedLevelZoneData> loadedZones = levelZoneLayoutLoader.LoadedZones;
            for (int zoneIndex = 0; zoneIndex < loadedZones.Count; zoneIndex++)
            {
                LoadedLevelZoneData zone = loadedZones[zoneIndex];
                LevelData zoneLevelData = zone.LevelData;
                if (zoneLevelData == null)
                    continue;

                IReadOnlyList<Vector2Int> zoneObstacleCells = zoneLevelData.ObstacleCells;
                for (int i = 0; i < zoneObstacleCells.Count; i++)
                    obstacleCellCache.Add(zone.Anchor + zoneObstacleCells[i]);
            }

            return;
        }

        if (levelData == null)
            return;

        IReadOnlyList<Vector2Int> obstacleCells = levelData.ObstacleCells;
        for (int i = 0; i < obstacleCells.Count; i++)
            obstacleCellCache.Add(obstacleCells[i]);
    }

    private bool IsUnexplored(Vector2Int grid)
    {
        return useFogVisibility
            && fogGridManager != null
            && fogGridManager.GetVisibility(grid) == FogVisibilityState.Unexplored;
    }

    private static Color MakeOpaque(Color color)
    {
        color.a = 1f;
        return color;
    }

    private bool TryGetGridSize(out Vector2Int gridSize)
    {
        if (levelZoneLayoutData != null)
        {
            gridSize = levelZoneLayoutData.TotalGridSize;
            return gridSize.x > 0 && gridSize.y > 0;
        }

        if (levelZoneLayoutLoader != null && levelZoneLayoutLoader.LayoutData != null)
        {
            gridSize = levelZoneLayoutLoader.LayoutData.TotalGridSize;
            return gridSize.x > 0 && gridSize.y > 0;
        }

        if (levelData != null)
        {
            gridSize = levelData.GridSize;
            return gridSize.x > 0 && gridSize.y > 0;
        }

        if (fogGridManager != null)
        {
            gridSize = fogGridManager.GridSize;
            return gridSize.x > 0 && gridSize.y > 0;
        }

        gridSize = Vector2Int.zero;
        return false;
    }

    private void EnsureTexture(Vector2Int gridSize)
    {
        if (minimapTexture != null && textureSize == gridSize)
        {
            minimapTexture.filterMode = filterMode;
            return;
        }

        ReleaseTexture();

        textureSize = gridSize;
        minimapTexture = new Texture2D(gridSize.x, gridSize.y, TextureFormat.RGBA32, false, true)
        {
            name = "DH Minimap Texture",
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private void ReleaseTexture()
    {
        if (minimapTexture == null)
            return;

        if (Application.isPlaying)
            Destroy(minimapTexture);
        else
            DestroyImmediate(minimapTexture);

        minimapTexture = null;
        textureSize = Vector2Int.zero;
        dirtyCells.Clear();
        hasPendingTextureApply = false;
    }

    private void HandleFogChanged()
    {
        if (dirtyCells.Count > 0)
            return;

        Refresh();
    }

    private void HandleFogCellVisibilityChanged(Vector2Int grid, FogVisibilityState visibility)
    {
        if (!IsGridInMinimap(grid, currentGridSize))
            return;

        dirtyCells.Add(grid);
    }

    private void ApplyDirtyCells()
    {
        if (minimapTexture == null || dirtyCells.Count == 0)
            return;

        foreach (Vector2Int grid in dirtyCells)
            RedrawCell(grid);

        dirtyCells.Clear();

        if (!hasPendingTextureApply)
            return;

        minimapTexture.Apply(false);
        hasPendingTextureApply = false;
    }

    private void RedrawCell(Vector2Int grid)
    {
        if (minimapTexture == null || !IsGridInMinimap(grid, currentGridSize))
            return;

        Color color = GetCellColor(grid);
        if (!IsUnexplored(grid) && TryGetStrategicCellColor(grid, out Color strategicColor))
            color = strategicColor;

        minimapTexture.SetPixel(grid.x, grid.y, color);
        hasPendingTextureApply = true;
    }

    private void SubscribeToFogGridManager()
    {
        if (fogGridManager == null)
            return;

        fogGridManager.FogChanged -= HandleFogChanged;
        fogGridManager.FogChanged += HandleFogChanged;
        fogGridManager.CellVisibilityChanged -= HandleFogCellVisibilityChanged;
        fogGridManager.CellVisibilityChanged += HandleFogCellVisibilityChanged;
    }

    private void UnsubscribeFromFogGridManager()
    {
        if (fogGridManager == null)
            return;

        fogGridManager.FogChanged -= HandleFogChanged;
        fogGridManager.CellVisibilityChanged -= HandleFogCellVisibilityChanged;
    }

    private void SubscribeToLevelLoaders()
    {
        LevelLoader.RuntimeLevelLoaded -= HandleRuntimeLevelLoaded;
        LevelLoader.RuntimeLevelLoaded += HandleRuntimeLevelLoaded;
        LevelZoneLayoutLoader.RuntimeLayoutLoaded -= HandleRuntimeLayoutLoaded;
        LevelZoneLayoutLoader.RuntimeLayoutLoaded += HandleRuntimeLayoutLoaded;
    }

    private void UnsubscribeFromLevelLoaders()
    {
        LevelLoader.RuntimeLevelLoaded -= HandleRuntimeLevelLoaded;
        LevelZoneLayoutLoader.RuntimeLayoutLoaded -= HandleRuntimeLayoutLoaded;
    }

    private void HandleRuntimeLevelLoaded(LevelLoader loader)
    {
        if (loader == null)
            return;

        if (levelLoader != null && levelLoader != loader)
            return;

        levelLoader = loader;
        levelData = loader.LevelData;

        if (gridManager == null)
            gridManager = loader.GridManager;

        Refresh();
    }

    private void SubscribeToHeroUnionStateChanges()
    {
        HeroUnionUnit.HeroUnionStateChanged -= HandleHeroUnionStateChanged;
        HeroUnionUnit.HeroUnionStateChanged += HandleHeroUnionStateChanged;
    }

    private void UnsubscribeFromHeroUnionStateChanges()
    {
        HeroUnionUnit.HeroUnionStateChanged -= HandleHeroUnionStateChanged;
    }

    private void HandleHeroUnionStateChanged(HeroUnionUnit heroUnion)
    {
        Refresh();
    }
    private void HandleRuntimeLayoutLoaded(LevelZoneLayoutLoader loader)
    {
        if (loader == null)
            return;

        if (levelZoneLayoutLoader != null && levelZoneLayoutLoader != loader)
            return;

        levelZoneLayoutLoader = loader;
        levelZoneLayoutData = loader.LayoutData;

        if (gridManager == null)
            gridManager = loader.GridManager;

        Refresh();
    }

    private void ResolveReferences()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();

        ResolveIconRoot();

        if (levelLoader == null)
            levelLoader = FindFirstObjectByType<LevelLoader>();

        if (levelZoneLayoutLoader == null)
            levelZoneLayoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();

        if (levelZoneLayoutData == null && levelZoneLayoutLoader != null)
            levelZoneLayoutData = levelZoneLayoutLoader.LayoutData;

        if (levelData == null && levelLoader != null)
            levelData = levelLoader.LevelData;

        if (gridManager == null && levelZoneLayoutLoader != null)
            gridManager = levelZoneLayoutLoader.GridManager;

        if (gridManager == null && levelLoader != null)
            gridManager = levelLoader.GridManager;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (fogGridManager == null)
            fogGridManager = FindFirstObjectByType<FogGridManager>();

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();
    }

    private void ResolveIconRoot()
    {
        if (iconRoot == null && targetImage != null)
            iconRoot = targetImage.rectTransform;
    }
}
