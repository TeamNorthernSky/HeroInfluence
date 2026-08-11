using UnityEngine;
using UnityEngine.UI;

public class MinimapCameraViewportOverlay : MonoBehaviour
{
    private const int CornerCount = 4;

    [Header("References")]
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LevelData levelData;
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private LevelZoneLayoutData levelZoneLayoutData;
    [SerializeField] private LevelZoneLayoutLoader levelZoneLayoutLoader;
    [SerializeField] private GridManager gridManager;

    [Header("Display")]
    [SerializeField] private bool drawEveryFrame = true;
    [SerializeField] private bool clampToMinimap = true;
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField, Min(1f)] private float lineThickness = 3f;
    [SerializeField] private Vector2 fixedBoxSizeNormalized = new Vector2(0.18f, 0.18f);

    private readonly Vector3[] worldCorners = new Vector3[CornerCount];
    private readonly Vector2[] minimapCorners = new Vector2[CornerCount];
    private readonly Image[] lineImages = new Image[CornerCount];

    private RectTransform minimapRect;
    private Plane groundPlane;

    private void Awake()
    {
        ResolveReferences();
        EnsureLines();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureLines();
        Refresh();
    }

    private void LateUpdate()
    {
        if (drawEveryFrame)
            Refresh();
    }

    [ContextMenu("Refresh Camera Viewport Overlay")]
    public void Refresh()
    {
        ResolveReferences();
        EnsureLines();

        if (minimapRect == null || targetCamera == null || gridManager == null || !TryGetGridSize(out Vector2Int gridSize))
        {
            SetLinesVisible(false);
            return;
        }

        if (!TryGetCameraGroundCenter(out Vector3 cameraGroundCenter))
        {
            SetLinesVisible(false);
            return;
        }

        Rect rect = minimapRect.rect;
        Vector2 center = WorldToMinimapPosition(cameraGroundCenter, gridSize, rect);
        SetFixedBoxCorners(center, rect);
        ApplyLines();
    }

    private bool TryGetCameraGroundCenter(out Vector3 groundCenter)
    {
        groundCenter = default;
        groundPlane = new Plane(Vector3.up, new Vector3(0f, gridManager.GetLandSurfaceY(), 0f));

        Ray ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!groundPlane.Raycast(ray, out float enter))
            return false;

        groundCenter = ray.GetPoint(enter);
        return true;
    }

    private Vector2 WorldToMinimapPosition(Vector3 worldPosition, Vector2Int gridSize, Rect rect)
    {
        Vector3 origin = gridManager.GridToWorldCenter(Vector2Int.zero);
        Vector3 xAxis = gridManager.GridToWorldCenter(Vector2Int.right) - origin;
        Vector3 yAxis = gridManager.GridToWorldCenter(Vector2Int.up) - origin;
        Vector3 delta = worldPosition - origin;

        float gridX = xAxis.sqrMagnitude > Mathf.Epsilon ? Vector3.Dot(delta, xAxis) / xAxis.sqrMagnitude : 0f;
        float gridY = yAxis.sqrMagnitude > Mathf.Epsilon ? Vector3.Dot(delta, yAxis) / yAxis.sqrMagnitude : 0f;

        float normalizedX = (gridX + 0.5f) / Mathf.Max(1, gridSize.x);
        float normalizedY = (gridY + 0.5f) / Mathf.Max(1, gridSize.y);

        Vector2 position = new Vector2(normalizedX * rect.width, normalizedY * rect.height);
        if (clampToMinimap)
        {
            position.x = Mathf.Clamp(position.x, 0f, rect.width);
            position.y = Mathf.Clamp(position.y, 0f, rect.height);
        }

        return position;
    }

    private void SetFixedBoxCorners(Vector2 center, Rect rect)
    {
        Vector2 boxSize = new Vector2(
            Mathf.Max(1f, rect.width * Mathf.Clamp01(fixedBoxSizeNormalized.x)),
            Mathf.Max(1f, rect.height * Mathf.Clamp01(fixedBoxSizeNormalized.y)));
        Vector2 halfSize = boxSize * 0.5f;

        float minX = center.x - halfSize.x;
        float maxX = center.x + halfSize.x;
        float minY = center.y - halfSize.y;
        float maxY = center.y + halfSize.y;

        if (clampToMinimap)
        {
            minX = Mathf.Clamp(minX, 0f, rect.width);
            maxX = Mathf.Clamp(maxX, 0f, rect.width);
            minY = Mathf.Clamp(minY, 0f, rect.height);
            maxY = Mathf.Clamp(maxY, 0f, rect.height);
        }

        minimapCorners[0] = new Vector2(minX, minY);
        minimapCorners[1] = new Vector2(maxX, minY);
        minimapCorners[2] = new Vector2(maxX, maxY);
        minimapCorners[3] = new Vector2(minX, maxY);
    }

    private void ApplyLines()
    {
        for (int i = 0; i < CornerCount; i++)
        {
            Image line = lineImages[i];
            if (line == null)
                continue;

            Vector2 start = minimapCorners[i];
            Vector2 end = minimapCorners[(i + 1) % CornerCount];
            Vector2 direction = end - start;

            RectTransform lineTransform = line.rectTransform;
            lineTransform.anchoredPosition = start;
            lineTransform.sizeDelta = new Vector2(direction.magnitude, lineThickness);
            lineTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            line.color = lineColor;

            if (!line.gameObject.activeSelf)
                line.gameObject.SetActive(true);
        }
    }

    private void EnsureLines()
    {
        if (minimapRect == null)
            return;

        for (int i = 0; i < CornerCount; i++)
        {
            if (lineImages[i] != null)
                continue;

            GameObject lineObject = new GameObject($"CameraViewportLine_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lineObject.transform.SetParent(minimapRect, false);

            Image image = lineObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = lineColor;

            RectTransform rectTransform = image.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.localScale = Vector3.one;

            lineImages[i] = image;
        }
    }

    private void SetLinesVisible(bool visible)
    {
        for (int i = 0; i < lineImages.Length; i++)
        {
            if (lineImages[i] != null)
                lineImages[i].gameObject.SetActive(visible);
        }
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

    private void ResolveReferences()
    {
        if (minimapImage == null)
            minimapImage = GetComponent<RawImage>();

        if (minimapImage != null)
            minimapRect = minimapImage.rectTransform;
        else if (minimapRect == null)
            minimapRect = GetComponent<RectTransform>();

        if (targetCamera == null)
            targetCamera = Camera.main;

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
    }

    private void OnValidate()
    {
        lineThickness = Mathf.Max(1f, lineThickness);
        fixedBoxSizeNormalized.x = Mathf.Clamp01(fixedBoxSizeNormalized.x);
        fixedBoxSizeNormalized.y = Mathf.Clamp01(fixedBoxSizeNormalized.y);

        if (lineImages == null)
            return;

        for (int i = 0; i < lineImages.Length; i++)
        {
            if (lineImages[i] != null)
                lineImages[i].color = lineColor;
        }
    }
}
