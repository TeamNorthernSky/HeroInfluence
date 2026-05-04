using UnityEngine;

public class FogRenderManager : MonoBehaviour
{
    private static readonly int FogVisibilityTexId = Shader.PropertyToID("_FogVisibilityTex");
    private static readonly int FogGridMinId = Shader.PropertyToID("_FogGridMin");
    private static readonly int FogGridMaxId = Shader.PropertyToID("_FogGridMax");
    private static readonly int FogGridWorldMinId = Shader.PropertyToID("_FogGridWorldMin");
    private static readonly int FogGridWorldSizeId = Shader.PropertyToID("_FogGridWorldSize");
    private static readonly int FogCellSizeId = Shader.PropertyToID("_FogCellSize");

    [Header("References")]
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private LevelData levelData;

    [Header("Render Bounds")]
    [SerializeField] private Vector2Int gridSize = new Vector2Int(20, 20);
    [SerializeField] private bool rebuildOnEnable = true;
    [SerializeField] private bool syncGridSizeWithLevelData = true;

    [Header("Texture Values")]
    [SerializeField, Range(0f, 1f)] private float unexploredValue = 0f;
    [SerializeField, Range(0f, 1f)] private float foggedValue = 0.5f;
    [SerializeField, Range(0f, 1f)] private float visibleValue = 1f;
    [SerializeField] private FilterMode fogTextureFilterMode = FilterMode.Bilinear;

    private Texture2D fogTexture;
    private bool isDirty = true;

    public Vector2Int GridMin => Vector2Int.zero;
    public Vector2Int GridMax => new Vector2Int(gridSize.x - 1, gridSize.y - 1);
    public Texture2D FogTexture => fogTexture;

    private void OnEnable()
    {
        TryAutoAssignLevelData();
        SyncGridSizeFromLevelData();
        EnsureValidGridSize();
        CreateTextureIfNeeded();
        SubscribeToFogChanges();
        ApplyShaderGlobals();

        if (rebuildOnEnable)
            MarkDirty();
    }

    private void OnDisable()
    {
        UnsubscribeFromFogChanges();
    }

    private void OnDestroy()
    {
        if (fogTexture != null)
            DestroyImmediate(fogTexture);
    }

    private void OnValidate()
    {
        TryAutoAssignLevelData();
        SyncGridSizeFromLevelData();
        EnsureValidGridSize();
        isDirty = true;

        if (!isActiveAndEnabled)
            return;

        CreateTextureIfNeeded();
        ApplyTextureSettings();
        ApplyShaderGlobals();
    }

    private void LateUpdate()
    {
        if (SyncGridSizeFromLevelData())
            CreateTextureIfNeeded();

        if (!isDirty)
            return;

        RebuildFogTexture();
    }

    [ContextMenu("Rebuild Fog Texture")]
    public void RebuildFogTexture()
    {
        CreateTextureIfNeeded();
        if (fogTexture == null)
            return;

        Vector2Int min = GridMin;
        Vector2Int max = GridMax;

        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                Vector2Int grid = new Vector2Int(x, y);
                FogVisibilityState visibility = fogGridManager != null
                    ? fogGridManager.GetVisibility(grid)
                    : FogVisibilityState.Unexplored;

                float value = GetVisibilityValue(visibility);
                Color color = new Color(value, value, value, 1f);
                fogTexture.SetPixel(x - min.x, y - min.y, color);
            }
        }

        fogTexture.Apply(false, false);
        ApplyShaderGlobals();
        isDirty = false;
    }

    public void MarkDirty()
    {
        isDirty = true;
    }

    private void HandleFogChanged()
    {
        MarkDirty();
    }

    private void SubscribeToFogChanges()
    {
        if (fogGridManager == null)
            return;

        fogGridManager.FogChanged -= HandleFogChanged;
        fogGridManager.FogChanged += HandleFogChanged;
    }

    private void UnsubscribeFromFogChanges()
    {
        if (fogGridManager == null)
            return;

        fogGridManager.FogChanged -= HandleFogChanged;
    }

    private void CreateTextureIfNeeded()
    {
        if (gridSize.x <= 0 || gridSize.y <= 0)
            return;

        if (fogTexture != null && fogTexture.width == gridSize.x && fogTexture.height == gridSize.y)
        {
            ApplyTextureSettings();
            return;
        }

        if (fogTexture != null)
            DestroyImmediate(fogTexture);

        fogTexture = new Texture2D(gridSize.x, gridSize.y, TextureFormat.RGBA32, false, true)
        {
            name = "FogVisibilityTexture",
            wrapMode = TextureWrapMode.Clamp
        };

        ApplyTextureSettings();
    }

    private void ApplyTextureSettings()
    {
        if (fogTexture == null)
            return;

        fogTexture.filterMode = fogTextureFilterMode;
        fogTexture.wrapMode = TextureWrapMode.Clamp;
    }

    private void ApplyShaderGlobals()
    {
        if (fogTexture == null)
            return;

        Vector2Int min = GridMin;
        Vector2Int max = GridMax;
        float cellSize = gridManager != null ? gridManager.CellSize : 1f;

        Vector3 minWorld = gridManager != null
            ? gridManager.GridToWorldCenter(min)
            : new Vector3(min.x * cellSize, 0f, min.y * cellSize);
        minWorld.x -= cellSize * 0.5f;
        minWorld.z -= cellSize * 0.5f;

        Vector2 worldSize = new Vector2(gridSize.x * cellSize, gridSize.y * cellSize);

        Shader.SetGlobalTexture(FogVisibilityTexId, fogTexture);
        Shader.SetGlobalVector(FogGridMinId, new Vector4(min.x, min.y, 0f, 0f));
        Shader.SetGlobalVector(FogGridMaxId, new Vector4(max.x, max.y, 0f, 0f));
        Shader.SetGlobalVector(FogGridWorldMinId, new Vector4(minWorld.x, minWorld.z, 0f, 0f));
        Shader.SetGlobalVector(FogGridWorldSizeId, new Vector4(worldSize.x, worldSize.y, 0f, 0f));
        Shader.SetGlobalFloat(FogCellSizeId, cellSize);
    }

    private float GetVisibilityValue(FogVisibilityState visibility)
    {
        switch (visibility)
        {
            case FogVisibilityState.Visible:
                return visibleValue;
            case FogVisibilityState.Fogged:
                return foggedValue;
            default:
                return unexploredValue;
        }
    }

    private void EnsureValidGridSize()
    {
        if (gridSize.x < 1)
            gridSize.x = 1;

        if (gridSize.y < 1)
            gridSize.y = 1;

    }

    private void TryAutoAssignLevelData()
    {
        if (levelData != null)
            return;

        LevelLoader levelLoader = FindFirstObjectByType<LevelLoader>();
        if (levelLoader != null)
            levelData = levelLoader.LevelData;
    }

    private bool SyncGridSizeFromLevelData()
    {
        if (!syncGridSizeWithLevelData || levelData == null)
            return false;

        Vector2Int nextGridSize = levelData.GridSize;
        if (nextGridSize == gridSize)
            return false;

        gridSize = nextGridSize;
        EnsureValidGridSize();
        MarkDirty();
        return true;
    }
}
