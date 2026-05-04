using UnityEngine;

/// <summary>
/// MapTerrain 프리팹에 붙어, PlayGridManagerJC의 Width/Height를 읽어
/// GroundPlane과 GridOverlay의 크기·위치를 런타임에 직사각형으로 조정한다.
///
/// Unity 기본 Plane 메시는 10×10 크기이므로, scale = gridSize / 10.
/// MapTerrain 루트는 그리드 중심이 월드 원점 근처 (width/2, 0, height/2)에 오도록 배치.
/// </summary>
public class MapTerrainSizer : MonoBehaviour
{
    [Header("Children (자동 탐색 대체 가능)")]
    [SerializeField] private Transform groundPlane;
    [SerializeField] private Transform gridOverlay;
    [SerializeField] private MeshRenderer gridOverlayRenderer;

    [Tooltip("Unity 기본 Plane 메시의 기본 크기")]
    [SerializeField] private float basePlaneSize = 10f;

    private static readonly int GridSizeId = Shader.PropertyToID("_GridSize");

    private void Start()
    {
        var grid = FindFirstObjectByType<PlayGridManagerJC>();
        if (grid == null)
        {
            Debug.LogError("[MapTerrainSizer] PlayGridManagerJC를 찾을 수 없습니다");
            return;
        }

        AutoResolveChildren();
        ApplySize(grid.Width, grid.Height);
    }

    private void AutoResolveChildren()
    {
        if (groundPlane == null)
        {
            var t = transform.Find("GroundPlane");
            if (t != null) groundPlane = t;
        }
        if (gridOverlay == null)
        {
            var t = transform.Find("GridOverlay");
            if (t != null) gridOverlay = t;
        }
        if (gridOverlayRenderer == null && gridOverlay != null)
        {
            gridOverlayRenderer = gridOverlay.GetComponent<MeshRenderer>();
        }
    }

    private void ApplySize(int gridWidth, int gridHeight)
    {
        float worldW = gridWidth * PlayGridManagerJC.CellSize;
        float worldH = gridHeight * PlayGridManagerJC.CellSize;
        float scaleX = worldW / basePlaneSize;
        float scaleZ = worldH / basePlaneSize;

        // MapTerrain 루트는 그리드 중심이 월드 (worldW/2, 0, worldH/2)에 오도록.
        // 기존 프리팹은 루트 (32,0,32) + 자식 (-32,0,-32) 오프셋으로 그리드 (0,0)~(64,64)를 표현.
        // 직사각형 대응: 루트 (worldW/2, 0, worldH/2), 자식은 (-worldW/2, y, -worldH/2)로 맞춘다.
        transform.localPosition = new Vector3(worldW * 0.5f, 0f, worldH * 0.5f);

        if (groundPlane != null)
        {
            groundPlane.localPosition = new Vector3(-worldW * 0.5f, 0f, -worldH * 0.5f);
            groundPlane.localScale = new Vector3(scaleX, 1f, scaleZ);
        }

        if (gridOverlay != null)
        {
            var p = gridOverlay.localPosition;
            gridOverlay.localPosition = new Vector3(-worldW * 0.5f, p.y, -worldH * 0.5f);
            gridOverlay.localScale = new Vector3(scaleX, 1f, scaleZ);
        }

        // GridLineShader의 _GridSize 프로퍼티에 (width, height) 전달
        if (gridOverlayRenderer != null)
        {
            var mpb = new MaterialPropertyBlock();
            gridOverlayRenderer.GetPropertyBlock(mpb);
            mpb.SetVector(GridSizeId, new Vector4(gridWidth, gridHeight, 0, 0));
            gridOverlayRenderer.SetPropertyBlock(mpb);
        }

        Debug.Log($"[MapTerrainSizer] {gridWidth}x{gridHeight} ({worldW}x{worldH}월드) 적용 완료");
    }
}
