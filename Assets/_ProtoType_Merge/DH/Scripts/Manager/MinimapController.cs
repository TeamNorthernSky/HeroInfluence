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
    [SerializeField] private GridManager gridManager;
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private OutpostRegistry outpostRegistry;
    [SerializeField] private CastleRegistry castleRegistry;
    [SerializeField] private VillainUnionBaseRegistry villainUnionBaseRegistry;

    [Header("Draw Options")]
    [SerializeField] private bool drawOnEnable = true;
    [SerializeField] private bool useFogVisibility = true;
    [SerializeField, Min(0f)] private float refreshInterval = 0.25f;
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
    private float nextRefreshTime;
    private readonly List<Image> partyIconPool = new List<Image>();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (fogGridManager != null)
            fogGridManager.FogChanged += Refresh;

        if (drawOnEnable)
            Refresh();
    }

    private void OnDisable()
    {
        if (fogGridManager != null)
            fogGridManager.FogChanged -= Refresh;
    }

    private void OnDestroy()
    {
        ReleaseTexture();
    }

    private void Update()
    {
        if (refreshInterval <= 0f || Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    [ContextMenu("Refresh Minimap")]
    public void Refresh()
    {
        ResolveReferences();

        if (targetImage == null || !TryGetGridSize(out Vector2Int gridSize))
            return;

        EnsureTexture(gridSize);
        DrawCells(gridSize);
        DrawStrategicObjects(gridSize);
        minimapTexture.Apply(false);
        targetImage.texture = minimapTexture;

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
            fogGridManager.FogChanged -= Refresh;

        fogGridManager = nextFogGridManager;

        if (isActiveAndEnabled && fogGridManager != null)
            fogGridManager.FogChanged += Refresh;

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
        DrawCastles(gridSize);
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

    private void DrawCastles(Vector2Int gridSize)
    {
        CastleUnit[] fallbackCastles = null;
        IReadOnlyList<CastleUnit> castles = castleRegistry != null ? castleRegistry.Castles : null;
        int count = castles != null ? castles.Count : 0;
        bool useFallback = count == 0;

        if (useFallback)
        {
            fallbackCastles = FindObjectsByType<CastleUnit>(FindObjectsSortMode.None);
            count = fallbackCastles.Length;
        }

        for (int i = 0; i < count; i++)
        {
            CastleUnit castle = useFallback ? fallbackCastles[i] : castles[i];
            if (castle == null || !castle.isActiveAndEnabled)
                continue;

            DrawComponentFootprint(castle, playerBuildingColor, gridSize, castle.GetCurrentGrid());
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

        PartyGridMover[] parties = FindObjectsByType<PartyGridMover>(FindObjectsSortMode.None);
        int iconIndex = 0;
        for (int i = 0; i < parties.Length; i++)
        {
            PartyGridMover party = parties[i];
            if (party == null || !party.isActiveAndEnabled)
                continue;

            Vector2Int grid = party.GetCurrentGrid();
            if (!IsGridInMinimap(grid, gridSize))
                continue;

            Image icon = GetPartyIcon(iconIndex);
            ApplyIcon(icon, grid, gridSize, partyIconColor, GetCellIconSize(gridSize, partyIconScale));
            iconIndex++;
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
        if (levelData == null)
            return false;

        IReadOnlyList<Vector2Int> obstacleCells = levelData.ObstacleCells;
        for (int i = 0; i < obstacleCells.Count; i++)
        {
            if (obstacleCells[i] == grid)
                return true;
        }

        return false;
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
    }

    private void ResolveReferences()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();

        ResolveIconRoot();

        if (levelLoader == null)
            levelLoader = FindFirstObjectByType<LevelLoader>();

        if (levelData == null && levelLoader != null)
            levelData = levelLoader.LevelData;

        if (gridManager == null && levelLoader != null)
            gridManager = levelLoader.GridManager;

        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (fogGridManager == null)
            fogGridManager = FindFirstObjectByType<FogGridManager>();

        if (outpostRegistry == null)
            outpostRegistry = FindFirstObjectByType<OutpostRegistry>();

        if (castleRegistry == null)
            castleRegistry = FindFirstObjectByType<CastleRegistry>();

        if (villainUnionBaseRegistry == null)
            villainUnionBaseRegistry = FindFirstObjectByType<VillainUnionBaseRegistry>();
    }

    private void ResolveIconRoot()
    {
        if (iconRoot == null && targetImage != null)
            iconRoot = targetImage.rectTransform;
    }
}
