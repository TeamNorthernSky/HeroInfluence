using UnityEngine;
using UnityEngine.Serialization;

// [260806 T2 슬림화] 안개 시각 데이터의 정본은 SDF(_FogDistanceTex, JC FogDistanceField)로 일원화됨.
// 본 컴포넌트는 렌더가 소비하는 월드 전역 3종(_FogGridWorldMin/Size/_FogCellSize) 푸시와
// 레벨 소스 → 그리드 크기 동기(FogGridManager.SetGridSize 포함)만 담당한다.
// (구 _FogVisibilityTex 텍스처 베이크·가시성 3값·4텍셀 폴백 계보는 철거)
public class FogRenderManager : MonoBehaviour
{
    private static readonly int FogGridWorldMinId = Shader.PropertyToID("_FogGridWorldMin");
    private static readonly int FogGridWorldSizeId = Shader.PropertyToID("_FogGridWorldSize");
    private static readonly int FogCellSizeId = Shader.PropertyToID("_FogCellSize");

    [Header("References")]
    [SerializeField] private FogGridManager fogGridManager;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private LevelZoneLayoutData levelZoneLayoutData;
    [SerializeField] private LevelData levelData;

    [Header("Render Bounds")]
    [SerializeField] private Vector2Int gridSize = new Vector2Int(20, 20);
    [FormerlySerializedAs("syncGridSizeWithLevelData")]
    [SerializeField] private bool syncGridSizeWithLevelSource = true;

    public Vector2Int GridMin => Vector2Int.zero;
    public Vector2Int GridMax => new Vector2Int(gridSize.x - 1, gridSize.y - 1);

    private void OnEnable()
    {
        TryAutoAssignLevelData();
        SyncGridSizeFromLevelSource();
        EnsureValidGridSize();
        ApplyShaderGlobals();
    }

    private void OnValidate()
    {
        TryAutoAssignLevelData();
        SyncGridSizeFromLevelSource();
        EnsureValidGridSize();

        if (!isActiveAndEnabled)
            return;

        ApplyShaderGlobals();
    }

    private void LateUpdate()
    {
        if (SyncGridSizeFromLevelSource())
            ApplyShaderGlobals();
    }

    private void ApplyShaderGlobals()
    {
        Vector2Int min = GridMin;
        float cellSize = gridManager != null ? gridManager.CellSize : 1f;

        Vector3 minWorld = gridManager != null
            ? gridManager.GridToWorldCenter(min)
            : new Vector3(min.x * cellSize, 0f, min.y * cellSize);
        minWorld.x -= cellSize * 0.5f;
        minWorld.z -= cellSize * 0.5f;

        Vector2 worldSize = new Vector2(gridSize.x * cellSize, gridSize.y * cellSize);

        Shader.SetGlobalVector(FogGridWorldMinId, new Vector4(minWorld.x, minWorld.z, 0f, 0f));
        Shader.SetGlobalVector(FogGridWorldSizeId, new Vector4(worldSize.x, worldSize.y, 0f, 0f));
        Shader.SetGlobalFloat(FogCellSizeId, cellSize);
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
        if (levelZoneLayoutData == null)
        {
            LevelZoneLayoutLoader zoneLayoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();
            if (zoneLayoutLoader != null)
                levelZoneLayoutData = zoneLayoutLoader.LayoutData;
        }

        if (levelData == null)
        {
            LevelLoader levelLoader = FindFirstObjectByType<LevelLoader>();
            if (levelLoader != null)
                levelData = levelLoader.LevelData;
        }
    }

    private bool SyncGridSizeFromLevelSource()
    {
        if (!syncGridSizeWithLevelSource)
            return false;

        if (!TryGetLevelSourceGridSize(out Vector2Int nextGridSize))
            return false;

        if (fogGridManager != null)
            fogGridManager.SetGridSize(nextGridSize);

        if (nextGridSize == gridSize)
            return false;

        gridSize = nextGridSize;
        EnsureValidGridSize();
        return true;
    }

    private bool TryGetLevelSourceGridSize(out Vector2Int sourceGridSize)
    {
        if (levelZoneLayoutData != null)
        {
            sourceGridSize = levelZoneLayoutData.TotalGridSize;
            return true;
        }

        if (levelData != null)
        {
            sourceGridSize = levelData.GridSize;
            return true;
        }

        sourceGridSize = default;
        return false;
    }
}
