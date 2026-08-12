using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class MinimapCameraNavigator : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private RawImage minimapImage;
    [SerializeField] private LevelData levelData;
    [SerializeField] private LevelLoader levelLoader;
    [SerializeField] private LevelZoneLayoutData levelZoneLayoutData;
    [SerializeField] private LevelZoneLayoutLoader levelZoneLayoutLoader;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private QuarterViewCameraFollower cameraFollower;

    [Header("Input")]
    [SerializeField] private bool enableClickNavigation = true;
    [SerializeField] private bool enableDragNavigation = true;
    [SerializeField] private bool forceMinimapRaycastTarget = true;

    private RectTransform minimapRect;
    private bool isDragging;

    private void Awake()
    {
        ResolveReferences();
        ApplyRaycastTarget();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyRaycastTarget();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanHandleClick(eventData))
            return;

        MoveCameraToPointer(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanHandleDrag(eventData))
            return;

        isDragging = true;
        MoveCameraToPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        MoveCameraToPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    private bool CanHandleClick(PointerEventData eventData)
    {
        return enableClickNavigation
            && eventData != null
            && eventData.button == PointerEventData.InputButton.Left;
    }

    private bool CanHandleDrag(PointerEventData eventData)
    {
        return enableDragNavigation
            && eventData != null
            && eventData.button == PointerEventData.InputButton.Left;
    }

    private void MoveCameraToPointer(PointerEventData eventData)
    {
        ResolveReferences();
        if (minimapRect == null || gridManager == null || cameraFollower == null || !TryGetGridSize(out Vector2Int gridSize))
            return;

        Camera eventCamera = eventData.pressEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            minimapRect,
            eventData.position,
            eventCamera,
            out Vector2 localPoint))
        {
            return;
        }

        Rect rect = minimapRect.rect;
        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedY = Mathf.Clamp01(normalizedY);

        Vector3 worldPosition = MinimapToWorldPosition(normalizedX, normalizedY, gridSize);
        cameraFollower.FocusWorldPosition(worldPosition);
    }

    private Vector3 MinimapToWorldPosition(float normalizedX, float normalizedY, Vector2Int gridSize)
    {
        float gridX = normalizedX * Mathf.Max(1, gridSize.x) - 0.5f;
        float gridY = normalizedY * Mathf.Max(1, gridSize.y) - 0.5f;

        Vector3 origin = gridManager.GridToWorldCenter(Vector2Int.zero);
        Vector3 xAxis = gridManager.GridToWorldCenter(Vector2Int.right) - origin;
        Vector3 yAxis = gridManager.GridToWorldCenter(Vector2Int.up) - origin;
        Vector3 worldPosition = origin + (xAxis * gridX) + (yAxis * gridY);
        worldPosition.y = gridManager.GetLandSurfaceY();
        return worldPosition;
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

        if (cameraFollower == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                cameraFollower = mainCamera.GetComponent<QuarterViewCameraFollower>();
        }

        if (cameraFollower == null)
            cameraFollower = FindFirstObjectByType<QuarterViewCameraFollower>();
    }

    private void ApplyRaycastTarget()
    {
        if (forceMinimapRaycastTarget && minimapImage != null)
            minimapImage.raycastTarget = true;
    }
}
