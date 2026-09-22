using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Quarter-view exploration camera that follows the active party and supports edge scrolling.
/// </summary>
[DisallowMultipleComponent]
public class QuarterViewCameraFollower : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool followEnabled = true;
    [SerializeField] private float followDelay = 0.5f;

    [Header("Offsets")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 12f, -8.6f);

    [Header("Smoothing")]
    [SerializeField] private float rotationLerp = 10f;

    [Header("Fixed Rotation")]
    [SerializeField] private Vector3 fixedEulerAngles = new Vector3(55f, 0f, 0f);

    [Header("Zoom")]
    [SerializeField] private float minZoomY = 5f;
    [SerializeField] private float maxZoomY = 30f;
    [SerializeField] private float zoomSpeed = 200f;
    [SerializeField] private bool keepTargetCenteredOnZoom = true;

    [Header("Edge Scrolling")]
    [SerializeField] private bool edgeScrollEnabled = true;
    [SerializeField] private bool keyboardPanEnabled = true;
    [SerializeField] private float keyboardPanSpeed = 20f;
    // 거리·속도·응답률은 Resources/JcPointerProfile 공용 프로필에서 관리한다.
    [SerializeField] private float edgeLimitRange = 50f;
    [SerializeField] private bool invertVerticalEdgeScroll = false;

    [Header("Map Bounds")]
    [Tooltip("Clamp the projected camera viewport to the data-driven map bounds.")]
    [SerializeField] private bool clampToMapBounds = true;
    [SerializeField, Min(0f)] private float mapBoundsPaddingCells = 0f;
    [SerializeField] private Vector2 mapBoundsMinOffsetCells;
    [SerializeField] private Vector2 mapBoundsMaxOffsetCells;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private LevelZoneLayoutData levelZoneLayoutData;
    [SerializeField] private LevelZoneLayoutLoader levelZoneLayoutLoader;
    [SerializeField] private LevelData levelData;
    [SerializeField] private LevelLoader levelLoader;

    [Header("UI Blocking")]
    [SerializeField] private bool blockEdgeScrollOverButtons = true;
    [SerializeField] private bool blockCameraInputDuringCombatPrompt = true;
    [SerializeField] private CombatPromptService combatPromptService;

    [Header("Reset")]
    [SerializeField] private KeyCode resetKey = KeyCode.Y;

    private static readonly List<RaycastResult> UiRaycastResults = new List<RaycastResult>();
    private static readonly Vector3[] ViewportCorners =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(1f, 0f, 0f),
        new Vector3(1f, 1f, 0f),
        new Vector3(0f, 1f, 0f)
    };

    private Vector3 followVelocity;
    private Vector3 smoothedFollowAnchor;
    private Vector3 panOffset;
    private Vector3 edgeScrollVelocity;
    private float defaultZoomY;
    private float defaultZoomZ;
    private float zoomZPerY;
    private bool hasSmoothedFollowAnchor;
    private Camera targetCamera;
    private readonly Vector3[] groundCorners = new Vector3[4];
    private bool constrainFollowToMapEdgeDuringMove;
    private bool holdFollowXAtMapEdge;
    private bool holdFollowZAtMapEdge;

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    public void SetFollowEnabled(bool enabled)
    {
        followEnabled = enabled;
    }

    public void RecenterOnFollowTarget()
    {
        panOffset = Vector3.zero;
        edgeScrollVelocity = Vector3.zero;
    }

    public void SnapToFollowTarget()
    {
        if (followTarget == null)
            return;

        Vector3 followAnchor = followTarget.position;
        smoothedFollowAnchor = new Vector3(followAnchor.x, 0f, followAnchor.z);
        followVelocity = Vector3.zero;
        hasSmoothedFollowAnchor = true;
        RecenterOnFollowTarget();
        ApplyCameraPositionFromCurrentState();
        ClampCameraToMapBounds();
    }

    public void SetFollowMapEdgeMoveConstraint(bool enabled)
    {
        constrainFollowToMapEdgeDuringMove = enabled;
        if (!enabled)
        {
            holdFollowXAtMapEdge = false;
            holdFollowZAtMapEdge = false;
        }

        edgeScrollVelocity = Vector3.zero;
    }

    public void FocusWorldPosition(Vector3 worldPosition)
    {
        Vector3 anchor = GetCurrentFollowAnchor();
        panOffset.x = worldPosition.x - anchor.x;
        panOffset.z = worldPosition.z - anchor.z;
        panOffset.y = 0f;
        edgeScrollVelocity = Vector3.zero;
        ClampPanOffset();
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        positionOffset.x = 0f;
        defaultZoomY = positionOffset.y;
        defaultZoomZ = positionOffset.z;
        zoomZPerY = Mathf.Abs(defaultZoomY) > 0.001f ? defaultZoomZ / defaultZoomY : 0f;

        if (combatPromptService == null)
            combatPromptService = FindFirstObjectByType<CombatPromptService>();
    }

    private void Update()
    {
        if (IsCameraInputBlocked())
        {
            edgeScrollVelocity = Vector3.zero;
            JcPointerInput.ClearScroll(this);
            return;
        }

        if (JcPointerInput.Inside) HandleZoomInput();
        HandleEdgeScrolling();
        if (JcPointerInput.Inside)
        {
            HandleKeyboardPanning();
            HandleResetInput();
        }
    }

    private void OnDisable()
    {
        edgeScrollVelocity = Vector3.zero;
        JcPointerInput.ClearScroll(this);
    }

    private void LateUpdate()
    {
        if (!followEnabled || followTarget == null)
            return;

        Vector3 previousPosition = transform.position;
        Vector3 followAnchor = followTarget.position;
        Vector3 targetFollowAnchor = new Vector3(followAnchor.x, 0f, followAnchor.z);

        if (!hasSmoothedFollowAnchor)
        {
            smoothedFollowAnchor = targetFollowAnchor;
            hasSmoothedFollowAnchor = true;
        }

        // Smooth follow movement with frame-rate independent damping.
        float smoothTime = Mathf.Max(0.01f, followDelay);
        float tRot = 1f - Mathf.Exp(-rotationLerp * Time.deltaTime);

        smoothedFollowAnchor = Vector3.SmoothDamp(
            smoothedFollowAnchor,
            targetFollowAnchor,
            ref followVelocity,
            smoothTime);

        Vector3 desiredPos = smoothedFollowAnchor + panOffset + positionOffset;
        desiredPos.x = smoothedFollowAnchor.x + panOffset.x;
        desiredPos.y = positionOffset.y;
        desiredPos.z = smoothedFollowAnchor.z + panOffset.z + positionOffset.z;

        if (constrainFollowToMapEdgeDuringMove)
        {
            UpdateMapEdgeFollowHolds();
            if (holdFollowXAtMapEdge)
                desiredPos.x = previousPosition.x;

            if (holdFollowZAtMapEdge)
                desiredPos.z = previousPosition.z;
        }

        transform.position = desiredPos;

        Quaternion desiredFixedRot = Quaternion.Euler(fixedEulerAngles);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredFixedRot, tRot);

        ClampCameraToMapBounds();
    }

    private void HandleZoomInput()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        positionOffset.y = Mathf.Clamp(positionOffset.y - scroll * zoomSpeed * Time.deltaTime, minZoomY, maxZoomY);
        ApplyZoomDepthOffset();
    }

    private void HandleEdgeScrolling()
    {
        JcPointerInput.ClearScroll(this);
        if (!edgeScrollEnabled || !followEnabled || followTarget == null || Time.deltaTime <= 0f ||
            (blockEdgeScrollOverButtons && JcPointerInput.Inside && IsPointerOverBlockingUI()))
        {
            edgeScrollVelocity = Vector3.zero;
            return;
        }

        JcPointerProfile profile = JcPointerProfile.Current;
        Vector2 edgeInput = JcPointerInput.EdgeVelocity(JcPointerInput.Position, JcPointerInput.Size, profile);

        if (edgeInput.sqrMagnitude <= .000001f)
        {
            edgeScrollVelocity = Vector3.zero;
            return;
        }

        if (invertVerticalEdgeScroll)
            edgeInput.y *= -1f;

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 desiredVelocity = (right * edgeInput.x) + (forward * edgeInput.y);
        float blend = profile.acceleration <= 0 ? 1 : 1f - Mathf.Exp(-profile.acceleration * Time.deltaTime);
        edgeScrollVelocity = Vector3.Lerp(edgeScrollVelocity, desiredVelocity, blend);

        edgeScrollVelocity.y = 0f;

        Vector3 previousPan = panOffset;
        panOffset += edgeScrollVelocity * Time.deltaTime;
        ClampPanOffset();
        if ((panOffset - previousPan).sqrMagnitude > .00000001f)
            JcPointerInput.ReportScroll(this, edgeInput.normalized);
        else edgeScrollVelocity = Vector3.zero;
    }

    private void HandleKeyboardPanning()
    {
        if (!edgeScrollEnabled || !keyboardPanEnabled)
            return;

        Vector2 keyboardInput = Vector2.zero;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            keyboardInput.x -= 1f;

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            keyboardInput.x += 1f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            keyboardInput.y += 1f;

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            keyboardInput.y -= 1f;

        if (keyboardInput.sqrMagnitude <= 0f)
            return;

        keyboardInput.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 moveDelta = ((right * keyboardInput.x) + (forward * keyboardInput.y))
            * Mathf.Max(0f, keyboardPanSpeed)
            * Time.deltaTime;
        moveDelta.y = 0f;

        panOffset += moveDelta;
        ClampPanOffset();
    }

    private void UpdateMapEdgeFollowHolds()
    {
        holdFollowXAtMapEdge = false;
        holdFollowZAtMapEdge = false;

        ResolveMapBoundsReferences();

        if (targetCamera == null || gridManager == null || !TryGetMapWorldBounds(out Rect mapBounds))
            return;

        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, gridManager.GetLandSurfaceY(), 0f));
        if (!TryGetViewportGroundRect(groundPlane, out Rect viewportRect))
            return;

        const float edgeEpsilon = 0.02f;
        holdFollowXAtMapEdge = viewportRect.xMin <= mapBounds.xMin + edgeEpsilon
            || viewportRect.xMax >= mapBounds.xMax - edgeEpsilon;
        holdFollowZAtMapEdge = viewportRect.yMin <= mapBounds.yMin + edgeEpsilon
            || viewportRect.yMax >= mapBounds.yMax - edgeEpsilon;
    }

    private void HandleResetInput()
    {
        if (!Input.GetKeyDown(resetKey))
            return;

        // First press recenters the camera and disables edge scrolling; second press re-enables it.
        if (edgeScrollEnabled)
        {
            RecenterOnFollowTarget();
            positionOffset = new Vector3(0f, defaultZoomY, defaultZoomZ);
            edgeScrollEnabled = false;
            return;
        }

        edgeScrollEnabled = true;
    }

    private void ApplyZoomDepthOffset()
    {
        if (!keepTargetCenteredOnZoom)
            return;

        positionOffset.z = positionOffset.y * zoomZPerY;
    }

    private Vector3 GetCurrentFollowAnchor()
    {
        if (hasSmoothedFollowAnchor)
            return smoothedFollowAnchor;

        if (followTarget == null)
            return Vector3.zero;

        Vector3 followAnchor = followTarget.position;
        return new Vector3(followAnchor.x, 0f, followAnchor.z);
    }

    private void ClampPanOffset()
    {
        panOffset.x = Mathf.Clamp(panOffset.x, -edgeLimitRange, edgeLimitRange);
        panOffset.z = Mathf.Clamp(panOffset.z, -edgeLimitRange, edgeLimitRange);
        panOffset.y = 0f;
    }

    private void ClampCameraToMapBounds()
    {
        if (!clampToMapBounds)
            return;

        ResolveMapBoundsReferences();

        if (targetCamera == null || gridManager == null || !TryGetMapWorldBounds(out Rect mapBounds))
            return;

        // Bounds are based on level data, not on scene colliders or the Land mesh size.
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, gridManager.GetLandSurfaceY(), 0f));
        if (!TryGetViewportGroundRect(groundPlane, out Rect viewportRect))
            return;

        if (TryShrinkZoomToFitBounds(mapBounds, groundPlane, ref viewportRect))
            ApplyCameraPositionFromCurrentState();

        Vector3 correction = Vector3.zero;
        correction.x = CalculateAxisCorrection(viewportRect.xMin, viewportRect.xMax, mapBounds.xMin, mapBounds.xMax);
        correction.z = CalculateAxisCorrection(viewportRect.yMin, viewportRect.yMax, mapBounds.yMin, mapBounds.yMax);

        if (Mathf.Approximately(correction.x, 0f) && Mathf.Approximately(correction.z, 0f))
            return;

        transform.position += correction;
        panOffset += correction;
        panOffset.y = 0f;

        if (!Mathf.Approximately(correction.x, 0f))
            edgeScrollVelocity.x = 0f;

        if (!Mathf.Approximately(correction.z, 0f))
            edgeScrollVelocity.z = 0f;
    }

    private bool TryShrinkZoomToFitBounds(Rect mapBounds, Plane groundPlane, ref Rect viewportRect)
    {
        bool changed = false;

        // If the viewport is larger than the map, pull zoom inward before clamping position.
        for (int i = 0; i < 6; i++)
        {
            float widthRatio = viewportRect.width > mapBounds.width && viewportRect.width > Mathf.Epsilon
                ? mapBounds.width / viewportRect.width
                : 1f;
            float heightRatio = viewportRect.height > mapBounds.height && viewportRect.height > Mathf.Epsilon
                ? mapBounds.height / viewportRect.height
                : 1f;
            float scale = Mathf.Min(widthRatio, heightRatio);

            if (scale >= 0.999f || positionOffset.y <= minZoomY + 0.001f)
                return changed;

            float nextZoomY = Mathf.Max(minZoomY, positionOffset.y * scale * 0.98f);
            if (Mathf.Approximately(nextZoomY, positionOffset.y))
                nextZoomY = Mathf.Max(minZoomY, positionOffset.y - 0.1f);

            positionOffset.y = nextZoomY;
            ApplyZoomDepthOffset();
            ApplyCameraPositionFromCurrentState();
            changed = true;

            if (!TryGetViewportGroundRect(groundPlane, out viewportRect))
                return changed;
        }

        return changed;
    }

    private void ApplyCameraPositionFromCurrentState()
    {
        Vector3 anchor = GetCurrentFollowAnchor();
        transform.position = new Vector3(
            anchor.x + panOffset.x,
            positionOffset.y,
            anchor.z + panOffset.z + positionOffset.z);
    }

    private bool TryGetViewportGroundRect(Plane groundPlane, out Rect rect)
    {
        rect = default;

        // Project the four camera viewport corners onto a virtual ground plane.
        for (int i = 0; i < ViewportCorners.Length; i++)
        {
            Ray ray = targetCamera.ViewportPointToRay(ViewportCorners[i]);
            if (!groundPlane.Raycast(ray, out float enter))
                return false;

            groundCorners[i] = ray.GetPoint(enter);
        }

        float minX = groundCorners[0].x;
        float maxX = groundCorners[0].x;
        float minZ = groundCorners[0].z;
        float maxZ = groundCorners[0].z;

        for (int i = 1; i < groundCorners.Length; i++)
        {
            Vector3 corner = groundCorners[i];
            minX = Mathf.Min(minX, corner.x);
            maxX = Mathf.Max(maxX, corner.x);
            minZ = Mathf.Min(minZ, corner.z);
            maxZ = Mathf.Max(maxZ, corner.z);
        }

        rect = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        return true;
    }

    private bool TryGetMapWorldBounds(out Rect bounds)
    {
        bounds = default;

        if (!TryGetGridSize(out Vector2Int gridSize))
            return false;

        int maxGridX = Mathf.Max(0, gridSize.x - 1);
        int maxGridY = Mathf.Max(0, gridSize.y - 1);
        Vector3 minCenter = gridManager.GridToWorldCenter(Vector2Int.zero);
        Vector3 maxCenter = gridManager.GridToWorldCenter(new Vector2Int(maxGridX, maxGridY));

        // Include half a cell so the bounds match the outer edge of the level grid.
        float halfCell = Mathf.Max(0.01f, gridManager.CellSize) * 0.5f;
        float cellSize = Mathf.Max(0.01f, gridManager.CellSize);
        float padding = Mathf.Max(0f, mapBoundsPaddingCells) * cellSize;
        float minX = Mathf.Min(minCenter.x, maxCenter.x) - halfCell - padding + mapBoundsMinOffsetCells.x * cellSize;
        float maxX = Mathf.Max(minCenter.x, maxCenter.x) + halfCell + padding + mapBoundsMaxOffsetCells.x * cellSize;
        float minZ = Mathf.Min(minCenter.z, maxCenter.z) - halfCell - padding + mapBoundsMinOffsetCells.y * cellSize;
        float maxZ = Mathf.Max(minCenter.z, maxCenter.z) + halfCell + padding + mapBoundsMaxOffsetCells.y * cellSize;

        bounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        return true;
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

        if (levelLoader != null && levelLoader.LevelData != null)
        {
            gridSize = levelLoader.LevelData.GridSize;
            return gridSize.x > 0 && gridSize.y > 0;
        }

        gridSize = default;
        return false;
    }

    private void ResolveMapBoundsReferences()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (levelZoneLayoutLoader == null)
            levelZoneLayoutLoader = FindFirstObjectByType<LevelZoneLayoutLoader>();

        if (levelLoader == null)
            levelLoader = FindFirstObjectByType<LevelLoader>();

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

    private static float CalculateAxisCorrection(float viewMin, float viewMax, float boundsMin, float boundsMax)
    {
        float viewSize = viewMax - viewMin;
        float boundsSize = boundsMax - boundsMin;

        if (viewSize >= boundsSize)
        {
            float viewCenter = (viewMin + viewMax) * 0.5f;
            float boundsCenter = (boundsMin + boundsMax) * 0.5f;
            return boundsCenter - viewCenter;
        }

        if (viewMin < boundsMin)
            return boundsMin - viewMin;

        if (viewMax > boundsMax)
            return boundsMax - viewMax;

        return 0f;
    }

    /// <summary>
    /// Returns true when the pointer is over UI that should block edge scrolling.
    /// Buttons and CameraEdgeScrollBlocker panels are treated as blocking UI.
    /// </summary>
    private static bool IsPointerOverBlockingUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        PointerEventData pointerEventData = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };

        UiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, UiRaycastResults);

        for (int i = 0; i < UiRaycastResults.Count; i++)
        {
            GameObject hitObject = UiRaycastResults[i].gameObject;
            if (hitObject == null)
                continue;

            if (hitObject.GetComponentInParent<Button>() != null)
                return true;

            if (hitObject.GetComponentInParent<CameraEdgeScrollBlocker>() != null)
                return true;
        }

        return false;
    }

    private bool IsCameraInputBlocked()
    {
        // 화면 밖은 경계 스크롤에 허용하되, 게임 클릭 게이트와는 구분한다.
        if (!JcPointerInput.CanControl || ModalManager.HasAny || WorldInputGate.IsTurnResolving)
            return true;

        if (!blockCameraInputDuringCombatPrompt)
            return false;

        if (combatPromptService == null)
            combatPromptService = FindFirstObjectByType<CombatPromptService>();

        return combatPromptService != null && combatPromptService.IsOpen;
    }
}

