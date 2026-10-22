using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [System.Serializable]
    private class ZoneViewCrop
    {
        public string zoneId;
        public int top;
        public int bottom;
        public int left;
        public int right;

        public void Apply(ref Vector2Int origin, ref Vector2Int size)
        {
            // Keep at least one cell on each axis even if margins exceed the zone size.
            int cropLeft = Mathf.Clamp(left, 0, size.x - 1);
            int cropRight = Mathf.Clamp(right, 0, size.x - cropLeft - 1);
            int cropBottom = Mathf.Clamp(bottom, 0, size.y - 1);
            int cropTop = Mathf.Clamp(top, 0, size.y - cropBottom - 1);
            int padLeft = Mathf.Max(0, -left);
            int padRight = Mathf.Max(0, -right);
            int padBottom = Mathf.Max(0, -bottom);
            int padTop = Mathf.Max(0, -top);
            origin += new Vector2Int(cropLeft - padLeft, cropBottom - padBottom);
            size += new Vector2Int(padLeft + padRight - cropLeft - cropRight,
                padBottom + padTop - cropBottom - cropTop);
        }
    }

    [Header("References")]
    [SerializeField] private RawImage targetImage;
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private Image iconPrefab;
    [SerializeField] private LevelData levelData;
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private LevelZoneLayoutData levelZoneLayoutData;
    [SerializeField] private LevelZoneLayoutLoader levelZoneLayoutLoader;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private PartyRegistry partyRegistry;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private HeroUnionRegistry heroUnionRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;
    [SerializeField] private EnemyRegistry enemyRegistry;
    [SerializeField] private MainEventRegistry mainEventRegistry;
    [SerializeField] private SubEventRegistry subEventRegistry;

    [Header("Draw Options")]
    [SerializeField] private bool drawOnEnable = true;
    [SerializeField, Min(0f)] private float partyIconRefreshInterval = 0.1f;
    [SerializeField] private FilterMode filterMode = FilterMode.Point;

    [Header("Cell Colors")]
    [SerializeField] private Color visibleColor = new Color(0.22f, 0.28f, 0.24f, 1f);
    [SerializeField] private Color fallbackColor = new Color(0.22f, 0.28f, 0.24f, 1f);
    [SerializeField] private Color obstacleColor = new Color(0f, 0f, 0f, 1f);

    [Header("Zone View Cropping")]
    [Tooltip("Positive: crop cells. Negative: add black padding. Top: +Y, Bottom: -Y, Left: -X, Right: +X. First matching Zone ID wins.")]
    [SerializeField] private List<ZoneViewCrop> zoneViewCrops = new List<ZoneViewCrop>();

    [Header("Strategic Objects")]
    [SerializeField] private bool showStrategicObjects = true;
    [SerializeField] private Color playerBuildingColor = new Color(0f, 0.35f, 1f, 1f);
    [SerializeField] private Color enemyBuildingColor = new Color(1f, 0f, 0f, 1f);
    [SerializeField] private Color neutralOutpostColor = new Color(1f, 0.85f, 0f, 1f);

    [Header("Strategic Object Icons")]
    [SerializeField] private bool showStrategicObjectIcons = true;
    [SerializeField] private Image strategicIconPrefab;
    [SerializeField] private Sprite playerBuildingSprite;
    [SerializeField] private Sprite enemyBuildingSprite;
    [SerializeField] private Sprite neutralBuildingSprite;
    [SerializeField] private Sprite eventOrStaticEnemySprite;
    [SerializeField] private Sprite mobileEnemySprite;
    [SerializeField] private Sprite subEventSprite;
    [SerializeField] private Sprite gateSprite;

    [Header("Strategic Object Icon Scales")]
    [SerializeField, Min(0.1f)] private float outpostIconScale = 1f;
    [SerializeField, Min(0.1f)] private float heroUnionIconScale = 1f;
    [SerializeField, Min(0.1f)] private float villainUnionIconScale = 1f;
    [SerializeField, Min(0.1f)] private float mainEventIconScale = 1f;
    [SerializeField, Min(0.1f)] private float staticEnemyIconScale = 1f;
    [SerializeField, Min(0.1f)] private float mobileEnemyIconScale = 1f;
    [SerializeField, Min(0.1f)] private float subEventIconScale = 1f;
    [SerializeField, Min(0.1f)] private float gateIconScale = 1f;

    [Header("Party Icons")]
    [SerializeField] private bool showPartyIcons = true;
    [SerializeField] private Sprite playerPartySprite;
    [SerializeField, Min(0.1f)] private float playerPartyIconScale = 1f;
    [SerializeField] private Color partyIconColor = new Color(0f, 1f, 0.1f, 1f);
    [SerializeField, Min(0.1f)] private float partyIconScale = 1f;

    private Texture2D minimapTexture;
    private Vector2Int textureSize;
    private Vector2Int currentGridSize;
    private Vector2Int currentViewOrigin;
    private RectInt currentContentBounds;
    private string currentViewZoneId = string.Empty;
    private float nextPartyIconRefreshTime;
    private readonly List<Image> strategicIconPool = new List<Image>();
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
        SubscribeToHeroUnionStateChanges();

        if (drawOnEnable)
            Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromLevelLoaders();
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

        if (targetImage == null || !TryGetMinimapView(out Vector2Int viewOrigin, out Vector2Int gridSize, out string zoneId))
            return;

        currentViewOrigin = viewOrigin;
        currentGridSize = gridSize;
        currentViewZoneId = zoneId;
        currentContentBounds = TryGetCurrentPartyZone(out LoadedLevelZoneData currentZone)
            ? new RectInt(currentZone.Anchor, currentZone.Size)
            : new RectInt(viewOrigin, gridSize);
        EnsureTexture(gridSize);
        RebuildObstacleCellCache();
        DrawCells(viewOrigin, gridSize);
        DrawStrategicObjects(gridSize);
        minimapTexture.Apply(false);
        targetImage.texture = minimapTexture;
        dirtyCells.Clear();
        hasPendingTextureApply = false;

        DrawStrategicObjectIcons(gridSize);
        DrawPartyIcons(gridSize);
    }

    public void RefreshPartyIconsOnly()
    {
        ResolveReferences();

        if (!TryGetMinimapView(out Vector2Int viewOrigin, out Vector2Int resolvedGridSize, out string zoneId))
        {
            HidePartyIcons(0);
            return;
        }

        if (currentGridSize != resolvedGridSize || currentViewOrigin != viewOrigin || currentViewZoneId != zoneId)
        {
            Refresh();
            return;
        }

        if (!showPartyIcons)
        {
            HidePartyIcons(0);
            return;
        }

        Vector2Int gridSize = currentGridSize;
        if (gridSize.x <= 0 || gridSize.y <= 0)
        {
            Refresh();
            return;
        }

        RefreshStrategicObjectIconsOnly(gridSize);
        DrawPartyIcons(gridSize);
    }

    public void SetLevelData(LevelData nextLevelData)
    {
        levelData = nextLevelData;
        Refresh();
    }

    private void DrawCells(Vector2Int viewOrigin, Vector2Int gridSize)
    {
        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Vector2Int grid = viewOrigin + new Vector2Int(x, y);
                minimapTexture.SetPixel(x, y, currentContentBounds.Contains(grid) ? GetCellColor(grid) : Color.black);
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
        bool useFallback = outpostRegistry == null;

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
        bool useFallback = heroUnionRegistry == null;

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
        bool useFallback = villainUnionBaseRegistry == null;

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
        if (!TryGlobalToMinimapGrid(grid, out Vector2Int localGrid))
            return;

        if (!IsGridInMinimap(localGrid, gridSize))
            return;

        minimapTexture.SetPixel(localGrid.x, localGrid.y, color);
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
        bool useFallback = outpostRegistry == null;

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
        bool useFallback = heroUnionRegistry == null;

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
        bool useFallback = villainUnionBaseRegistry == null;

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

    private void DrawStrategicObjectIcons(Vector2Int gridSize)
    {
        if (!showStrategicObjectIcons || targetImage == null)
        {
            HideStrategicIcons(0);
            return;
        }

        ResolveIconRoot();

        int iconIndex = 0;
        iconIndex = DrawOutpostIcons(gridSize, iconIndex);
        iconIndex = DrawHeroUnionIcons(gridSize, iconIndex);
        iconIndex = DrawVillainUnionIcons(gridSize, iconIndex);
        iconIndex = DrawGateIcons(gridSize, iconIndex);
        iconIndex = DrawMainEventIcons(gridSize, iconIndex);
        iconIndex = DrawEnemyIcons(gridSize, iconIndex, EnemyBehaviorType.Static);
        iconIndex = DrawSubEventIcons(gridSize, iconIndex);
        iconIndex = DrawEnemyIcons(gridSize, iconIndex, EnemyBehaviorType.Mobile);

        HideStrategicIcons(iconIndex);
    }

    private void RefreshStrategicObjectIconsOnly(Vector2Int gridSize)
    {
        DrawStrategicObjectIcons(gridSize);
    }

    private int DrawOutpostIcons(Vector2Int gridSize, int iconIndex)
    {
        Outpost[] fallbackOutposts = null;
        IReadOnlyList<Outpost> outposts = outpostRegistry != null ? outpostRegistry.Outposts : null;
        int count = outposts != null ? outposts.Count : 0;
        bool useFallback = outpostRegistry == null;

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

            Sprite sprite = GetOutpostMinimapSprite(outpost.outpostState);
            iconIndex = TryDrawStrategicIcon(outpost, sprite, outpostIconScale, gridSize, outpost.GetAnchorGrid(gridManager), iconIndex);
        }

        return iconIndex;
    }

    private int DrawMainEventIcons(Vector2Int gridSize, int iconIndex)
    {
        MainEventObject[] fallbackEvents = null;
        IReadOnlyList<MainEventObject> mainEvents = mainEventRegistry != null ? mainEventRegistry.MainEvents : null;
        int count = mainEvents != null ? mainEvents.Count : 0;
        bool useFallback = mainEventRegistry == null;

        if (useFallback)
        {
            fallbackEvents = FindObjectsByType<MainEventObject>(FindObjectsSortMode.None);
            count = fallbackEvents.Length;
        }

        for (int i = 0; i < count; i++)
        {
            MainEventObject mainEvent = useFallback ? fallbackEvents[i] : mainEvents[i];
            if (mainEvent == null || !mainEvent.isActiveAndEnabled)
                continue;

            iconIndex = TryDrawStrategicIcon(
                mainEvent,
                eventOrStaticEnemySprite,
                mainEventIconScale,
                gridSize,
                mainEvent.GetCurrentGrid(gridManager),
                iconIndex);
        }

        return iconIndex;
    }

    private int DrawSubEventIcons(Vector2Int gridSize, int iconIndex)
    {
        SubEventObject[] fallbackEvents = null;
        IReadOnlyList<SubEventObject> subEvents = subEventRegistry != null ? subEventRegistry.SubEvents : null;
        int count = subEvents != null ? subEvents.Count : 0;
        bool useFallback = subEventRegistry == null;

        if (useFallback)
        {
            fallbackEvents = FindObjectsByType<SubEventObject>(FindObjectsSortMode.None);
            count = fallbackEvents.Length;
        }

        for (int i = 0; i < count; i++)
        {
            SubEventObject subEvent = useFallback ? fallbackEvents[i] : subEvents[i];
            if (subEvent == null || !subEvent.isActiveAndEnabled)
                continue;

            iconIndex = TryDrawStrategicIcon(
                subEvent,
                subEventSprite,
                subEventIconScale,
                gridSize,
                subEvent.GetCurrentGrid(gridManager),
                iconIndex);
        }

        return iconIndex;
    }

    private int DrawEnemyIcons(Vector2Int gridSize, int iconIndex, EnemyBehaviorType targetBehaviorType)
    {
        EnemyGridMover[] fallbackEnemies = null;
        IReadOnlyList<EnemyGridMover> enemies = enemyRegistry != null ? enemyRegistry.Enemies : null;
        int count = enemies != null ? enemies.Count : 0;
        bool useFallback = enemyRegistry == null;

        if (useFallback)
        {
            fallbackEnemies = FindObjectsByType<EnemyGridMover>(FindObjectsSortMode.None);
            count = fallbackEnemies.Length;
        }

        for (int i = 0; i < count; i++)
        {
            EnemyGridMover enemy = useFallback ? fallbackEnemies[i] : enemies[i];
            if (enemy == null || !enemy.isActiveAndEnabled)
                continue;

            if (enemy.BehaviorType != targetBehaviorType)
                continue;

            Sprite sprite = enemy.BehaviorType == EnemyBehaviorType.Mobile
                ? mobileEnemySprite
                : eventOrStaticEnemySprite;
            float scale = enemy.BehaviorType == EnemyBehaviorType.Mobile
                ? mobileEnemyIconScale
                : staticEnemyIconScale;
            iconIndex = TryDrawStrategicIcon(enemy, sprite, scale, gridSize, enemy.GetCurrentGrid(), iconIndex);
        }

        return iconIndex;
    }

    private int DrawGateIcons(Vector2Int gridSize, int iconIndex)
    {
        if (gateSprite == null)
            return iconIndex;

        GateRuntimeController[] gates = FindObjectsByType<GateRuntimeController>(FindObjectsSortMode.None);
        List<Vector2Int> gateCells = new List<Vector2Int>();
        for (int i = 0; i < gates.Length; i++)
        {
            GateRuntimeController gate = gates[i];
            if (gate == null || !gate.isActiveAndEnabled)
                continue;

            gateCells.Clear();
            gate.CollectTeleportCells(gateCells);
            if (!TryGetCellsMinimapRect(gateCells, out Vector2 centerLocalGrid, out Vector2 sizeInCells))
                continue;

            sizeInCells = MakeSquareSize(sizeInCells);
            Image icon = GetStrategicIcon(iconIndex);
            ApplyStrategicIcon(icon, gateSprite, gateIconScale, centerLocalGrid, gridSize, sizeInCells);
            iconIndex++;
        }

        return iconIndex;
    }

    private int DrawHeroUnionIcons(Vector2Int gridSize, int iconIndex)
    {
        HeroUnionUnit[] fallbackHeroUnions = null;
        IReadOnlyList<HeroUnionUnit> heroUnions = heroUnionRegistry != null ? heroUnionRegistry.HeroUnions : null;
        int count = heroUnions != null ? heroUnions.Count : 0;
        bool useFallback = heroUnionRegistry == null;

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

            Sprite sprite = heroUnion.IsClaimedByHero ? playerBuildingSprite : neutralBuildingSprite;
            iconIndex = TryDrawStrategicIcon(heroUnion, sprite, heroUnionIconScale, gridSize, heroUnion.GetCurrentGrid(), iconIndex);
        }

        return iconIndex;
    }

    private int DrawVillainUnionIcons(Vector2Int gridSize, int iconIndex)
    {
        VillainUnionBase[] fallbackBases = null;
        IReadOnlyList<VillainUnionBase> bases = villainUnionBaseRegistry != null
            ? villainUnionBaseRegistry.VillainUnionBases
            : null;
        int count = bases != null ? bases.Count : 0;
        bool useFallback = villainUnionBaseRegistry == null;

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

            iconIndex = TryDrawStrategicIcon(
                villainUnionBase,
                enemyBuildingSprite,
                villainUnionIconScale,
                gridSize,
                villainUnionBase.GetAnchorGrid(),
                iconIndex);
        }

        return iconIndex;
    }

    private int TryDrawStrategicIcon(
        Component component,
        Sprite sprite,
        float iconScale,
        Vector2Int gridSize,
        Vector2Int fallbackGrid,
        int iconIndex)
    {
        if (component == null || sprite == null)
            return iconIndex;

        if (!TryGetComponentMinimapRect(component, fallbackGrid, out Vector2 centerLocalGrid, out Vector2 sizeInCells))
            return iconIndex;

        Image icon = GetStrategicIcon(iconIndex);
        ApplyStrategicIcon(icon, sprite, iconScale, centerLocalGrid, gridSize, sizeInCells);
        return iconIndex + 1;
    }

    private bool TryGetComponentMinimapRect(
        Component component,
        Vector2Int fallbackGrid,
        out Vector2 centerLocalGrid,
        out Vector2 sizeInCells)
    {
        centerLocalGrid = Vector2.zero;
        sizeInCells = Vector2.one;

        if (component == null)
            return false;

        MultiGridOccupant occupant = component.GetComponent<MultiGridOccupant>();
        if (occupant == null)
        {
            if (!TryGlobalToMinimapGrid(fallbackGrid, out Vector2Int localGrid))
                return false;

            centerLocalGrid = new Vector2(localGrid.x + 0.5f, localGrid.y + 0.5f);
            sizeInCells = Vector2.one;
            return true;
        }

        IReadOnlyList<Vector2Int> occupiedCells = occupant.GetOccupiedCells();
        if (occupiedCells == null || occupiedCells.Count == 0)
            return false;

        Vector2Int min = occupiedCells[0];
        Vector2Int max = occupiedCells[0];
        bool hasVisibleCell = false;

        for (int i = 0; i < occupiedCells.Count; i++)
        {
            Vector2Int cell = occupiedCells[i];
            min = Vector2Int.Min(min, cell);
            max = Vector2Int.Max(max, cell);

            if (TryGlobalToMinimapGrid(cell, out _))
                hasVisibleCell = true;
        }

        if (!hasVisibleCell)
            return false;

        BuildLocalRect(min, max, out centerLocalGrid, out sizeInCells);
        return true;
    }

    private bool TryGetCellsMinimapRect(
        IReadOnlyList<Vector2Int> cells,
        out Vector2 centerLocalGrid,
        out Vector2 sizeInCells)
    {
        centerLocalGrid = Vector2.zero;
        sizeInCells = Vector2.one;

        if (cells == null || cells.Count == 0)
            return false;

        Vector2Int min = cells[0];
        Vector2Int max = cells[0];
        bool hasVisibleCell = false;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            min = Vector2Int.Min(min, cell);
            max = Vector2Int.Max(max, cell);

            if (TryGlobalToMinimapGrid(cell, out _))
                hasVisibleCell = true;
        }

        if (!hasVisibleCell)
            return false;

        BuildLocalRect(min, max, out centerLocalGrid, out sizeInCells);
        return true;
    }

    private void BuildLocalRect(
        Vector2Int globalMin,
        Vector2Int globalMax,
        out Vector2 centerLocalGrid,
        out Vector2 sizeInCells)
    {
        Vector2Int localMin = globalMin - currentViewOrigin;
        Vector2Int localMax = globalMax - currentViewOrigin;
        centerLocalGrid = new Vector2(
            (localMin.x + localMax.x + 1f) * 0.5f,
            (localMin.y + localMax.y + 1f) * 0.5f);
        sizeInCells = new Vector2(localMax.x - localMin.x + 1, localMax.y - localMin.y + 1);
    }

    private static Vector2 MakeSquareSize(Vector2 sizeInCells)
    {
        float maxSize = Mathf.Max(sizeInCells.x, sizeInCells.y);
        return new Vector2(maxSize, maxSize);
    }

    private void ApplyStrategicIcon(
        Image icon,
        Sprite sprite,
        float iconScale,
        Vector2 centerLocalGrid,
        Vector2Int gridSize,
        Vector2 sizeInCells)
    {
        if (icon == null || iconRoot == null)
            return;

        RectTransform iconTransform = icon.rectTransform;
        icon.sprite = sprite;
        icon.color = Color.white;
        iconTransform.anchoredPosition = GridCenterToMinimapPosition(centerLocalGrid, gridSize);
        Vector2 cellSize = GetCellSize(gridSize);
        float scale = Mathf.Max(0.1f, iconScale);
        iconTransform.sizeDelta = new Vector2(
            cellSize.x * sizeInCells.x * scale,
            cellSize.y * sizeInCells.y * scale);
        iconTransform.SetAsLastSibling();

        if (!icon.gameObject.activeSelf)
            icon.gameObject.SetActive(true);
    }

    private Sprite GetOutpostMinimapSprite(OutpostState state)
    {
        return state switch
        {
            OutpostState.Claimed => playerBuildingSprite,
            OutpostState.EnemyClaimed => enemyBuildingSprite,
            _ => neutralBuildingSprite
        };
    }

    private Image GetStrategicIcon(int index)
    {
        while (strategicIconPool.Count <= index)
            strategicIconPool.Add(CreateStrategicIcon());

        return strategicIconPool[index];
    }

    private Image CreateStrategicIcon()
    {
        ResolveIconRoot();

        Image icon = strategicIconPrefab != null
            ? Instantiate(strategicIconPrefab, iconRoot)
            : new GameObject("Strategic Minimap Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();

        RectTransform rectTransform = icon.rectTransform;
        rectTransform.SetParent(iconRoot, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;

        icon.raycastTarget = false;
        icon.preserveAspect = false;
        icon.gameObject.SetActive(false);
        return icon;
    }

    private void HideStrategicIcons(int firstHiddenIndex)
    {
        for (int i = firstHiddenIndex; i < strategicIconPool.Count; i++)
        {
            if (strategicIconPool[i] != null)
                strategicIconPool[i].gameObject.SetActive(false);
        }
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
            if (TryGlobalToMinimapGrid(grid, out Vector2Int localGrid) && IsGridInMinimap(localGrid, gridSize))
            {
                Image icon = GetPartyIcon(iconIndex);
                float scale = playerPartySprite != null ? playerPartyIconScale : partyIconScale;
                ApplyIcon(icon, localGrid, gridSize, partyIconColor, GetCellIconSize(gridSize, scale));
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
        icon.sprite = playerPartySprite;
        iconTransform.anchoredPosition = GridToMinimapPosition(grid, gridSize);
        iconTransform.sizeDelta = new Vector2(size, size);
        icon.color = playerPartySprite != null ? Color.white : color;
        iconTransform.SetAsLastSibling();

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
        return GridCenterToMinimapPosition(new Vector2(grid.x + 0.5f, grid.y + 0.5f), gridSize);
    }

    private Vector2 GridCenterToMinimapPosition(Vector2 centerGrid, Vector2Int gridSize)
    {
        Rect rect = iconRoot != null ? iconRoot.rect : targetImage.rectTransform.rect;
        float normalizedX = centerGrid.x / Mathf.Max(1, gridSize.x);
        float normalizedY = centerGrid.y / Mathf.Max(1, gridSize.y);
        return new Vector2(normalizedX * rect.width, normalizedY * rect.height);
    }

    private Vector2 GetCellSize(Vector2Int gridSize)
    {
        Rect rect = iconRoot != null ? iconRoot.rect : targetImage.rectTransform.rect;
        return new Vector2(
            rect.width / Mathf.Max(1, gridSize.x),
            rect.height / Mathf.Max(1, gridSize.y));
    }

    private float GetCellIconSize(Vector2Int gridSize, float scale)
    {
        Vector2 cellSize = GetCellSize(gridSize);
        return Mathf.Min(cellSize.x, cellSize.y) * scale;
    }

    private static bool IsGridInMinimap(Vector2Int grid, Vector2Int gridSize)
    {
        return grid.x >= 0 && grid.y >= 0 && grid.x < gridSize.x && grid.y < gridSize.y;
    }

    private Color GetCellColor(Vector2Int grid)
    {
        return IsObstacleCell(grid) ? obstacleColor : fallbackColor;
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

        gridSize = Vector2Int.zero;
        return false;
    }

    public bool TryGetDisplayBounds(out Vector2Int viewOrigin, out Vector2Int gridSize)
    {
        ResolveReferences();
        return TryGetMinimapView(out viewOrigin, out gridSize, out _);
    }

    private bool TryGetMinimapView(out Vector2Int viewOrigin, out Vector2Int gridSize, out string zoneId)
    {
        if (TryGetCurrentPartyZone(out LoadedLevelZoneData zone))
        {
            viewOrigin = zone.Anchor;
            gridSize = zone.Size;
            zoneId = zone.ZoneId ?? string.Empty;
            if (gridSize.x <= 0 || gridSize.y <= 0)
                return false;

            if (zoneViewCrops != null)
            {
                for (int i = 0; i < zoneViewCrops.Count; i++)
                {
                    ZoneViewCrop crop = zoneViewCrops[i];
                    if (crop == null || string.IsNullOrWhiteSpace(crop.zoneId) ||
                        !string.Equals(crop.zoneId.Trim(), zoneId.Trim(), System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    crop.Apply(ref viewOrigin, ref gridSize);
                    break;
                }
            }

            return true;
        }

        viewOrigin = Vector2Int.zero;
        zoneId = string.Empty;
        return TryGetGridSize(out gridSize);
    }

    private bool TryGetCurrentPartyZone(out LoadedLevelZoneData currentZone)
    {
        currentZone = default;

        if (!HasLoadedZoneData)
            return false;

        PartyGridMover party = partyRegistry != null ? partyRegistry.PlayerParty : null;
        if (party == null)
            return false;

        return TryGetZoneAtGrid(party.GetCurrentGrid(), out currentZone);
    }

    private bool TryGetZoneAtGrid(Vector2Int grid, out LoadedLevelZoneData currentZone)
    {
        currentZone = default;
        if (!HasLoadedZoneData)
            return false;

        IReadOnlyList<LoadedLevelZoneData> zones = levelZoneLayoutLoader.LoadedZones;
        for (int i = 0; i < zones.Count; i++)
        {
            LoadedLevelZoneData zone = zones[i];
            if (zone.Size.x <= 0 || zone.Size.y <= 0)
                continue;

            RectInt bounds = new RectInt(zone.Anchor, zone.Size);
            if (!bounds.Contains(grid))
                continue;

            currentZone = zone;
            return true;
        }

        return false;
    }

    private bool TryGlobalToMinimapGrid(Vector2Int globalGrid, out Vector2Int localGrid)
    {
        localGrid = globalGrid - currentViewOrigin;
        return currentContentBounds.Contains(globalGrid) && IsGridInMinimap(localGrid, currentGridSize);
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
        if (minimapTexture == null || !TryGlobalToMinimapGrid(grid, out Vector2Int localGrid))
            return;

        Color color = GetCellColor(grid);
        if (TryGetStrategicCellColor(grid, out Color strategicColor))
            color = strategicColor;

        minimapTexture.SetPixel(localGrid.x, localGrid.y, color);
        hasPendingTextureApply = true;
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

        if (partyRegistry == null)
            partyRegistry = FindFirstObjectByType<PartyRegistry>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (heroUnionRegistry == null)
            heroUnionRegistry = FindFirstObjectByType<HeroUnionRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();

        if (enemyRegistry == null)
            enemyRegistry = FindFirstObjectByType<EnemyRegistry>();

        if (mainEventRegistry == null)
            mainEventRegistry = FindFirstObjectByType<MainEventRegistry>();

        if (subEventRegistry == null)
            subEventRegistry = FindFirstObjectByType<SubEventRegistry>();
    }

    private void ResolveIconRoot()
    {
        if (iconRoot == null && targetImage != null)
            iconRoot = targetImage.rectTransform;
    }
}
