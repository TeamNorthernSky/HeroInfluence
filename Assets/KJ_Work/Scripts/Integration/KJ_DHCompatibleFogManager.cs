using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class KJ_DHCompatibleFogManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private LevelData levelData;

    [Header("Grid Settings")]
    [SerializeField] private Vector2Int fallbackGridSize = new Vector2Int(50, 50);
    [SerializeField] private bool syncGridSizeWithLevelData = true;

    [Header("Fog Toggle")]
    [SerializeField] private bool fogEnabled = true;

    [Header("Layer Restore Delays")]
    [SerializeField, Min(0f)] private float lowLayerDelay = 5f;
    [SerializeField, Min(0f)] private float midLayerDelay = 5.5f;
    [SerializeField, Min(0f)] private float highLayerDelay = 6f;

    [Header("Restore Duration")]
    [SerializeField, Min(0.01f)] private float restoreDuration = 0.5f;

    [Header("FogHidable Clip Threshold")]
    [SerializeField, Range(0f, 1f)] private float fogHidableLowThreshold = 0.05f;

    [Header("Mask Rendering")]
    [SerializeField, Min(0f)] private float maskSmoothEdge = 0.5f;

    private RenderTexture rtCurrent;
    private RenderTexture rtExplored;
    private RenderTexture rtExploredTemp;
    private Texture2D currentStateTexture;

    private Material maskMaterial;
    private Material decayMaterial;
    private CommandBuffer cmd;

    private Vector2Int activeGridSize;
    private bool initialized;
    private bool currentTextureDirty;
    private bool[] currentVisibleCells;

    private static readonly int CurrentTexGlobalId = Shader.PropertyToID("_VisibilityCurrentTex");
    private static readonly int ExploredTexGlobalId = Shader.PropertyToID("_VisibilityExploredTex");
    private static readonly int SmoothEdgeId = Shader.PropertyToID("_SmoothEdge");
    private static readonly int GridWorldSizeId = Shader.PropertyToID("_GridWorldSize");
    private static readonly int CurrentTexInputId = Shader.PropertyToID("_CurrentTex");
    private static readonly int ExploredTexInputId = Shader.PropertyToID("_ExploredTex");
    private static readonly int RestoreDelaysId = Shader.PropertyToID("_RestoreDelays");
    private static readonly int RestoreDurationId = Shader.PropertyToID("_RestoreDuration");
    private static readonly int FogDeltaTimeId = Shader.PropertyToID("_FogDeltaTime");
    private static readonly int FogHidableLowThresholdId = Shader.PropertyToID("_FogHidableLowThreshold");

    public bool FogEnabled
    {
        get => fogEnabled;
        set => fogEnabled = value;
    }

    public Vector2Int ActiveGridSize => activeGridSize;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnValidate()
    {
        fallbackGridSize.x = Mathf.Max(1, fallbackGridSize.x);
        fallbackGridSize.y = Mathf.Max(1, fallbackGridSize.y);

        if (!isActiveAndEnabled)
            return;

        Initialize();
    }

    private void OnDisable()
    {
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    public void Initialize()
    {
        initialized = false;
        ResolveReferences();

        Vector2Int nextGridSize = ResolveGridSize();
        if (nextGridSize.x <= 0 || nextGridSize.y <= 0)
        {
            Debug.LogError("[KJ_DHCompatibleFogManager] Invalid grid size.");
            return;
        }

        if (!EnsureMaterials())
            return;

        EnsureTextures(nextGridSize);
        if (rtCurrent == null || rtExplored == null || rtExploredTemp == null)
            return;

        EnsureRuntimeState(nextGridSize);
        ApplyGridGlobals(nextGridSize);

        Shader.SetGlobalTexture(CurrentTexGlobalId, rtCurrent);
        Shader.SetGlobalTexture(ExploredTexGlobalId, rtExplored);

        activeGridSize = nextGridSize;
        initialized = rtCurrent != null && rtExplored != null && rtExploredTemp != null;
        PushCurrentVisibilityToTexture(true);
    }

    public void UpdatePlayerVisibility(Vector2Int playerGridPos, int sightRadiusCells)
    {
        if (!initialized)
            Initialize();

        if (gridManager == null)
            return;

        MarkVisibleCells(playerGridPos, sightRadiusCells);
    }

    public void ClearCurrentVisibility()
    {
        if (currentVisibleCells == null)
            return;

        System.Array.Clear(currentVisibleCells, 0, currentVisibleCells.Length);
        currentTextureDirty = true;
    }

    public void SetFogEnabled(bool enabled)
    {
        fogEnabled = enabled;
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;

        Vector2Int resolvedSize = ResolveGridSize();
        if (resolvedSize != activeGridSize)
            Initialize();

        if (rtCurrent == null || rtExplored == null || rtExploredTemp == null)
            return;

        if (maskMaterial == null || decayMaterial == null)
            return;

        cmd.Clear();

        if (!fogEnabled)
        {
            cmd.SetRenderTarget(rtCurrent);
            cmd.ClearRenderTarget(false, true, Color.white);
            Graphics.ExecuteCommandBuffer(cmd);
            return;
        }

        PushCurrentVisibilityToTexture();

        cmd.SetGlobalVector(RestoreDelaysId, new Vector4(lowLayerDelay, midLayerDelay, highLayerDelay, 0f));
        cmd.SetGlobalFloat(RestoreDurationId, restoreDuration);
        cmd.SetGlobalFloat(FogHidableLowThresholdId, fogHidableLowThreshold);
        cmd.SetGlobalFloat(FogDeltaTimeId, Time.deltaTime);
        cmd.SetGlobalTexture(ExploredTexInputId, rtExplored);
        cmd.SetGlobalTexture(CurrentTexInputId, rtCurrent);
        cmd.Blit(Texture2D.blackTexture, rtExploredTemp, decayMaterial);

        Graphics.ExecuteCommandBuffer(cmd);

        RenderTexture swap = rtExplored;
        rtExplored = rtExploredTemp;
        rtExploredTemp = swap;
        Shader.SetGlobalTexture(ExploredTexGlobalId, rtExplored);
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
            gridManager = FindFirstObjectByType<GridManager>();

        if (levelLoader == null)
            levelLoader = FindFirstObjectByType<LevelLoader>();

        if (levelData == null && levelLoader != null)
            levelData = levelLoader.LevelData;
    }

    private Vector2Int ResolveGridSize()
    {
        if (syncGridSizeWithLevelData)
        {
            if (levelData == null && levelLoader != null)
                levelData = levelLoader.LevelData;

            if (levelData != null)
                return levelData.GridSize;
        }

        return fallbackGridSize;
    }

    private bool EnsureMaterials()
    {
        if (maskMaterial == null)
        {
            Shader maskShader = Shader.Find("Custom/JC/FogMask");
            if (maskShader == null)
            {
                Debug.LogError("[KJ_DHCompatibleFogManager] Missing shader Custom/JC/FogMask.");
                return false;
            }

            maskMaterial = new Material(maskShader) { hideFlags = HideFlags.DontSave };
        }

        if (decayMaterial == null)
        {
            Shader decayShader = Shader.Find("Custom/JC/FogDecay");
            if (decayShader == null)
            {
                Debug.LogError("[KJ_DHCompatibleFogManager] Missing shader Custom/JC/FogDecay.");
                return false;
            }

            decayMaterial = new Material(decayShader) { hideFlags = HideFlags.DontSave };
        }

        maskMaterial.SetFloat(SmoothEdgeId, maskSmoothEdge);

        if (cmd == null)
            cmd = new CommandBuffer { name = "KJ_DHCompatibleFogManager" };

        return true;
    }

    private void EnsureTextures(Vector2Int gridSize)
    {
        rtCurrent = EnsureTexture(rtCurrent, gridSize.x, gridSize.y, RenderTextureFormat.R8, "KJ_FogRT_Current");
        rtExplored = EnsureTexture(rtExplored, gridSize.x, gridSize.y, RenderTextureFormat.ARGBFloat, "KJ_FogRT_Explored");
        rtExploredTemp = EnsureTexture(rtExploredTemp, gridSize.x, gridSize.y, RenderTextureFormat.ARGBFloat, "KJ_FogRT_ExploredTemp");
    }

    private void EnsureRuntimeState(Vector2Int gridSize)
    {
        int cellCount = gridSize.x * gridSize.y;

        if (currentVisibleCells == null || currentVisibleCells.Length != cellCount)
        {
            currentVisibleCells = new bool[cellCount];
            currentTextureDirty = true;
        }

        if (currentStateTexture == null || currentStateTexture.width != gridSize.x || currentStateTexture.height != gridSize.y)
        {
            if (currentStateTexture != null)
                DestroyImmediate(currentStateTexture);

            currentStateTexture = new Texture2D(gridSize.x, gridSize.y, TextureFormat.RFloat, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "KJ_FogCurrentState",
                hideFlags = HideFlags.DontSave,
            };

            currentTextureDirty = true;
        }
    }

    private void ApplyGridGlobals(Vector2Int gridSize)
    {
        float cellSize = gridManager != null ? gridManager.CellSize : 1f;
        float worldWidth = gridSize.x * cellSize;
        float worldHeight = gridSize.y * cellSize;
        Shader.SetGlobalVector(GridWorldSizeId, new Vector4(worldWidth, worldHeight, 1f / worldWidth, 1f / worldHeight));
    }

    private static RenderTexture EnsureTexture(RenderTexture current, int width, int height, RenderTextureFormat format, string name)
    {
        if (current != null && current.width == width && current.height == height)
            return current;

        if (current != null)
        {
            current.Release();
            Object.DestroyImmediate(current);
        }

        var next = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
            enableRandomWrite = false,
        };

        next.Create();
        ClearTexture(next, Color.clear);
        return next;
    }

    private static void ClearTexture(RenderTexture target, Color clearColor)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        GL.Clear(false, true, clearColor);
        RenderTexture.active = previous;
    }

    private void MarkVisibleCells(Vector2Int centerGrid, int sightRadiusCells)
    {
        if (currentVisibleCells == null)
            return;

        int radius = Mathf.Max(0, sightRadiusCells);
        int radiusSquared = radius * radius;

        for (int y = centerGrid.y - radius; y <= centerGrid.y + radius; y++)
        {
            for (int x = centerGrid.x - radius; x <= centerGrid.x + radius; x++)
            {
                if (!IsInsideGrid(x, y))
                    continue;

                int dx = x - centerGrid.x;
                int dy = y - centerGrid.y;
                if ((dx * dx) + (dy * dy) > radiusSquared)
                    continue;

                currentVisibleCells[ToIndex(x, y)] = true;
            }
        }

        currentTextureDirty = true;
    }

    private void PushCurrentVisibilityToTexture(bool force = false)
    {
        if (!force && !currentTextureDirty)
            return;

        if (currentStateTexture == null || currentVisibleCells == null || rtCurrent == null)
            return;

        int width = activeGridSize.x;
        int height = activeGridSize.y;
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = ToIndex(x, y);
                pixels[index] = currentVisibleCells[index] ? Color.white : Color.clear;
            }
        }

        currentStateTexture.SetPixels(pixels);
        currentStateTexture.Apply(false, false);
        Graphics.Blit(currentStateTexture, rtCurrent);
        Shader.SetGlobalTexture(CurrentTexGlobalId, rtCurrent);
        currentTextureDirty = false;
    }

    private bool IsInsideGrid(int x, int y)
    {
        return x >= 0 && y >= 0 && x < activeGridSize.x && y < activeGridSize.y;
    }

    private int ToIndex(int x, int y)
    {
        return (y * activeGridSize.x) + x;
    }

    private void ReleaseResources()
    {
        ReleaseTexture(ref rtCurrent);
        ReleaseTexture(ref rtExplored);
        ReleaseTexture(ref rtExploredTemp);
        initialized = false;

        if (currentStateTexture != null)
            DestroyImmediate(currentStateTexture);

        if (maskMaterial != null)
            DestroyImmediate(maskMaterial);

        if (decayMaterial != null)
            DestroyImmediate(decayMaterial);

        cmd?.Release();
    }

    private static void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null)
            return;

        texture.Release();
        Object.DestroyImmediate(texture);
        texture = null;
    }
}
